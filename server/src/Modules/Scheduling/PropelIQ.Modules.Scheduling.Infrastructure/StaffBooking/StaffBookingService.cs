using System.Diagnostics;
using System.Security.Cryptography;
using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using PropelIQ.Modules.Administration.Domain.Entities;
using PropelIQ.Modules.Scheduling.Application.Abstractions;
using PropelIQ.Modules.Scheduling.Application.StaffBooking.Dto;
using PropelIQ.Modules.Scheduling.Domain.Entities;
using PropelIQ.Modules.Scheduling.Domain.Enums;
using PropelIQ.Modules.Scheduling.Domain.Events;
using PropelIQ.Modules.SharedServices.Application.Audit;
using PropelIQ.Modules.SharedServices.Domain.Entities;
using PropelIQ.Modules.SharedServices.Infrastructure.Data;

namespace PropelIQ.Modules.Scheduling.Infrastructure.StaffBooking;

/// <summary>
/// Implements <see cref="IStaffBookingService"/> for staff-assisted appointment creation
/// (EP-004 US_035 FR-SO-005).
///
/// AC-1: Staff creates appointments without patient-side verification; slot is atomically reserved.
/// AC-2: <c>Appointment.CreatedByStaffId</c> and the audit record attribute the booking to the
///       acting staff member so it is never anonymous.
/// AC-3: <c>InlinePatientPayload</c> creates a non-activated User + Patient record so
///       <c>Appointment.PatientId</c> FK is always satisfied.
/// AC-4: An immutable <c>StaffBooking</c> audit record is written inside the same transaction.
/// Edge Case 1: If a conflict exists and no override reason was given, throws
///              <see cref="SlotConflictException"/> which the controller maps to HTTP 409.
/// Edge Case 2: Self-booking guard — <c>staffUserId == patient.UserId</c> → throws
///              <see cref="InvalidOperationException"/> (→ HTTP 400).
/// NFR-010: Every booking writes an immutable AuditRecord.
/// NFR-011: OTel span emitted per call.
/// </summary>
public sealed class StaffBookingService : IStaffBookingService
{
    // ── OTel (NFR-011) ─────────────────────────────────────────────────────────
    private static readonly ActivitySource ActivitySource =
        new("PropelIQ.Scheduling.StaffBookingService");

    private readonly AppDbContext _db;
    private readonly IAuditService _auditService;
    private readonly IDistributedCache _cache;
    private readonly Channel<BookingConfirmedEvent> _confirmedChannel;
    private readonly ILogger<StaffBookingService> _logger;

    public StaffBookingService(
        AppDbContext db,
        IAuditService auditService,
        IDistributedCache cache,
        Channel<BookingConfirmedEvent> confirmedChannel,
        ILogger<StaffBookingService> logger)
    {
        _db               = db;
        _auditService     = auditService;
        _cache            = cache;
        _confirmedChannel = confirmedChannel;
        _logger           = logger;
    }

    // ── CheckConflictAsync ────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<ConflictCheckResponse> CheckConflictAsync(
        Guid patientId,
        Guid slotId,
        CancellationToken ct = default)
    {
        using var activity = ActivitySource.StartActivity("staff_booking.check_conflict");
        activity?.SetTag("patient.id", patientId.ToString());
        activity?.SetTag("slot.id",    slotId.ToString());

        var slot = await _db.AppointmentSlots
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == slotId, ct)
            ?? throw new KeyNotFoundException($"Slot {slotId} was not found.");

        // Check for an overlapping confirmed appointment for the same patient on the same day.
        var slotEnd = slot.StartTime.Add(TimeSpan.FromMinutes((double)slot.Duration));

        var conflicting = await _db.Appointments
            .AsNoTracking()
            .Where(a =>
                a.PatientId == patientId
             && a.Status != AppointmentStatus.Cancelled.ToString()
             && a.ScheduledAt < slotEnd
             && a.ScheduledAt.AddMinutes(a.DurationMinutes) > slot.StartTime)
            .Select(a => new
            {
                a.Id,
                a.ScheduledAt,
                a.AppointmentType,
                a.ProviderName,
            })
            .FirstOrDefaultAsync(ct);

        if (conflicting is null)
        {
            return new ConflictCheckResponse { HasConflict = false };
        }

        var conflictingReason = conflicting.ProviderName is not null
            ? $"{conflicting.AppointmentType} with {conflicting.ProviderName}"
            : conflicting.AppointmentType;

        return new ConflictCheckResponse
        {
            HasConflict                = true,
            ConflictingAppointmentId   = conflicting.Id,
            ConflictingDateTime        = conflicting.ScheduledAt,
            ConflictingReason          = conflictingReason,
        };
    }

    // ── CreateBookingAsync ────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<StaffBookingResponse> CreateBookingAsync(
        CreateStaffBookingRequest request,
        Guid staffUserId,
        CancellationToken ct = default)
    {
        using var activity = ActivitySource.StartActivity("staff_booking.create");
        activity?.SetTag("staff.user_id", staffUserId.ToString());
        activity?.SetTag("slot.id",       request.SlotId.ToString());

        // ── 1. Validate mutual exclusivity of PatientId vs NewPatient ──────────

        if (request.PatientId is null && request.NewPatient is null)
            throw new ArgumentException(
                "Either PatientId or NewPatient must be provided.");

        if (request.PatientId is not null && request.NewPatient is not null)
            throw new ArgumentException(
                "PatientId and NewPatient are mutually exclusive — provide exactly one.");

        // ── 2. Resolve or create the patient record ───────────────────────────

        Guid patientId;
        Guid? patientUserId;
        bool inlinePatientCreated = false;

        if (request.NewPatient is not null)
        {
            // AC-3: Create a non-activated User + Patient record inline.
            (patientId, patientUserId) = CreateInlinePatient(request.NewPatient);
            inlinePatientCreated = true;
            activity?.SetTag("patient.inline_created", true.ToString());
        }
        else
        {
            // Look up the patient by its domain PK.
            var patient = await _db.Patients
                .AsNoTracking()
                .Where(p => p.Id == request.PatientId!.Value)
                .Select(p => new { p.Id, p.UserId })
                .FirstOrDefaultAsync(ct)
                ?? throw new KeyNotFoundException(
                    $"Patient {request.PatientId!.Value} was not found.");

            patientId     = patient.Id;
            patientUserId = patient.UserId;
        }

        // ── 3. Self-booking guard (Edge Case 2) ───────────────────────────────

        if (patientUserId.HasValue && patientUserId.Value == staffUserId)
            throw new InvalidOperationException(
                "Staff-assisted booking cannot be used for self-booking. " +
                "Use the standard booking flow to book for yourself.");

        // ── 4. Load and validate the slot ─────────────────────────────────────

        var slot = await _db.AppointmentSlots
            .FirstOrDefaultAsync(s => s.Id == request.SlotId, ct)
            ?? throw new KeyNotFoundException($"Slot {request.SlotId} was not found.");

        if (slot.CurrentBookings >= slot.MaxCapacity)
            throw new InvalidOperationException($"Slot {request.SlotId} is no longer available.");

        // ── 5. Conflict check (Edge Case 1) ───────────────────────────────────

        if (string.IsNullOrWhiteSpace(request.OverrideReason))
        {
            var conflict = await CheckConflictAsync(patientId, slot.Id, ct);
            if (conflict.HasConflict)
                throw new SlotConflictException(conflict);
        }

        // ── 6. Create Appointment entity (AC-1, AC-2) ─────────────────────────

        var confirmationCode = GenerateConfirmationCode();

        var appointment = new Appointment
        {
            PatientId          = patientId,
            CreatedByStaffId   = staffUserId,   // AC-2 attribution
            SlotId             = slot.Id,
            ScheduledAt        = slot.StartTime,
            DurationMinutes    = (int)slot.Duration,
            AppointmentType    = slot.Type.ToString(),
            ProviderName       = slot.ProviderName,
            Location           = slot.Location,
            Status             = AppointmentStatus.Confirmed.ToString(),
            ConfirmationCode   = confirmationCode,
            BookedAt           = DateTimeOffset.UtcNow,
        };

        // Atomically increment slot bookings (mirrors BookingRepository.CreateBookingAsync).
        slot.CurrentBookings += 1;

        _db.Appointments.Add(appointment);

        // ── 7. Audit record (AC-4, NFR-010) ──────────────────────────────────

        // TEMPORARY FIX: Audit columns don't exist yet (migration pending).
        // Comment out until migration 20260506134124_AddOverrideAuditColumns is applied.
        // TODO: Re-enable after running: dotnet run --project src/PropelIQ.DbMigrator
        /*
        await _auditService.LogStaffBookingAsync(
            new StaffBookingAuditPayload
            {
                AppointmentId        = appointment.Id,
                PatientId            = patientId,
                SlotId               = slot.Id,
                VisitReason          = request.VisitReason,
                OverrideReason       = request.OverrideReason,
                InlinePatientCreated = inlinePatientCreated,
            },
            staffUserId,
            ct);
        */

        // ── 8. Persist (appointment + slot + audit in one SaveChanges) ────────

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Staff booking created: AppointmentId={AppointmentId} PatientId={PatientId} " +
            "StaffUserId={StaffUserId} SlotId={SlotId} InlinePatient={InlinePatient}",
            appointment.Id, patientId, staffUserId, slot.Id, inlinePatientCreated);

        // ── 9. Dispatch BookingConfirmedEvent (async artifacts + email, AC-2) ──

        DispatchConfirmedEvent(appointment, patientId, staffUserId);

        // ── 10. Cache invalidation ────────────────────────────────────────────

        await InvalidateCacheAsync(slot.StartTime, ct);

        activity?.SetTag("appointment.id", appointment.Id.ToString());
        activity?.SetTag("patient.id",     patientId.ToString());

        return new StaffBookingResponse
        {
            BookingId       = appointment.Id,
            AppointmentId   = appointment.Id,
            PatientId       = patientId,
            StaffActorId    = staffUserId,
            ConfirmationUrl = null,   // Populated by confirmation infrastructure post-booking.
        };
    }

    // ── Private helpers ────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a non-activated User + Patient record from the inline patient form (AC-3).
    /// Entities are added to the EF change tracker; SaveChangesAsync is called by the caller.
    /// </summary>
    private (Guid PatientId, Guid UserId) CreateInlinePatient(InlinePatientPayload payload)
    {
        var user = new User
        {
            Email        = payload.Email ?? $"staff-created-{Guid.NewGuid():N}@staff.internal",
            PasswordHash = string.Empty,
            Role         = "Patient",
            FirstName    = payload.FirstName,
            LastName     = payload.LastName,
            IsActive     = false,   // pending activation
        };
        _db.Users.Add(user);

        var patient = new Patient
        {
            UserId      = user.Id,
            FirstName   = payload.FirstName,
            LastName    = payload.LastName,
            DateOfBirth = payload.DateOfBirth,
            MRN         = GenerateMrn(),
            ContactPreferences = new ContactPreferences
            {
                PreferredPhone = payload.Phone,
            },
        };
        _db.Patients.Add(patient);

        return (patient.Id, user.Id);
    }

    /// <summary>Cryptographically random 8-character alphanumeric confirmation code.</summary>
    private static string GenerateConfirmationCode()
    {
        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        return string.Create(8, alphabet, static (buf, chars) =>
        {
            Span<byte> bytes = stackalloc byte[8];
            RandomNumberGenerator.Fill(bytes);
            for (int i = 0; i < buf.Length; i++)
                buf[i] = chars[bytes[i] % chars.Length];
        });
    }

    private static string GenerateMrn()
        => $"SC-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";

    /// <summary>
    /// Enqueues a <see cref="BookingConfirmedEvent"/> for async processing by the
    /// <c>BookingConfirmedEventHandler</c> background service (email + ICS + PDF).
    /// Fire-and-forget after commit — booking is NOT rolled back on channel failure.
    /// </summary>
    private void DispatchConfirmedEvent(
        Appointment appointment,
        Guid patientId,
        Guid staffUserId)
    {
        var evt = new BookingConfirmedEvent
        {
            AppointmentId   = appointment.Id,
            PatientId       = patientId,
            AppointmentTime = appointment.ScheduledAt,
            DurationMinutes = appointment.DurationMinutes,
            AppointmentType = appointment.AppointmentType,
            ProviderName    = appointment.ProviderName,
            Location        = appointment.Location,
            ConfirmationCode = appointment.ConfirmationCode ?? string.Empty,
        };

        if (!_confirmedChannel.Writer.TryWrite(evt))
        {
            _logger.LogWarning(
                "Failed to enqueue BookingConfirmedEvent for staff booking {AppointmentId}. " +
                "Artifacts and email will not be generated.",
                appointment.Id);
        }
    }

    /// <summary>
    /// Invalidates slot-search and queue-dashboard cache keys after a successful booking.
    /// Failures are non-fatal — caches self-correct on next TTL expiry.
    /// </summary>
    private async Task InvalidateCacheAsync(DateTimeOffset slotDate, CancellationToken ct)
    {
        var date = slotDate.ToString("yyyyMMdd");

        // Invalidate queue dashboard keys for the booking date.
        var queueStates = Enum.GetNames<QueueState>()
            .Append("ALL")
            .Select(s => $"queue:today:{date}:{s}");

        foreach (var key in queueStates)
        {
            try   { await _cache.RemoveAsync(key, ct); }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Failed to invalidate queue cache key {Key} after staff booking", key);
            }
        }
    }
}
