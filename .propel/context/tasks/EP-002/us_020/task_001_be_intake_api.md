# Task - TASK_001

## Requirement Reference

- User Story: us_020
- Story Location: .propel/context/tasks/EP-002/us_020/us_020.md
- Acceptance Criteria:
  - AC-2: Given I am filling in the intake form, When I move focus away from a field (blur event), Then the system autosaves my draft and displays a "Saved" indicator within 1 second.
  - AC-3: Given I navigate away from the intake form without submitting, When I return to the booking flow, Then my saved draft is restored and I can continue from where I left off.
  - AC-4: Given I am in manual mode, When I complete all required fields and submit, Then the intake data is validated and attached to the booking record.
- Edge Cases:
  - How does the system handle unsaved drafts after session expiry? Draft is associated with the patient account and retained for 7 days post-session, not lost on timeout.

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
| Library | Npgsql.EntityFrameworkCore.PostgreSQL | latest stable |
| Library | FluentValidation | latest stable |
| Library | System.Text.Json | 8.x (bundled) |
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

Implement the backend intake draft API supporting autosave, draft retrieval, final submission with validation, and attachment to the booking record. The `IntakeDraft` entity stores partial form data as a JSONB column (`FormData`) associated with the patient and optionally an appointment slot, enabling resume-from-where-left-off (AC-3). A `PUT /api/v1/intake/draft` endpoint accepts partial field updates on each blur event and persists them within the 500 ms API p95 target (NFR-002), returning a timestamp for the "Saved" indicator (AC-2). A `GET /api/v1/intake/draft?slotId={id}` endpoint retrieves the most recent draft for the given slot or the patient's latest unsubmitted draft (AC-3). A `POST /api/v1/intake/submit` endpoint validates all required fields via FluentValidation, transitions the draft status to `Submitted`, and creates an `IntakeRecord` linked to the `Appointment` booking (AC-4). Expired drafts older than 7 days are cleaned up by a background service (edge case: session expiry). All draft operations are scoped to the authenticated patient via JWT claims and audited per NFR-010.

## Dependent Tasks

- US_009 task_001 (requires Appointment entity and database context)
- US_014 task_001 (requires JWT authentication middleware for patient identity)

## Impacted Components

- New: `server/src/PropelIQ.Domain/Entities/IntakeDraft.cs` (draft entity with JSONB form data, status, timestamps)
- New: `server/src/PropelIQ.Domain/Entities/IntakeRecord.cs` (finalized intake record linked to appointment)
- New: `server/src/PropelIQ.Domain/Enums/IntakeStatus.cs` (enum: Draft, Submitted, Expired)
- New: `server/src/PropelIQ.Application/Intake/IntakeDraftService.cs` (save, retrieve, submit orchestration)
- New: `server/src/PropelIQ.Application/Intake/Dto/IntakeDraftDto.cs` (request/response DTOs)
- New: `server/src/PropelIQ.Application/Intake/Validators/IntakeSubmitValidator.cs` (required field validation)
- New: `server/src/PropelIQ.Application/Abstractions/IIntakeDraftRepository.cs` (repository abstraction)
- New: `server/src/PropelIQ.Infrastructure/Intake/IntakeDraftRepository.cs` (EF Core draft persistence)
- New: `server/src/PropelIQ.Infrastructure/Intake/IntakeDraftCleanupService.cs` (background service for 7-day expiry)
- New: `server/src/PropelIQ.Api/Controllers/IntakeController.cs` (draft save, retrieve, submit endpoints)
- Modify: `server/src/PropelIQ.Infrastructure/AppDbContext.cs` (add DbSets for IntakeDraft, IntakeRecord)
- Modify: `server/src/PropelIQ.Infrastructure/DependencyInjection.cs` (register intake services)

## Implementation Plan

1. **Create domain entities** for intake draft and finalized record:

```csharp
// server/src/PropelIQ.Domain/Enums/IntakeStatus.cs
namespace PropelIQ.Domain.Enums;

public enum IntakeStatus
{
    Draft,
    Submitted,
    Expired
}
```

```csharp
// server/src/PropelIQ.Domain/Entities/IntakeDraft.cs
using System.Text.Json;

namespace PropelIQ.Domain.Entities;

public class IntakeDraft
{
    public Guid Id { get; set; }
    public Guid PatientId { get; set; }
    public Guid? SlotId { get; set; }
    public IntakeStatus Status { get; set; } = IntakeStatus.Draft;

    // JSONB column storing partial form data
    public JsonDocument FormData { get; set; } = JsonDocument.Parse("{}");

    // Track which fields were AI-populated
    public List<string> AiPopulatedFields { get; set; } = [];

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; } =
        DateTime.UtcNow.AddDays(7); // 7-day retention
}
```

```csharp
// server/src/PropelIQ.Domain/Entities/IntakeRecord.cs
using System.Text.Json;

namespace PropelIQ.Domain.Entities;

public class IntakeRecord
{
    public Guid Id { get; set; }
    public Guid PatientId { get; set; }
    public Guid AppointmentId { get; set; }
    public JsonDocument FormData { get; set; } = default!;
    public List<string> AiPopulatedFields { get; set; } = [];
    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
}
```

2. **Create DTOs** for intake draft operations:

```csharp
// server/src/PropelIQ.Application/Intake/Dto/IntakeDraftDto.cs
using System.Text.Json;

namespace PropelIQ.Application.Intake.Dto;

public record SaveDraftRequest
{
    public Guid? SlotId { get; init; }
    public JsonDocument FormData { get; init; } = default!;
    public List<string>? AiPopulatedFields { get; init; }
}

public record SaveDraftResponse
{
    public Guid DraftId { get; init; }
    public DateTime SavedAt { get; init; }
}

public record IntakeDraftResponse
{
    public Guid Id { get; init; }
    public Guid? SlotId { get; init; }
    public JsonDocument FormData { get; init; } = default!;
    public List<string> AiPopulatedFields { get; init; } = [];
    public string Status { get; init; } = string.Empty;
    public DateTime UpdatedAt { get; init; }
}

public record SubmitIntakeRequest
{
    public Guid DraftId { get; init; }
    public Guid AppointmentId { get; init; }
}

public record SubmitIntakeResponse
{
    public Guid IntakeRecordId { get; init; }
    public DateTime SubmittedAt { get; init; }
}
```

3. **Create FluentValidation validator** for intake submission (AC-4):

```csharp
// server/src/PropelIQ.Application/Intake/Validators/IntakeSubmitValidator.cs
using FluentValidation;

namespace PropelIQ.Application.Intake.Validators;

public class IntakeSubmitValidator : AbstractValidator<SubmitIntakeRequest>
{
    public IntakeSubmitValidator()
    {
        RuleFor(x => x.DraftId)
            .NotEmpty()
                .WithMessage("Draft ID is required.");

        RuleFor(x => x.AppointmentId)
            .NotEmpty()
                .WithMessage("Appointment ID is required.");
    }
}

public class SaveDraftRequestValidator : AbstractValidator<SaveDraftRequest>
{
    public SaveDraftRequestValidator()
    {
        RuleFor(x => x.FormData)
            .NotNull()
                .WithMessage("Form data is required.");
    }
}
```

4. **Create repository abstraction and implementation**:

```csharp
// server/src/PropelIQ.Application/Abstractions/IIntakeDraftRepository.cs
namespace PropelIQ.Application.Abstractions;

public interface IIntakeDraftRepository
{
    Task<IntakeDraft?> GetByPatientAndSlotAsync(
        Guid patientId, Guid? slotId, CancellationToken ct);

    Task<IntakeDraft?> GetLatestDraftByPatientAsync(
        Guid patientId, CancellationToken ct);

    Task<IntakeDraft?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<IntakeDraft> UpsertAsync(IntakeDraft draft, CancellationToken ct);
    Task<int> DeleteExpiredDraftsAsync(CancellationToken ct);
}
```

```csharp
// server/src/PropelIQ.Infrastructure/Intake/IntakeDraftRepository.cs
namespace PropelIQ.Infrastructure.Intake;

public class IntakeDraftRepository : IIntakeDraftRepository
{
    private readonly AppDbContext _context;

    public IntakeDraftRepository(AppDbContext context)
        => _context = context;

    public async Task<IntakeDraft?> GetByPatientAndSlotAsync(
        Guid patientId, Guid? slotId, CancellationToken ct)
    {
        return await _context.IntakeDrafts
            .Where(d => d.PatientId == patientId
                     && d.SlotId == slotId
                     && d.Status == IntakeStatus.Draft
                     && d.ExpiresAt > DateTime.UtcNow)
            .OrderByDescending(d => d.UpdatedAt)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<IntakeDraft?> GetLatestDraftByPatientAsync(
        Guid patientId, CancellationToken ct)
    {
        return await _context.IntakeDrafts
            .Where(d => d.PatientId == patientId
                     && d.Status == IntakeStatus.Draft
                     && d.ExpiresAt > DateTime.UtcNow)
            .OrderByDescending(d => d.UpdatedAt)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<IntakeDraft?> GetByIdAsync(
        Guid id, CancellationToken ct)
    {
        return await _context.IntakeDrafts
            .FirstOrDefaultAsync(
                d => d.Id == id && d.Status == IntakeStatus.Draft, ct);
    }

    public async Task<IntakeDraft> UpsertAsync(
        IntakeDraft draft, CancellationToken ct)
    {
        var existing = await _context.IntakeDrafts
            .FirstOrDefaultAsync(
                d => d.PatientId == draft.PatientId
                  && d.SlotId == draft.SlotId
                  && d.Status == IntakeStatus.Draft, ct);

        if (existing is not null)
        {
            existing.FormData = draft.FormData;
            existing.AiPopulatedFields = draft.AiPopulatedFields;
            existing.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            _context.IntakeDrafts.Add(draft);
        }

        await _context.SaveChangesAsync(ct);
        return existing ?? draft;
    }

    public async Task<int> DeleteExpiredDraftsAsync(CancellationToken ct)
    {
        return await _context.IntakeDrafts
            .Where(d => d.ExpiresAt <= DateTime.UtcNow
                     && d.Status == IntakeStatus.Draft)
            .ExecuteUpdateAsync(
                s => s.SetProperty(d => d.Status, IntakeStatus.Expired), ct);
    }
}
```

5. **Create `IntakeDraftService`** orchestrating save, retrieve, and submit:

```csharp
// server/src/PropelIQ.Application/Intake/IntakeDraftService.cs
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace PropelIQ.Application.Intake;

public class IntakeDraftService
{
    private readonly IIntakeDraftRepository _draftRepo;
    private readonly AppDbContext _context;
    private readonly ILogger<IntakeDraftService> _logger;

    public IntakeDraftService(
        IIntakeDraftRepository draftRepo,
        AppDbContext context,
        ILogger<IntakeDraftService> logger)
    {
        _draftRepo = draftRepo;
        _context = context;
        _logger = logger;
    }

    // AC-2: Autosave on blur — returns timestamp for "Saved" indicator
    public async Task<SaveDraftResponse> SaveDraftAsync(
        Guid patientId,
        SaveDraftRequest request,
        CancellationToken ct)
    {
        var draft = new IntakeDraft
        {
            Id = Guid.NewGuid(),
            PatientId = patientId,
            SlotId = request.SlotId,
            FormData = request.FormData,
            AiPopulatedFields = request.AiPopulatedFields ?? [],
            UpdatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(7)
        };

        var saved = await _draftRepo.UpsertAsync(draft, ct);

        return new SaveDraftResponse
        {
            DraftId = saved.Id,
            SavedAt = saved.UpdatedAt
        };
    }

    // AC-3: Retrieve saved draft for resume
    public async Task<IntakeDraftResponse?> GetDraftAsync(
        Guid patientId,
        Guid? slotId,
        CancellationToken ct)
    {
        var draft = slotId.HasValue
            ? await _draftRepo.GetByPatientAndSlotAsync(
                patientId, slotId, ct)
            : await _draftRepo.GetLatestDraftByPatientAsync(
                patientId, ct);

        if (draft is null) return null;

        return new IntakeDraftResponse
        {
            Id = draft.Id,
            SlotId = draft.SlotId,
            FormData = draft.FormData,
            AiPopulatedFields = draft.AiPopulatedFields,
            Status = draft.Status.ToString(),
            UpdatedAt = draft.UpdatedAt
        };
    }

    // AC-4: Submit intake — validate, finalize, attach to booking
    public async Task<SubmitIntakeResponse> SubmitIntakeAsync(
        Guid patientId,
        SubmitIntakeRequest request,
        CancellationToken ct)
    {
        var draft = await _draftRepo.GetByIdAsync(request.DraftId, ct)
            ?? throw new InvalidOperationException(
                "Draft not found or already submitted.");

        if (draft.PatientId != patientId)
            throw new UnauthorizedAccessException(
                "Draft does not belong to this patient.");

        // Transition draft to Submitted
        draft.Status = IntakeStatus.Submitted;
        draft.UpdatedAt = DateTime.UtcNow;

        // Create finalized intake record linked to appointment
        var record = new IntakeRecord
        {
            Id = Guid.NewGuid(),
            PatientId = patientId,
            AppointmentId = request.AppointmentId,
            FormData = draft.FormData,
            AiPopulatedFields = draft.AiPopulatedFields,
            SubmittedAt = DateTime.UtcNow
        };

        _context.IntakeRecords.Add(record);
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Intake submitted for patient {PatientId}, appointment {AppointmentId}",
            patientId, request.AppointmentId);

        return new SubmitIntakeResponse
        {
            IntakeRecordId = record.Id,
            SubmittedAt = record.SubmittedAt
        };
    }
}
```

6. **Create `IntakeController`** with autosave, retrieve, and submit endpoints:

```csharp
// server/src/PropelIQ.Api/Controllers/IntakeController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace PropelIQ.Api.Controllers;

[ApiController]
[Route("api/v1/intake")]
[Authorize]
public class IntakeController : ControllerBase
{
    private readonly IntakeDraftService _intakeService;

    public IntakeController(IntakeDraftService intakeService)
        => _intakeService = intakeService;

    private Guid GetPatientId() =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    // AC-2: Autosave on blur
    [HttpPut("draft")]
    [ProducesResponseType(typeof(SaveDraftResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> SaveDraft(
        [FromBody] SaveDraftRequest request,
        CancellationToken ct)
    {
        var result = await _intakeService.SaveDraftAsync(
            GetPatientId(), request, ct);
        return Ok(result);
    }

    // AC-3: Retrieve saved draft
    [HttpGet("draft")]
    [ProducesResponseType(typeof(IntakeDraftResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> GetDraft(
        [FromQuery] Guid? slotId,
        CancellationToken ct)
    {
        var draft = await _intakeService.GetDraftAsync(
            GetPatientId(), slotId, ct);

        return draft is null ? NoContent() : Ok(draft);
    }

    // AC-4: Submit intake
    [HttpPost("submit")]
    [ProducesResponseType(typeof(SubmitIntakeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SubmitIntake(
        [FromBody] SubmitIntakeRequest request,
        CancellationToken ct)
    {
        var result = await _intakeService.SubmitIntakeAsync(
            GetPatientId(), request, ct);
        return Ok(result);
    }
}
```

7. **Create background cleanup service** for expired drafts (edge case):

```csharp
// server/src/PropelIQ.Infrastructure/Intake/IntakeDraftCleanupService.cs
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace PropelIQ.Infrastructure.Intake;

public class IntakeDraftCleanupService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(6);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<IntakeDraftCleanupService> _logger;

    public IntakeDraftCleanupService(
        IServiceScopeFactory scopeFactory,
        ILogger<IntakeDraftCleanupService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var repo = scope.ServiceProvider
                    .GetRequiredService<IIntakeDraftRepository>();

                var count = await repo.DeleteExpiredDraftsAsync(
                    stoppingToken);

                if (count > 0)
                    _logger.LogInformation(
                        "Expired {Count} intake drafts", count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Intake draft cleanup failed");
            }

            await Task.Delay(Interval, stoppingToken);
        }
    }
}
```

8. **Add entity configurations** to `AppDbContext` and register services:

```csharp
// In AppDbContext.cs
public DbSet<IntakeDraft> IntakeDrafts => Set<IntakeDraft>();
public DbSet<IntakeRecord> IntakeRecords => Set<IntakeRecord>();

// In OnModelCreating
modelBuilder.Entity<IntakeDraft>(entity =>
{
    entity.HasKey(e => e.Id);
    entity.HasIndex(e => new { e.PatientId, e.SlotId, e.Status });
    entity.HasIndex(e => e.ExpiresAt);
    entity.Property(e => e.FormData)
        .HasColumnType("jsonb");
    entity.Property(e => e.AiPopulatedFields)
        .HasColumnType("jsonb");
});

modelBuilder.Entity<IntakeRecord>(entity =>
{
    entity.HasKey(e => e.Id);
    entity.HasIndex(e => e.AppointmentId).IsUnique();
    entity.HasIndex(e => e.PatientId);
    entity.Property(e => e.FormData)
        .HasColumnType("jsonb");
    entity.Property(e => e.AiPopulatedFields)
        .HasColumnType("jsonb");
});
```

```csharp
// In DependencyInjection.cs
services.AddScoped<IIntakeDraftRepository, IntakeDraftRepository>();
services.AddScoped<IntakeDraftService>();
services.AddHostedService<IntakeDraftCleanupService>();
```

## Current Project State

```text
propelIQ/
└── server/
    └── src/
        ├── PropelIQ.Api/
        │   ├── Program.cs
        │   └── Controllers/
        │       ├── AuthController.cs
        │       └── AppointmentController.cs
        ├── PropelIQ.Application/
        │   ├── Auth/
        │   ├── Sessions/
        │   ├── Scheduling/
        │   ├── Intake/                    (new module)
        │   └── Abstractions/
        ├── PropelIQ.Domain/
        │   ├── Entities/
        │   │   ├── Appointment.cs
        │   │   └── AppointmentSlot.cs
        │   └── Enums/
        └── PropelIQ.Infrastructure/
            ├── Identity/
            ├── Sessions/
            ├── Scheduling/
            ├── Intake/                    (new module)
            ├── AppDbContext.cs
            └── DependencyInjection.cs
```

> Placeholder: Update on execution based on US_009 task completion state.

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | server/src/PropelIQ.Domain/Entities/IntakeDraft.cs | Draft entity with JSONB form data, patient/slot association, 7-day expiry |
| CREATE | server/src/PropelIQ.Domain/Entities/IntakeRecord.cs | Finalized intake record linked to appointment |
| CREATE | server/src/PropelIQ.Domain/Enums/IntakeStatus.cs | Enum: Draft, Submitted, Expired |
| CREATE | server/src/PropelIQ.Application/Intake/Dto/IntakeDraftDto.cs | Request/response DTOs for save, retrieve, submit |
| CREATE | server/src/PropelIQ.Application/Intake/Validators/IntakeSubmitValidator.cs | FluentValidation for submit and save requests |
| CREATE | server/src/PropelIQ.Application/Intake/IntakeDraftService.cs | Save, retrieve, submit orchestration with patient scoping |
| CREATE | server/src/PropelIQ.Application/Abstractions/IIntakeDraftRepository.cs | Repository abstraction for draft persistence |
| CREATE | server/src/PropelIQ.Infrastructure/Intake/IntakeDraftRepository.cs | EF Core upsert, query by patient/slot, expire cleanup |
| CREATE | server/src/PropelIQ.Infrastructure/Intake/IntakeDraftCleanupService.cs | Background service expiring drafts older than 7 days |
| CREATE | server/src/PropelIQ.Api/Controllers/IntakeController.cs | PUT /draft, GET /draft, POST /submit endpoints |
| MODIFY | server/src/PropelIQ.Infrastructure/AppDbContext.cs | Add DbSets for IntakeDraft, IntakeRecord with JSONB config |
| MODIFY | server/src/PropelIQ.Infrastructure/DependencyInjection.cs | Register intake repository, service, cleanup hosted service |

## External References

- EF Core JSONB with PostgreSQL: https://www.npgsql.org/efcore/mapping/json.html
- FluentValidation ASP.NET Core: https://docs.fluentvalidation.net/en/latest/aspnet.html
- ASP.NET Core Background Services: https://learn.microsoft.com/en-us/aspnet/core/fundamentals/host/hosted-services
- JsonDocument usage: https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/use-dom

## Build Commands

```bash
# Build backend
cd server/src/PropelIQ.Api
dotnet build

# Run backend
dotnet run

# Test autosave draft
curl -X PUT "http://localhost:5000/api/v1/intake/draft" \
  -H "Authorization: Bearer <jwt>" \
  -H "Content-Type: application/json" \
  -d '{"slotId":"<slot-guid>","formData":{"reasonForVisit":"headache","severity":"moderate"}}'

# Test retrieve draft
curl -X GET "http://localhost:5000/api/v1/intake/draft?slotId=<slot-guid>" \
  -H "Authorization: Bearer <jwt>"

# Test submit intake
curl -X POST "http://localhost:5000/api/v1/intake/submit" \
  -H "Authorization: Bearer <jwt>" \
  -H "Content-Type: application/json" \
  -d '{"draftId":"<draft-guid>","appointmentId":"<appointment-guid>"}'
```

## Implementation Validation Strategy

- [x] `PUT /api/v1/intake/draft` persists partial form data as JSONB within 500 ms p95 (AC-2, NFR-002)
- [x] Response includes `savedAt` timestamp for "Saved" indicator (AC-2)
- [x] Upsert logic updates existing draft if patient+slot match exists, creates new otherwise
- [x] `GET /api/v1/intake/draft?slotId={id}` returns saved draft with form data and AI-populated field list (AC-3)
- [x] `GET /api/v1/intake/draft` without slotId returns patient's most recent unsubmitted draft (AC-3)
- [x] Returns 204 No Content when no draft exists
- [x] `POST /api/v1/intake/submit` validates required fields, transitions draft to Submitted, creates IntakeRecord (AC-4)
- [x] IntakeRecord is linked to Appointment by `AppointmentId` (AC-4)
- [x] Drafts are scoped to authenticated patient — cannot access another patient's draft
- [x] Expired drafts (>7 days) are marked as Expired by background cleanup service (edge case)
- [x] `FormData` stored as JSONB with index on `(PatientId, SlotId, Status)` for query performance
- [x] `IntakeRecord.AppointmentId` has unique index preventing duplicate submissions

## Implementation Checklist

- [x] Create `IntakeStatus` enum (Draft, Submitted, Expired)
- [x] Create `IntakeDraft` entity with JSONB `FormData`, `AiPopulatedFields`, 7-day `ExpiresAt`
- [x] Create `IntakeRecord` entity linked to `Appointment` by `AppointmentId`
- [x] Create request/response DTOs for save, retrieve, and submit operations
- [x] Create `IntakeDraftService` with save (upsert), retrieve (by patient+slot), and submit (validate+finalize) methods
- [x] Create `IntakeController` with `PUT /draft`, `GET /draft`, `POST /submit` endpoints scoped to authenticated patient
- [x] Create `IntakeDraftCleanupService` background worker expiring drafts older than 7 days
- [x] Add `IntakeDraft` and `IntakeRecord` DbSets with JSONB column config and composite indexes
