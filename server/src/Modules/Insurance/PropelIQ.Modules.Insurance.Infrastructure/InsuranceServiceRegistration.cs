using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PropelIQ.Modules.Insurance.Application.Abstractions;
using PropelIQ.Modules.Insurance.Infrastructure.Configuration;
using PropelIQ.Modules.Insurance.Infrastructure.Security;
using QuestPDF.Infrastructure;

namespace PropelIQ.Modules.Insurance.Infrastructure;

/// <summary>
/// DI registration for the Insurance module infrastructure layer.
/// Called from the API composition root (Program.cs).
/// </summary>
public static class InsuranceServiceRegistration
{
    public static IServiceCollection AddInsuranceInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // US_039 AC-3: QuestPDF community licence (free, MIT — no SaaS revenue).
        // Must be set before any Document.Create() call.
        QuestPDF.Settings.License = LicenseType.Community;

        // US_038 AC-1: AES-256 field-level encryption (singletons — key material loaded once).
        services.AddSingleton<EncryptionKeyProvider>();
        services.AddSingleton<IEncryptionService, AesEncryptionService>();

        // US_038 task_002: Cloudflare R2 card image storage.
        // R2Configuration bound from "CloudflareR2" config section; env var overrides
        // credentials for production (Vault-managed secrets).
        var r2Section = configuration.GetSection(R2Configuration.SectionName);
        var r2Config = new R2Configuration
        {
            BucketName = r2Section[nameof(R2Configuration.BucketName)] ?? string.Empty,
            Endpoint = r2Section[nameof(R2Configuration.Endpoint)] ?? string.Empty,
            Region = r2Section[nameof(R2Configuration.Region)] ?? "auto",
            // Vault-injected env vars take precedence over appsettings (SECURITY: never log these).
            AccessKeyId = Environment.GetEnvironmentVariable("CLOUDFLARE_R2_ACCESS_KEY_ID")
                          ?? r2Section[nameof(R2Configuration.AccessKeyId)]
                          ?? string.Empty,
            SecretAccessKey = Environment.GetEnvironmentVariable("CLOUDFLARE_R2_SECRET_ACCESS_KEY")
                              ?? r2Section[nameof(R2Configuration.SecretAccessKey)]
                              ?? string.Empty,
        };
        services.AddSingleton(r2Config);
        services.AddSingleton<ICardImageStorageService, R2CardImageStorageService>();

        // Validation service — scoped (owns EF Core unit-of-work for audit record write).
        services.AddScoped<IInsuranceValidationService, InsuranceValidationService>();

        // Profile persistence service — scoped (EF Core upsert + encrypt/decrypt).
        services.AddScoped<IInsuranceProfileService, InsuranceProfileService>();

        // US_039: Insurance verification report (paged listing, PDF export, CSV export).
        services.AddScoped<IInsuranceReportService, InsuranceReportService>();

        // Background retry service — hosted service (singleton; resolves scoped deps per tick).
        services.AddHostedService<InsuranceValidationRetryService>();

        // US_038 NFR-007: key rotation — only registered when explicitly opt-in in config.
        if (configuration.GetValue<bool>("InsuranceEncryption:RotationEnabled"))
            services.AddHostedService<InsuranceKeyRotationService>();

        return services;
    }
}
