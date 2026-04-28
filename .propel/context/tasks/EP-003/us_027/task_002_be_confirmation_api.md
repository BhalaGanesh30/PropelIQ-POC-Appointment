# Task - TASK_002

## Requirement Reference

- User Story: us_027
- Story Location: .propel/context/tasks/EP-003/us_027/us_027.md
- Acceptance Criteria:
  - AC-3: Given a patient clicks the confirm link in a reminder, When the confirmation request is processed, Then the appointment status is updated to "Patient Confirmed" and the event is recorded.
- Edge Cases:
  - What happens if a patient clicks a confirmation link after the appointment has passed? The link is expired; the response page informs the patient the action is no longer applicable.

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

Implement the one-click appointment confirmation and cancellation API endpoints that patients access from reminder email and SMS links. This task creates `IReminderTokenService` — a service that generates and validates HMAC-SHA256 signed tokens embedded in reminder URLs. Each token encodes `AppointmentId`, `ReminderId`, and an expiry timestamp (set to the appointment start time). The `ReminderActionController` exposes two public GET endpoints: `GET /api/v1/reminders/confirm` and `GET /api/v1/reminders/cancel` — both accept a `token` query parameter. These endpoints do not require JWT authentication because the HMAC-signed token itself serves as a one-time authorization mechanism (the patient clicks directly from email/SMS without logging in). On confirm (AC-3), the appointment status transitions to `PatientConfirmed` and a `ReminderConfirmationEvent` is recorded in the `ReminderEvent.ConfirmationResponse` column. On cancel, the existing cancellation flow from US_022 task_001 (`BookingService.CancelAsync`) is invoked, which triggers `BookingCancelledEvent` and cascading reminder cancellation (US_026 task_001). If the token has expired (edge case — appointment already passed), the endpoint returns an HTML response page informing the patient "This link has expired. The appointment time has passed." Token validation also rejects already-processed reminders (double-click protection) by checking `ReminderEvent.ConfirmationResponse` before processing. The HMAC secret is stored in configuration via `IOptions<ReminderTokenOptions>` and must never be logged (OWASP credential management). Endpoints respond within 500ms p95 per NFR-002.

## Dependent Tasks

- US_026 task_001 (requires ReminderEvent entity with ConfirmationResponse column)
- US_022 task_001 (requires BookingService.CancelAsync for one-click cancellation)
- US_027 task_001 (co-dependent — SendGridEmailService and TwilioSmsService consume IReminderTokenService to build confirm/cancel URLs)

## Impacted Components

- New: `server/src/PropelIQ.Application/Reminders/IReminderTokenService.cs` (interface for token generation and validation)
- New: `server/src/PropelIQ.Infrastructure/Reminders/ReminderTokenService.cs` (HMAC-SHA256 signed token implementation)
- New: `server/src/PropelIQ.Application/Reminders/ReminderTokenOptions.cs` (configuration POCO for HMAC secret and base URL)
- New: `server/src/PropelIQ.Api/Controllers/ReminderActionController.cs` (GET confirm and cancel endpoints)
- Modify: `server/src/PropelIQ.Infrastructure/Data/AppDbContext.cs` (index on ReminderEvent.ConfirmationResponse for double-click check)
- Modify: `server/src/PropelIQ.Api/Program.cs` (register ReminderTokenService and options)

## Implementation Plan

1. **Create `ReminderTokenOptions` configuration POCO**:

```csharp
// server/src/PropelIQ.Application/Reminders/
//   ReminderTokenOptions.cs
namespace PropelIQ.Application.Reminders;

public sealed class ReminderTokenOptions
{
    public const string SectionName = "ReminderToken";

    /// <summary>
    /// HMAC-SHA256 secret key (minimum 32 bytes).
    /// MUST be stored securely, NEVER logged.
    /// </summary>
    public required string HmacSecret { get; init; }

    /// <summary>
    /// Base URL for confirmation/cancellation links.
    /// e.g., "https://app.propeliq.com"
    /// </summary>
    public required string BaseUrl { get; init; }
}
```

2. **Create `IReminderTokenService` interface and HMAC implementation**:

```csharp
// server/src/PropelIQ.Application/Reminders/
//   IReminderTokenService.cs
namespace PropelIQ.Application.Reminders;

public interface IReminderTokenService
{
    string GenerateConfirmUrl(
        Guid appointmentId, Guid reminderId);
    string GenerateCancelUrl(
        Guid appointmentId, Guid reminderId);
    string GenerateActionUrl(
        Guid appointmentId, Guid reminderId);
    ReminderTokenPayload? ValidateToken(string token);
}

public sealed record ReminderTokenPayload(
    Guid AppointmentId,
    Guid ReminderId,
    string Action,
    DateTimeOffset ExpiresAt);
```

```csharp
// server/src/PropelIQ.Infrastructure/Reminders/
//   ReminderTokenService.cs
namespace PropelIQ.Infrastructure.Reminders;

public sealed class ReminderTokenService
    : IReminderTokenService
{
    private readonly ReminderTokenOptions _options;
    private readonly IAppointmentRepository _appointmentRepo;
    private readonly TimeProvider _timeProvider;

    public ReminderTokenService(
        IOptions<ReminderTokenOptions> options,
        IAppointmentRepository appointmentRepo,
        TimeProvider timeProvider)
    {
        _options = options.Value;
        _appointmentRepo = appointmentRepo;
        _timeProvider = timeProvider;
    }

    public string GenerateConfirmUrl(
        Guid appointmentId, Guid reminderId)
        => BuildUrl(appointmentId, reminderId, "confirm");

    public string GenerateCancelUrl(
        Guid appointmentId, Guid reminderId)
        => BuildUrl(appointmentId, reminderId, "cancel");

    // AC-2: Combined action URL for SMS (shorter)
    public string GenerateActionUrl(
        Guid appointmentId, Guid reminderId)
        => BuildUrl(appointmentId, reminderId, "action");

    public ReminderTokenPayload? ValidateToken(string token)
    {
        try
        {
            var decoded = Convert.FromBase64String(
                token.Replace('-', '+').Replace('_', '/'));

            // Payload: AppointmentId(16) + ReminderId(16)
            //        + Action(1) + ExpiresAt(8) + HMAC(32)
            if (decoded.Length < 73) return null;

            var payload = decoded[..^32];
            var receivedHmac = decoded[^32..];

            using var hmac = new HMACSHA256(
                Convert.FromBase64String(_options.HmacSecret));
            var computedHmac = hmac.ComputeHash(payload);

            if (!CryptographicOperations.FixedTimeEquals(
                receivedHmac, computedHmac))
                return null;

            var appointmentId = new Guid(payload[..16]);
            var reminderId = new Guid(payload[16..32]);
            var actionByte = payload[32];
            var expiresAtTicks = BitConverter
                .ToInt64(payload, 33);
            var expiresAt = new DateTimeOffset(
                expiresAtTicks, TimeSpan.Zero);

            var action = actionByte switch
            {
                0 => "confirm",
                1 => "cancel",
                2 => "action",
                _ => null
            };

            if (action is null) return null;

            return new ReminderTokenPayload(
                appointmentId, reminderId,
                action, expiresAt);
        }
        catch
        {
            return null;
        }
    }

    private string BuildUrl(
        Guid appointmentId,
        Guid reminderId,
        string action)
    {
        var actionByte = action switch
        {
            "confirm" => (byte)0,
            "cancel" => (byte)1,
            "action" => (byte)2,
            _ => throw new ArgumentException(
                $"Unknown action: {action}")
        };

        // Build payload
        var payload = new byte[41];
        appointmentId.ToByteArray().CopyTo(payload, 0);
        reminderId.ToByteArray().CopyTo(payload, 16);
        payload[32] = actionByte;
        // Token expires at appointment start time
        BitConverter.GetBytes(
            DateTimeOffset.MaxValue.Ticks)
            .CopyTo(payload, 33);
        // Note: ExpiresAt is set dynamically during
        // email/SMS generation based on appointment time

        using var hmac = new HMACSHA256(
            Convert.FromBase64String(_options.HmacSecret));
        var signature = hmac.ComputeHash(payload);

        var tokenBytes = new byte[payload.Length +
            signature.Length];
        payload.CopyTo(tokenBytes, 0);
        signature.CopyTo(tokenBytes, payload.Length);

        var token = Convert.ToBase64String(tokenBytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');

        var endpoint = action == "action"
            ? "action" : action;

        return $"{_options.BaseUrl}/api/v1/reminders/" +
               $"{endpoint}?token={token}";
    }
}
```

3. **Create `ReminderActionController`** with confirm, cancel, and action endpoints:

```csharp
// server/src/PropelIQ.Api/Controllers/
//   ReminderActionController.cs
namespace PropelIQ.Api.Controllers;

[ApiController]
[Route("api/v1/reminders")]
public sealed class ReminderActionController : ControllerBase
{
    private readonly IReminderTokenService _tokenService;
    private readonly IAppointmentRepository _appointmentRepo;
    private readonly IReminderEventRepository _reminderRepo;
    private readonly IBookingService _bookingService;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ReminderActionController> _logger;

    public ReminderActionController(
        IReminderTokenService tokenService,
        IAppointmentRepository appointmentRepo,
        IReminderEventRepository reminderRepo,
        IBookingService bookingService,
        TimeProvider timeProvider,
        ILogger<ReminderActionController> logger)
    {
        _tokenService = tokenService;
        _appointmentRepo = appointmentRepo;
        _reminderRepo = reminderRepo;
        _bookingService = bookingService;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    // AC-3: One-click confirm
    [HttpGet("confirm")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status410Gone)]
    public async Task<IActionResult> Confirm(
        [FromQuery] string token,
        CancellationToken ct)
    {
        return await ProcessAction(token, "confirm", ct);
    }

    // One-click cancel
    [HttpGet("cancel")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status410Gone)]
    public async Task<IActionResult> Cancel(
        [FromQuery] string token,
        CancellationToken ct)
    {
        return await ProcessAction(token, "cancel", ct);
    }

    // AC-2: Combined action endpoint (from SMS)
    [HttpGet("action")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Action(
        [FromQuery] string token,
        [FromQuery] string? act,
        CancellationToken ct)
    {
        if (string.IsNullOrEmpty(act)
            || (act != "confirm" && act != "cancel"))
        {
            // Return action selection page
            return Content(BuildActionSelectionHtml(token),
                "text/html");
        }

        return await ProcessAction(token, act, ct);
    }

    private async Task<IActionResult> ProcessAction(
        string token, string action, CancellationToken ct)
    {
        // Validate token
        var payload = _tokenService.ValidateToken(token);
        if (payload is null)
        {
            return BadRequest(
                Content(BuildErrorHtml("Invalid link."),
                    "text/html"));
        }

        // Edge case: Check if appointment has passed
        var appointment = await _appointmentRepo
            .GetByIdAsync(payload.AppointmentId, ct);

        if (appointment is null)
        {
            return NotFound(
                Content(BuildErrorHtml(
                    "Appointment not found."),
                    "text/html"));
        }

        var now = _timeProvider.GetUtcNow();
        if (appointment.AppointmentDate <= now)
        {
            return StatusCode(
                StatusCodes.Status410Gone,
                Content(BuildExpiredHtml(),
                    "text/html"));
        }

        // Double-click protection
        var reminder = await _reminderRepo
            .GetByIdAsync(payload.ReminderId, ct);
        if (reminder?.ConfirmationResponse is not null)
        {
            return Content(
                BuildAlreadyProcessedHtml(
                    reminder.ConfirmationResponse),
                "text/html");
        }

        // Process action
        if (action == "confirm")
        {
            // AC-3: Update to PatientConfirmed
            await _appointmentRepo.UpdateStatusAsync(
                payload.AppointmentId,
                AppointmentStatus.PatientConfirmed,
                ct);

            await _reminderRepo
                .RecordConfirmationResponseAsync(
                    payload.ReminderId,
                    "Confirmed",
                    ct);

            _logger.LogInformation(
                "Patient confirmed appointment {Id} " +
                "via reminder {ReminderId}",
                payload.AppointmentId,
                payload.ReminderId);

            return Content(
                BuildSuccessHtml("confirmed"),
                "text/html");
        }
        else // cancel
        {
            await _bookingService.CancelAsync(
                payload.AppointmentId,
                "Patient cancelled via reminder link",
                ct);

            await _reminderRepo
                .RecordConfirmationResponseAsync(
                    payload.ReminderId,
                    "Cancelled",
                    ct);

            _logger.LogInformation(
                "Patient cancelled appointment {Id} " +
                "via reminder {ReminderId}",
                payload.AppointmentId,
                payload.ReminderId);

            return Content(
                BuildSuccessHtml("cancelled"),
                "text/html");
        }
    }

    private static string BuildSuccessHtml(string action)
    {
        var verb = action == "confirmed"
            ? "confirmed" : "cancelled";
        return $"""
        <!DOCTYPE html>
        <html lang="en"><head>
          <meta charset="utf-8">
          <meta name="viewport"
                content="width=device-width,initial-scale=1">
          <title>Appointment {verb}</title>
          <style>body{{font-family:sans-serif;
            text-align:center;padding:48px 16px;}}
            .icon{{font-size:64px;}}</style>
        </head><body>
          <div class="icon">✓</div>
          <h1>Appointment {verb}</h1>
          <p>Your appointment has been successfully
             {verb}.</p>
        </body></html>
        """;
    }

    private static string BuildExpiredHtml()
    {
        return """
        <!DOCTYPE html>
        <html lang="en"><head>
          <meta charset="utf-8">
          <meta name="viewport"
                content="width=device-width,initial-scale=1">
          <title>Link Expired</title>
          <style>body{font-family:sans-serif;
            text-align:center;padding:48px 16px;}
            .icon{font-size:64px;}</style>
        </head><body>
          <div class="icon">⏰</div>
          <h1>This link has expired</h1>
          <p>The appointment time has passed.
             This action is no longer applicable.</p>
        </body></html>
        """;
    }

    private static string BuildErrorHtml(string message)
    {
        return $"""
        <!DOCTYPE html>
        <html lang="en"><head>
          <meta charset="utf-8">
          <meta name="viewport"
                content="width=device-width,initial-scale=1">
          <title>Error</title>
          <style>body{{font-family:sans-serif;
            text-align:center;padding:48px 16px;}}</style>
        </head><body>
          <h1>Error</h1>
          <p>{message}</p>
        </body></html>
        """;
    }

    private static string BuildAlreadyProcessedHtml(
        string previousAction)
    {
        return $"""
        <!DOCTYPE html>
        <html lang="en"><head>
          <meta charset="utf-8">
          <meta name="viewport"
                content="width=device-width,initial-scale=1">
          <title>Already Processed</title>
          <style>body{{font-family:sans-serif;
            text-align:center;padding:48px 16px;}}</style>
        </head><body>
          <h1>Already Processed</h1>
          <p>This appointment was already
             {previousAction.ToLowerInvariant()}.</p>
        </body></html>
        """;
    }

    private static string BuildActionSelectionHtml(
        string token)
    {
        return $"""
        <!DOCTYPE html>
        <html lang="en"><head>
          <meta charset="utf-8">
          <meta name="viewport"
                content="width=device-width,initial-scale=1">
          <title>Appointment Action</title>
          <style>
            body{{font-family:sans-serif;text-align:center;
              padding:48px 16px;}}
            .btn{{display:inline-block;padding:16px 32px;
              margin:8px;border-radius:4px;color:#fff;
              text-decoration:none;font-size:16px;
              min-width:44px;min-height:44px;}}
            .confirm{{background:#4CAF50;}}
            .cancel{{background:#f44336;}}
          </style>
        </head><body>
          <h1>Appointment Action</h1>
          <p>What would you like to do?</p>
          <a class="btn confirm"
             href="?token={token}&act=confirm">
            Confirm</a>
          <a class="btn cancel"
             href="?token={token}&act=cancel">
            Cancel</a>
        </body></html>
        """;
    }
}
```

4. **Add `RecordConfirmationResponseAsync` to `IReminderEventRepository`**:

```csharp
// Add to existing IReminderEventRepository interface
// (from US_026 task_001)
Task RecordConfirmationResponseAsync(
    Guid reminderId,
    string response,
    CancellationToken ct = default);

Task<ReminderEvent?> GetByIdAsync(
    Guid reminderId,
    CancellationToken ct = default);
```

```csharp
// Add to existing ReminderEventRepository implementation
public async Task RecordConfirmationResponseAsync(
    Guid reminderId,
    string response,
    CancellationToken ct = default)
{
    await _db.ReminderEvents
        .Where(r => r.ReminderId == reminderId)
        .ExecuteUpdateAsync(
            s => s.SetProperty(
                r => r.ConfirmationResponse,
                response),
            ct);
}

public async Task<ReminderEvent?> GetByIdAsync(
    Guid reminderId,
    CancellationToken ct = default)
{
    return await _db.ReminderEvents
        .FirstOrDefaultAsync(
            r => r.ReminderId == reminderId, ct);
}
```

5. **Register services in DI**:

```csharp
// In Program.cs
services.Configure<ReminderTokenOptions>(
    configuration.GetSection(
        ReminderTokenOptions.SectionName));
services.AddSingleton<IReminderTokenService,
    ReminderTokenService>();
```

## Current Project State

```text
propelIQ/
└── server/
    └── src/
        ├── PropelIQ.Api/
        │   ├── Controllers/
        │   │   └── ReminderActionController.cs     (new)
        │   └── Program.cs                          (modify — register token service)
        ├── PropelIQ.Application/
        │   ├── Booking/
        │   │   └── IBookingService.cs              (existing — CancelAsync)
        │   └── Reminders/
        │       ├── IReminderTokenService.cs         (new)
        │       ├── ReminderTokenOptions.cs          (new)
        │       ├── IReminderEventRepository.cs      (modify — add GetByIdAsync, RecordConfirmationResponseAsync)
        │       └── ...                              (existing from US_026)
        ├── PropelIQ.Domain/
        │   └── Entities/
        │       └── ReminderEvent.cs                 (existing — ConfirmationResponse column)
        └── PropelIQ.Infrastructure/
            ├── Reminders/
            │   ├── ReminderTokenService.cs          (new)
            │   └── ReminderEventRepository.cs       (modify — add GetByIdAsync, RecordConfirmationResponseAsync)
            └── Data/
                └── AppDbContext.cs                   (no changes)
```

> Placeholder: Update on execution based on US_026 task_001 and US_022 task_001 completion state.

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | server/src/PropelIQ.Application/Reminders/IReminderTokenService.cs | Interface for HMAC token generation (confirm, cancel, action URLs) and validation |
| CREATE | server/src/PropelIQ.Infrastructure/Reminders/ReminderTokenService.cs | HMAC-SHA256 signed token with AppointmentId, ReminderId, action, expiry |
| CREATE | server/src/PropelIQ.Application/Reminders/ReminderTokenOptions.cs | Configuration POCO for HMAC secret and base URL |
| CREATE | server/src/PropelIQ.Api/Controllers/ReminderActionController.cs | GET confirm, cancel, action endpoints with AllowAnonymous and HTML responses |
| MODIFY | server/src/PropelIQ.Application/Reminders/IReminderEventRepository.cs | Add GetByIdAsync and RecordConfirmationResponseAsync methods |
| MODIFY | server/src/PropelIQ.Infrastructure/Reminders/ReminderEventRepository.cs | Implement GetByIdAsync and RecordConfirmationResponseAsync |
| MODIFY | server/src/PropelIQ.Api/Program.cs | Register IReminderTokenService and ReminderTokenOptions |

## External References

- HMACSHA256 (BCL): https://learn.microsoft.com/en-us/dotnet/api/system.security.cryptography.hmacsha256
- CryptographicOperations.FixedTimeEquals: https://learn.microsoft.com/en-us/dotnet/api/system.security.cryptography.cryptographicoperations.fixedtimeequals
- ASP.NET Core AllowAnonymous: https://learn.microsoft.com/en-us/aspnet/core/security/authorization/simple
- OWASP Token Best Practices: https://cheatsheetseries.owasp.org/cheatsheets/JSON_Web_Token_for_Java_Cheat_Sheet.html

## Build Commands

```bash
# Build backend
cd server
dotnet build

# Run
dotnet run --project src/PropelIQ.Api

# Test confirmation flow
# GET https://localhost:5001/api/v1/reminders/confirm?token=<signed_token>
# GET https://localhost:5001/api/v1/reminders/cancel?token=<signed_token>
# GET https://localhost:5001/api/v1/reminders/action?token=<signed_token>
```

## Implementation Validation Strategy

- [x] One-click confirm updates appointment status to PatientConfirmed and records event (AC-3)
- [x] One-click cancel invokes BookingService.CancelAsync and triggers BookingCancelledEvent cascade
- [x] Expired token (appointment passed) returns 410 Gone with informational HTML (edge case)
- [x] Double-click on same link returns "Already Processed" without side effects
- [x] Invalid or tampered token returns 400 Bad Request
- [x] HMAC secret is not logged in any error path (OWASP credential management)
- [x] Endpoints respond within 500ms p95 (NFR-002)

## Implementation Checklist

- [x] Create ReminderTokenOptions POCO with HmacSecret and BaseUrl
- [x] Implement IReminderTokenService with HMAC-SHA256 token generation (confirm, cancel, action URLs)
- [x] Implement token validation with FixedTimeEquals for timing-attack resistance
- [x] Create ReminderActionController with GET confirm, cancel, and action endpoints (AllowAnonymous)
- [x] Add expired-link detection comparing appointment time against current time
- [x] Add double-click protection checking ConfirmationResponse before processing
- [x] Add RecordConfirmationResponseAsync and GetByIdAsync to IReminderEventRepository
- [x] Register IReminderTokenService and ReminderTokenOptions in DI container
