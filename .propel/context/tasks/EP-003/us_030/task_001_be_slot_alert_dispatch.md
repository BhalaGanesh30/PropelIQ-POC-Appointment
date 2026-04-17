# Task - TASK_001

## Requirement Reference

- User Story: us_030
- Story Location: .propel/context/tasks/EP-003/us_030/us_030.md
- Acceptance Criteria:
  - AC-1: Given I am on the preferred-slot waitlist and a matching slot becomes available, When the system dispatches the alert, Then I receive an email and/or SMS (per my channel preferences) within 5 minutes of the slot opening.
  - AC-3: Given I view the claim page, When I click "Claim Appointment," Then the slot is reserved for me atomically and I receive the standard booking confirmation artifacts.
  - AC-4: Given the 2-hour claim window expires, When I attempt to claim after expiry, Then the link is invalidated and I am informed the slot was offered to another patient.
- Edge Cases:
  - What happens if the patient claims the slot but the confirmation email fails? Slot is still reserved; retry logic attempts email redelivery; patient can access confirmation from their dashboard.
  - How does the system handle timezone differences for the claim countdown? Countdown is always shown in the patient's browser timezone; the expiry timestamp is stored in UTC and converted client-side.

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
| Library | Polly | latest stable |
| Library | SendGrid SDK | latest stable |
| Library | Twilio SDK | latest stable |
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

Implement the preferred-slot alert dispatch and claim lifecycle that bridges US_023's `SlotOfferedEvent` to the multi-channel notification infrastructure from US_027. When the `WaitlistMatchingWorker` (US_023 task_001) publishes a `SlotOfferedEvent`, the `SlotAlertDispatchHandler` consumes it via `System.Threading.Channels` and dispatches an immediate alert within 5 minutes (AC-1) through the patient's enabled channels (email and/or SMS per `IPatientPreferenceRepository` from US_029 task_001). The email contains slot details (date, time, type, provider) and an HMAC-signed claim link (reusing `IReminderTokenService` from US_027 task_002) that encodes the `WaitlistEntryId`, `OfferedSlotId`, and `ExpiresAtUtc` (current time + 2 hours). The SMS contains a concise message with a short claim link. The `POST /api/v1/waitlist/{id}/claim` endpoint (already scaffolded in US_023 task_001) is enhanced to accept the HMAC claim token, validate expiry against the UTC timestamp (AC-4), and perform atomic slot reservation via the optimistic concurrency pattern from US_021 task_001. On successful claim, a `BookingConfirmedEvent` is published for standard confirmation artifacts. If the confirmation email fails after a successful claim (edge case 1), the slot remains reserved and `ReminderDispatchWorker` retry logic (from US_026 task_002) re-attempts delivery; the patient can also see the confirmation on their dashboard. The `ClaimWindowExpiryWorker` (US_023 task_001) handles 2-hour expiry — when a claim link is used after expiry, the endpoint returns `410 Gone` with a descriptive message that the slot was offered to another patient (AC-4). The `WaitlistEntry` entity is extended with `OfferedAt`, `ExpiresAt`, and `ClaimTokenHash` columns to track the offer lifecycle. All expiry timestamps are persisted in UTC (edge case 2).

## Dependent Tasks

- US_023 task_001 (requires WaitlistMatchingWorker, SlotOfferedEvent, claim endpoint, ClaimWindowExpiryWorker, WaitlistEntry entity)
- US_027 task_001 (requires SendGridEmailService, TwilioSmsService for multi-channel dispatch)
- US_027 task_002 (requires IReminderTokenService for HMAC-signed claim links)
- US_029 task_001 (requires IPatientPreferenceRepository.GetEnabledChannelsAsync for channel preferences)

## Impacted Components

- New: `server/src/PropelIQ.Application/Waitlist/SlotAlertDispatchHandler.cs` (consumes SlotOfferedEvent, dispatches alert)
- New: `server/src/PropelIQ.Application/Waitlist/Models/SlotAlertPayload.cs` (alert content model)
- New: `server/src/PropelIQ.Application/Waitlist/ISlotAlertService.cs` (interface for alert generation)
- New: `server/src/PropelIQ.Infrastructure/Waitlist/SlotAlertService.cs` (email/SMS content builder with claim link)
- Modify: `server/src/PropelIQ.Domain/Entities/WaitlistEntry.cs` (add OfferedAt, ExpiresAt, ClaimTokenHash columns)
- Modify: `server/src/PropelIQ.Api/Controllers/WaitlistController.cs` (enhance claim endpoint with HMAC token validation and 410 Gone)
- Modify: `server/src/PropelIQ.Infrastructure/Data/AppDbContext.cs` (index on ExpiresAt for expiry worker queries)
- Modify: `server/src/PropelIQ.Api/Program.cs` (register SlotAlertDispatchHandler and Channel subscription)

## Implementation Plan

1. **Extend `WaitlistEntry` entity with offer lifecycle columns**:

```csharp
// Add to server/src/PropelIQ.Domain/Entities/WaitlistEntry.cs
public DateTimeOffset? OfferedAt { get; set; }
public DateTimeOffset? ExpiresAt { get; set; }  // UTC always (edge case 2)
public string? ClaimTokenHash { get; set; }
```

```csharp
// In AppDbContext.OnModelCreating
builder.Entity<WaitlistEntry>(entity =>
{
    entity.HasIndex(e => e.ExpiresAt)
          .HasDatabaseName("IX_WaitlistEntry_ExpiresAt")
          .HasFilter("\"ExpiresAt\" IS NOT NULL " +
              "AND \"Status\" = 'Offered'");
});
```

2. **Create `SlotAlertPayload` and `ISlotAlertService`**:

```csharp
// server/src/PropelIQ.Application/Waitlist/Models/
//   SlotAlertPayload.cs
namespace PropelIQ.Application.Waitlist.Models;

public sealed record SlotAlertPayload(
    Guid WaitlistEntryId,
    Guid PatientId,
    string PatientName,
    string PatientEmail,
    string? PatientPhone,
    DateTimeOffset SlotDateTime,
    string SlotType,
    string ProviderName,
    int DurationMinutes,
    string ClaimUrl,
    DateTimeOffset ExpiresAtUtc);
```

```csharp
// server/src/PropelIQ.Application/Waitlist/
//   ISlotAlertService.cs
namespace PropelIQ.Application.Waitlist;

public interface ISlotAlertService
{
    Task<SlotAlertPayload> BuildAlertAsync(
        Guid waitlistEntryId,
        Guid offeredSlotId,
        CancellationToken ct = default);
}
```

3. **Implement `SlotAlertService`** with HMAC claim link:

```csharp
// server/src/PropelIQ.Infrastructure/Waitlist/
//   SlotAlertService.cs
namespace PropelIQ.Infrastructure.Waitlist;

public sealed class SlotAlertService : ISlotAlertService
{
    private readonly AppDbContext _db;
    private readonly IReminderTokenService _tokenService;
    private readonly TimeProvider _timeProvider;
    private static readonly TimeSpan ClaimWindow =
        TimeSpan.FromHours(2);

    public SlotAlertService(
        AppDbContext db,
        IReminderTokenService tokenService,
        TimeProvider timeProvider)
    {
        _db = db;
        _tokenService = tokenService;
        _timeProvider = timeProvider;
    }

    public async Task<SlotAlertPayload> BuildAlertAsync(
        Guid waitlistEntryId,
        Guid offeredSlotId,
        CancellationToken ct = default)
    {
        var entry = await _db.WaitlistEntries
            .Include(e => e.Patient)
            .FirstAsync(e =>
                e.EntryId == waitlistEntryId, ct);

        var slot = await _db.Appointments
            .FirstAsync(a =>
                a.AppointmentId == offeredSlotId, ct);

        var now = _timeProvider.GetUtcNow();
        var expiresAt = now + ClaimWindow;

        // Generate HMAC-signed claim token
        // Reuses IReminderTokenService pattern from
        // US_027 task_002
        var claimToken = _tokenService.GenerateToken(
            waitlistEntryId, offeredSlotId, expiresAt);

        // Persist offer state
        entry.OfferedAt = now;
        entry.ExpiresAt = expiresAt;
        entry.ClaimTokenHash = Convert.ToBase64String(
            SHA256.HashData(
                Encoding.UTF8.GetBytes(claimToken)));
        entry.Status = "Offered";
        await _db.SaveChangesAsync(ct);

        var claimUrl = $"/claim?token={claimToken}";

        return new SlotAlertPayload(
            waitlistEntryId,
            entry.PatientId,
            $"{entry.Patient.FirstName} " +
                $"{entry.Patient.LastName}",
            entry.Patient.Email,
            entry.Patient.Phone,
            slot.DateTime,
            slot.Type,
            "Provider",  // Resolve from slot
            slot.DurationMinutes,
            claimUrl,
            expiresAt);
    }
}
```

4. **Create `SlotAlertDispatchHandler`** (BackgroundService):

```csharp
// server/src/PropelIQ.Application/Waitlist/
//   SlotAlertDispatchHandler.cs
namespace PropelIQ.Application.Waitlist;

public sealed class SlotAlertDispatchHandler
    : BackgroundService
{
    private readonly ChannelReader<SlotOfferedEvent> _reader;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SlotAlertDispatchHandler> _logger;

    public SlotAlertDispatchHandler(
        ChannelReader<SlotOfferedEvent> reader,
        IServiceScopeFactory scopeFactory,
        ILogger<SlotAlertDispatchHandler> logger)
    {
        _reader = reader;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        await foreach (var evt in _reader
            .ReadAllAsync(stoppingToken))
        {
            try
            {
                await DispatchAlertAsync(evt, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to dispatch slot alert for " +
                    "entry {EntryId}", evt.WaitlistEntryId);
            }
        }
    }

    // AC-1: Dispatch within 5 minutes of slot opening
    private async Task DispatchAlertAsync(
        SlotOfferedEvent evt,
        CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var alertService = scope.ServiceProvider
            .GetRequiredService<ISlotAlertService>();
        var prefRepo = scope.ServiceProvider
            .GetRequiredService<IPatientPreferenceRepository>();
        var emailService = scope.ServiceProvider
            .GetRequiredService<IEmailService>();
        var smsService = scope.ServiceProvider
            .GetRequiredService<ISmsService>();

        var payload = await alertService.BuildAlertAsync(
            evt.WaitlistEntryId, evt.OfferedSlotId, ct);

        var channels = await prefRepo
            .GetEnabledChannelsAsync(payload.PatientId, ct);

        // AC-1: Dispatch via all enabled channels
        if (channels.Contains("Email"))
        {
            await emailService.SendAsync(
                payload.PatientEmail,
                "A preferred slot is available!",
                BuildEmailBody(payload),
                ct);
        }

        if (channels.Contains("SMS")
            && !string.IsNullOrEmpty(payload.PatientPhone))
        {
            await smsService.SendAsync(
                payload.PatientPhone,
                BuildSmsBody(payload),
                ct);
        }

        _logger.LogInformation(
            "Slot alert dispatched for entry {Id} " +
            "via {Channels}",
            evt.WaitlistEntryId,
            string.Join(", ", channels));
    }

    private static string BuildEmailBody(
        SlotAlertPayload p) =>
        $"""
        <h2>A preferred slot is available!</h2>
        <p>Hi {p.PatientName},</p>
        <p>A slot matching your preferences is now
        available:</p>
        <table>
          <tr><td>Date</td>
              <td>{p.SlotDateTime:dddd, MMMM d, yyyy}</td>
          </tr>
          <tr><td>Time</td>
              <td>{p.SlotDateTime:h:mm tt}</td></tr>
          <tr><td>Type</td>
              <td>{p.SlotType}</td></tr>
          <tr><td>Duration</td>
              <td>{p.DurationMinutes} min</td></tr>
        </table>
        <p><strong>You have 2 hours to claim this
        slot.</strong></p>
        <a href="{p.ClaimUrl}"
           style="background:#1976D2;color:#fff;
           padding:12px 24px;text-decoration:none;
           border-radius:4px;">
           Claim Appointment
        </a>
        <p>This offer expires at
        {p.ExpiresAtUtc:h:mm tt} UTC.</p>
        """;

    private static string BuildSmsBody(
        SlotAlertPayload p) =>
        $"A preferred slot is available on " +
        $"{p.SlotDateTime:MMM d} at " +
        $"{p.SlotDateTime:h:mm tt}. " +
        $"Claim within 2h: {p.ClaimUrl}";
}
```

5. **Enhance claim endpoint with HMAC validation and 410 Gone**:

```csharp
// Enhancements to WaitlistController.Claim
// server/src/PropelIQ.Api/Controllers/
//   WaitlistController.cs

// AC-3: Atomic claim with HMAC token
[HttpPost("{id:guid}/claim")]
[Authorize(Roles = "Patient")]
[ProducesResponseType(200)]
[ProducesResponseType(400)]
[ProducesResponseType(410)]
public async Task<IActionResult> Claim(
    Guid id,
    [FromBody] ClaimSlotRequest request,
    CancellationToken ct)
{
    // Validate HMAC token
    if (!_tokenService.ValidateToken(
        request.ClaimToken, out var tokenData))
    {
        return BadRequest(new { Message =
            "Invalid claim token" });
    }

    var entry = await _waitlistRepo
        .GetByIdAsync(id, ct);
    if (entry is null)
        return NotFound();

    // AC-4: Check 2-hour expiry (UTC)
    var now = _timeProvider.GetUtcNow();
    if (entry.ExpiresAt.HasValue
        && now > entry.ExpiresAt.Value)
    {
        return StatusCode(410, new
        {
            Message = "This offer has expired. " +
                "The slot has been offered to " +
                "another patient.",
            ExpiredAt = entry.ExpiresAt.Value
        });
    }

    if (entry.Status != "Offered")
    {
        return BadRequest(new { Message =
            "Slot is no longer available" });
    }

    // AC-3: Atomic reservation
    try
    {
        await _waitlistRepo.ClaimAsync(
            id, GetPatientId(), ct);

        // Publish BookingConfirmedEvent for
        // standard confirmation artifacts
        await _eventWriter.WriteAsync(
            new BookingConfirmedEvent(
                entry.PreferredSlotId!.Value,
                entry.PatientId),
            ct);

        return Ok(new { Message =
            "Appointment claimed successfully" });
    }
    catch (DbUpdateConcurrencyException)
    {
        // Edge case from US_023: concurrent claim
        return Conflict(new { Message =
            "Slot was claimed by another patient" });
    }
}

public sealed record ClaimSlotRequest(
    string ClaimToken);
```

6. **Register handler and Channel in DI**:

```csharp
// In Program.cs — if SlotOfferedEvent channel not yet
// registered from US_023:
var slotOfferedChannel =
    Channel.CreateUnbounded<SlotOfferedEvent>();
services.AddSingleton(slotOfferedChannel.Reader);
services.AddSingleton(slotOfferedChannel.Writer);
services.AddHostedService<SlotAlertDispatchHandler>();
services.AddScoped<ISlotAlertService, SlotAlertService>();
```

## Current Project State

```text
propelIQ/
└── server/
    └── src/
        ├── PropelIQ.Api/
        │   ├── Controllers/
        │   │   └── WaitlistController.cs               (modify — enhance claim with HMAC + 410)
        │   └── Program.cs                              (modify — register handler, Channel)
        ├── PropelIQ.Application/
        │   └── Waitlist/
        │       ├── ISlotAlertService.cs                 (new)
        │       ├── SlotAlertDispatchHandler.cs          (new)
        │       └── Models/
        │           └── SlotAlertPayload.cs              (new)
        ├── PropelIQ.Domain/
        │   └── Entities/
        │       └── WaitlistEntry.cs                     (modify — add OfferedAt, ExpiresAt, ClaimTokenHash)
        └── PropelIQ.Infrastructure/
            ├── Waitlist/
            │   └── SlotAlertService.cs                  (new)
            └── Data/
                └── AppDbContext.cs                       (modify — filtered index on ExpiresAt)
```

> Placeholder: Update on execution based on US_023 task_001 and US_027 task_001/task_002 completion state.

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | server/src/PropelIQ.Application/Waitlist/ISlotAlertService.cs | Interface for building alert payload with HMAC claim link |
| CREATE | server/src/PropelIQ.Application/Waitlist/SlotAlertDispatchHandler.cs | BackgroundService consuming SlotOfferedEvent, dispatching via channels |
| CREATE | server/src/PropelIQ.Application/Waitlist/Models/SlotAlertPayload.cs | Alert content model with slot details and claim URL |
| CREATE | server/src/PropelIQ.Infrastructure/Waitlist/SlotAlertService.cs | Builds alert payload, generates HMAC claim token, persists offer state |
| MODIFY | server/src/PropelIQ.Domain/Entities/WaitlistEntry.cs | Add OfferedAt, ExpiresAt (UTC), ClaimTokenHash columns |
| MODIFY | server/src/PropelIQ.Api/Controllers/WaitlistController.cs | Enhance claim with HMAC validation, 410 Gone on expiry |
| MODIFY | server/src/PropelIQ.Infrastructure/Data/AppDbContext.cs | Filtered index on ExpiresAt for offered entries |
| MODIFY | server/src/PropelIQ.Api/Program.cs | Register SlotAlertDispatchHandler, ISlotAlertService, Channel |

## External References

- System.Threading.Channels: https://learn.microsoft.com/en-us/dotnet/core/extensions/channels
- HMAC-SHA256 in .NET: https://learn.microsoft.com/en-us/dotnet/api/system.security.cryptography.hmacsha256
- HTTP 410 Gone: https://developer.mozilla.org/en-US/docs/Web/HTTP/Status/410
- Polly Retry: https://www.thepollyproject.org/

## Build Commands

```bash
# Build backend
cd server
dotnet build

# Add migration for waitlist offer columns
dotnet ef migrations add AddWaitlistOfferColumns \
  --project src/PropelIQ.Infrastructure \
  --startup-project src/PropelIQ.Api
dotnet ef database update \
  --startup-project src/PropelIQ.Api

# Run
dotnet run --project src/PropelIQ.Api
```

## Implementation Validation Strategy

- [ ] Alert dispatched within 5 minutes of slot opening via enabled channels (AC-1)
- [ ] HMAC-signed claim link contains WaitlistEntryId, OfferedSlotId, ExpiresAtUtc
- [ ] Claim endpoint atomically reserves slot and publishes BookingConfirmedEvent (AC-3)
- [ ] Expired claim returns 410 Gone with descriptive message (AC-4)
- [ ] Confirmation email failure does not un-reserve the slot (edge case 1)
- [ ] All expiry timestamps stored in UTC (edge case 2)
- [ ] Concurrent claim attempts handled via optimistic concurrency (409 Conflict)
- [ ] Patient channel preferences respected for alert dispatch

## Implementation Checklist

- [ ] Add OfferedAt, ExpiresAt, ClaimTokenHash to WaitlistEntry entity
- [ ] Create filtered index on ExpiresAt for offered entries
- [ ] Implement ISlotAlertService with HMAC claim link generation
- [ ] Build email and SMS alert content with slot details
- [ ] Create SlotAlertDispatchHandler consuming SlotOfferedEvent via Channel
- [ ] Enhance claim endpoint with HMAC token validation and 410 Gone
- [ ] Register handler, service, and Channel in DI
- [ ] Verify atomic reservation with BookingConfirmedEvent on success
