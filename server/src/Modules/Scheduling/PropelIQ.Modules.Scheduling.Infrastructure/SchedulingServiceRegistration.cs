using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PropelIQ.Modules.Scheduling.Application.Abstractions;
using PropelIQ.Modules.Scheduling.Application.Booking.Artifacts;
using PropelIQ.Modules.Scheduling.Application.Scheduling.Validators;
using PropelIQ.Modules.Scheduling.Domain.Events;
using PropelIQ.Modules.Scheduling.Infrastructure.AI;
using PropelIQ.Modules.Scheduling.Infrastructure.Booking;
using PropelIQ.Modules.Scheduling.Infrastructure.Caching;
using PropelIQ.Modules.Scheduling.Infrastructure.Intake;
using PropelIQ.Modules.Scheduling.Infrastructure.Scheduling;
using PropelIQ.Modules.Scheduling.Infrastructure.Waitlist;
using PropelIQ.Modules.Scheduling.Infrastructure.Appointments;
using System.Threading.Channels;

namespace PropelIQ.Modules.Scheduling.Infrastructure;

/// <summary>
/// DI registration for the Scheduling module infrastructure layer.
/// Called from the API composition root (Program.cs).
/// </summary>
public static class SchedulingServiceRegistration
{
    public static IServiceCollection AddSchedulingInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // ── Slot search ───────────────────────────────────────────────────────

        // Repository — scoped per HTTP request
        services.AddScoped<ISlotRepository, SlotRepository>();

        // Cache service — singleton (IDistributedCache is already singleton)
        services.AddSingleton<SlotCacheService>();

        // Search service — scoped (depends on scoped ISlotRepository)
        services.AddScoped<ISlotSearchService, SlotSearchService>();

        // FluentValidation — register validators from both Application assemblies
        services.AddValidatorsFromAssemblyContaining<SlotSearchQueryValidator>();

        // ── Intake draft ──────────────────────────────────────────────────────

        // Repository — scoped per HTTP request
        services.AddScoped<IIntakeDraftRepository, IntakeDraftRepository>();

        // Service — scoped (depends on scoped repository and AppDbContext)
        services.AddScoped<IntakeDraftService>();

        // Background cleanup — hosted service (runs every 6 hours)
        services.AddHostedService<IntakeDraftCleanupService>();

        // ── AI-assisted intake prefill ────────────────────────────────────────
        // IntakeAssistService depends on IAiGatewayClient (registered by AddAiGateway
        // in Program.cs) — scoped to match controller lifetime.
        services.AddScoped<IntakeAssistService>();

        // ── Booking creation ──────────────────────────────────────────────────

        // Repository — scoped per HTTP request (wraps AppDbContext SaveChanges)
        services.AddScoped<IBookingRepository, BookingRepository>();

        // In-process event channel — singleton; unbounded so TryWrite never blocks.
        services.AddSingleton(Channel.CreateUnbounded<BookingConfirmedEvent>());

        // In-process channels for reschedule and cancellation notifications (US_024).
        services.AddSingleton(Channel.CreateUnbounded<BookingRescheduledEvent>());
        services.AddSingleton(Channel.CreateUnbounded<BookingCancelledEvent>());

        // Service — scoped (depends on scoped IBookingRepository + singleton Channels)
        services.AddScoped<BookingService>();

        // ── Confirmation artifact pipeline ────────────────────────────────────

        // ICS configuration (US_024): PRODID, default timezone, organizer email.
        services.Configure<IcsOptions>(configuration.GetSection(IcsOptions.SectionName));

        // Generators — scoped (stateless; create per request to avoid shared state)
        services.AddScoped<PdfGenerator>();
        services.AddScoped<QrCodeGenerator>();
        services.AddScoped<IcsGenerator>();

        // Storage — scoped (reads configuration; file-system impl)
        services.AddScoped<IArtifactStorage, ArtifactStorage>();

        // Email service — scoped (owns Polly pipeline; logger injected)
        services.AddScoped<IConfirmationEmailService, ConfirmationEmailService>();

        // Orchestrator — scoped (depends on scoped generators, storage, email)
        services.AddScoped<ConfirmationArtifactService>();

        // Background worker — hosted service (singleton; uses IServiceScopeFactory for scoped deps)
        services.AddHostedService<BookingConfirmedEventHandler>();

        // Background workers for reschedule and cancellation ICS delivery (US_024).
        services.AddHostedService<BookingRescheduledEventHandler>();
        services.AddHostedService<BookingCancelledEventHandler>();

        // ── Waitlist (US_023) ─────────────────────────────────────────────────

        // Repository — scoped per request
        services.AddScoped<IWaitlistRepository, WaitlistRepository>();

        // In-process channels — singleton; unbounded so TryWrite never blocks.
        services.AddSingleton(Channel.CreateUnbounded<SlotReleasedMessage>());
        services.AddSingleton(Channel.CreateUnbounded<SlotOfferedEvent>());
        services.AddSingleton(Channel.CreateUnbounded<ClaimExpiredEvent>());

        // Service — scoped (depends on scoped repos + singleton channels)
        services.AddScoped<WaitlistService>();

        // Background workers — hosted services (singleton lifecycle, scoped deps via factory)
        services.AddHostedService<WaitlistMatchingWorker>();
        services.AddHostedService<ClaimWindowExpiryWorker>();

        // ── Appointment history (US_025) ──────────────────────────────────────

        // Repository — scoped per request (wraps AppDbContext; uses AsNoTracking reads)
        services.AddScoped<IAppointmentHistoryRepository, AppointmentHistoryRepository>();

        // PDF generator — singleton (stateless QuestPDF renderer; static license init once)
        services.AddSingleton<AppointmentHistoryPdfGenerator>();

        // Service — scoped (depends on scoped repository + singleton PDF generator)
        services.AddScoped<AppointmentHistoryService>();

        return services;
    }
}
