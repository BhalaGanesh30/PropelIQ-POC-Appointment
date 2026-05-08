using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PropelIQ.Modules.Scheduling.Application.Abstractions;
using PropelIQ.Modules.Scheduling.Application.AI;
using PropelIQ.Modules.Scheduling.Application.AI.Models;
using PropelIQ.Modules.Scheduling.Application.Booking.Artifacts;
using PropelIQ.Modules.Scheduling.Application.Queue;
using PropelIQ.Modules.Scheduling.Application.Reminders;
using PropelIQ.Modules.Scheduling.Application.Scheduling.Validators;
using PropelIQ.Modules.Scheduling.Application.Waitlist;
using PropelIQ.Modules.Scheduling.Domain.Events;
using PropelIQ.Modules.Scheduling.Infrastructure.AI;
using PropelIQ.Modules.Scheduling.Infrastructure.Booking;
using PropelIQ.Modules.Scheduling.Infrastructure.Caching;
using PropelIQ.Modules.Scheduling.Infrastructure.Intake;
using PropelIQ.Modules.Scheduling.Infrastructure.Queue;
using PropelIQ.Modules.Scheduling.Infrastructure.Appointments;
using PropelIQ.Modules.Scheduling.Infrastructure.Reminders;
using PropelIQ.Modules.Scheduling.Infrastructure.Scheduling;
using PropelIQ.Modules.Scheduling.Infrastructure.Waitlist;
using PropelIQ.Modules.Scheduling.Infrastructure.Override;
using PropelIQ.Modules.Scheduling.Infrastructure.Walkin;
using PropelIQ.Modules.Scheduling.Infrastructure.StaffBooking;
using PropelIQ.Modules.Scheduling.Infrastructure.Schedule;
using SendGrid;
using System.Threading.Channels;
using Twilio.Clients;

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

        // Slot alert dispatch (US_030): consumes Channel<SlotOfferedEvent> and dispatches
        // email/SMS alerts to patients within the 5-minute SLA (AC-1).
        // ISlotAlertService is scoped — resolved per event via IServiceScopeFactory.
        services.AddScoped<ISlotAlertService, SlotAlertService>();
        services.AddHostedService<SlotAlertDispatchHandler>();

        // ── Appointment history (US_025) ──────────────────────────────────────

        // Repository — scoped per request (wraps AppDbContext; uses AsNoTracking reads)
        services.AddScoped<IAppointmentHistoryRepository, AppointmentHistoryRepository>();

        // PDF generator — singleton (stateless QuestPDF renderer; static license init once)
        services.AddSingleton<AppointmentHistoryPdfGenerator>();

        // Service — scoped (depends on scoped repository + singleton PDF generator)
        services.AddScoped<AppointmentHistoryService>();

        // ── Reminder lifecycle (US_026) ───────────────────────────────────────

        // Repository — scoped (wraps AppDbContext)
        services.AddScoped<IReminderEventRepository, ReminderEventRepository>();

        // Patient preference reader — scoped (queries Patients via AppDbContext)
        services.AddScoped<IPatientPreferenceRepository, PatientPreferenceRepository>();

        // Scheduling service — scoped (depends on scoped repos + TimeProvider)
        services.AddScoped<IReminderSchedulingService, ReminderSchedulingService>();

        // ── Reminder dispatch (US_026 AC-2) ──────────────────────────────────

        // Repository — scoped (due-query, claim, sent, retry updates)
        services.AddScoped<IReminderDispatchRepository, ReminderDispatchRepository>();

        // Dead-letter repository — scoped (AC-4: persist failed reminders)
        services.AddScoped<IDeadLetterRepository, DeadLetterRepository>();

        // ── Email & SMS providers (US_027 AC-1, AC-2) ────────────────────────

        // SendGrid configuration and client
        services.Configure<SendGridOptions>(configuration.GetSection(SendGridOptions.SectionName));
        services.AddSingleton<ISendGridClient>(sp =>
        {
            var opts = sp.GetRequiredService<
                Microsoft.Extensions.Options.IOptions<SendGridOptions>>().Value;
            return new SendGridClient(opts.ApiKey);
        });

        // Twilio configuration and client
        services.Configure<TwilioOptions>(configuration.GetSection(TwilioOptions.SectionName));
        services.AddSingleton<ITwilioRestClient>(sp =>
        {
            var opts = sp.GetRequiredService<
                Microsoft.Extensions.Options.IOptions<TwilioOptions>>().Value;
            return new TwilioRestClient(opts.AccountSid, opts.AuthToken);
        });

        // Token service — HMAC-signed URLs for one-click confirm/cancel (US_027 task_002).
        // Falls back to stub if ReminderToken config section is absent.
        var hmacSecret = configuration[$"{ReminderTokenOptions.SectionName}:HmacSecret"];
        if (!string.IsNullOrWhiteSpace(hmacSecret))
        {
            services.Configure<ReminderTokenOptions>(
                configuration.GetSection(ReminderTokenOptions.SectionName));
            services.AddScoped<IReminderTokenService, ReminderTokenService>();
        }
        else
        {
            services.AddScoped<IReminderTokenService, StubReminderTokenService>();
        }

        // Reminder email (SendGrid) and SMS (Twilio) services — scoped
        services.AddScoped<IReminderEmailService, SendGridEmailService>();
        services.AddScoped<IReminderSmsService, TwilioSmsService>();

        // Dispatcher — scoped (wraps IReminderEmailService + IReminderSmsService with Polly pipeline)
        services.AddScoped<INotificationDispatcher, NotificationDispatcher>();

        // Background worker — hosted service (singleton; resolves scoped deps per tick)
        services.AddHostedService<ReminderDispatchWorker>();

        // ── No-show risk scoring (US_028) ─────────────────────────────────────

        // Feature extractor — scoped (EF Core queries via AppDbContext)
        services.AddScoped<IPatientHistoryFeatureExtractor, PatientHistoryFeatureExtractor>();

        // Scoring service — scoped (depends on IAiGatewayClient + scoped repos)
        services.AddScoped<INoShowRiskScoringService, NoShowRiskScoringService>();

        // ── High-risk alert channel (US_028 task_002) ─────────────────────────
        // Unbounded channel — producer is HighRiskNotificationWorker; consumer is
        // notification infrastructure (SignalR broadcaster, email/SMS dispatcher).
        var highRiskChannel = Channel.CreateUnbounded<HighRiskAlertEvent>(
            new UnboundedChannelOptions { SingleWriter = true });
        services.AddSingleton(highRiskChannel);
        services.AddSingleton(highRiskChannel.Reader);
        services.AddSingleton(highRiskChannel.Writer);

        // Background workers — hosted services (singleton, scoped deps via factory)
        services.AddHostedService<RiskScoreRefreshWorker>();
        services.AddHostedService<HighRiskNotificationWorker>();

        // ── Queue API (EP-004 US_031) ─────────────────────────────────────────

        // Options — binds Queue:CacheTtlSeconds from appsettings.json.
        services.Configure<QueueOptions>(configuration.GetSection(QueueOptions.SectionName));

        // Options — binds WaitTime:DefaultServiceDurationMinutes and
        // WaitTime:AppointmentTypeDurations from appsettings.json.
        services.Configure<WaitTimeOptions>(configuration.GetSection(WaitTimeOptions.SectionName));

        // Wait-time estimation — singleton: pure service with read-only config state.
        // task_003 implementation; replaces StubWaitTimeEstimationService.
        services.AddSingleton<IWaitTimeEstimationService, WaitTimeEstimationService>();

        // Queue service — scoped (depends on AppDbContext + IDistributedCache + singleton wait-time)
        services.AddScoped<IQueueService, QueueService>();

        // ── Appointment state machine (EP-004 US_032) ─────────────────────────

        // Scoped: owns EF Core unit-of-work for the state transition + audit write.
        services.AddScoped<IAppointmentStateMachineService, AppointmentStateMachineService>();

        // ── Walk-in registration (EP-004 US_033) ──────────────────────────────

        // Options — binds WalkIn:CapacityThreshold from appsettings.json.
        services.Configure<WalkinOptions>(configuration.GetSection(WalkinOptions.SectionName));

        // Walk-in service — scoped (EF Core unit-of-work + cache invalidation).
        services.AddScoped<IWalkinService, WalkinService>();

        // Patient search service — scoped (read-only EF Core query).
        services.AddScoped<IPatientSearchService, PatientSearchService>();

        // ── Scheduling override (EP-004 US_034) ───────────────────────────────

        // Override service — scoped (EF Core transaction + IAuditService).
        services.AddScoped<ISchedulingOverrideService, SchedulingOverrideService>();

        // ── Staff-assisted booking (EP-004 US_035) ────────────────────────────

        // Staff booking service — scoped (EF Core transaction + IAuditService + IDistributedCache).
        services.AddScoped<IStaffBookingService, StaffBookingService>();

        // ── Daily schedule calendar (EP-004 US_036) ───────────────────────────

        // Schedule service — scoped (EF Core + IDistributedCache + IAuditService).
        services.AddScoped<IScheduleService, ScheduleService>();

        return services;
    }
}
