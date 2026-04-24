using PropelIQ.Modules.Scheduling.Application.Booking.Artifacts.Models;
using PropelIQ.Modules.Scheduling.Domain.Events;

namespace PropelIQ.Modules.Scheduling.Application.Booking.Artifacts;

/// <summary>
/// Abstraction for transactional booking-related emails with PDF, QR, and ICS attachments.
///
/// AC-2 (US_021): confirmation email with all three artifacts.
/// AC-3 (US_024): reschedule email with updated ICS (SEQUENCE incremented).
/// AC-4 (US_024): cancellation email with STATUS:CANCELLED ICS so calendar apps remove the event.
/// Edge case: implementations retry up to 3 times with exponential backoff on failure.
/// </summary>
public interface IConfirmationEmailService
{
    /// <summary>Sends the initial booking confirmation email (AC-2, US_021).</summary>
    Task SendConfirmationAsync(
        BookingConfirmedEvent booking,
        ArtifactResult artifacts,
        CancellationToken ct);

    /// <summary>
    /// Sends the reschedule confirmation email with an updated ICS attachment (AC-3, US_024).
    /// </summary>
    Task SendRescheduleConfirmationAsync(
        BookingRescheduledEvent booking,
        byte[]? icsBytes,
        CancellationToken ct);

    /// <summary>
    /// Sends the cancellation email with a STATUS:CANCELLED ICS attachment so calendar apps
    /// automatically remove the event (AC-4, US_024).
    /// </summary>
    Task SendCancellationConfirmationAsync(
        BookingCancelledEvent booking,
        byte[]? icsBytes,
        CancellationToken ct);
}

