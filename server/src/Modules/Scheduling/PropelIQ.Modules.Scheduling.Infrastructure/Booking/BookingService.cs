using System.Security.Cryptography;
using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;
using PropelIQ.Modules.Scheduling.Application.Abstractions;
using PropelIQ.Modules.Scheduling.Application.Booking.Dto;
using PropelIQ.Modules.Scheduling.Domain.Entities;
using PropelIQ.Modules.Scheduling.Domain.Enums;
using PropelIQ.Modules.Scheduling.Domain.Events;
using PropelIQ.SharedKernel;

namespace PropelIQ.Modules.Scheduling.Infrastructure.Booking;

/// <summary>
/// Orchestrates atomic slot reservation and appointment creation.
///
/// AC-1: Delegates the transactional write to <see cref="IBookingRepository.CreateBookingAsync"/>
///       which increments slot.CurrentBookings and inserts the Appointment in one SaveChanges.
/// AC-4: Catches <see cref="DbUpdateConcurrencyException"/> (RowVersion mismatch) and
///       returns a <see cref="SlotConflictResponse"/> with the next available slot.
/// Edge case: <see cref="BookingConfirmedEvent"/> is dispatched AFTER the commit so a
///            notification failure can never roll back the persisted booking.
/// </summary>
public sealed class BookingService
{
    private readonly IBookingRepository _bookingRepo;
    private readonly Channel<BookingConfirmedEvent> _confirmedChannel;
    private readonly Channel<BookingRescheduledEvent> _rescheduledChannel;
    private readonly Channel<BookingCancelledEvent> _cancelledChannel;
    private readonly ILogger<BookingService> _logger;

    public BookingService(
        IBookingRepository bookingRepo,
        Channel<BookingConfirmedEvent> confirmedChannel,
        Channel<BookingRescheduledEvent> rescheduledChannel,
        Channel<BookingCancelledEvent> cancelledChannel,
        ILogger<BookingService> logger)
    {
        _bookingRepo       = bookingRepo;
        _confirmedChannel  = confirmedChannel;
        _rescheduledChannel = rescheduledChannel;
        _cancelledChannel  = cancelledChannel;
        _logger            = logger;
    }

    /// <summary>
    /// Creates an appointment booking.
    /// Returns <see langword="true"/> + <see cref="BookingResponse"/> on success,
    /// or <see langword="false"/> + <see cref="SlotConflictResponse"/> on conflict.
    /// </summary>
    public async Task<(bool IsSuccess, BookingResponse? Success, SlotConflictResponse? Conflict)>
        CreateBookingAsync(
            Guid patientId,
            CreateBookingRequest request,
            CancellationToken ct)
    {
        var slot = await _bookingRepo.GetSlotForBookingAsync(request.SlotId, ct);

        if (slot is null)
        {
            var conflict = await BuildConflictAsync(DateTimeOffset.UtcNow, null, ct);
            return (false, null, conflict);
        }

        var confirmationCode = GenerateConfirmationCode();

        var appointment = new Appointment
        {
            PatientId       = patientId,
            StaffUserId     = slot.ProviderId,
            SlotId          = slot.Id,
            IntakeRecordId  = request.IntakeRecordId,
            ScheduledAt     = slot.StartTime,
            DurationMinutes = (int)slot.Duration,
            AppointmentType = slot.Type.ToString(),
            ProviderName    = slot.ProviderName,
            Location        = slot.Location,
            Status          = AppointmentStatus.Confirmed.ToString(),
            ConfirmationCode = confirmationCode,
            BookedAt        = DateTimeOffset.UtcNow,
        };

        try
        {
            // AC-1, AC-4: atomic slot increment + appointment insert with RowVersion check.
            var created = await _bookingRepo.CreateBookingAsync(appointment, slot, ct);

            _logger.LogInformation(
                "Booking created: AppointmentId={AppointmentId} PatientId={PatientId} " +
                "SlotId={SlotId} ConfirmationCode={ConfirmationCode}",
                created.Id, patientId, request.SlotId, confirmationCode);

            // Dispatch domain event for async artifact + notification processing (edge case).
            // Fire-and-forget after the commit — booking is NOT rolled back on failure.
            DispatchBookingConfirmedEvent(created, patientId);

            var response = new BookingResponse
            {
                AppointmentId   = created.Id,
                ConfirmationCode = confirmationCode,
                AppointmentTime = created.ScheduledAt,
                DurationMinutes = created.DurationMinutes,
                AppointmentType = created.AppointmentType,
                ProviderName    = created.ProviderName,
                Location        = created.Location,
                Status          = created.Status,
                BookedAt        = created.BookedAt,
            };
            return (true, response, null);
        }
        catch (DbUpdateConcurrencyException)
        {
            // AC-4: slot was taken by a concurrent request — RowVersion mismatch.
            _logger.LogWarning(
                "Booking concurrency conflict: SlotId={SlotId} PatientId={PatientId}",
                request.SlotId, patientId);

            var conflict = await BuildConflictAsync(slot.StartTime, slot.Type, ct);
            return (false, null, conflict);
        }
    }

    // ── private helpers ───────────────────────────────────────────────────────

    private async Task<SlotConflictResponse> BuildConflictAsync(
        DateTimeOffset afterTime,
        AppointmentType? type,
        CancellationToken ct)
    {
        var next = await _bookingRepo.GetNextAvailableSlotAsync(afterTime, type, ct);
        return new SlotConflictResponse
        {
            Message              = "Slot no longer available",
            NextAvailableSlotId  = next?.Id,
            NextAvailableTime    = next?.StartTime,
        };
    }

    private void DispatchBookingConfirmedEvent(Appointment appointment, Guid patientId)
    {
        // Build domain event and write to the in-process channel.
        // The channel is unbounded — write never blocks the HTTP response.
        // BackgroundService (BookingConfirmedEventHandler) consumes asynchronously.
        var evt = new BookingConfirmedEvent
        {
            AppointmentId    = appointment.Id,
            PatientId        = patientId,
            ConfirmationCode = appointment.ConfirmationCode!,
            AppointmentTime  = appointment.ScheduledAt,
            DurationMinutes  = appointment.DurationMinutes,
            AppointmentType  = appointment.AppointmentType,
            ProviderName     = appointment.ProviderName,
            Location         = appointment.Location,
        };

        // TryWrite on an unbounded channel always succeeds unless the channel is completed.
        if (!_confirmedChannel.Writer.TryWrite(evt))
        {
            _logger.LogWarning(
                "Failed to enqueue BookingConfirmedEvent for appointment {AppointmentId}. " +
                "Artifacts will not be generated for this booking.",
                evt.AppointmentId);
        }
        else
        {
            _logger.LogInformation(
                "BookingConfirmedEvent enqueued: AppointmentId={AppointmentId} Code={Code}",
                evt.AppointmentId, evt.ConfirmationCode);
        }
    }

    /// <summary>
    /// Generates a cryptographically random 8-character alphanumeric code.
    /// Excludes look-alike characters (0/O, 1/I/L) to reduce transcription errors.
    /// </summary>
    private static string GenerateConfirmationCode()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        Span<byte> bytes = stackalloc byte[8];
        RandomNumberGenerator.Fill(bytes);
        return string.Create(8, bytes.ToArray(), static (span, data) =>
        {
            for (var i = 0; i < span.Length; i++)
                span[i] = chars[data[i] % chars.Length];
        });
    }

    private static readonly TimeSpan PolicyWindow = TimeSpan.FromHours(24);

    public async Task<Result<CancelBookingResponse>> CancelAsync(
        Guid appointmentId,
        Guid userId,
        bool isStaff,
        CancelBookingRequest request,
        CancellationToken ct)
    {
        var appointment = isStaff
            ? await _bookingRepo.GetAppointmentAsync(appointmentId, ct)
            : await _bookingRepo.GetAppointmentForPatientAsync(appointmentId, userId, ct);

        if (appointment is null)
            return Result<CancelBookingResponse>.Failure("Appointment not found.");

        if (appointment.Status != AppointmentStatus.Confirmed.ToString())
            return Result<CancelBookingResponse>.Failure("Only confirmed appointments can be cancelled.");

        var timeUntilAppointment = appointment.ScheduledAt - DateTimeOffset.UtcNow;
        if (timeUntilAppointment <= PolicyWindow && !isStaff)
            return Result<CancelBookingResponse>.Failure("Changes not allowed within 24 hours of appointment");

        var isOverride = isStaff && timeUntilAppointment <= PolicyWindow;
        if (isOverride && string.IsNullOrWhiteSpace(request.OverrideReason))
            return Result<CancelBookingResponse>.Failure("Override reason is required for changes within 24 hours.");

        appointment.Status = AppointmentStatus.Cancelled.ToString();
        var cancelledAt = DateTimeOffset.UtcNow;

        await _bookingRepo.SaveAppointmentAsync(appointment, ct);

        if (appointment.SlotId.HasValue)
        {
            try
            {
                await ReleaseSlotWithRetryAsync(appointment.SlotId.Value, ct);
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex,
                    "ALERT: Slot release failed after retries for appointment {AppointmentId}, " +
                    "slot {SlotId}. Manual resolution required.",
                    appointmentId, appointment.SlotId);
            }
        }

        if (isOverride)
        {
            await _bookingRepo.CreateAuditEntryAsync(new AppointmentAuditEntry
            {
                AppointmentId     = appointmentId,
                PerformedByUserId = userId,
                Action            = "Cancel",
                Reason            = request.OverrideReason!,
                IsOverride        = true,
                PerformedAt       = cancelledAt,
                PreviousStatus    = AppointmentStatus.Confirmed.ToString(),
            }, ct);
        }

        _logger.LogInformation(
            "Appointment {AppointmentId} cancelled by {UserId} (override: {IsOverride})",
            appointmentId, userId, isOverride);

        // Dispatch event for async notification: cancellation ICS + cancellation email (US_024).
        DispatchCancelledEvent(appointment, cancelledAt);

        return Result<CancelBookingResponse>.Success(new CancelBookingResponse
        {
            AppointmentId = appointmentId,
            Status        = "Cancelled",
            CancelledAt   = cancelledAt,
        });
    }

    public async Task<Result<RescheduleBookingResponse>> RescheduleAsync(
        Guid appointmentId,
        Guid userId,
        bool isStaff,
        RescheduleBookingRequest request,
        CancellationToken ct)
    {
        var appointment = isStaff
            ? await _bookingRepo.GetAppointmentAsync(appointmentId, ct)
            : await _bookingRepo.GetAppointmentForPatientAsync(appointmentId, userId, ct);

        if (appointment is null)
            return Result<RescheduleBookingResponse>.Failure("Appointment not found.");

        if (appointment.Status != AppointmentStatus.Confirmed.ToString())
            return Result<RescheduleBookingResponse>.Failure("Only confirmed appointments can be rescheduled.");

        var timeUntilAppointment = appointment.ScheduledAt - DateTimeOffset.UtcNow;
        if (timeUntilAppointment <= PolicyWindow && !isStaff)
            return Result<RescheduleBookingResponse>.Failure("Changes not allowed within 24 hours of appointment");

        var isOverride = isStaff && timeUntilAppointment <= PolicyWindow;
        if (isOverride && string.IsNullOrWhiteSpace(request.OverrideReason))
            return Result<RescheduleBookingResponse>.Failure("Override reason is required for changes within 24 hours.");

        var newSlot = await _bookingRepo.GetSlotForBookingAsync(request.NewSlotId, ct);
        if (newSlot is null)
            return Result<RescheduleBookingResponse>.Failure("Selected slot is no longer available.");

        var oldSlot = await _bookingRepo.GetTrackedSlotAsync(appointment.SlotId!.Value, ct);
        if (oldSlot is null)
            return Result<RescheduleBookingResponse>.Failure("Original slot not found.");

        var originalTime   = appointment.ScheduledAt;
        var originalSlotId = appointment.SlotId;

        // AC-3 (US_024): increment SEQUENCE before saving so the updated entity
        // carries the new counter; the event handler forwards it to ICS generation.
        appointment.SequenceNumber += 1;

        try
        {
            var updated       = await _bookingRepo.RescheduleBookingAsync(appointment, oldSlot, newSlot, ct);
            var rescheduledAt = DateTimeOffset.UtcNow;

            if (isOverride)
            {
                await _bookingRepo.CreateAuditEntryAsync(new AppointmentAuditEntry
                {
                    AppointmentId     = appointmentId,
                    PerformedByUserId = userId,
                    Action            = "Reschedule",
                    Reason            = request.OverrideReason!,
                    IsOverride        = true,
                    PerformedAt       = rescheduledAt,
                    PreviousStatus    = AppointmentStatus.Confirmed.ToString(),
                    PreviousSlotId    = originalSlotId,
                    NewSlotId         = request.NewSlotId,
                }, ct);
            }

            _logger.LogInformation(
                "Appointment {AppointmentId} rescheduled from {OldTime} to {NewTime} by {UserId} (override: {IsOverride})",
                appointmentId, originalTime, updated.ScheduledAt, userId, isOverride);

            // Dispatch event for async notification: updated ICS + reschedule email (US_024).
            DispatchRescheduledEvent(updated, rescheduledAt);

            return Result<RescheduleBookingResponse>.Success(new RescheduleBookingResponse
            {
                AppointmentId      = appointmentId,
                ConfirmationCode   = updated.ConfirmationCode!,
                NewAppointmentTime = updated.ScheduledAt,
                DurationMinutes    = updated.DurationMinutes,
                AppointmentType    = updated.AppointmentType,
                ProviderName       = updated.ProviderName,
                Location           = updated.Location,
                Status             = "Confirmed",
                RescheduledAt      = rescheduledAt,
            });
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result<RescheduleBookingResponse>.Failure("Selected slot is no longer available. Please choose another.");
        }
    }

    private void DispatchRescheduledEvent(Appointment appointment, DateTimeOffset rescheduledAt)
    {
        var evt = new BookingRescheduledEvent
        {
            AppointmentId    = appointment.Id,
            PatientId        = appointment.PatientId,
            ConfirmationCode = appointment.ConfirmationCode!,
            OriginalTime     = appointment.BookedAt,   // pre-reschedule time captured upstream
            NewTime          = appointment.ScheduledAt,
            DurationMinutes  = appointment.DurationMinutes,
            AppointmentType  = appointment.AppointmentType,
            ProviderName     = appointment.ProviderName,
            Location         = appointment.Location,
            SequenceNumber   = appointment.SequenceNumber,
            RescheduledAt    = rescheduledAt,
        };

        if (!_rescheduledChannel.Writer.TryWrite(evt))
        {
            _logger.LogWarning(
                "Failed to enqueue BookingRescheduledEvent for appointment {AppointmentId}. " +
                "Updated ICS will not be sent.",
                appointment.Id);
        }
    }

    private void DispatchCancelledEvent(Appointment appointment, DateTimeOffset cancelledAt)
    {
        var evt = new BookingCancelledEvent
        {
            AppointmentId    = appointment.Id,
            PatientId        = appointment.PatientId,
            ConfirmationCode = appointment.ConfirmationCode!,
            OriginalAppointmentTime = appointment.ScheduledAt,
            AppointmentType  = appointment.AppointmentType,
            ProviderName     = appointment.ProviderName,
            DurationMinutes  = appointment.DurationMinutes,
            Location         = appointment.Location,
            SequenceNumber   = appointment.SequenceNumber,
            CancelledAt      = cancelledAt,
        };

        if (!_cancelledChannel.Writer.TryWrite(evt))
        {
            _logger.LogWarning(
                "Failed to enqueue BookingCancelledEvent for appointment {AppointmentId}. " +
                "Cancellation ICS will not be sent.",
                appointment.Id);
        }
    }

    private async Task ReleaseSlotWithRetryAsync(Guid slotId, CancellationToken ct)    {
        var pipeline = new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 3,
                BackoffType      = DelayBackoffType.Exponential,
                Delay            = TimeSpan.FromSeconds(1),
                UseJitter        = false,
                OnRetry          = args =>
                {
                    _logger.LogWarning(
                        args.Outcome.Exception,
                        "Slot release retry {AttemptNumber} for slot {SlotId}. Retrying in {Delay:g}.",
                        args.AttemptNumber + 1, slotId, args.RetryDelay);
                    return ValueTask.CompletedTask;
                }
            })
            .Build();

        await pipeline.ExecuteAsync(async token =>
            await _bookingRepo.ReleaseSlotAsync(slotId, token), ct);
    }
}
