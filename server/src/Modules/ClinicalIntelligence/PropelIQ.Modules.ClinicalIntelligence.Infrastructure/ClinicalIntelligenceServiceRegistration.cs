using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PropelIQ.Modules.ClinicalIntelligence.Application.Abstractions;
using PropelIQ.Modules.ClinicalIntelligence.Infrastructure.AI;
using PropelIQ.Modules.ClinicalIntelligence.Infrastructure.Configuration;
using PropelIQ.Modules.ClinicalIntelligence.Infrastructure.Options;
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

        // EP-008 US_049: ICD-10 coding suggestion pipeline (FR-CA-006).
        services.Configure<CodingSuggestionOptions>(configuration.GetSection("AI"));
        services.AddScoped<ICodingAiGatewayClient, CodingAiGatewayClient>();
        services.AddScoped<ICodingSchemaValidator, CodingSchemaValidator>();

        // Named HttpClient for the embedding /embeddings call — uses the same LiteLLM base URL.
        var liteLlmBaseUrl = configuration["AiGateway:BaseUrl"] ?? "http://localhost:4000";
        services.AddHttpClient<EvidenceRetrievalService>(client =>
            client.BaseAddress = new Uri(liteLlmBaseUrl));
        services.AddScoped<IEvidenceRetrievalService, EvidenceRetrievalService>();

        services.AddScoped<ICodingSuggestionOrchestrator, CodingSuggestionOrchestrator>();

        // EP-008 US_050: CPT/E/M suggestion pipeline (FR-MC-002 Hybrid).
        services.AddScoped<ICptCodeRepository, CptCodeRepository>();
        services.AddScoped<IAppointmentTypeMapper, AppointmentTypeMapper>();
        services.AddScoped<ICptCodeFreshnessService, CptCodeFreshnessService>();
        services.AddScoped<ICptCodeValidationService, CptCodeValidationService>();
        services.AddScoped<ICptCodingAiGatewayClient, CptCodingAiGatewayClient>();
        services.AddScoped<ICptSuggestionOrchestrator, CptSuggestionOrchestrator>();

        // EP-008 US_051: Accept/Modify/Reject coding decision workflow (FR-MC-003 Hybrid, AIR-005).
        services.AddScoped<ICodingDecisionWorkflowService, CodingDecisionWorkflowService>();

        // EP-008 US_052: Code search and favorites management (FR-MC-004 [DETERMINISTIC]).
        services.AddScoped<ICodeReferenceRepository, CodeReferenceRepository>();
        services.AddScoped<ICodeFavoriteRepository, CodeFavoriteRepository>();
        services.AddScoped<ICodeSearchService, CodeSearchService>();

        // Background workers (TR-005)
        services.AddHostedService<MalwareScanRetryService>();
        services.AddHostedService<OcrWorkerService>();
        services.AddHostedService<ExtractionWorkerService>();

        // Extraction configuration
        services.Configure<ExtractionConfiguration>(configuration.GetSection(ExtractionConfiguration.SectionName));

        return services;
    }
}

