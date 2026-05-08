using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Npgsql;
using PropelIQ.Modules.SharedServices.Application.Audit;
using PropelIQ.Modules.SharedServices.Infrastructure.Audit;
using PropelIQ.Modules.SharedServices.Infrastructure.Caching;
using PropelIQ.Modules.SharedServices.Infrastructure.Data;
using PropelIQ.Modules.SharedServices.Infrastructure.Data.Seed;
using PropelIQ.Modules.SharedServices.Infrastructure.Identity;
using PropelIQ.Modules.SharedServices.Infrastructure.Notifications;
using PropelIQ.SharedKernel.Auth;
using PropelIQ.SharedKernel.Caching;
using PropelIQ.SharedKernel.Notifications;

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

        return services;
    }
}
