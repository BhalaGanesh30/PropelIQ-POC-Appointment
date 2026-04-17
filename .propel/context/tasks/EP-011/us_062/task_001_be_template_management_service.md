# Task - TASK_001

## Requirement Reference

- User Story: us_062
- Story Location: .propel/context/tasks/EP-011/us_062/us_062.md
- Acceptance Criteria:
  - AC-1: Given I am authenticated as an Admin, When I create or edit an HTML or SMS notification template, Then the template is saved as a new version with the change date and my identity, while previous versions are preserved.
  - AC-2: Given I am editing a template, When I click "Preview," Then a rendered preview of the template is shown with sample data substituted for the merge fields (e.g., patient name, appointment date).
  - AC-3: Given I want to revert to a previous template version, When I select a prior version and click "Restore," Then the selected version becomes active as a new version and existing queued notifications using the old template remain unaffected.
  - AC-4: Given an HTML template contains an invalid merge field placeholder, When I save the template, Then a validation error identifies the invalid placeholder and blocks the save.
- Edge Cases:
  - What happens if an SMS template exceeds the 160-character limit? A character counter warns the user; templates exceeding 160 characters are flagged as multi-part SMS and the estimated message count is shown.
  - How does the system handle templates that reference deleted merge fields? Template validation detects orphaned placeholders and warns the admin before saving.

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
| Database | PostgreSQL | 15.x |
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

Implement `ITemplateManagementService` in PropelIQ.Application with ASP.NET Core Web API endpoints for versioned HTML and SMS notification template management. The service supports listing templates, creating/editing templates (each save produces a new immutable version — AC-1), rendering a live preview by substituting merge fields with sample data (AC-2), restoring a previous version as a new active version without affecting queued notifications (AC-3), and validating merge field placeholders against a known registry to block saves with invalid placeholders (AC-4). An `MergeFieldRegistry` provides the canonical set of allowed merge fields with sample values. SMS templates include a character-count calculation that detects multi-part messages exceeding 160 characters (edge case 1). Validation also detects orphaned placeholders referencing deleted merge fields (edge case 2). Every mutation writes an audit record via `IAuditRecordService` (NFR-010).

## Dependent Tasks

- US_062 task_002 (requires notification_templates and template_versions tables)
- US_059 task_001 (requires IConfigurationService for template usage settings)
- US_056 task_001 (requires IAuditRecordService for audit logging)
- US_015 task_001 (requires Admin authorization middleware)

## Impacted Components

- New: `PropelIQ.Application/Services/ITemplateManagementService.cs` (service contract)
- New: `PropelIQ.Infrastructure/Services/TemplateManagementService.cs` (EF Core implementation)
- New: `PropelIQ.Application/Services/MergeFieldRegistry.cs` (allowed merge fields with sample values)
- New: `PropelIQ.Application/Validators/TemplateSaveValidator.cs` (FluentValidation for save requests)
- New: `PropelIQ.Api/Controllers/Admin/TemplatesController.cs` (REST endpoints)
- New: `PropelIQ.Application/DTOs/TemplateDto.cs` (request/response DTOs)
- New: `PropelIQ.Domain/Entities/NotificationTemplate.cs` (domain entity)
- New: `PropelIQ.Domain/Entities/TemplateVersion.cs` (version entity)

## Implementation Plan

1. **Define domain entities and DTOs**:

```csharp
// PropelIQ.Domain/Entities/
//   NotificationTemplate.cs

public class NotificationTemplate
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    // "HTML" or "SMS"
    public string Description { get; set; }
        = string.Empty;
    public Guid? CurrentVersionId { get; set; }
    public DateTime CreatedAtUtc { get; set; }

    public TemplateVersion? CurrentVersion
        { get; set; }
    public ICollection<TemplateVersion> Versions
        { get; set; } = [];
}

// PropelIQ.Domain/Entities/TemplateVersion.cs

public class TemplateVersion
{
    public Guid Id { get; set; }
    public Guid TemplateId { get; set; }
    public int VersionNumber { get; set; }
    public string Content { get; set; }
        = string.Empty;
    public string? Subject { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public Guid CreatedByUserId { get; set; }
    public string CreatedByName { get; set; }
        = string.Empty;

    public NotificationTemplate Template
        { get; set; } = null!;
}
```

```csharp
// PropelIQ.Application/DTOs/TemplateDto.cs

public record TemplateListItemDto(
    Guid Id,
    string Name,
    string Type,
    string Description,
    int CurrentVersionNumber,
    DateTime LastModifiedUtc);

public record TemplateDetailDto(
    Guid Id,
    string Name,
    string Type,
    string Description,
    TemplateVersionDto CurrentVersion);

public record TemplateVersionDto(
    Guid Id,
    int VersionNumber,
    string Content,
    string? Subject,
    bool IsActive,
    DateTime CreatedAtUtc,
    string CreatedByName);

public record SaveTemplateRequest(
    string Content,
    string? Subject);

public record PreviewRequest(
    string Content,
    string? Subject);

public record PreviewResponse(
    string RenderedHtml,
    string? RenderedSubject,
    SmsInfo? SmsInfo);

public record SmsInfo(
    int CharacterCount,
    bool IsMultiPart,
    int EstimatedSegments);

public record TemplateValidationResult(
    bool IsValid,
    List<string> InvalidPlaceholders,
    List<string> OrphanedPlaceholders);
```

2. **Implement `MergeFieldRegistry`** — canonical set of allowed merge fields with sample values:

```csharp
// PropelIQ.Application/Services/
//   MergeFieldRegistry.cs

public sealed class MergeFieldRegistry
{
    private static readonly
        Dictionary<string, MergeField> _fields
            = new()
    {
        ["patient_name"] = new(
            "patient_name",
            "Patient Name",
            "Jane Smith"),
        ["appointment_date"] = new(
            "appointment_date",
            "Appointment Date",
            "2026-05-15"),
        ["appointment_time"] = new(
            "appointment_time",
            "Appointment Time",
            "10:30 AM"),
        ["clinic_name"] = new(
            "clinic_name",
            "Clinic Name",
            "PropelIQ Health Center"),
        ["provider_name"] = new(
            "provider_name",
            "Provider Name",
            "Dr. Sarah Johnson"),
        ["appointment_type"] = new(
            "appointment_type",
            "Appointment Type",
            "Follow-up Visit"),
        ["cancellation_link"] = new(
            "cancellation_link",
            "Cancellation Link",
            "https://example.com/cancel/abc123"),
        ["reschedule_link"] = new(
            "reschedule_link",
            "Reschedule Link",
            "https://example.com/reschedule/abc123")
    };

    public bool IsValid(string fieldName) =>
        _fields.ContainsKey(fieldName);

    public IReadOnlyDictionary<string, MergeField>
        GetAll() => _fields;

    public string Substitute(
        string content)
    {
        var result = content;
        foreach (var (key, field) in _fields)
        {
            result = result.Replace(
                $"{{{{{key}}}}}",
                field.SampleValue);
        }
        return result;
    }

    public List<string> ExtractPlaceholders(
        string content)
    {
        var matches = Regex.Matches(
            content, @"\{\{(\w+)\}\}");
        return matches
            .Select(m => m.Groups[1].Value)
            .Distinct()
            .ToList();
    }
}

public record MergeField(
    string Name,
    string DisplayName,
    string SampleValue);
```

3. **Implement `ITemplateManagementService`**:

```csharp
// PropelIQ.Application/Services/
//   ITemplateManagementService.cs

public interface ITemplateManagementService
{
    Task<PagedResult<TemplateListItemDto>>
        ListAsync(
            string? typeFilter,
            int page,
            int pageSize,
            CancellationToken ct = default);

    Task<TemplateDetailDto> GetByIdAsync(
        Guid templateId,
        CancellationToken ct = default);

    Task<List<TemplateVersionDto>>
        GetVersionsAsync(
            Guid templateId,
            int page,
            int pageSize,
            CancellationToken ct = default);

    Task<TemplateVersionDto> SaveAsync(
        Guid templateId,
        SaveTemplateRequest request,
        Guid adminId,
        CancellationToken ct = default);

    Task<PreviewResponse> PreviewAsync(
        Guid templateId,
        PreviewRequest request,
        CancellationToken ct = default);

    Task<TemplateVersionDto> RestoreVersionAsync(
        Guid templateId,
        Guid versionId,
        Guid adminId,
        CancellationToken ct = default);

    Task<TemplateValidationResult>
        ValidateAsync(
            Guid templateId,
            string content,
            CancellationToken ct = default);
}
```

Implementation flow for `SaveAsync` (AC-1):
- Read current template with active version
- Run `ValidateAsync` on content — reject with 422 if invalid placeholders found (AC-4)
- Deactivate current active version (`IsActive = false`)
- Create new `TemplateVersion` with incremented version number, `IsActive = true`, admin identity and timestamp
- Update template `CurrentVersionId` FK
- Write audit record via `IAuditRecordService`

Implementation flow for `PreviewAsync` (AC-2):
- Accept content string (could be unsaved draft)
- Call `MergeFieldRegistry.Substitute(content)` to replace `{{field}}` tokens with sample values
- For HTML: return rendered HTML
- For SMS: return plain text with `SmsInfo` (character count, `IsMultiPart = count > 160`, `EstimatedSegments = ceil(count / 153)` per GSM concatenation rules)

Implementation flow for `RestoreVersionAsync` (AC-3):
- Load the specified historical version
- Deactivate current active version
- Create a **new** version with the old content (copy), incremented version number, `IsActive = true`
- This ensures queued notifications referencing the previous version_id are unaffected
- Write audit record

Implementation flow for `ValidateAsync` (AC-4, edge cases 1 & 2):
- Extract all `{{placeholder}}` tokens from content
- Check each against `MergeFieldRegistry.IsValid()`
- Invalid: not in registry → `InvalidPlaceholders` list
- Orphaned: previously existed but since removed → `OrphanedPlaceholders` list
- Return `TemplateValidationResult`

4. **Create REST endpoints** at `/api/v1/admin/templates`:

```csharp
// PropelIQ.Api/Controllers/Admin/
//   TemplatesController.cs

[ApiController]
[Route("api/v1/admin/templates")]
[Authorize(Roles = "Admin")]
public class TemplatesController
    : ControllerBase
{
    private readonly ITemplateManagementService
        _service;

    public TemplatesController(
        ITemplateManagementService service)
    {
        _service = service;
    }

    // GET /api/v1/admin/templates
    //   ?type=HTML&page=1&pageSize=25
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? type,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken ct = default)
    {
        var result = await _service
            .ListAsync(type, page, pageSize, ct);
        return Ok(result);
    }

    // GET /api/v1/admin/templates/{id}
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(
        Guid id,
        CancellationToken ct = default)
    {
        var result = await _service
            .GetByIdAsync(id, ct);
        return Ok(result);
    }

    // GET /api/v1/admin/templates/{id}/versions
    //   ?page=1&pageSize=25
    [HttpGet("{id:guid}/versions")]
    public async Task<IActionResult> Versions(
        Guid id,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken ct = default)
    {
        var result = await _service
            .GetVersionsAsync(
                id, page, pageSize, ct);
        return Ok(result);
    }

    // POST /api/v1/admin/templates/{id}
    [HttpPost("{id:guid}")]
    public async Task<IActionResult> Save(
        Guid id,
        [FromBody] SaveTemplateRequest request,
        CancellationToken ct = default)
    {
        var adminId = GetUserId();
        var result = await _service
            .SaveAsync(id, request, adminId, ct);
        return Ok(result);
    }

    // POST /api/v1/admin/templates/{id}/preview
    [HttpPost("{id:guid}/preview")]
    public async Task<IActionResult> Preview(
        Guid id,
        [FromBody] PreviewRequest request,
        CancellationToken ct = default)
    {
        var result = await _service
            .PreviewAsync(id, request, ct);
        return Ok(result);
    }

    // POST /api/v1/admin/templates/{id}
    //   /restore/{versionId}
    [HttpPost("{id:guid}/restore/{versionId:guid}")]
    public async Task<IActionResult> Restore(
        Guid id,
        Guid versionId,
        CancellationToken ct = default)
    {
        var adminId = GetUserId();
        var result = await _service
            .RestoreVersionAsync(
                id, versionId, adminId, ct);
        return Ok(result);
    }

    // POST /api/v1/admin/templates/{id}/validate
    [HttpPost("{id:guid}/validate")]
    public async Task<IActionResult> Validate(
        Guid id,
        [FromBody] string content,
        CancellationToken ct = default)
    {
        var result = await _service
            .ValidateAsync(id, content, ct);
        if (!result.IsValid)
            return UnprocessableEntity(result);
        return Ok(result);
    }

    private Guid GetUserId() =>
        Guid.Parse(
            User.FindFirst("sub")!.Value);
}
```

## Current Project State

```text
propelIQ/
├── PropelIQ.Api/
│   └── Controllers/
│       └── Admin/
│           └── TemplatesController.cs       (new)
├── PropelIQ.Application/
│   ├── DTOs/
│   │   └── TemplateDto.cs                   (new)
│   ├── Services/
│   │   ├── ITemplateManagementService.cs    (new)
│   │   └── MergeFieldRegistry.cs            (new)
│   └── Validators/
│       └── TemplateSaveValidator.cs          (new)
├── PropelIQ.Domain/
│   └── Entities/
│       ├── NotificationTemplate.cs          (new)
│       └── TemplateVersion.cs               (new)
└── PropelIQ.Infrastructure/
    └── Services/
        └── TemplateManagementService.cs     (new)
```

> Placeholder: Update on execution based on US_062 task_002 completion state.

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | PropelIQ.Domain/Entities/NotificationTemplate.cs | Template aggregate root entity |
| CREATE | PropelIQ.Domain/Entities/TemplateVersion.cs | Immutable version entity with content, subject, identity |
| CREATE | PropelIQ.Application/DTOs/TemplateDto.cs | Request/response DTOs for list, detail, save, preview, validation |
| CREATE | PropelIQ.Application/Services/ITemplateManagementService.cs | Service contract with List, GetById, GetVersions, Save, Preview, Restore, Validate |
| CREATE | PropelIQ.Application/Services/MergeFieldRegistry.cs | Canonical merge field set with sample values and substitution logic |
| CREATE | PropelIQ.Application/Validators/TemplateSaveValidator.cs | FluentValidation rules for SaveTemplateRequest |
| CREATE | PropelIQ.Infrastructure/Services/TemplateManagementService.cs | EF Core implementation of ITemplateManagementService |
| CREATE | PropelIQ.Api/Controllers/Admin/TemplatesController.cs | REST endpoints at /api/v1/admin/templates with Admin authorization |

## External References

- ASP.NET Core 8 Controller-based APIs: https://learn.microsoft.com/en-us/aspnet/core/web-api/?view=aspnetcore-8.0
- FluentValidation for ASP.NET Core: https://docs.fluentvalidation.net/en/latest/aspnet.html
- EF Core 8 Change Tracking: https://learn.microsoft.com/en-us/ef/core/change-tracking/
- GSM SMS Concatenation (153 chars per segment): https://en.wikipedia.org/wiki/Concatenated_SMS
- Regex.Matches for placeholder extraction: https://learn.microsoft.com/en-us/dotnet/api/system.text.regularexpressions.regex.matches

## Build Commands

```bash
# Build backend
cd PropelIQ.Api
dotnet build

# Run API
dotnet run

# Test template management flow:
# 1. POST /api/v1/admin/templates/{id}
#    with HTML content → verify new version created (AC-1)
# 2. POST /api/v1/admin/templates/{id}/preview
#    → verify sample data substitution (AC-2)
# 3. POST /api/v1/admin/templates/{id}/restore/{versionId}
#    → verify old content becomes new active version (AC-3)
# 4. POST /api/v1/admin/templates/{id}
#    with {{invalid_field}} → verify 422 with error (AC-4)
# 5. POST /api/v1/admin/templates/{id}
#    with 200-char SMS → verify SmsInfo.IsMultiPart (edge case 1)
```

## Implementation Validation Strategy

- [ ] Save creates new version with admin identity and timestamp (AC-1)
- [ ] Previous versions remain accessible after save (AC-1)
- [ ] Preview substitutes all merge fields with sample data (AC-2)
- [ ] Restore copies old version content as new active version (AC-3)
- [ ] Queued notifications referencing old version_id are unaffected after restore (AC-3)
- [ ] Invalid merge field placeholders return 422 with identified fields (AC-4)
- [ ] SMS character count and multi-part segment estimation returned (edge case 1)
- [ ] Orphaned placeholders detected and listed in validation result (edge case 2)

## Implementation Checklist

- [ ] Define NotificationTemplate and TemplateVersion domain entities
- [ ] Create request/response DTOs for all template operations
- [ ] Implement MergeFieldRegistry with allowed fields, sample values, and substitution logic
- [ ] Implement ITemplateManagementService with Save (new version per edit), Preview, Restore, and Validate
- [ ] Add FluentValidation rules for SaveTemplateRequest (non-empty content, subject required for HTML)
- [ ] Create TemplatesController with Admin-authorized REST endpoints
- [ ] Wire audit logging via IAuditRecordService for save, restore mutations
- [ ] Implement SMS character counting with multi-part segment estimation (ceil(count/153) for concatenated SMS)
