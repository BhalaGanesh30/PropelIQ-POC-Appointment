using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using PropelIQ.Modules.Scheduling.Application.Abstractions;
using PropelIQ.Modules.Scheduling.Application.Booking.Dto;
using PropelIQ.Modules.Scheduling.Application.Waitlist.Dto;
using PropelIQ.Modules.Scheduling.Domain.Entities;
using PropelIQ.Modules.Scheduling.Domain.Enums;
using PropelIQ.Modules.Scheduling.Domain.Events;
using PropelIQ.Modules.Scheduling.Infrastructure.Booking;
using PropelIQ.SharedKernel;

namespace PropelIQ.Modules.Scheduling.Infrastructure.Waitlist;

/// <summary>
/// Orchestrates waitlist join, slot-offer matching, claim, and expiry-rotation (US_023).
///
/// AC-1: <see cref="JoinAsync"/> persists preferred slot parameters with a FIFO position.
/// AC-2: <see cref="MatchSlotToWaitlistAsync"/> finds the first eligible Active entry
///       (FIFO order) and transitions it to Offered, dispatching <see cref="SlotOfferedEvent"/>.
/// AC-3: <see cref="ClaimAsync"/> delegates atomic reservation to <see cref="BookingService"/>;
///       concurrent claim attempts are handled by the same RowVersion optimistic concurrency.
/// AC-4: <see cref="ExpireAndRotateAsync"/> marks Expired and re-runs slot matching so the
///       next eligible patient is offered the slot.
/// Edge case: when the concurrent claim loses the race, the entry is reset to Active.
/// NFR-010: all state transitions are logged with structured properties.
/// </summary>
public sealed class WaitlistService
{
    private readonly IWaitlistRepository _waitlistRepo;
    private readonly IBookingRepository _bookingRepo;
    private readonly BookingService _bookingService;
    private readonly Channel<SlotOfferedEvent> _slotOfferedChannel;
    private readonly Channel<ClaimExpiredEvent> _claimExpiredChannel;
    private readonly ILogger<WaitlistService> _logger;

    private static readonly TimeSpan ClaimWindow = TimeSpan.FromHours(2);

    public WaitlistService(
        IWaitlistRepository waitlistRepo,
        IBookingRepository bookingRepo,
        BookingService bookingService,
        Channel<SlotOfferedEvent> slotOfferedChannel,
        Channel<ClaimExpiredEvent> claimExpiredChannel,
        ILogger<WaitlistService> logger)
    {
        _waitlistRepo       = waitlistRepo;
        _bookingRepo        = bookingRepo;
        _bookingService     = bookingService;
        _slotOfferedChannel = slotOfferedChannel;
        _claimExpiredChannel = claimExpiredChannel;
        _logger             = logger;
    }

    // ── AC-1: Join ─────────────────────────────────────────────────────────────

    /// <summary>Persists a new waitlist entry with preferred slot parameters.</summary>
    public async Task<WaitlistEntryResponse> JoinAsync(
        Guid patientId,
        JoinWaitlistRequest request,
        CancellationToken ct)
    {
        // patientId from JWT is the auth user ID; waitlist_entries.patient_id FK requires app.patients.id.
        var resolvedPatientId = await _bookingRepo.ResolvePatientIdAsync(patientId, ct)
            ?? throw new InvalidOperationException(
                $"No patient record found for user {patientId}.");

        var position = await _waitlistRepo.GetNextPositionAsync(ct);

        var entry = new WaitlistEntry
        {
            PatientId                 = resolvedPatientId,
            Status                    = WaitlistStatus.Active,
            PreferredDateStart        = request.PreferredDateStart,
            PreferredDateEnd          = request.PreferredDateEnd,
            PreferredDurationMinutes  = request.PreferredDurationMinutes,
            PreferredAppointmentType  = request.PreferredAppointmentType,
            Position                  = position,
        };

        await _waitlistRepo.AddAsync(entry, ct);

        _logger.LogInformation(
            "Patient {PatientId} joined waitlist at position {Position} " +
            "for {Type} {Duration}min between {Start:s} and {End:s}",
            resolvedPatientId, position, request.PreferredAppointmentType,
            request.PreferredDurationMinutes,
            request.PreferredDateStart, request.PreferredDateEnd);

        return MapToResponse(entry);
    }

    // ── AC-2: Match ────────────────────────────────────────────────────────────

    /// <summary>
    /// Offers a released slot to the first FIFO-eligible Active waitlist entry.
    /// Called by <see cref="WaitlistMatchingWorker"/> within 5 minutes of slot availability.
    /// </summary>
    public async Task MatchSlotToWaitlistAsync(
        Guid slotId,
        DateTimeOffset slotTime,
        int durationMinutes,
        string appointmentType,
        string? providerName,
        CancellationToken ct)
    {
        var eligible = await _waitlistRepo.FindEligibleEntriesForSlotAsync(
            slotTime, durationMinutes, appointmentType, ct);

        if (eligible.Count == 0)
        {
            _logger.LogDebug("No waitlist matches for released slot {SlotId}.", slotId);
            return;
        }

        var first = eligible[0];
        var expiresAt = DateTimeOffset.UtcNow.Add(ClaimWindow);

        first.Status        = WaitlistStatus.Offered;
        first.OfferedSlotId = slotId;
        first.OfferedAt     = DateTimeOffset.UtcNow;
        first.ClaimExpiresAt = expiresAt;

        await _waitlistRepo.UpdateAsync(first, ct);

        _logger.LogInformation(
            "Slot {SlotId} offered to waitlist entry {EntryId} (patient {PatientId}). " +
            "Claim expires at {ExpiresAt:s}.",
            slotId, first.Id, first.PatientId, expiresAt);

        // Dispatch event for notification handler — fire-and-forget after DB commit.
        _slotOfferedChannel.Writer.TryWrite(new SlotOfferedEvent
        {
            WaitlistEntryId = first.Id,
            PatientId       = first.PatientId,
            SlotId          = slotId,
            SlotTime        = slotTime,
            DurationMinutes = durationMinutes,
            AppointmentType = appointmentType,
            ProviderName    = providerName,
            ClaimExpiresAt  = expiresAt,
        });
    }

    // ── AC-3: Claim ────────────────────────────────────────────────────────────

    /// <summary>
    /// Atomically claims the offered slot by delegating to <see cref="BookingService"/>.
    /// Returns <see cref="Result{T}.Success"/> with booking details on success.
    /// Returns <see cref="Result{T}.Failure"/> with a descriptive message on any failure,
    /// including concurrent claim (edge case: entry reset to Active so patient stays queued).
    /// </summary>
    public async Task<Result<ClaimWaitlistResponse>> ClaimAsync(
        Guid waitlistEntryId,
        Guid patientId,
        CancellationToken ct)
    {
        var entry = await _waitlistRepo.GetByIdForPatientAsync(
            waitlistEntryId, patientId, ct);

        if (entry is null)
            return Result<ClaimWaitlistResponse>.Failure("Waitlist entry not found.");

        if (entry.Status != WaitlistStatus.Offered)
            return Result<ClaimWaitlistResponse>.Failure(
                "No slot is currently offered for this entry.");

        if (entry.ClaimExpiresAt <= DateTimeOffset.UtcNow)
            return Result<ClaimWaitlistResponse>.Failure("Claim window has expired.");

        if (entry.OfferedSlotId is null)
            return Result<ClaimWaitlistResponse>.Failure(
                "No slot is associated with this offer.");

        // Reuse BookingService for atomic reservation + optimistic concurrency (AC-3).
        var bookingRequest = new CreateBookingRequest
        {
            SlotId         = entry.OfferedSlotId.Value,
            IntakeRecordId = Guid.Empty, // Waitlist claims bypass intake (task specification).
        };

        var (isSuccess, success, _) =
            await _bookingService.CreateBookingAsync(patientId, bookingRequest, ct);

        if (!isSuccess)
        {
            // Edge case: concurrent claim — another patient reserved the slot first.
            _logger.LogWarning(
                "Waitlist claim failed for entry {EntryId}: slot {SlotId} was concurrently " +
                "claimed. Patient {PatientId} reset to Active and remains on waitlist.",
                waitlistEntryId, entry.OfferedSlotId, patientId);

            entry.Status        = WaitlistStatus.Active;
            entry.OfferedSlotId = null;
            entry.OfferedAt     = null;
            entry.ClaimExpiresAt = null;
            await _waitlistRepo.UpdateAsync(entry, ct);

            return Result<ClaimWaitlistResponse>.Failure(
                "Slot was claimed by another patient. You remain on the waitlist.");
        }

        entry.Status       = WaitlistStatus.Claimed;
        entry.ClaimedAt    = DateTimeOffset.UtcNow;
        entry.AppointmentId = success!.AppointmentId;
        await _waitlistRepo.UpdateAsync(entry, ct);

        _logger.LogInformation(
            "Waitlist entry {EntryId} claimed by patient {PatientId}. " +
            "Appointment {AppointmentId} created.",
            waitlistEntryId, patientId, success.AppointmentId);

        return Result<ClaimWaitlistResponse>.Success(new ClaimWaitlistResponse
        {
            AppointmentId   = success.AppointmentId,
            ConfirmationCode = success.ConfirmationCode,
            AppointmentTime = success.AppointmentTime,
            DurationMinutes = success.DurationMinutes,
            AppointmentType = success.AppointmentType,
            ProviderName    = success.ProviderName,
            Location        = success.Location,
        });
    }

    // ── AC-4: Expire and rotate ────────────────────────────────────────────────

    /// <summary>
    /// Marks an Offered entry as Expired and re-matches the slot to the next eligible patient.
    /// Called by <see cref="ClaimWindowExpiryWorker"/> every minute.
    /// </summary>
    public async Task ExpireAndRotateAsync(WaitlistEntry entry, CancellationToken ct)
    {
        var slotId = entry.OfferedSlotId;

        entry.Status    = WaitlistStatus.Expired;
        entry.ExpiredAt = DateTimeOffset.UtcNow;
        await _waitlistRepo.UpdateAsync(entry, ct);

        _logger.LogInformation(
            "Waitlist entry {EntryId} expired. Rotating slot {SlotId} to next patient.",
            entry.Id, slotId);

        // Notify the patient their offer lapsed.
        if (slotId.HasValue)
        {
            _claimExpiredChannel.Writer.TryWrite(new ClaimExpiredEvent
            {
                WaitlistEntryId = entry.Id,
                PatientId       = entry.PatientId,
                SlotId          = slotId.Value,
            });

            // Re-match the released slot to the next eligible patient.
            var slot = await _bookingRepo.GetSlotForBookingAsync(slotId.Value, ct);

            if (slot is not null)
            {
                await MatchSlotToWaitlistAsync(
                    slot.Id,
                    slot.StartTime,
                    (int)slot.Duration,
                    slot.Type.ToString(),
                    slot.ProviderName,
                    ct);
            }
        }
    }

    // ── Query ──────────────────────────────────────────────────────────────────

    /// <summary>Returns Active and Offered waitlist entries for the given patient.</summary>
    public async Task<List<WaitlistEntryResponse>> GetEntriesAsync(
        Guid patientId, CancellationToken ct)
    {
        var entries = await _waitlistRepo.GetActiveEntriesForPatientAsync(patientId, ct);
        return entries.ConvertAll(MapToResponse);
    }

    /// <summary>Cancels a specific waitlist entry owned by the given patient.</summary>
    public async Task<Result> CancelEntryAsync(
        Guid entryId, Guid patientId, CancellationToken ct)
    {
        var entry = await _waitlistRepo.GetByIdForPatientAsync(entryId, patientId, ct);

        if (entry is null)
            return Result.Failure("Waitlist entry not found.");

        if (entry.Status is WaitlistStatus.Claimed or WaitlistStatus.Cancelled)
            return Result.Failure("Entry cannot be cancelled in its current state.");

        entry.Status      = WaitlistStatus.Cancelled;
        entry.CancelledAt = DateTimeOffset.UtcNow;
        await _waitlistRepo.UpdateAsync(entry, ct);

        _logger.LogInformation(
            "Waitlist entry {EntryId} cancelled by patient {PatientId}.",
            entryId, patientId);

        return Result.Success();
    }

    // ── Mapping ────────────────────────────────────────────────────────────────

    private static WaitlistEntryResponse MapToResponse(WaitlistEntry e) => new()
    {
        Id                       = e.Id,
        Status                   = e.Status.ToString(),
        PreferredDateStart       = e.PreferredDateStart,
        PreferredDateEnd         = e.PreferredDateEnd,
        PreferredDurationMinutes = e.PreferredDurationMinutes,
        PreferredAppointmentType = e.PreferredAppointmentType,
        OfferedSlotId            = e.OfferedSlotId,
        OfferedAt                = e.OfferedAt,
        ClaimExpiresAt           = e.ClaimExpiresAt,
        Position                 = e.Position,
        CreatedAt                = e.CreatedAt,
    };
}
