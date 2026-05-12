# Task - TASK_001

## Requirement Reference

- User Story: us_059
- Story Location: .propel/context/tasks/EP-011/us_059/us_059.md
- Acceptance Criteria:
  - AC-1: Given I am authenticated as an Admin, When I update a system configuration (e.g., reminder cadence, session timeout, slot duration template), Then the change is validated, saved with a version number and timestamp, and takes effect for new events from that point forward.
  - AC-2: Given I submit an invalid configuration value (e.g., a session timeout below the minimum), When the validation runs, Then the save is blocked and a descriptive error message explains the constraint.
  - AC-3: Given I want to review configuration history, When I open the configuration version history, Then all previous versions are listed with the change date, changed by (admin identity), and the before/after values.
  - AC-4: Given a configuration rollback is needed, When I select a previous version and click "Restore," Then the previous configuration is reapplied as a new version (not an overwrite) and takes effect immediately.
- Edge Cases:
  - What happens if two admins change the same configuration simultaneously? Optimistic concurrency control detects the conflict; the second admin is shown the current value and must confirm or cancel their change.
  - How does the system handle configurations that affect in-progress operations? Configuration changes are applied to new operations only; in-flight reminders or sessions use the configuration active at their creation time.

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
| Library | EF Core | 8.x |
| Library | FluentValidation | 11.x |
| AI/ML | N/A | N/A |
| Vector Store | N/A | N/A |
| AI Gateway | N/A | N/A |
| Mobile | N/A | N/A |

**Note**: All code, and libraries, MUST be compatible with versions above.

## AI References (AI Tasks Only)

| Reference Type | Value |
|----------------|-------|
| **AI Impact** | No |
| **AIR Requirements** | N/A |
| **AI Pattern** | N/A |
| **Prompt Template Path** | N/A |
| **Guardrails Config** | N/A |
| **Model Provider** | N/A |

## Mobile References (Mobile Tasks Only)

| Reference Type | Value |
|----------------|-------|
| **Mobile Impact** | No |
| **Platform Target** | N/A |
| **Min OS Version** | N/A |
| **Mobile Framework** | N/A |

## Task Overview

Implement the backend configuration management service with versioned persistence, constraint validation, optimistic concurrency, rollback capability, and admin-only REST API. An `IConfigurationService` contract in the Application layer provides `GetCurrentAsync(category)`, `UpdateAsync(category, values, adminId, expectedVersion)`, `GetHistoryAsync(category)`, and `RestoreVersionAsync(category, versionId, adminId)`. The service manages four configuration categories defined by FR-AD-001: slot templates (duration, buffer time, availability windows), reminder rules (cadence intervals, channel preferences, escalation thresholds), session policy (timeout minutes, warning lead, max concurrent sessions), and communication templates (default sender, reply-to, footer text). Each `UpdateAsync` call validates the submitted values via a category-specific `IConfigurationValidator` registered through FluentValidation — for example, session timeout must be >= 5 minutes and <= 60 minutes (AC-2). Valid changes are persisted as a new `ConfigurationVersion` row (never an UPDATE) with auto-incremented version number, timestamp, admin identity, and a JSONB diff of before/after values (AC-1, AC-3). Optimistic concurrency is enforced by comparing the `expectedVersion` parameter against the current latest version; a mismatch returns 409 Conflict with the current value so the frontend can present the conflict to the admin (edge case 1). The `RestoreVersionAsync` method reads the target version's snapshot, validates it against current rules, and persists it as a new version with a `RestoredFromVersion` reference (AC-4). A `ConfigurationCacheService` maintains an in-memory dictionary refreshed on write; consumers (reminder worker, session middleware, slot service) read from cache. Configuration changes apply only to new operations — in-flight processes retain the version active at their start (edge case 2) by capturing the version ID at operation creation time. An `IAuditRecordService.WriteAsync` call logs every configuration change as a `ConfigChanged` audit event. Endpoints: `GET /api/v1/admin/config/{category}` (current config), `PUT /api/v1/admin/config/{category}` (update with version header), `GET /api/v1/admin/config/{category}/history` (version list), `POST /api/v1/admin/config/{category}/restore/{versionId}` (rollback).

## Dependent Tasks

- US_015 task_001 (requires Admin role authorization via RBAC)
- US_056 task_001 (requires `IAuditRecordService` for configuration change audit logging)
- US_059 task_002 (requires `configuration_versions` table and seed data)

## Impacted Components

- New: `server/src/PropelIQ.Application/Configuration/IConfigurationService.cs` (service contract)
- New: `server/src/PropelIQ.Application/Configuration/ConfigurationCategory.cs` (enum for slot_templates, reminder_rules, session_policy, communication_templates)
- New: `server/src/PropelIQ.Application/Configuration/ConfigurationUpdateRequest.cs` (update DTO with expectedVersion)
- New: `server/src/PropelIQ.Application/Configuration/ConfigurationVersionDto.cs` (history response DTO)
- New: `server/src/PropelIQ.Application/Configuration/Validators/SlotTemplateValidator.cs` (FluentValidation rules)
- New: `server/src/PropelIQ.Application/Configuration/Validators/ReminderRuleValidator.cs` (FluentValidation rules)
- New: `server/src/PropelIQ.Application/Configuration/Validators/SessionPolicyValidator.cs` (FluentValidation rules)
- New: `server/src/PropelIQ.Application/Configuration/Validators/CommunicationTemplateValidator.cs` (FluentValidation rules)
- New: `server/src/PropelIQ.Infrastructure/Configuration/ConfigurationService.cs` (versioned persistence with OCC)
- New: `server/src/PropelIQ.Infrastructure/Configuration/ConfigurationCacheService.cs` (in-memory cache with write-through)
- New: `server/src/PropelIQ.Api/Controllers/Admin/ConfigurationController.cs` (REST endpoints)
- Modify: `server/src/PropelIQ.Api/Program.cs` (register configuration services and validators)

## Implementation Plan

1. **Define `ConfigurationCategory` enum and `IConfigurationService` contract**:

```csharp
// server/src/PropelIQ.Application/Configuration/
//   ConfigurationCategory.cs
namespace PropelIQ.Application.Configuration;

public enum ConfigurationCategory
{
    SlotTemplates,
    ReminderRules,
    SessionPolicy,
    CommunicationTemplates
}
```

```csharp
// server/src/PropelIQ.Application/Configuration/
//   IConfigurationService.cs
namespace PropelIQ.Application.Configuration;

public interface IConfigurationService
{
    Task<ConfigurationSnapshot> GetCurrentAsync(
        ConfigurationCategory category,
        CancellationToken ct = default);

    Task<ConfigurationUpdateResult> UpdateAsync(
        ConfigurationCategory category,
        ConfigurationUpdateRequest request,
        CancellationToken ct = default);

    Task<IReadOnlyList<ConfigurationVersionDto>>
        GetHistoryAsync(
            ConfigurationCategory category,
            CancellationToken ct = default);

    Task<ConfigurationUpdateResult>
        RestoreVersionAsync(
            ConfigurationCategory category,
            Guid versionId,
            Guid adminId,
            CancellationToken ct = default);
}

public sealed record ConfigurationSnapshot
{
    public required Guid VersionId { get; init; }
    public required int VersionNumber { get; init; }
    public required ConfigurationCategory Category
        { get; init; }
    public required Dictionary<string, object> Values
        { get; init; }
    public required DateTime UpdatedAtUtc { get; init; }
    public required string UpdatedByName { get; init; }
}

public sealed record ConfigurationUpdateResult
{
    public required bool Success { get; init; }
    public Guid? VersionId { get; init; }
    public int? VersionNumber { get; init; }
    public bool ConflictDetected { get; init; }
    public ConfigurationSnapshot? CurrentValue
        { get; init; }
    public IReadOnlyList<string>? ValidationErrors
        { get; init; }
}
```

2. **Implement category-specific FluentValidation validators**. Each validator enforces business constraints per FR-AD-001:

```csharp
// server/src/PropelIQ.Application/Configuration/
//   Validators/SessionPolicyValidator.cs
namespace PropelIQ.Application.Configuration
    .Validators;

public sealed class SessionPolicyValidator
    : AbstractValidator<Dictionary<string, object>>
{
    public SessionPolicyValidator()
    {
        RuleFor(v => v)
            .Must(v => v.ContainsKey("timeoutMinutes"))
            .WithMessage(
                "Session timeout is required.");

        RuleFor(v => Convert.ToInt32(
                v["timeoutMinutes"]))
            .InclusiveBetween(5, 60)
            .WithMessage(
                "Session timeout must be between "
                + "5 and 60 minutes.");

        RuleFor(v => Convert.ToInt32(
                v["warningLeadMinutes"]))
            .InclusiveBetween(1, 10)
            .When(v => v.ContainsKey(
                "warningLeadMinutes"))
            .WithMessage(
                "Warning lead must be between "
                + "1 and 10 minutes.");
    }
}
```

Similar validators for `SlotTemplateValidator` (duration 5–120 min, buffer 0–30 min), `ReminderRuleValidator` (cadence >= 1 hour, max reminders 1–10), and `CommunicationTemplateValidator` (sender email format, footer max 500 chars). Each returns descriptive error messages per AC-2.

3. **Implement `ConfigurationService`** in the Infrastructure layer. On `UpdateAsync`: (a) load the current latest version for the category, (b) compare `expectedVersion` against the loaded version — if mismatch, return `ConflictDetected = true` with the current snapshot (edge case 1), (c) resolve the appropriate validator from DI and validate, (d) compute a JSONB diff between old and new values, (e) insert a new `ConfigurationVersion` row with incremented version number, (f) write-through to `ConfigurationCacheService`, (g) emit `ConfigChanged` audit event via `IAuditRecordService`. On `RestoreVersionAsync`: (a) load the target version's snapshot, (b) validate against current rules (AC-4 — ensures restored config still meets constraints), (c) persist as new version with `restored_from_version_id` reference.

4. **Implement `ConfigurationCacheService`** as a singleton that maintains `ConcurrentDictionary<ConfigurationCategory, ConfigurationSnapshot>`. Populated on application startup via `IHostedService` reading latest versions from the database. Updated on every successful write. Consumers (`ReminderWorker`, session middleware, slot service) read from cache for zero-latency access. Edge case 2 is handled by capturing `versionId` at operation creation — the in-flight operation references its captured version, not the cache.

5. **Implement `ConfigurationController`** with Admin-only endpoints:

```csharp
// server/src/PropelIQ.Api/Controllers/Admin/
//   ConfigurationController.cs
namespace PropelIQ.Api.Controllers.Admin;

[ApiController]
[Route("api/v1/admin/config")]
[Authorize(Roles = "Admin")]
public sealed class ConfigurationController
    : ControllerBase
{
    private readonly IConfigurationService _service;

    public ConfigurationController(
        IConfigurationService service)
        => _service = service;

    // GET /api/v1/admin/config/{category}
    [HttpGet("{category}")]
    public async Task<IActionResult> GetCurrent(
        ConfigurationCategory category,
        CancellationToken ct)
    {
        var snapshot = await _service
            .GetCurrentAsync(category, ct);
        Response.Headers["ETag"] =
            $"\"{snapshot.VersionNumber}\"";
        return Ok(snapshot);
    }

    // PUT /api/v1/admin/config/{category}
    // Requires If-Match header for OCC (edge case 1)
    [HttpPut("{category}")]
    public async Task<IActionResult> Update(
        ConfigurationCategory category,
        [FromBody] ConfigurationUpdateRequest request,
        CancellationToken ct)
    {
        if (!Request.Headers.TryGetValue(
                "If-Match", out var etagHeader))
            return BadRequest(
                "If-Match header required.");

        if (!int.TryParse(
                etagHeader.ToString()
                    .Trim('"'), out var version))
            return BadRequest(
                "Invalid If-Match header.");

        request = request with
        {
            ExpectedVersion = version,
            AdminId = GetAdminId()
        };

        var result = await _service
            .UpdateAsync(category, request, ct);

        if (result.ConflictDetected)
            return Conflict(result.CurrentValue);

        if (result.ValidationErrors?.Count > 0)
            return UnprocessableEntity(
                result.ValidationErrors);

        return Ok(new
        {
            result.VersionId,
            result.VersionNumber
        });
    }

    // GET /api/v1/admin/config/{category}/history
    [HttpGet("{category}/history")]
    public async Task<IActionResult> GetHistory(
        ConfigurationCategory category,
        CancellationToken ct)
    {
        var history = await _service
            .GetHistoryAsync(category, ct);
        return Ok(history);
    }

    // POST /api/v1/admin/config/{category}/
    //   restore/{versionId}
    [HttpPost("{category}/restore/{versionId:guid}")]
    public async Task<IActionResult> Restore(
        ConfigurationCategory category,
        Guid versionId,
        CancellationToken ct)
    {
        var result = await _service
            .RestoreVersionAsync(
                category, versionId,
                GetAdminId(), ct);

        if (result.ValidationErrors?.Count > 0)
            return UnprocessableEntity(
                result.ValidationErrors);

        return Ok(new
        {
            result.VersionId,
            result.VersionNumber
        });
    }

    private Guid GetAdminId() =>
        Guid.Parse(User.FindFirst("sub")!.Value);
}
```

6. **Register services and validators in `Program.cs`**: `IConfigurationService` as scoped, `ConfigurationCacheService` as singleton + hosted service for startup population, all four category validators, and the controller. Add ETag middleware for configuration responses.

## Current Project State

```text
propelIQ/
└── server/
    └── src/
        ├── PropelIQ.Api/
        │   ├── Controllers/
        │   │   └── Admin/
        │   │       └── ConfigurationController.cs       (new)
        │   └── Program.cs                                (modify)
        ├── PropelIQ.Application/
        │   └── Configuration/
        │       ├── IConfigurationService.cs              (new)
        │       ├── ConfigurationCategory.cs              (new)
        │       ├── ConfigurationUpdateRequest.cs         (new)
        │       ├── ConfigurationVersionDto.cs            (new)
        │       └── Validators/
        │           ├── SlotTemplateValidator.cs          (new)
        │           ├── ReminderRuleValidator.cs          (new)
        │           ├── SessionPolicyValidator.cs         (new)
        │           └── CommunicationTemplateValidator.cs (new)
        └── PropelIQ.Infrastructure/
            └── Configuration/
                ├── ConfigurationService.cs               (new)
                └── ConfigurationCacheService.cs          (new)
```

> Placeholder: Update on execution based on US_059 task_002 and US_056 task_001 completion state.

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | server/src/PropelIQ.Application/Configuration/IConfigurationService.cs | Service contract with GetCurrent, Update, GetHistory, RestoreVersion |
| CREATE | server/src/PropelIQ.Application/Configuration/ConfigurationCategory.cs | Enum for 4 configuration categories from FR-AD-001 |
| CREATE | server/src/PropelIQ.Application/Configuration/ConfigurationUpdateRequest.cs | Update DTO with values, expectedVersion, adminId |
| CREATE | server/src/PropelIQ.Application/Configuration/ConfigurationVersionDto.cs | History response with version, timestamp, admin, before/after diff |
| CREATE | server/src/PropelIQ.Application/Configuration/Validators/SlotTemplateValidator.cs | Duration 5-120 min, buffer 0-30 min constraints |
| CREATE | server/src/PropelIQ.Application/Configuration/Validators/ReminderRuleValidator.cs | Cadence >= 1h, max reminders 1-10 constraints |
| CREATE | server/src/PropelIQ.Application/Configuration/Validators/SessionPolicyValidator.cs | Timeout 5-60 min, warning lead 1-10 min constraints |
| CREATE | server/src/PropelIQ.Application/Configuration/Validators/CommunicationTemplateValidator.cs | Email format, footer max 500 chars constraints |
| CREATE | server/src/PropelIQ.Infrastructure/Configuration/ConfigurationService.cs | Versioned persistence with OCC, JSONB diff, audit logging |
| CREATE | server/src/PropelIQ.Infrastructure/Configuration/ConfigurationCacheService.cs | Singleton cache with startup population and write-through |
| CREATE | server/src/PropelIQ.Api/Controllers/Admin/ConfigurationController.cs | Admin-only REST endpoints with ETag-based OCC |
| MODIFY | server/src/PropelIQ.Api/Program.cs | Register configuration services, validators, cache, and hosted service |

## External References

- ASP.NET Core ETag Concurrency: https://learn.microsoft.com/en-us/aspnet/core/web-api/handle-concurrency
- FluentValidation ASP.NET Integration: https://docs.fluentvalidation.net/en/latest/aspnet.html
- EF Core Concurrency Tokens: https://learn.microsoft.com/en-us/ef/core/saving/concurrency
- ConcurrentDictionary: https://learn.microsoft.com/en-us/dotnet/api/system.collections.concurrent.concurrentdictionary-2

## Build Commands

```bash
# Build backend
cd server
dotnet build

# Run backend
dotnet run --project src/PropelIQ.Api

# Test configuration endpoints:
# 1. GET /api/v1/admin/config/SessionPolicy
# 2. PUT with If-Match header and new values
# 3. GET /api/v1/admin/config/SessionPolicy/history
# 4. POST /api/v1/admin/config/SessionPolicy/
#    restore/{versionId}
```

## Implementation Validation Strategy

- [ ] Configuration update creates new version row, never updates existing (AC-1)
- [ ] Invalid values (e.g., timeout < 5 min) return 422 with descriptive error (AC-2)
- [ ] Version history returns all versions with change date, admin identity, before/after diff (AC-3)
- [ ] Restore creates a new version referencing the restored source, not an overwrite (AC-4)
- [ ] Concurrent updates with stale If-Match return 409 Conflict (edge case 1)
- [ ] In-flight operations use the version active at their creation time (edge case 2)
- [ ] All endpoints restricted to Admin role

## Implementation Checklist

- [x] Define ConfigurationCategory enum and IConfigurationService contract with DTOs
- [x] Implement FluentValidation validators for all 4 configuration categories with descriptive errors
- [x] Implement ConfigurationService with versioned insert-only persistence and JSONB diff tracking
- [x] Implement optimistic concurrency control via ETag/If-Match with 409 Conflict response
- [x] Implement RestoreVersionAsync creating a new version from historical snapshot
- [x] Implement ConfigurationCacheService with startup population and write-through update
- [x] Create ConfigurationController with GET current, PUT update, GET history, POST restore endpoints
- [x] Register all services, validators, cache, and hosted service in Program.cs
