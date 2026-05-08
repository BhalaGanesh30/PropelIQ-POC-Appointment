using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PropelIQ.Modules.ClinicalIntelligence.Application.Abstractions;
using PropelIQ.Modules.ClinicalIntelligence.Infrastructure.AI;
using PropelIQ.Modules.ClinicalIntelligence.Infrastructure.Configuration;
using PropelIQ.Modules.ClinicalIntelligence.Infrastructure.Queues;
using PropelIQ.Modules.ClinicalIntelligence.Infrastructure.Repositories;
using PropelIQ.Modules.ClinicalIntelligence.Infrastructure.Services;
using PropelIQ.Modules.ClinicalIntelligence.Infrastructure.Storage;
using PropelIQ.Modules.ClinicalIntelligence.Infrastructure.Workers;
using PropelIQ.Modules.Insurance.Infrastructure.Configuration;

namespace PropelIQ.Modules.ClinicalIntelligence.Infrastructure;

/// <summary>
/// DI registration for the ClinicalIntelligence module infrastructure layer.
/// Called from the API composition root (Program.cs) to register
/// document upload services, ClamAV scan service, R2 storage, OCR worker, and retry worker.
/// </summary>
public static class ClinicalIntelligenceServiceRegistration
{
    public static IServiceCollection AddClinicalIntelligenceInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // ClamAV configuration
        services.Configure<ClamAvConfiguration>(configuration.GetSection("ClamAv"));

        // OCR configuration — bound from "Ocr" section
        services.Configure<OcrConfiguration>(configuration.GetSection(OcrConfiguration.SectionName));

        // R2 configuration — reuse the shared CloudflareR2 section from Insurance (same bucket)
        var r2Config = configuration
            .GetSection(R2Configuration.SectionName)
            .Get<R2Configuration>() ?? new R2Configuration();
        services.AddSingleton(r2Config);

        // Storage
        services.AddSingleton<IR2DocumentStorageService, R2DocumentStorageService>();

        // OCR channel — singleton for back-pressure queue (Edge Case 2)
        services.AddSingleton<OcrJobChannel>();
        // Extraction channel — singleton for extraction pipeline
        services.AddSingleton<ExtractionJobChannel>();

        // Repositories
        services.AddScoped<IClinicalDocumentRepository, ClinicalDocumentRepository>();
        services.AddScoped<IDeadLetterRepository, DeadLetterRepository>();

        // Services
        services.AddScoped<IMalwareScanService, ClamAvScanService>();
        services.AddScoped<IDocumentUploadService, DocumentUploadService>();
        services.AddScoped<IOcrProcessingService, TesseractOcrService>();
        services.AddScoped<IDocumentViewerService, DocumentViewerService>();
        // Extraction pipeline services
        services.AddScoped<IClinicalExtractionService, ClinicalExtractionService>();
        services.AddScoped<IClinicalFactRepository, ClinicalFactRepository>();
        services.AddScoped<INormalizationService, NormalizationService>();
        services.AddScoped<IPiiRedactionService, PiiRedactionService>();
        services.AddScoped<IPromptBuilder, ExtractionPromptBuilder>();
        services.AddScoped<IExtractionSchemaValidator, ExtractionSchemaValidator>();

        // Profile aggregation — scoped so it inherits the scoped DbContext via IClinicalFactRepository.
        services.AddScoped<IPatientProfileAggregationService, PatientProfileAggregationService>();

        // EP-007 US_046: Conflict detection services (FR-CA-003).
        // AddMemoryCache() is idempotent; required by ConflictRuleRepository for 5-min rule cache.
        services.AddMemoryCache();
        services.AddScoped<IConflictAlertRepository, ConflictAlertRepository>();
        services.AddScoped<IConflictRuleRepository, ConflictRuleRepository>();
        services.AddScoped<IConflictDetectionService, ConflictDetectionService>();
        services.AddScoped<IConflictCacheService, ConflictCacheService>();

        // EP-007 US_047: Fact editing, verification, and audit history (FR-CA-004).
        services.AddScoped<IAuditRepository, AuditRepository>();
        services.AddScoped<ICodingDecisionRepository, CodingDecisionRepository>();
        services.AddScoped<IFactEditingService, FactEditingService>();

        // EP-007 US_048: Clinical timeline aggregation (FR-CA-005).
        services.AddScoped<ITimelineService, TimelineService>();
        services.AddScoped<ITimelineCacheService, TimelineCacheService>();

        // Background workers (TR-005)
        services.AddHostedService<MalwareScanRetryService>();
        services.AddHostedService<OcrWorkerService>();
        services.AddHostedService<ExtractionWorkerService>();

        // Extraction configuration
        services.Configure<ExtractionConfiguration>(configuration.GetSection(ExtractionConfiguration.SectionName));

        return services;
    }
}

