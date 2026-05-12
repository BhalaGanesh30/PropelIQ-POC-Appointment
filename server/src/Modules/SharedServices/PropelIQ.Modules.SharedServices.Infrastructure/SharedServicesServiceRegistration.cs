using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Npgsql;
using PropelIQ.Modules.SharedServices.Application.AI;
using PropelIQ.Modules.SharedServices.Application.Audit;
using PropelIQ.Modules.SharedServices.Application.AiAudit;
using PropelIQ.Modules.SharedServices.Application.Compliance;
using PropelIQ.Modules.SharedServices.Application.Configuration;
using PropelIQ.Modules.SharedServices.Application.Disclosure;
using FluentValidation;
using PropelIQ.Modules.SharedServices.Application.Administration;
using PropelIQ.Modules.SharedServices.Application.Administration.Validators;
using PropelIQ.Modules.SharedServices.Application.Kpi;
using PropelIQ.Modules.SharedServices.Infrastructure.Administration;
using PropelIQ.Modules.SharedServices.Infrastructure.Kpi;
using PropelIQ.Modules.SharedServices.Infrastructure.AI;
using PropelIQ.Modules.SharedServices.Infrastructure.AiAudit;
using PropelIQ.Modules.SharedServices.Infrastructure.Audit;
using PropelIQ.Modules.SharedServices.Infrastructure.Caching;
using PropelIQ.Modules.SharedServices.Infrastructure.Compliance;
using PropelIQ.Modules.SharedServices.Infrastructure.Configuration;
using PropelIQ.Modules.SharedServices.Infrastructure.Data;
using PropelIQ.Modules.SharedServices.Infrastructure.Data.Seed;
using PropelIQ.Modules.SharedServices.Infrastructure.Disclosure;
using PropelIQ.Modules.SharedServices.Infrastructure.Identity;
using PropelIQ.Modules.SharedServices.Infrastructure.Notifications;
using PropelIQ.SharedKernel.Auth;
using PropelIQ.SharedKernel.Caching;
using PropelIQ.SharedKernel.Notifications;
using QuestPDF.Infrastructure;
using System.Threading.Channels;

namespace PropelIQ.Modules.SharedServices.Infrastructure;

/// <summary>
/// DI registration for the SharedServices module infrastructure layer.
/// Called from the API composition root (Program.cs) to register
/// cross-cutting infrastructure: email, storage, notification, and caching clients.
/// </summary>
public static class SharedServicesServiceRegistration
{
    public static IServiceCollection AddSharedServicesInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // QuestPDF community licence declaration (US_058).
        // Must be called once before any PDF document is generated.
        // Community licence is free for organisations with annual revenue < $1M USD.
        QuestPDF.Settings.License = LicenseType.Community;

        // Register the shared AppDbContext with Npgsql + pgvector support.
        // DR-001: PostgreSQL 15 primary datastore; UseVector() enables vector(n) column mapping.
        // The connection string key "Default" matches appsettings.json and .env.example.
        var connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException(
                "Connection string 'Default' is missing from configuration. " +
                "Ensure appsettings.Development.json or environment variables are configured.");

        // Npgsql 8+ requires an explicit opt-in for dynamic JSON serialization of
        // complex types such as List<string> written to jsonb columns (e.g. AiPopulatedFields).
        // Build a shared NpgsqlDataSource with EnableDynamicJson() and pass it to UseNpgsql
        // instead of the raw connection string so the type mapping is applied globally.
        var appDataSource = new NpgsqlDataSourceBuilder(connectionString)
            .EnableDynamicJson()
            .Build();

        // Register as singleton so the data source lifetime is managed by the container
        // and disposed cleanly on application shutdown.
        services.AddSingleton(appDataSource);

        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(
                appDataSource,
                npgsqlOptions =>
                {
                    npgsqlOptions.UseVector();
                    npgsqlOptions.MigrationsHistoryTable("__ef_migrations_history", "app");
                })
            .UseSnakeCaseNamingConvention()
            .UseAsyncSeeding(async (context, _, ct) =>
                await AppDbContextSeed.SeedAsync((AppDbContext)context, ct)));

        // ── Redis Cache Service (TR-004) ─────────────────────────────────────
        // Bind CacheSettings section; RedisCacheService resolves IDistributedCache
        // registered by AddStackExchangeRedisCache in the API composition root.
        services.Configure<CacheOptions>(
            configuration.GetSection(CacheOptions.SectionName));

        services.AddSingleton<ICacheService, RedisCacheService>();
        services.AddSingleton<ICacheInvalidator, SlotCacheInvalidator>();

        // ── ASP.NET Core Identity (auth schema) ──────────────────────────────
        // AuthDbContext is separate from AppDbContext so Identity tables live in
        // the 'auth' schema without conflicting with domain entities in 'app'.
        services.AddDbContext<AuthDbContext>(options =>
            options.UseNpgsql(
                connectionString,
                npgsqlOptions =>
                    npgsqlOptions.MigrationsHistoryTable("__auth_migrations_history", "auth"))
            .UseSnakeCaseNamingConvention());

        services.AddIdentityCore<ApplicationUser>(options =>
            {
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = true;
                options.Password.RequiredLength = 8;
                options.User.RequireUniqueEmail = true;
                options.SignIn.RequireConfirmedEmail = true;
                options.Tokens.EmailConfirmationTokenProvider = TokenOptions.DefaultEmailProvider;
                // Lockout after 5 consecutive failures for 15 minutes (OWASP A07).
                options.Lockout.MaxFailedAccessAttempts = 5;
                // AC-3/AC-4: 30-minute lockout duration after 5 failed attempts (us_018).
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(30);
                options.Lockout.AllowedForNewUsers = true;
                // Use a named token provider for password reset so its 24-hour TTL
                // does not conflict with the 48-hour staff-invitation token (us_016).
                options.Tokens.PasswordResetTokenProvider = "PasswordReset";
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<AuthDbContext>()
            .AddDefaultTokenProviders()
            // Named provider: 24-hour password-reset tokens (us_018 edge case).
            .AddTokenProvider<DataProtectorTokenProvider<ApplicationUser>>("PasswordReset")
            .AddSignInManager<SignInManager<ApplicationUser>>();

        // ── JWT Token Service ────────────────────────────────────────────────
        // Binds JwtSettings from configuration and validates at startup.
        services.AddOptions<JwtSettings>()
            .Bind(configuration.GetSection(JwtSettings.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IActiveSessionRepository, ActiveSessionRepository>();

        // ── Notification Sender ──────────────────────────────────────────────
        // If "Email:Smtp:Host" is configured, use the real SMTP sender.
        // Otherwise fall back to StubNotificationSender (logs to console only).
        var smtpHost = configuration["Email:Smtp:Host"];
        if (!string.IsNullOrWhiteSpace(smtpHost))
        {
            services.Configure<SmtpSettings>(configuration.GetSection(SmtpSettings.SectionName));
            services.AddScoped<INotificationSender, SmtpNotificationSender>();
        }
        else
        {
            services.AddScoped<INotificationSender, StubNotificationSender>();
        }

        // ── Audit service (EP-004 US_034 AC-2, AC-4) ─────────────────────────
        // Scoped: reads/writes AppDbContext within the same unit-of-work.
        services.AddScoped<IAuditService, AuditService>();

        // ── Channel-based audit record service (US_056, AC-1) ─────────────────
        // AuditRecordChannel is singleton (lives for the process lifetime).
        // AuditRecordService wraps the channel writer — registered as singleton since
        // it holds no scoped state.
        // AuditRecordWriterWorker and DeadLetterRetryWorker are BackgroundServices.
        // AuditLogExportService is scoped (needs AppDbContext).
        services.AddSingleton<AuditRecordChannel>();
        services.AddSingleton<IAuditRecordService, AuditRecordService>();
        services.AddHostedService<AuditRecordWriterWorker>();
        services.AddHostedService<DeadLetterRetryWorker>();
        services.AddScoped<AuditLogExportService>();

        // ── Retention partitioning (US_056 task_002, AC-3, DR-005) ───────────
        // PartitionMaintenanceService is singleton — stateless; db context passed per-call.
        // RetentionPolicyWorker is a daily BackgroundService; depends on PartitionMaintenanceService.
        services.AddSingleton<PartitionMaintenanceService>();
        services.AddHostedService<RetentionPolicyWorker>();

        // ── PII Redaction Pipeline (US_054, AIR-009, AIR-010) ─────────────────
        // Bind redaction options from "AI:Redaction" configuration section.
        // HmacKey and EncryptionKey MUST be sourced from secrets vault in production.
        services.Configure<PiiRedactionOptions>(
            configuration.GetSection(PiiRedactionOptions.SectionName));

        // NlpPiiDetector is stateless (regex patterns only) — safe as singleton.
        services.AddSingleton<NlpPiiDetector>();

        // RedactionMapStore uses ICacheService (singleton) + IOptions — safe as singleton.
        services.AddSingleton<IRedactionMapStore, RedactionMapStore>();

        // PatientContextAclFilter is stateless — safe as singleton.
        services.AddSingleton<IPatientContextAclFilter, PatientContextAclFilter>();

        // PiiRedactionService uses IAuditService (scoped) — must be scoped.
        services.AddScoped<IPiiRedactionService, PiiRedactionService>();

        // ── AI Audit Service (US_055, AIR-011) ────────────────────────────────
        // AiAuditService is scoped (uses AppDbContext).
        services.AddScoped<IAiAuditService, AiAuditService>();

        // Outbox processor retries failed audit writes every 60 seconds.
        services.AddHostedService<AiAuditOutboxProcessor>();

        // ── Patient disclosure service (US_057, AC-2, AC-3) ──────────────────
        // DisclosureService is scoped (needs AppDbContext, INotificationSender).
        // DisclosureCompilationWorker polls every 30s via IServiceScopeFactory.
        services.AddScoped<IDisclosureService, DisclosureService>();
        services.AddHostedService<DisclosureCompilationWorker>();

        // ── Compliance report service (US_058, AC-1–AC-4) ─────────────────────
        // ComplianceJobChannel is singleton (process-lifetime bounded channel, capacity 50).
        // ComplianceReportPdfRenderer is singleton (stateless QuestPDF renderer).
        // ComplianceReportGenerator is scoped (reads AppDbContext).
        // ComplianceReportDistributor is scoped (reads AppDbContext + sends email).
        // ComplianceReportService is scoped (orchestrates generator + renderer + persistence).
        // ComplianceReportScheduleWorker polls schedules every minute (BackgroundService).
        // ComplianceReportJobWorker drains the async job channel (BackgroundService).
        services.AddSingleton<ComplianceJobChannel>();
        services.AddSingleton<ComplianceReportPdfRenderer>();
        services.AddScoped<ComplianceReportGenerator>();
        services.AddScoped<ComplianceReportDistributor>();
        services.AddScoped<IComplianceReportService, ComplianceReportService>();
        services.AddHostedService<ComplianceReportScheduleWorker>();
        services.AddHostedService<ComplianceReportJobWorker>();

        // ── Configuration management service (US_059, AC-1–AC-4) ─────────────
        // ConfigurationCacheService is singleton + IHostedService for startup population.
        // The same instance is resolved as both the cache dependency and the hosted service
        // so only one object is created (shared registration pattern).
        // ConfigurationService is scoped (needs AppDbContext per request).
        services.AddSingleton<ConfigurationCacheService>();
        services.AddHostedService(sp => sp.GetRequiredService<ConfigurationCacheService>());
        services.AddScoped<IConfigurationService, ConfigurationService>();

        // ── KPI dashboard service (US_060, AC-1–AC-4) ─────────────────────────
        // KpiSnapshotCacheService is singleton — ConcurrentDictionary, no scoped dependencies.
        // KpiReportPdfRenderer is singleton — stateless QuestPDF renderer.
        // KpiMetricsService is scoped — needs AppDbContext per request.
        // KpiDistributionWorker polls every 5 minutes (BackgroundService).
        services.AddSingleton<KpiSnapshotCacheService>();
        services.AddSingleton<KpiReportPdfRenderer>();
        services.AddScoped<IKpiMetricsService, KpiMetricsService>();
        services.AddHostedService<KpiDistributionWorker>();

        // ── User management service (US_061, AC-1–AC-4) ───────────────────────
        // UserManagementService is scoped — reads and writes AppDbContext per request.
        // BulkActionValidator is transient (FluentValidation default lifetime).
        services.AddScoped<IUserManagementService, UserManagementService>();
        services.AddTransient<IValidator<BulkActionRequest>, BulkActionValidator>();

        return services;
    }
}
