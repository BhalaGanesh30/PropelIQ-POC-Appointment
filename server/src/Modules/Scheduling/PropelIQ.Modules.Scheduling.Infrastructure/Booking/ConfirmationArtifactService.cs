using Microsoft.Extensions.Logging;
using PropelIQ.Modules.Scheduling.Application.Booking.Artifacts;
using PropelIQ.Modules.Scheduling.Application.Booking.Artifacts.Models;
using PropelIQ.Modules.Scheduling.Domain.Events;

namespace PropelIQ.Modules.Scheduling.Infrastructure.Booking;

/// <summary>
/// Orchestrates PDF, QR, and ICS generation with per-artifact error isolation.
/// AC-2, AC-3: generates all three artifacts; each generator failure is logged
/// independently so other artifacts can still be produced and emailed.
/// Edge case: PDF generation failure never blocks QR or ICS generation.
/// </summary>
public sealed class ConfirmationArtifactService
{
    private readonly PdfGenerator _pdfGenerator;
    private readonly QrCodeGenerator _qrCodeGenerator;
    private readonly IcsGenerator _icsGenerator;
    private readonly IArtifactStorage _storage;
    private readonly IConfirmationEmailService _emailService;
    private readonly ILogger<ConfirmationArtifactService> _logger;

    public ConfirmationArtifactService(
        PdfGenerator pdfGenerator,
        QrCodeGenerator qrCodeGenerator,
        IcsGenerator icsGenerator,
        IArtifactStorage storage,
        IConfirmationEmailService emailService,
        ILogger<ConfirmationArtifactService> logger)
    {
        _pdfGenerator    = pdfGenerator;
        _qrCodeGenerator = qrCodeGenerator;
        _icsGenerator    = icsGenerator;
        _storage         = storage;
        _emailService    = emailService;
        _logger          = logger;
    }

    /// <summary>
    /// Generates and stores all three confirmation artifacts.
    /// Each generator runs independently — failure in one does not prevent the others.
    /// </summary>
    public async Task<ArtifactResult> GenerateAndStoreAsync(
        BookingConfirmedEvent booking, CancellationToken ct)
    {
        var containerPath = $"bookings/{booking.AppointmentId}";
        var result = new ArtifactResult();

        // PDF (AC-2, AC-3) — must contain date, time, duration, type, provider.
        try
        {
            var pdfBytes = _pdfGenerator.GenerateConfirmationPdf(booking);
            result.PdfPath  = await _storage.StoreAsync(
                containerPath, "confirmation.pdf", pdfBytes, "application/pdf", ct);
            result.PdfBytes = pdfBytes;

            _logger.LogInformation(
                "PDF generated for appointment {AppointmentId}",
                booking.AppointmentId);
        }
        catch (Exception ex)
        {
            // Edge case: PDF failure must not block QR or ICS.
            _logger.LogError(ex,
                "PDF generation failed for appointment {AppointmentId}. Will retry asynchronously.",
                booking.AppointmentId);
        }

        // QR code (AC-2) — encodes appointmentId|confirmationCode.
        try
        {
            var qrBytes = _qrCodeGenerator.GenerateQrCode(
                booking.ConfirmationCode, booking.AppointmentId);
            result.QrCodePath  = await _storage.StoreAsync(
                containerPath, "qrcode.png", qrBytes, "image/png", ct);
            result.QrCodeBytes = qrBytes;

            _logger.LogInformation(
                "QR code generated for appointment {AppointmentId}",
                booking.AppointmentId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "QR code generation failed for appointment {AppointmentId}",
                booking.AppointmentId);
        }

        // ICS (AC-2) — calendar event with alarm.
        try
        {
            var icsBytes = _icsGenerator.GenerateBookingIcs(booking);
            result.IcsPath  = await _storage.StoreAsync(
                containerPath, "appointment.ics", icsBytes, "text/calendar", ct);
            result.IcsBytes = icsBytes;

            _logger.LogInformation(
                "ICS file generated for appointment {AppointmentId}",
                booking.AppointmentId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "ICS generation failed for appointment {AppointmentId}",
                booking.AppointmentId);
        }

        return result;
    }

    /// <summary>
    /// Sends the confirmation email with all available artifacts attached.
    /// Delegate to <see cref="IConfirmationEmailService"/> which handles Polly retry.
    /// </summary>
    public async Task SendConfirmationEmailAsync(
        BookingConfirmedEvent booking,
        ArtifactResult artifacts,
        CancellationToken ct)
    {
        await _emailService.SendConfirmationAsync(booking, artifacts, ct);
    }
}
