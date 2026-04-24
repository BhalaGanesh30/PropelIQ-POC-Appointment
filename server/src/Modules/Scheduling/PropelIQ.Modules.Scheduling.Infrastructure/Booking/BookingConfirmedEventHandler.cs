using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PropelIQ.Modules.Scheduling.Domain.Events;
using PropelIQ.Modules.SharedServices.Infrastructure.Data;
using System.Threading.Channels;

namespace PropelIQ.Modules.Scheduling.Infrastructure.Booking;

/// <summary>
/// Background worker that consumes <see cref="BookingConfirmedEvent"/> messages from
/// the in-process channel and orchestrates artifact generation + email delivery.
///
/// Design decisions:
/// - Scoped services (AppDbContext, artifact service) are resolved per event via
///   <see cref="IServiceScopeFactory"/> because BackgroundService is registered as singleton.
/// - The booking record is updated with artifact paths after successful generation
///   so the confirmation page can serve downloads immediately (AC-3).
/// - Any unhandled exception is caught and logged; the booking remains valid (edge case).
/// NFR-010: all operations audit-logged via ILogger structured logging.
/// </summary>
public sealed class BookingConfirmedEventHandler : BackgroundService
{
    private readonly Channel<BookingConfirmedEvent> _channel;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<BookingConfirmedEventHandler> _logger;

    public BookingConfirmedEventHandler(
        Channel<BookingConfirmedEvent> channel,
        IServiceScopeFactory scopeFactory,
        ILogger<BookingConfirmedEventHandler> logger)
    {
        _channel     = channel;
        _scopeFactory = scopeFactory;
        _logger      = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        _logger.LogInformation("BookingConfirmedEventHandler started.");

        await foreach (var evt in _channel.Reader.ReadAllAsync(ct))
        {
            try
            {
                await ProcessEventAsync(evt, ct);
            }
            catch (Exception ex)
            {
                // Edge case: booking persists even if the entire pipeline fails.
                _logger.LogError(ex,
                    "Failed to process BookingConfirmedEvent for appointment " +
                    "{AppointmentId}. Booking remains valid.",
                    evt.AppointmentId);
            }
        }

        _logger.LogInformation("BookingConfirmedEventHandler stopped.");
    }

    private async Task ProcessEventAsync(BookingConfirmedEvent evt, CancellationToken ct)
    {
        using var scope          = _scopeFactory.CreateScope();
        var artifactService      = scope.ServiceProvider.GetRequiredService<ConfirmationArtifactService>();
        var db                   = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Generate all three artifacts (per-artifact failures are isolated inside the service).
        var artifacts = await artifactService.GenerateAndStoreAsync(evt, ct);

        // Update the Appointment entity with artifact paths and generation timestamp.
        var appointment = await db.Set<PropelIQ.Modules.Scheduling.Domain.Entities.Appointment>()
            .FirstOrDefaultAsync(a => a.Id == evt.AppointmentId, ct);

        if (appointment is not null)
        {
            appointment.PdfStoragePath       = artifacts.PdfPath;
            appointment.QrCodeStoragePath    = artifacts.QrCodePath;
            appointment.IcsStoragePath       = artifacts.IcsPath;
            appointment.ArtifactsGenerated   = artifacts.AllGenerated;
            appointment.ArtifactsGeneratedAt = artifacts.AllGenerated
                ? DateTimeOffset.UtcNow
                : null;

            await db.SaveChangesAsync(ct);

            _logger.LogInformation(
                "Artifact paths persisted for appointment {AppointmentId} " +
                "[AllGenerated={AllGenerated}]",
                evt.AppointmentId, artifacts.AllGenerated);
        }

        // Send confirmation email with all available artifacts (AC-2).
        await artifactService.SendConfirmationEmailAsync(evt, artifacts, ct);

        // Update EmailSent / retry count on the appointment record.
        if (appointment is not null)
        {
            appointment.EmailSent        = true;
            appointment.EmailRetryCount += 1;
            await db.SaveChangesAsync(ct);
        }

        _logger.LogInformation(
            "Confirmation artifacts generated and email sent for appointment {AppointmentId}",
            evt.AppointmentId);
    }
}
