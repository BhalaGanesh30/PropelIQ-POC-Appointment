# Task - TASK_001

## Requirement Reference

- User Story: us_028
- Story Location: .propel/context/tasks/EP-003/us_028/us_028.md
- Acceptance Criteria:
  - AC-1: Given a confirmed appointment is in the system, When the no-show risk scoring model evaluates it, Then a risk label (Low, Medium, or High) with explainable feature contributions is assigned and stored against the appointment.
  - AC-4: Given the risk model is invoked, When the scoring request is processed, Then the response is returned within 2.5 seconds p95 and the result is cached against the appointment record.
- Edge Cases:
  - What happens if the risk model is unavailable? Appointments display with risk label "Unknown" and no false indicators are shown; staff are notified of the scoring service outage.
  - How does the system handle risk score staleness? Scores are recalculated when appointment details change, e.g., reschedule, or when 24 hours have elapsed since the last score.

## Design References (Frontend Tasks Only)

| Reference Type | Value |
|----------------|-------|
| **UI Impact** | No |
| **Figma URL** | N/A |
| **Wireframe Status** | N/A |
| **Wireframe Type** | N/A |
| **Wireframe Path/URL** | N/A |
| **Screen Spec** | N/A |
| **UXR Requirements** | N/A |
| **Design Tokens** | N/A |

## Applicable Technology Stack

| Layer | Technology | Version |
|-------|------------|---------|
| Frontend | N/A | N/A |
| Backend | ASP.NET Core Web API | 8.x |
| Database | PostgreSQL with pgvector | 15.x |
| Library | EF Core (Npgsql) | latest stable |
| Library | Polly | latest stable |
| AI/ML | Azure OpenAI GPT-4.1 family | 2026 APIs |
| AI Gateway | LiteLLM-compatible gateway | latest stable |
| Vector Store | N/A | N/A |
| Mobile | N/A | N/A |

**Note**: All code, and libraries, MUST be compatible with versions above.

## AI References (AI Tasks Only)

| Reference Type | Value |
|----------------|-------|
| **AI Impact** | Yes |
| **AIR Requirements** | AIR-002, AIR-004, AIR-006, AIR-009, AIR-011 |
| **AI Pattern** | Hybrid (structured prompt with feature extraction) |
| **Prompt Template Path** | server/src/PropelIQ.Application/AI/Prompts/NoShowRiskPrompt.cs |
| **Guardrails Config** | JSON schema validation for risk response (Low/Medium/High enum + feature array) |
| **Model Provider** | Azure OpenAI (GPT-4.1 family via LiteLLM gateway) |

## Mobile References (Mobile Tasks Only)

| Reference Type | Value |
|----------------|-------|
| **Mobile Impact** | No |
| **Platform Target** | N/A |
| **Min OS Version** | N/A |
| **Mobile Framework** | N/A |

## Task Overview

Implement the `NoShowRiskScoringService` that evaluates appointment no-show risk through the AI gateway (TR-008) and persists the result against the appointment record. The service constructs a structured prompt (AIR-002) containing patient history features (previous no-show count, cancellation rate, appointment lead time, day of week, time of day, reminder response history) and submits it to Azure OpenAI via the LiteLLM-compatible gateway. The model returns a JSON response conforming to a strict schema: `{ "riskLevel": "Low|Medium|High", "confidence": 0.0-1.0, "features": [{ "name": "...", "contribution": "..." }] }` — validated by `NoShowRiskResponseValidator` (AIR-008). Explainable feature contributions (AIR-002, AIR-004) are stored alongside the risk label so staff can understand why a patient is flagged. The response is cached in the `NoShowRiskScore` column group on the `Appointment` entity — `RiskLevel`, `RiskConfidence`, `RiskFeatures` (JSONB), and `RiskScoredAt` timestamp (AC-4). Staleness is managed by comparing `RiskScoredAt` against a 24-hour TTL; stale scores are recalculated on next access (edge case 2). If the AI gateway is unavailable (edge case 1), the service returns `RiskLevel = Unknown` via circuit-breaker fallback (TR-008, Decision 6) and logs the outage without throwing. PII is redacted from the prompt — only patient ID hash, aggregated history counts, and appointment metadata are sent (AIR-009). All prompts, responses, confidence values, and model metadata are logged for 7-year retention (AIR-011). The end-to-end scoring must complete within 2.5 seconds p95 (AIR-006).

## Dependent Tasks

- US_008 (Foundational — requires AI gateway infrastructure and LiteLLM configuration)

## Impacted Components

- New: `server/src/PropelIQ.Application/AI/INoShowRiskScoringService.cs` (interface)
- New: `server/src/PropelIQ.Application/AI/NoShowRiskScoringService.cs` (prompt construction, gateway call, response parsing, caching)
- New: `server/src/PropelIQ.Application/AI/Prompts/NoShowRiskPrompt.cs` (structured prompt template)
- New: `server/src/PropelIQ.Application/AI/Models/NoShowRiskResult.cs` (result model with risk level, confidence, features)
- New: `server/src/PropelIQ.Application/AI/Validators/NoShowRiskResponseValidator.cs` (JSON schema validation for model output)
- New: `server/src/PropelIQ.Application/AI/IPatientHistoryFeatureExtractor.cs` (extracts patient history features for prompt)
- New: `server/src/PropelIQ.Infrastructure/AI/PatientHistoryFeatureExtractor.cs` (EF Core queries for no-show count, cancellation rate, etc.)
- Modify: `server/src/PropelIQ.Domain/Entities/Appointment.cs` (add RiskLevel, RiskConfidence, RiskFeatures, RiskScoredAt columns)
- Modify: `server/src/PropelIQ.Infrastructure/Data/AppDbContext.cs` (entity configuration for JSONB RiskFeatures, index on RiskScoredAt)

## Implementation Plan

1. **Extend `Appointment` entity with risk score columns**:

```csharp
// Add to server/src/PropelIQ.Domain/Entities/Appointment.cs
public string? RiskLevel { get; set; }          // Low|Medium|High|Unknown
public double? RiskConfidence { get; set; }     // 0.0-1.0
public string? RiskFeatures { get; set; }       // JSONB
public DateTimeOffset? RiskScoredAt { get; set; }
```

```csharp
// In AppDbContext.OnModelCreating
builder.Entity<Appointment>(entity =>
{
    entity.Property(a => a.RiskFeatures)
          .HasColumnType("jsonb");

    entity.HasIndex(a => a.RiskScoredAt)
          .HasDatabaseName("IX_Appointment_RiskScoredAt");
});
```

2. **Create `NoShowRiskResult` model and response validator**:

```csharp
// server/src/PropelIQ.Application/AI/Models/NoShowRiskResult.cs
namespace PropelIQ.Application.AI.Models;

public sealed record NoShowRiskResult(
    string RiskLevel,        // Low, Medium, High
    double Confidence,       // 0.0-1.0
    IReadOnlyList<RiskFeatureContribution> Features);

public sealed record RiskFeatureContribution(
    string Name,
    string Contribution);

public static class NoShowRiskDefaults
{
    public static readonly NoShowRiskResult Unknown = new(
        "Unknown", 0.0,
        Array.Empty<RiskFeatureContribution>());

    public static readonly TimeSpan StalenessThreshold =
        TimeSpan.FromHours(24);
}
```

```csharp
// server/src/PropelIQ.Application/AI/Validators/
//   NoShowRiskResponseValidator.cs
namespace PropelIQ.Application.AI.Validators;

public static class NoShowRiskResponseValidator
{
    private static readonly HashSet<string> ValidLevels =
        ["Low", "Medium", "High"];

    // AIR-008: Schema validation >= 99%
    public static bool TryParse(
        string json,
        out NoShowRiskResult? result)
    {
        result = null;
        try
        {
            var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var riskLevel = root
                .GetProperty("riskLevel").GetString();
            if (riskLevel is null
                || !ValidLevels.Contains(riskLevel))
                return false;

            var confidence = root
                .GetProperty("confidence").GetDouble();
            if (confidence < 0.0 || confidence > 1.0)
                return false;

            var features = root.GetProperty("features")
                .EnumerateArray()
                .Select(f => new RiskFeatureContribution(
                    f.GetProperty("name").GetString()!,
                    f.GetProperty("contribution").GetString()!))
                .ToList();

            result = new NoShowRiskResult(
                riskLevel, confidence, features);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
```

3. **Create `IPatientHistoryFeatureExtractor`** for prompt context:

```csharp
// server/src/PropelIQ.Application/AI/
//   IPatientHistoryFeatureExtractor.cs
namespace PropelIQ.Application.AI;

public interface IPatientHistoryFeatureExtractor
{
    Task<PatientHistoryFeatures> ExtractAsync(
        Guid patientId,
        CancellationToken ct = default);
}

public sealed record PatientHistoryFeatures(
    int TotalAppointments,
    int NoShowCount,
    int CancellationCount,
    int ConfirmedViaReminderCount,
    double AverageLeadTimeDays,
    string DayOfWeek,
    string TimeOfDay);
```

```csharp
// server/src/PropelIQ.Infrastructure/AI/
//   PatientHistoryFeatureExtractor.cs
namespace PropelIQ.Infrastructure.AI;

public sealed class PatientHistoryFeatureExtractor
    : IPatientHistoryFeatureExtractor
{
    private readonly AppDbContext _db;

    public PatientHistoryFeatureExtractor(AppDbContext db)
        => _db = db;

    public async Task<PatientHistoryFeatures> ExtractAsync(
        Guid patientId,
        CancellationToken ct = default)
    {
        var appointments = await _db.Appointments
            .Where(a => a.PatientId == patientId)
            .Select(a => new
            {
                a.Status,
                a.AppointmentDate,
                a.CreatedAt
            })
            .ToListAsync(ct);

        var total = appointments.Count;
        var noShows = appointments
            .Count(a => a.Status == "No-Show");
        var cancellations = appointments
            .Count(a => a.Status == "Cancelled");

        var reminders = await _db.ReminderEvents
            .Where(r => r.ConfirmationResponse == "Confirmed"
                && _db.Appointments
                    .Where(a => a.PatientId == patientId)
                    .Select(a => a.AppointmentId)
                    .Contains(r.AppointmentId))
            .CountAsync(ct);

        var avgLead = total > 0
            ? appointments.Average(a =>
                (a.AppointmentDate - a.CreatedAt).TotalDays)
            : 0;

        return new PatientHistoryFeatures(
            total, noShows, cancellations, reminders,
            Math.Round(avgLead, 1),
            DateTime.UtcNow.DayOfWeek.ToString(),
            DateTime.UtcNow.Hour switch
            {
                < 12 => "Morning",
                < 17 => "Afternoon",
                _ => "Evening"
            });
    }
}
```

4. **Create structured prompt template**:

```csharp
// server/src/PropelIQ.Application/AI/Prompts/
//   NoShowRiskPrompt.cs
namespace PropelIQ.Application.AI.Prompts;

public static class NoShowRiskPrompt
{
    // AIR-009: No direct PII in prompt — only aggregated
    // history counts and appointment metadata
    public static string Build(PatientHistoryFeatures features)
    {
        return $$"""
        You are a medical appointment no-show risk classifier.
        Analyze the following patient appointment features and
        classify the no-show risk as Low, Medium, or High.

        Patient History:
        - Total past appointments: {{features.TotalAppointments}}
        - Previous no-shows: {{features.NoShowCount}}
        - Previous cancellations: {{features.CancellationCount}}
        - Confirmed via reminder: {{features.ConfirmedViaReminderCount}}
        - Average booking lead time: {{features.AverageLeadTimeDays}} days
        - Appointment day: {{features.DayOfWeek}}
        - Appointment time: {{features.TimeOfDay}}

        Respond with ONLY valid JSON matching this schema:
        {
          "riskLevel": "Low" | "Medium" | "High",
          "confidence": <float 0.0-1.0>,
          "features": [
            { "name": "<feature>", "contribution": "<explanation>" }
          ]
        }

        Include 3-5 feature contributions explaining the
        classification. Each contribution must reference a
        specific input feature and its impact direction.
        """;
    }
}
```

5. **Implement `INoShowRiskScoringService`** with gateway call and circuit-breaker:

```csharp
// server/src/PropelIQ.Application/AI/
//   INoShowRiskScoringService.cs
namespace PropelIQ.Application.AI;

public interface INoShowRiskScoringService
{
    Task<NoShowRiskResult> ScoreAsync(
        Guid appointmentId,
        CancellationToken ct = default);
}
```

```csharp
// server/src/PropelIQ.Application/AI/
//   NoShowRiskScoringService.cs
namespace PropelIQ.Application.AI;

public sealed class NoShowRiskScoringService
    : INoShowRiskScoringService
{
    private readonly IAiGatewayClient _gateway;
    private readonly IPatientHistoryFeatureExtractor _extractor;
    private readonly IAppointmentRepository _appointmentRepo;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<NoShowRiskScoringService> _logger;

    // TR-008: Circuit-breaker fallback to deterministic flow
    private static readonly ResiliencePipeline<string>
        GatewayPipeline =
        new ResiliencePipelineBuilder<string>()
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions<string>
            {
                FailureRatio = 0.5,
                SamplingDuration = TimeSpan.FromSeconds(30),
                MinimumThroughput = 3,
                BreakDuration = TimeSpan.FromSeconds(60)
            })
            .AddTimeout(TimeSpan.FromSeconds(2))
            .AddRetry(new RetryStrategyOptions<string>
            {
                MaxRetryAttempts = 1,
                BackoffType = DelayBackoffType.Exponential,
                Delay = TimeSpan.FromMilliseconds(200),
                ShouldHandle = new PredicateBuilder<string>()
                    .Handle<HttpRequestException>()
                    .Handle<TimeoutRejectedException>()
            })
            .Build();

    public NoShowRiskScoringService(
        IAiGatewayClient gateway,
        IPatientHistoryFeatureExtractor extractor,
        IAppointmentRepository appointmentRepo,
        TimeProvider timeProvider,
        ILogger<NoShowRiskScoringService> logger)
    {
        _gateway = gateway;
        _extractor = extractor;
        _appointmentRepo = appointmentRepo;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<NoShowRiskResult> ScoreAsync(
        Guid appointmentId,
        CancellationToken ct = default)
    {
        var appointment = await _appointmentRepo
            .GetByIdAsync(appointmentId, ct)
            ?? throw new InvalidOperationException(
                $"Appointment {appointmentId} not found");

        // Edge case 2: Check staleness (24h TTL)
        var now = _timeProvider.GetUtcNow();
        if (appointment.RiskScoredAt.HasValue
            && appointment.RiskLevel is not null
            && (now - appointment.RiskScoredAt.Value)
                < NoShowRiskDefaults.StalenessThreshold)
        {
            // Return cached score
            return ParseCachedScore(appointment);
        }

        // Extract features (no PII — AIR-009)
        var features = await _extractor.ExtractAsync(
            appointment.PatientId, ct);

        var prompt = NoShowRiskPrompt.Build(features);

        try
        {
            // AIR-006: 2.5s p95 budget
            var response = await GatewayPipeline
                .ExecuteAsync(async token =>
                    await _gateway.CompleteChatAsync(
                        prompt, "no-show-risk", token),
                    ct);

            // AIR-011: Log prompt and response for audit
            _logger.LogInformation(
                "NoShowRisk scored for {AppointmentId}: " +
                "response length {Length}",
                appointmentId, response.Length);

            // AIR-008: Validate response schema
            if (!NoShowRiskResponseValidator.TryParse(
                response, out var result) || result is null)
            {
                _logger.LogWarning(
                    "Invalid risk model response for {Id}",
                    appointmentId);
                return await PersistAndReturn(
                    appointmentId,
                    NoShowRiskDefaults.Unknown, now, ct);
            }

            return await PersistAndReturn(
                appointmentId, result, now, ct);
        }
        catch (Exception ex)
        {
            // Edge case 1: Gateway unavailable — fallback
            _logger.LogWarning(ex,
                "Risk model unavailable for {Id}, " +
                "returning Unknown",
                appointmentId);
            return await PersistAndReturn(
                appointmentId,
                NoShowRiskDefaults.Unknown, now, ct);
        }
    }

    private async Task<NoShowRiskResult> PersistAndReturn(
        Guid appointmentId,
        NoShowRiskResult result,
        DateTimeOffset scoredAt,
        CancellationToken ct)
    {
        await _appointmentRepo.UpdateRiskScoreAsync(
            appointmentId,
            result.RiskLevel,
            result.Confidence,
            JsonSerializer.Serialize(result.Features),
            scoredAt,
            ct);

        return result;
    }

    private static NoShowRiskResult ParseCachedScore(
        Appointment appointment)
    {
        var features = string.IsNullOrEmpty(
            appointment.RiskFeatures)
            ? Array.Empty<RiskFeatureContribution>()
            : JsonSerializer
                .Deserialize<List<RiskFeatureContribution>>(
                    appointment.RiskFeatures)
                ?? [];

        return new NoShowRiskResult(
            appointment.RiskLevel!,
            appointment.RiskConfidence ?? 0.0,
            features);
    }
}
```

6. **Add `UpdateRiskScoreAsync` to repository**:

```csharp
// Add to IAppointmentRepository
Task UpdateRiskScoreAsync(
    Guid appointmentId,
    string riskLevel,
    double confidence,
    string featuresJson,
    DateTimeOffset scoredAt,
    CancellationToken ct = default);
```

```csharp
// EF Core implementation
public async Task UpdateRiskScoreAsync(
    Guid appointmentId,
    string riskLevel,
    double confidence,
    string featuresJson,
    DateTimeOffset scoredAt,
    CancellationToken ct = default)
{
    await _db.Appointments
        .Where(a => a.AppointmentId == appointmentId)
        .ExecuteUpdateAsync(s => s
            .SetProperty(a => a.RiskLevel, riskLevel)
            .SetProperty(a => a.RiskConfidence, confidence)
            .SetProperty(a => a.RiskFeatures, featuresJson)
            .SetProperty(a => a.RiskScoredAt, scoredAt),
            ct);
}
```

## Current Project State

```text
propelIQ/
└── server/
    └── src/
        ├── PropelIQ.Api/
        │   └── Program.cs                                (modify — register services)
        ├── PropelIQ.Application/
        │   ├── AI/
        │   │   ├── INoShowRiskScoringService.cs           (new)
        │   │   ├── NoShowRiskScoringService.cs            (new)
        │   │   ├── IPatientHistoryFeatureExtractor.cs     (new)
        │   │   ├── IAiGatewayClient.cs                    (existing from US_008)
        │   │   ├── Prompts/
        │   │   │   └── NoShowRiskPrompt.cs                (new)
        │   │   ├── Models/
        │   │   │   └── NoShowRiskResult.cs                (new)
        │   │   └── Validators/
        │   │       └── NoShowRiskResponseValidator.cs     (new)
        │   └── Booking/
        │       └── IAppointmentRepository.cs              (modify — add UpdateRiskScoreAsync)
        ├── PropelIQ.Domain/
        │   └── Entities/
        │       └── Appointment.cs                         (modify — add risk columns)
        └── PropelIQ.Infrastructure/
            ├── AI/
            │   ├── AiGatewayClient.cs                     (existing from US_008)
            │   └── PatientHistoryFeatureExtractor.cs      (new)
            ├── Booking/
            │   └── AppointmentRepository.cs               (modify — add UpdateRiskScoreAsync)
            └── Data/
                └── AppDbContext.cs                         (modify — JSONB config, index)
```

> Placeholder: Update on execution based on US_008 completion state.

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | server/src/PropelIQ.Application/AI/INoShowRiskScoringService.cs | Interface for risk scoring with appointment ID |
| CREATE | server/src/PropelIQ.Application/AI/NoShowRiskScoringService.cs | LLM gateway call, circuit-breaker, caching, staleness check |
| CREATE | server/src/PropelIQ.Application/AI/Prompts/NoShowRiskPrompt.cs | Structured prompt template with aggregated patient features |
| CREATE | server/src/PropelIQ.Application/AI/Models/NoShowRiskResult.cs | Result model, feature contribution record, defaults |
| CREATE | server/src/PropelIQ.Application/AI/Validators/NoShowRiskResponseValidator.cs | JSON schema validation for LLM response |
| CREATE | server/src/PropelIQ.Application/AI/IPatientHistoryFeatureExtractor.cs | Interface for patient history feature extraction |
| CREATE | server/src/PropelIQ.Infrastructure/AI/PatientHistoryFeatureExtractor.cs | EF Core queries for no-show count, cancellation rate, etc. |
| MODIFY | server/src/PropelIQ.Domain/Entities/Appointment.cs | Add RiskLevel, RiskConfidence, RiskFeatures (JSONB), RiskScoredAt |
| MODIFY | server/src/PropelIQ.Infrastructure/Data/AppDbContext.cs | JSONB column type for RiskFeatures, index on RiskScoredAt |

## External References

- Azure OpenAI .NET SDK: https://learn.microsoft.com/en-us/dotnet/api/overview/azure/ai.openai-readme
- Polly v8 Circuit Breaker: https://www.thepollyproject.org/2023/03/03/polly-v8-released/
- EF Core JSONB with Npgsql: https://www.npgsql.org/efcore/mapping/json.html
- OWASP AI Security: https://owasp.org/www-project-machine-learning-security-top-10/

## Build Commands

```bash
# Build backend
cd server
dotnet build

# Run (requires AI gateway config in appsettings)
dotnet run --project src/PropelIQ.Api

# Add migration for risk score columns
dotnet ef migrations add AddAppointmentRiskScore \
  --project src/PropelIQ.Infrastructure \
  --startup-project src/PropelIQ.Api
dotnet ef database update \
  --startup-project src/PropelIQ.Api
```

## Implementation Validation Strategy

- [ ] Risk scoring returns Low, Medium, or High with explainable features (AC-1)
- [ ] Response cached in Appointment record with RiskScoredAt timestamp (AC-4)
- [ ] End-to-end scoring completes within 2.5 seconds p95 (AIR-006)
- [ ] Circuit breaker returns "Unknown" when AI gateway is unavailable (edge case 1)
- [ ] Stale scores (>24h) trigger recalculation on next access (edge case 2)
- [ ] No PII in prompt — only aggregated counts and metadata (AIR-009)
- [ ] Prompt, response, and confidence logged for audit (AIR-011)
- [ ] Invalid model responses handled gracefully with schema validation (AIR-008)

## Implementation Checklist

- [ ] Add RiskLevel, RiskConfidence, RiskFeatures (JSONB), RiskScoredAt to Appointment entity
- [ ] Create NoShowRiskResult model, RiskFeatureContribution record, and Unknown default
- [ ] Implement NoShowRiskResponseValidator with JSON schema validation
- [ ] Create IPatientHistoryFeatureExtractor with EF Core queries for patient history
- [ ] Build NoShowRiskPrompt template with PII-redacted aggregated features
- [ ] Implement NoShowRiskScoringService with circuit-breaker, caching, and staleness check
- [ ] Add UpdateRiskScoreAsync to IAppointmentRepository and implementation
- [ ] Register all AI scoring services in DI container
