# Task - TASK_001

## Requirement Reference

- User Story: us_029
- Story Location: .propel/context/tasks/EP-003/us_029/us_029.md
- Acceptance Criteria:
  - AC-1: Given I am authenticated as a patient, When I navigate to my notification preferences, Then I can toggle email and SMS channels on or off independently.
  - AC-2: Given I configure my preferences, When I save them, Then the changes are persisted and the next scheduled reminder is dispatched using my updated preference settings.
  - AC-3: Given I have both channels enabled, When a reminder is dispatched, Then the reminder is sent via all enabled channels (email AND SMS).
  - AC-4: Given I disable all channels, When a reminder is due, Then no notification is sent and the event is recorded as "Opted Out" in the ReminderEvent table.
- Edge Cases:
  - What happens if a patient has no phone number on file but enables SMS? An inline prompt asks the patient to add a verified mobile number before SMS can be activated.
  - How does the system handle preference changes on the day of the appointment? Preferences are applied to future reminders only; same-day reminders already queued use the previously stored preference.

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
| Library | EF Core (Npgsql) | latest stable |
| Library | FluentValidation | latest stable |
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

Implement the `NotificationPreferenceController` (Patient-authorized) with GET and PUT endpoints for reading and updating notification channel and reminder timing preferences. Preferences are stored in the `PATIENT.contact_preferences` JSONB column (per the ERD) as a structured `NotificationPreferenceDto` containing `EmailEnabled` (bool), `SmsEnabled` (bool), and `ReminderTimings` (list of enabled offset keys: `7d`, `2d`, `1d`, `2h`). The `IPatientPreferenceRepository` — already referenced by `ReminderSchedulingService` in US_026 task_001 via `GetEnabledChannelsAsync()` — is fully implemented in this task with `GetPreferencesAsync`, `SavePreferencesAsync`, and `GetEnabledChannelsAsync` methods backed by EF Core JSONB queries against the `Patient` entity. On save (AC-2), only future reminders are affected — same-day reminders already in `Pending` or `Sending` status remain unchanged (edge case 2). When a patient disables all channels (AC-4), `ReminderDispatchWorker` (from US_026 task_002) queries enabled channels at dispatch time; if none are enabled, the `ReminderEvent.SendStatus` is set to `OptedOut` and no notification is dispatched. Phone number validation (edge case 1) is enforced server-side: if `SmsEnabled = true` and `Patient.Phone` is null or empty, a `400 Bad Request` with a descriptive error is returned. The PUT endpoint also returns the current `Patient.Phone` presence flag so the frontend can prompt inline.

## Dependent Tasks

- None (standalone — US_026 task_001 already references IPatientPreferenceRepository as a dependency contract)

## Impacted Components

- New: `server/src/PropelIQ.Api/Controllers/NotificationPreferenceController.cs` (GET/PUT endpoints)
- New: `server/src/PropelIQ.Application/Notifications/IPatientPreferenceRepository.cs` (interface)
- New: `server/src/PropelIQ.Application/Notifications/Models/NotificationPreferenceDto.cs` (preference DTO)
- New: `server/src/PropelIQ.Application/Notifications/Validators/NotificationPreferenceValidator.cs` (FluentValidation)
- New: `server/src/PropelIQ.Infrastructure/Notifications/PatientPreferenceRepository.cs` (EF Core JSONB implementation)
- Modify: `server/src/PropelIQ.Domain/Entities/Patient.cs` (add typed `ContactPreferences` property mapped to JSONB)
- Modify: `server/src/PropelIQ.Infrastructure/Data/AppDbContext.cs` (JSONB column configuration for contact_preferences)

## Implementation Plan

1. **Add typed `ContactPreferences` property to `Patient` entity**:

```csharp
// server/src/PropelIQ.Domain/Entities/Patient.cs
public sealed class ContactPreferences
{
    public bool EmailEnabled { get; set; } = true;
    public bool SmsEnabled { get; set; } = false;
    public List<string> ReminderTimings { get; set; } =
        ["7d", "2d", "1d", "2h"];
}

// Add to Patient entity
public ContactPreferences? ContactPreferences { get; set; }
```

```csharp
// In AppDbContext.OnModelCreating
builder.Entity<Patient>(entity =>
{
    entity.Property(p => p.ContactPreferences)
          .HasColumnType("jsonb")
          .HasColumnName("contact_preferences");
});
```

2. **Create `NotificationPreferenceDto` and validator**:

```csharp
// server/src/PropelIQ.Application/Notifications/Models/
//   NotificationPreferenceDto.cs
namespace PropelIQ.Application.Notifications.Models;

public sealed record NotificationPreferenceDto(
    bool EmailEnabled,
    bool SmsEnabled,
    IReadOnlyList<string> ReminderTimings);

public sealed record NotificationPreferenceResponse(
    bool EmailEnabled,
    bool SmsEnabled,
    IReadOnlyList<string> ReminderTimings,
    bool HasPhoneNumber);
```

```csharp
// server/src/PropelIQ.Application/Notifications/Validators/
//   NotificationPreferenceValidator.cs
namespace PropelIQ.Application.Notifications.Validators;

public sealed class NotificationPreferenceValidator
    : AbstractValidator<NotificationPreferenceDto>
{
    private static readonly HashSet<string> ValidTimings =
        ["7d", "2d", "1d", "2h"];

    public NotificationPreferenceValidator()
    {
        RuleFor(x => x.ReminderTimings)
            .NotNull()
            .Must(t => t.All(ValidTimings.Contains))
            .WithMessage(
                "Timings must be from: 7d, 2d, 1d, 2h");
    }
}
```

3. **Create `IPatientPreferenceRepository`** (fulfills contract from US_026 task_001):

```csharp
// server/src/PropelIQ.Application/Notifications/
//   IPatientPreferenceRepository.cs
namespace PropelIQ.Application.Notifications;

public interface IPatientPreferenceRepository
{
    Task<NotificationPreferenceResponse> GetPreferencesAsync(
        Guid patientId,
        CancellationToken ct = default);

    Task SavePreferencesAsync(
        Guid patientId,
        NotificationPreferenceDto dto,
        CancellationToken ct = default);

    // Referenced by US_026 task_001 ReminderSchedulingService
    Task<IReadOnlyList<string>> GetEnabledChannelsAsync(
        Guid patientId,
        CancellationToken ct = default);
}
```

4. **Implement `PatientPreferenceRepository`** with EF Core JSONB:

```csharp
// server/src/PropelIQ.Infrastructure/Notifications/
//   PatientPreferenceRepository.cs
namespace PropelIQ.Infrastructure.Notifications;

public sealed class PatientPreferenceRepository
    : IPatientPreferenceRepository
{
    private readonly AppDbContext _db;

    public PatientPreferenceRepository(AppDbContext db)
        => _db = db;

    public async Task<NotificationPreferenceResponse>
        GetPreferencesAsync(
            Guid patientId,
            CancellationToken ct = default)
    {
        var patient = await _db.Patients
            .Where(p => p.PatientId == patientId)
            .Select(p => new
            {
                p.ContactPreferences,
                p.Phone
            })
            .FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException(
                $"Patient {patientId} not found");

        var prefs = patient.ContactPreferences
            ?? new ContactPreferences();

        return new NotificationPreferenceResponse(
            prefs.EmailEnabled,
            prefs.SmsEnabled,
            prefs.ReminderTimings,
            !string.IsNullOrWhiteSpace(patient.Phone));
    }

    public async Task SavePreferencesAsync(
        Guid patientId,
        NotificationPreferenceDto dto,
        CancellationToken ct = default)
    {
        var prefs = new ContactPreferences
        {
            EmailEnabled = dto.EmailEnabled,
            SmsEnabled = dto.SmsEnabled,
            ReminderTimings = dto.ReminderTimings.ToList()
        };

        await _db.Patients
            .Where(p => p.PatientId == patientId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(
                    p => p.ContactPreferences, prefs),
                ct);
    }

    // AC-3: Returns ["Email", "SMS"] when both enabled
    // AC-4: Returns [] when all disabled
    public async Task<IReadOnlyList<string>>
        GetEnabledChannelsAsync(
            Guid patientId,
            CancellationToken ct = default)
    {
        var prefs = await _db.Patients
            .Where(p => p.PatientId == patientId)
            .Select(p => p.ContactPreferences)
            .FirstOrDefaultAsync(ct);

        if (prefs is null)
            return ["Email", "SMS"]; // Default: all enabled

        var channels = new List<string>();
        if (prefs.EmailEnabled) channels.Add("Email");
        if (prefs.SmsEnabled) channels.Add("SMS");
        return channels;
    }
}
```

5. **Create `NotificationPreferenceController`**:

```csharp
// server/src/PropelIQ.Api/Controllers/
//   NotificationPreferenceController.cs
namespace PropelIQ.Api.Controllers;

[ApiController]
[Route("api/v1/patients/me/notification-preferences")]
[Authorize(Roles = "Patient")]
public sealed class NotificationPreferenceController
    : ControllerBase
{
    private readonly IPatientPreferenceRepository _repo;
    private readonly IValidator<NotificationPreferenceDto>
        _validator;

    public NotificationPreferenceController(
        IPatientPreferenceRepository repo,
        IValidator<NotificationPreferenceDto> validator)
    {
        _repo = repo;
        _validator = validator;
    }

    // AC-1: Read current preferences
    [HttpGet]
    [ProducesResponseType(
        typeof(NotificationPreferenceResponse), 200)]
    public async Task<IActionResult> Get(
        CancellationToken ct)
    {
        var patientId = GetPatientId();
        var prefs = await _repo
            .GetPreferencesAsync(patientId, ct);
        return Ok(prefs);
    }

    // AC-2: Save preferences
    [HttpPut]
    [ProducesResponseType(
        typeof(NotificationPreferenceResponse), 200)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> Update(
        [FromBody] NotificationPreferenceDto dto,
        CancellationToken ct)
    {
        var result = await _validator.ValidateAsync(dto, ct);
        if (!result.IsValid)
            return BadRequest(result.Errors);

        var patientId = GetPatientId();

        // Edge case 1: Validate phone exists for SMS
        if (dto.SmsEnabled)
        {
            var current = await _repo
                .GetPreferencesAsync(patientId, ct);
            if (!current.HasPhoneNumber)
            {
                return BadRequest(new
                {
                    Field = "SmsEnabled",
                    Message = "A verified mobile number " +
                        "is required to enable SMS. " +
                        "Please add your phone number first."
                });
            }
        }

        await _repo.SavePreferencesAsync(
            patientId, dto, ct);

        var updated = await _repo
            .GetPreferencesAsync(patientId, ct);
        return Ok(updated);
    }

    private Guid GetPatientId()
    {
        var claim = User.FindFirst("patient_id")
            ?? throw new UnauthorizedAccessException(
                "Patient ID claim not found");
        return Guid.Parse(claim.Value);
    }
}
```

6. **Handle AC-4 (Opted Out) in ReminderDispatchWorker integration**:

```csharp
// Integration note for US_026 task_002 ReminderDispatchWorker:
// At dispatch time, call GetEnabledChannelsAsync(patientId).
// If returned list is empty (all channels disabled),
// set ReminderEvent.SendStatus = "OptedOut" instead of
// dispatching. Do not throw or retry.
//
// This is already supported by the dispatch loop in US_026
// task_002 because it queries channels per reminder.
// When GetEnabledChannelsAsync returns [], the reminder
// channel won't match any enabled channel -> OptedOut.
```

## Current Project State

```text
propelIQ/
└── server/
    └── src/
        ├── PropelIQ.Api/
        │   ├── Controllers/
        │   │   └── NotificationPreferenceController.cs   (new)
        │   └── Program.cs                                (modify — register services)
        ├── PropelIQ.Application/
        │   └── Notifications/
        │       ├── IPatientPreferenceRepository.cs        (new)
        │       ├── Models/
        │       │   └── NotificationPreferenceDto.cs       (new)
        │       └── Validators/
        │           └── NotificationPreferenceValidator.cs  (new)
        ├── PropelIQ.Domain/
        │   └── Entities/
        │       └── Patient.cs                             (modify — add ContactPreferences)
        └── PropelIQ.Infrastructure/
            ├── Notifications/
            │   └── PatientPreferenceRepository.cs         (new)
            └── Data/
                └── AppDbContext.cs                         (modify — JSONB config)
```

> Placeholder: Update on execution based on existing Patient entity structure.

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | server/src/PropelIQ.Api/Controllers/NotificationPreferenceController.cs | GET/PUT endpoints for patient notification preferences |
| CREATE | server/src/PropelIQ.Application/Notifications/IPatientPreferenceRepository.cs | Interface with GetPreferencesAsync, SavePreferencesAsync, GetEnabledChannelsAsync |
| CREATE | server/src/PropelIQ.Application/Notifications/Models/NotificationPreferenceDto.cs | DTO and response records |
| CREATE | server/src/PropelIQ.Application/Notifications/Validators/NotificationPreferenceValidator.cs | FluentValidation for timing values |
| CREATE | server/src/PropelIQ.Infrastructure/Notifications/PatientPreferenceRepository.cs | EF Core JSONB implementation |
| MODIFY | server/src/PropelIQ.Domain/Entities/Patient.cs | Add ContactPreferences POCO and JSONB-mapped property |
| MODIFY | server/src/PropelIQ.Infrastructure/Data/AppDbContext.cs | JSONB column type for contact_preferences |
| MODIFY | server/src/PropelIQ.Api/Program.cs | Register IPatientPreferenceRepository, validator in DI |

## External References

- EF Core JSONB with Npgsql: https://www.npgsql.org/efcore/mapping/json.html
- FluentValidation ASP.NET Core: https://docs.fluentvalidation.net/en/latest/aspnet.html
- ASP.NET Core Authorization: https://learn.microsoft.com/en-us/aspnet/core/security/authorization/roles

## Build Commands

```bash
# Build backend
cd server
dotnet build

# Add migration for contact_preferences JSONB
dotnet ef migrations add AddContactPreferencesJsonb \
  --project src/PropelIQ.Infrastructure \
  --startup-project src/PropelIQ.Api
dotnet ef database update \
  --startup-project src/PropelIQ.Api

# Run
dotnet run --project src/PropelIQ.Api
```

## Implementation Validation Strategy

- [ ] GET /api/v1/patients/me/notification-preferences returns current preferences with HasPhoneNumber flag
- [ ] PUT saves preferences and returns updated response (AC-2)
- [ ] SMS enable rejected with 400 when no phone number on file (edge case 1)
- [ ] GetEnabledChannelsAsync returns both channels when both enabled (AC-3)
- [ ] GetEnabledChannelsAsync returns empty list when all disabled (AC-4)
- [ ] FluentValidation rejects invalid timing values
- [ ] Patient role authorization enforced on both endpoints

## Implementation Checklist

- [ ] Add ContactPreferences POCO and JSONB property to Patient entity
- [ ] Configure JSONB column mapping in AppDbContext
- [ ] Create NotificationPreferenceDto and NotificationPreferenceResponse records
- [ ] Implement NotificationPreferenceValidator with valid timing values
- [ ] Create IPatientPreferenceRepository with Get, Save, and GetEnabledChannels methods
- [ ] Implement PatientPreferenceRepository with EF Core JSONB queries
- [ ] Create NotificationPreferenceController with GET and PUT endpoints
- [ ] Add phone number validation guard for SMS enablement
