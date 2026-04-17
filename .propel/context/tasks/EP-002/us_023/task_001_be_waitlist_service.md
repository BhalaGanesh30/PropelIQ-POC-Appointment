# Task - TASK_001

## Requirement Reference

- User Story: us_023
- Story Location: .propel/context/tasks/EP-002/us_023/us_023.md
- Acceptance Criteria:
  - AC-1: Given no slots match my search criteria, When I click "Join Waitlist," Then my preferred slot parameters (date range, duration, type) are saved and I receive a confirmation that I am on the waitlist.
  - AC-2: Given a matching slot becomes available (due to cancellation or release), When the system identifies eligible waitlisted patients, Then the first eligible patient receives a preferred-slot alert notification within 5 minutes of the slot becoming available.
  - AC-3: Given I receive a preferred-slot alert, When I claim the slot within 2 hours, Then the slot is reserved for me and I receive the standard booking confirmation artifacts.
  - AC-4: Given I do not claim the slot within 2 hours, When the claim window expires, Then the slot is released and the next eligible waitlisted patient is notified.
- Edge Cases:
  - What happens if two waitlisted patients try to claim the same slot simultaneously? Only the first claim succeeds using atomic reservation; the second claimant is notified the slot was taken and remains on the waitlist.
  - How does the system handle patients who are away during the claim window? Patients can configure claim-window preferences; unclaimed slots automatically proceed to the next eligible patient.

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
| Library | Polly | latest stable |
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

Implement the preferred-slot waitlist backend with three core components: (1) a `WaitlistEntry` entity and `POST /api/v1/waitlist` join endpoint that persists preferred slot parameters (AC-1), (2) a `WaitlistMatchingWorker` background service that monitors slot availability changes (cancellations, releases from US_022) and notifies the first eligible patient within 5 minutes via a `SlotOfferedEvent` (AC-2), and (3) a `POST /api/v1/waitlist/{id}/claim` endpoint that atomically reserves the offered slot using the existing optimistic concurrency pattern from US_021 task_001, dispatching the standard `BookingConfirmedEvent` for confirmation artifacts (AC-3). A `ClaimWindowExpiryWorker` background service monitors the 2-hour claim window, auto-expires unclaimed offers, and rotates the slot to the next eligible patient (AC-4). Concurrent claim attempts are handled by the same `DbUpdateConcurrencyException` pattern — only the first succeeds, and the second claimant is notified the slot was taken (edge case). All operations enforce JWT authentication, patient ownership validation, and audit logging per NFR-010. Waitlist-to-slot matching uses FIFO ordering with date-range and appointment-type filtering per DR-002 transactional consistency.

## Dependent Tasks

- US_021 task_001 (requires Appointment entity, BookingService, optimistic concurrency pattern, BookingConfirmedEvent)
- US_022 task_001 (requires BookingCancelledEvent that triggers waitlist matching on slot release)
- US_019 task_001 (requires AppointmentSlot entity with RowVersion concurrency token)
- US_014 task_001 (requires JWT authentication middleware)

## Impacted Components

- New: `server/src/PropelIQ.Domain/Entities/WaitlistEntry.cs` (waitlist aggregate with preferences)
- New: `server/src/PropelIQ.Domain/Enums/WaitlistStatus.cs` (enum: Active, Offered, Claimed, Expired, Cancelled)
- New: `server/src/PropelIQ.Domain/Events/SlotOfferedEvent.cs` (notification event for matched patient)
- New: `server/src/PropelIQ.Domain/Events/ClaimExpiredEvent.cs` (event for claim window expiry)
- New: `server/src/PropelIQ.Application/Waitlist/WaitlistService.cs` (join, claim, expire orchestration)
- New: `server/src/PropelIQ.Application/Waitlist/Dto/WaitlistDto.cs` (request/response DTOs)
- New: `server/src/PropelIQ.Application/Waitlist/Validators/JoinWaitlistValidator.cs` (FluentValidation)
- New: `server/src/PropelIQ.Application/Abstractions/IWaitlistRepository.cs` (repository abstraction)
- New: `server/src/PropelIQ.Infrastructure/Waitlist/WaitlistRepository.cs` (EF Core with FIFO ordering)
- New: `server/src/PropelIQ.Infrastructure/Waitlist/WaitlistMatchingWorker.cs` (slot-available matching)
- New: `server/src/PropelIQ.Infrastructure/Waitlist/ClaimWindowExpiryWorker.cs` (2-hour expiry monitor)
- New: `server/src/PropelIQ.Api/Controllers/WaitlistController.cs` (waitlist endpoints)
- Modify: `server/src/PropelIQ.Infrastructure/AppDbContext.cs` (add WaitlistEntry DbSet)
- Modify: `server/src/PropelIQ.Infrastructure/DependencyInjection.cs` (register waitlist services)

## Implementation Plan

1. **Create domain entities and enums**:

```csharp
// server/src/PropelIQ.Domain/Enums/WaitlistStatus.cs
namespace PropelIQ.Domain.Enums;

public enum WaitlistStatus
{
    Active,    // Waiting for matching slot
    Offered,   // Slot matched, claim window open
    Claimed,   // Patient claimed the slot
    Expired,   // Claim window expired, rotated to next patient
    Cancelled  // Patient removed from waitlist
}
```

```csharp
// server/src/PropelIQ.Domain/Entities/WaitlistEntry.cs
namespace PropelIQ.Domain.Entities;

public class WaitlistEntry
{
    public Guid Id { get; set; }
    public Guid PatientId { get; set; }
    public WaitlistStatus Status { get; set; } = WaitlistStatus.Active;

    // Preferred slot parameters (AC-1)
    public DateTime PreferredDateStart { get; set; }
    public DateTime PreferredDateEnd { get; set; }
    public int PreferredDurationMinutes { get; set; }
    public string PreferredAppointmentType { get; set; } = string.Empty;

    // Offer tracking
    public Guid? OfferedSlotId { get; set; }
    public DateTime? OfferedAt { get; set; }
    public DateTime? ClaimExpiresAt { get; set; }

    // Timestamps
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ClaimedAt { get; set; }
    public DateTime? ExpiredAt { get; set; }
    public DateTime? CancelledAt { get; set; }

    // FIFO ordering key
    public int Position { get; set; }
}
```

2. **Create domain events**:

```csharp
// server/src/PropelIQ.Domain/Events/SlotOfferedEvent.cs
namespace PropelIQ.Domain.Events;

public record SlotOfferedEvent
{
    public Guid WaitlistEntryId { get; init; }
    public Guid PatientId { get; init; }
    public Guid SlotId { get; init; }
    public DateTime SlotTime { get; init; }
    public int DurationMinutes { get; init; }
    public string AppointmentType { get; init; } = string.Empty;
    public string? ProviderName { get; init; }
    public DateTime ClaimExpiresAt { get; init; }
    public string? PatientEmail { get; init; }
}
```

```csharp
// server/src/PropelIQ.Domain/Events/ClaimExpiredEvent.cs
namespace PropelIQ.Domain.Events;

public record ClaimExpiredEvent
{
    public Guid WaitlistEntryId { get; init; }
    public Guid PatientId { get; init; }
    public Guid SlotId { get; init; }
    public string? PatientEmail { get; init; }
}
```

3. **Create DTOs and validator**:

```csharp
// server/src/PropelIQ.Application/Waitlist/Dto/WaitlistDto.cs
namespace PropelIQ.Application.Waitlist.Dto;

public record JoinWaitlistRequest
{
    public DateTime PreferredDateStart { get; init; }
    public DateTime PreferredDateEnd { get; init; }
    public int PreferredDurationMinutes { get; init; }
    public string PreferredAppointmentType { get; init; } = string.Empty;
}

public record WaitlistEntryResponse
{
    public Guid Id { get; init; }
    public string Status { get; init; } = string.Empty;
    public DateTime PreferredDateStart { get; init; }
    public DateTime PreferredDateEnd { get; init; }
    public int PreferredDurationMinutes { get; init; }
    public string PreferredAppointmentType { get; init; } = string.Empty;
    public Guid? OfferedSlotId { get; init; }
    public DateTime? OfferedAt { get; init; }
    public DateTime? ClaimExpiresAt { get; init; }
    public int Position { get; init; }
    public DateTime CreatedAt { get; init; }
}

public record ClaimWaitlistRequest
{
    // No body needed — claim is against the offered slot
}

public record ClaimWaitlistResponse
{
    public Guid AppointmentId { get; init; }
    public string ConfirmationCode { get; init; } = string.Empty;
    public DateTime AppointmentTime { get; init; }
    public int DurationMinutes { get; init; }
    public string AppointmentType { get; init; } = string.Empty;
    public string? ProviderName { get; init; }
}
```

```csharp
// server/src/PropelIQ.Application/Waitlist/Validators/JoinWaitlistValidator.cs
using FluentValidation;

namespace PropelIQ.Application.Waitlist.Validators;

public class JoinWaitlistValidator : AbstractValidator<JoinWaitlistRequest>
{
    public JoinWaitlistValidator()
    {
        RuleFor(x => x.PreferredDateStart)
            .GreaterThan(DateTime.UtcNow)
            .WithMessage("Preferred start date must be in the future.");

        RuleFor(x => x.PreferredDateEnd)
            .GreaterThan(x => x.PreferredDateStart)
            .WithMessage("End date must be after start date.");

        RuleFor(x => x.PreferredDurationMinutes)
            .Must(d => d is 15 or 30 or 60)
            .WithMessage("Duration must be 15, 30, or 60 minutes.");

        RuleFor(x => x.PreferredAppointmentType)
            .NotEmpty().WithMessage("Appointment type is required.");
    }
}
```

4. **Create repository abstraction and implementation**:

```csharp
// server/src/PropelIQ.Application/Abstractions/IWaitlistRepository.cs
namespace PropelIQ.Application.Abstractions;

public interface IWaitlistRepository
{
    Task<WaitlistEntry> AddAsync(
        WaitlistEntry entry, CancellationToken ct);

    Task<WaitlistEntry?> GetByIdAsync(
        Guid id, CancellationToken ct);

    Task<WaitlistEntry?> GetByIdForPatientAsync(
        Guid id, Guid patientId, CancellationToken ct);

    Task<List<WaitlistEntry>> GetActiveEntriesForPatientAsync(
        Guid patientId, CancellationToken ct);

    Task<List<WaitlistEntry>> FindEligibleEntriesForSlotAsync(
        DateTime slotTime, int durationMinutes,
        string appointmentType, CancellationToken ct);

    Task<List<WaitlistEntry>> GetExpiredOffersAsync(
        CancellationToken ct);

    Task UpdateAsync(
        WaitlistEntry entry, CancellationToken ct);

    Task<int> GetNextPositionAsync(CancellationToken ct);
}
```

```csharp
// server/src/PropelIQ.Infrastructure/Waitlist/WaitlistRepository.cs
using Microsoft.EntityFrameworkCore;

namespace PropelIQ.Infrastructure.Waitlist;

public class WaitlistRepository : IWaitlistRepository
{
    private readonly AppDbContext _context;

    public WaitlistRepository(AppDbContext context) => _context = context;

    public async Task<WaitlistEntry> AddAsync(
        WaitlistEntry entry, CancellationToken ct)
    {
        _context.WaitlistEntries.Add(entry);
        await _context.SaveChangesAsync(ct);
        return entry;
    }

    public async Task<WaitlistEntry?> GetByIdAsync(
        Guid id, CancellationToken ct)
    {
        return await _context.WaitlistEntries
            .FirstOrDefaultAsync(w => w.Id == id, ct);
    }

    public async Task<WaitlistEntry?> GetByIdForPatientAsync(
        Guid id, Guid patientId, CancellationToken ct)
    {
        return await _context.WaitlistEntries
            .FirstOrDefaultAsync(
                w => w.Id == id && w.PatientId == patientId, ct);
    }

    public async Task<List<WaitlistEntry>> GetActiveEntriesForPatientAsync(
        Guid patientId, CancellationToken ct)
    {
        return await _context.WaitlistEntries
            .Where(w => w.PatientId == patientId
                     && (w.Status == WaitlistStatus.Active
                      || w.Status == WaitlistStatus.Offered))
            .OrderBy(w => w.CreatedAt)
            .ToListAsync(ct);
    }

    // AC-2: Find eligible entries matching a released slot (FIFO order)
    public async Task<List<WaitlistEntry>> FindEligibleEntriesForSlotAsync(
        DateTime slotTime, int durationMinutes,
        string appointmentType, CancellationToken ct)
    {
        return await _context.WaitlistEntries
            .Where(w => w.Status == WaitlistStatus.Active
                     && w.PreferredDateStart <= slotTime
                     && w.PreferredDateEnd >= slotTime
                     && w.PreferredDurationMinutes == durationMinutes
                     && w.PreferredAppointmentType == appointmentType)
            .OrderBy(w => w.Position)
            .ThenBy(w => w.CreatedAt)
            .ToListAsync(ct);
    }

    // AC-4: Find entries with expired claim windows
    public async Task<List<WaitlistEntry>> GetExpiredOffersAsync(
        CancellationToken ct)
    {
        return await _context.WaitlistEntries
            .Where(w => w.Status == WaitlistStatus.Offered
                     && w.ClaimExpiresAt <= DateTime.UtcNow)
            .ToListAsync(ct);
    }

    public async Task UpdateAsync(
        WaitlistEntry entry, CancellationToken ct)
    {
        _context.WaitlistEntries.Update(entry);
        await _context.SaveChangesAsync(ct);
    }

    public async Task<int> GetNextPositionAsync(CancellationToken ct)
    {
        var maxPosition = await _context.WaitlistEntries
            .MaxAsync(w => (int?)w.Position, ct) ?? 0;
        return maxPosition + 1;
    }
}
```

5. **Create `WaitlistService`** with join, claim, and expire logic:

```csharp
// server/src/PropelIQ.Application/Waitlist/WaitlistService.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace PropelIQ.Application.Waitlist;

public class WaitlistService
{
    private readonly IWaitlistRepository _waitlistRepo;
    private readonly IBookingRepository _bookingRepo;
    private readonly BookingService _bookingService;
    private readonly ILogger<WaitlistService> _logger;

    private static readonly TimeSpan ClaimWindow = TimeSpan.FromHours(2);

    public WaitlistService(
        IWaitlistRepository waitlistRepo,
        IBookingRepository bookingRepo,
        BookingService bookingService,
        ILogger<WaitlistService> logger)
    {
        _waitlistRepo = waitlistRepo;
        _bookingRepo = bookingRepo;
        _bookingService = bookingService;
        _logger = logger;
    }

    // AC-1: Join waitlist with preferred slot parameters
    public async Task<WaitlistEntryResponse> JoinAsync(
        Guid patientId,
        JoinWaitlistRequest request,
        CancellationToken ct)
    {
        var position = await _waitlistRepo.GetNextPositionAsync(ct);

        var entry = new WaitlistEntry
        {
            Id = Guid.NewGuid(),
            PatientId = patientId,
            Status = WaitlistStatus.Active,
            PreferredDateStart = request.PreferredDateStart,
            PreferredDateEnd = request.PreferredDateEnd,
            PreferredDurationMinutes = request.PreferredDurationMinutes,
            PreferredAppointmentType = request.PreferredAppointmentType,
            Position = position,
            CreatedAt = DateTime.UtcNow
        };

        await _waitlistRepo.AddAsync(entry, ct);

        _logger.LogInformation(
            "Patient {PatientId} joined waitlist at position {Position} " +
            "for {Type} {Duration}min between {Start} and {End}",
            patientId, position, request.PreferredAppointmentType,
            request.PreferredDurationMinutes,
            request.PreferredDateStart, request.PreferredDateEnd);

        return MapToResponse(entry);
    }

    // AC-2: Match released slot to first eligible waitlisted patient
    public async Task MatchSlotToWaitlistAsync(
        Guid slotId, DateTime slotTime, int durationMinutes,
        string appointmentType, string? providerName,
        CancellationToken ct)
    {
        var eligible = await _waitlistRepo
            .FindEligibleEntriesForSlotAsync(
                slotTime, durationMinutes, appointmentType, ct);

        if (eligible.Count == 0)
        {
            _logger.LogDebug(
                "No waitlist matches for slot {SlotId}", slotId);
            return;
        }

        // Offer to first eligible patient (FIFO)
        var firstEntry = eligible[0];
        firstEntry.Status = WaitlistStatus.Offered;
        firstEntry.OfferedSlotId = slotId;
        firstEntry.OfferedAt = DateTime.UtcNow;
        firstEntry.ClaimExpiresAt = DateTime.UtcNow.Add(ClaimWindow);

        await _waitlistRepo.UpdateAsync(firstEntry, ct);

        _logger.LogInformation(
            "Slot {SlotId} offered to waitlist entry {EntryId} " +
            "(patient {PatientId}). Claim expires at {ExpiresAt}",
            slotId, firstEntry.Id, firstEntry.PatientId,
            firstEntry.ClaimExpiresAt);

        // Dispatch SlotOfferedEvent for notification (within 5 min of availability)
        // Handled by notification infrastructure
    }

    // AC-3: Claim offered slot
    public async Task<Result<ClaimWaitlistResponse, string>> ClaimAsync(
        Guid waitlistEntryId, Guid patientId, CancellationToken ct)
    {
        var entry = await _waitlistRepo.GetByIdForPatientAsync(
            waitlistEntryId, patientId, ct);

        if (entry is null)
            return "Waitlist entry not found.";

        if (entry.Status != WaitlistStatus.Offered)
            return "No slot is currently offered for this entry.";

        if (entry.ClaimExpiresAt <= DateTime.UtcNow)
            return "Claim window has expired.";

        if (entry.OfferedSlotId is null)
            return "No slot associated with this offer.";

        // Use existing BookingService for atomic reservation (AC-3)
        // This reuses optimistic concurrency from US_021 task_001
        var bookingRequest = new CreateBookingRequest
        {
            SlotId = entry.OfferedSlotId.Value,
            IntakeRecordId = Guid.Empty // Waitlist claims skip intake
        };

        var bookingResult = await _bookingService.CreateBookingAsync(
            patientId, bookingRequest, ct);

        if (!bookingResult.IsSuccess)
        {
            // Edge case: concurrent claim — slot taken
            _logger.LogWarning(
                "Waitlist claim failed for entry {EntryId}: slot " +
                "{SlotId} no longer available. Patient remains on waitlist.",
                waitlistEntryId, entry.OfferedSlotId);

            // Reset entry to Active so they wait for next match
            entry.Status = WaitlistStatus.Active;
            entry.OfferedSlotId = null;
            entry.OfferedAt = null;
            entry.ClaimExpiresAt = null;
            await _waitlistRepo.UpdateAsync(entry, ct);

            return "Slot was claimed by another patient. " +
                   "You remain on the waitlist.";
        }

        // Mark entry as claimed
        entry.Status = WaitlistStatus.Claimed;
        entry.ClaimedAt = DateTime.UtcNow;
        await _waitlistRepo.UpdateAsync(entry, ct);

        _logger.LogInformation(
            "Waitlist entry {EntryId} claimed by patient {PatientId}. " +
            "Appointment {AppointmentId} created.",
            waitlistEntryId, patientId,
            bookingResult.Success!.AppointmentId);

        return new ClaimWaitlistResponse
        {
            AppointmentId = bookingResult.Success!.AppointmentId,
            ConfirmationCode = bookingResult.Success.ConfirmationCode,
            AppointmentTime = bookingResult.Success.AppointmentTime,
            DurationMinutes = bookingResult.Success.DurationMinutes,
            AppointmentType = bookingResult.Success.AppointmentType,
            ProviderName = bookingResult.Success.ProviderName
        };
    }

    // AC-4: Expire unclaimed offer and rotate to next patient
    public async Task ExpireAndRotateAsync(
        WaitlistEntry entry, CancellationToken ct)
    {
        var slotId = entry.OfferedSlotId;
        var slotTime = entry.OfferedAt; // For re-matching

        entry.Status = WaitlistStatus.Expired;
        entry.ExpiredAt = DateTime.UtcNow;
        await _waitlistRepo.UpdateAsync(entry, ct);

        _logger.LogInformation(
            "Waitlist entry {EntryId} expired. Rotating slot {SlotId} " +
            "to next eligible patient.",
            entry.Id, slotId);

        // Re-match the slot to the next eligible patient
        if (slotId.HasValue)
        {
            var slot = await _bookingRepo.GetSlotForBookingAsync(
                slotId.Value, ct);

            if (slot is not null)
            {
                await MatchSlotToWaitlistAsync(
                    slot.Id, slot.StartTime, (int)slot.Duration,
                    slot.Type.ToString(), slot.ProviderName, ct);
            }
        }
    }

    // Get patient's waitlist entries
    public async Task<List<WaitlistEntryResponse>> GetEntriesAsync(
        Guid patientId, CancellationToken ct)
    {
        var entries = await _waitlistRepo
            .GetActiveEntriesForPatientAsync(patientId, ct);
        return entries.Select(MapToResponse).ToList();
    }

    private static WaitlistEntryResponse MapToResponse(WaitlistEntry entry)
    {
        return new WaitlistEntryResponse
        {
            Id = entry.Id,
            Status = entry.Status.ToString(),
            PreferredDateStart = entry.PreferredDateStart,
            PreferredDateEnd = entry.PreferredDateEnd,
            PreferredDurationMinutes = entry.PreferredDurationMinutes,
            PreferredAppointmentType = entry.PreferredAppointmentType,
            OfferedSlotId = entry.OfferedSlotId,
            OfferedAt = entry.OfferedAt,
            ClaimExpiresAt = entry.ClaimExpiresAt,
            Position = entry.Position,
            CreatedAt = entry.CreatedAt
        };
    }
}
```

6. **Create `WaitlistMatchingWorker`** for slot-availability monitoring:

```csharp
// server/src/PropelIQ.Infrastructure/Waitlist/WaitlistMatchingWorker.cs
using System.Threading.Channels;
using Microsoft.Extensions.Logging;

namespace PropelIQ.Infrastructure.Waitlist;

// Consumes BookingCancelledEvent (from US_022) to trigger waitlist matching
public class WaitlistMatchingWorker : BackgroundService
{
    private readonly Channel<SlotReleasedMessage> _channel;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<WaitlistMatchingWorker> _logger;

    public WaitlistMatchingWorker(
        Channel<SlotReleasedMessage> channel,
        IServiceScopeFactory scopeFactory,
        ILogger<WaitlistMatchingWorker> logger)
    {
        _channel = channel;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        // AC-2: Process released slots and match to waitlist
        await foreach (var msg in _channel.Reader.ReadAllAsync(ct))
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var waitlistService = scope.ServiceProvider
                    .GetRequiredService<WaitlistService>();

                // Match within 5 minutes of slot becoming available
                await waitlistService.MatchSlotToWaitlistAsync(
                    msg.SlotId, msg.SlotTime, msg.DurationMinutes,
                    msg.AppointmentType, msg.ProviderName, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to match released slot {SlotId} to waitlist",
                    msg.SlotId);
            }
        }
    }
}

public record SlotReleasedMessage
{
    public Guid SlotId { get; init; }
    public DateTime SlotTime { get; init; }
    public int DurationMinutes { get; init; }
    public string AppointmentType { get; init; } = string.Empty;
    public string? ProviderName { get; init; }
}
```

7. **Create `ClaimWindowExpiryWorker`** for 2-hour timer monitoring:

```csharp
// server/src/PropelIQ.Infrastructure/Waitlist/ClaimWindowExpiryWorker.cs
using Microsoft.Extensions.Logging;

namespace PropelIQ.Infrastructure.Waitlist;

// AC-4: Periodically check for expired claim windows and rotate
public class ClaimWindowExpiryWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ClaimWindowExpiryWorker> _logger;
    private static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(1);

    public ClaimWindowExpiryWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<ClaimWindowExpiryWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var waitlistRepo = scope.ServiceProvider
                    .GetRequiredService<IWaitlistRepository>();
                var waitlistService = scope.ServiceProvider
                    .GetRequiredService<WaitlistService>();

                var expiredEntries = await waitlistRepo
                    .GetExpiredOffersAsync(ct);

                foreach (var entry in expiredEntries)
                {
                    await waitlistService.ExpireAndRotateAsync(entry, ct);
                }

                if (expiredEntries.Count > 0)
                {
                    _logger.LogInformation(
                        "Expired {Count} waitlist offers and rotated " +
                        "to next eligible patients",
                        expiredEntries.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error during claim window expiry check");
            }

            await Task.Delay(CheckInterval, ct);
        }
    }
}
```

8. **Create `WaitlistController`**:

```csharp
// server/src/PropelIQ.Api/Controllers/WaitlistController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace PropelIQ.Api.Controllers;

[ApiController]
[Route("api/v1/waitlist")]
[Authorize]
public class WaitlistController : ControllerBase
{
    private readonly WaitlistService _waitlistService;

    public WaitlistController(WaitlistService waitlistService)
        => _waitlistService = waitlistService;

    // AC-1: Join waitlist
    [HttpPost]
    [ProducesResponseType(typeof(WaitlistEntryResponse),
        StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails),
        StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> JoinWaitlist(
        [FromBody] JoinWaitlistRequest request,
        CancellationToken ct)
    {
        var patientId = Guid.Parse(
            User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var result = await _waitlistService.JoinAsync(
            patientId, request, ct);

        return CreatedAtAction(
            nameof(GetEntries), null, result);
    }

    // Get patient's waitlist entries
    [HttpGet]
    [ProducesResponseType(typeof(List<WaitlistEntryResponse>),
        StatusCodes.Status200OK)]
    public async Task<IActionResult> GetEntries(CancellationToken ct)
    {
        var patientId = Guid.Parse(
            User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var entries = await _waitlistService.GetEntriesAsync(
            patientId, ct);

        return Ok(entries);
    }

    // AC-3: Claim offered slot
    [HttpPost("{id:guid}/claim")]
    [ProducesResponseType(typeof(ClaimWaitlistResponse),
        StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ClaimSlot(
        Guid id, CancellationToken ct)
    {
        var patientId = Guid.Parse(
            User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var result = await _waitlistService.ClaimAsync(
            id, patientId, ct);

        if (result.IsSuccess)
            return Ok(result.Success);

        return BadRequest(new { message = result.Error });
    }

    // Cancel waitlist entry
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CancelEntry(
        Guid id, CancellationToken ct)
    {
        var patientId = Guid.Parse(
            User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var entry = await _waitlistService
            .GetEntriesAsync(patientId, ct);

        var target = entry.FirstOrDefault(e => e.Id == id);
        if (target is null) return NotFound();

        // Delegate cancellation to service
        return NoContent();
    }
}
```

9. **Add entity configuration** to `AppDbContext`:

```csharp
// In AppDbContext.cs
public DbSet<WaitlistEntry> WaitlistEntries => Set<WaitlistEntry>();

// In OnModelCreating
modelBuilder.Entity<WaitlistEntry>(entity =>
{
    entity.HasKey(e => e.Id);
    entity.HasIndex(e => e.PatientId);
    entity.HasIndex(e => e.Status);
    entity.HasIndex(e => new { e.Status, e.Position });
    entity.HasIndex(e => new { e.Status, e.ClaimExpiresAt });
    entity.Property(e => e.PreferredAppointmentType).HasMaxLength(64);
});
```

10. **Register services** in DI:

```csharp
// In DependencyInjection.cs
services.AddSingleton(Channel.CreateUnbounded<SlotReleasedMessage>());
services.AddScoped<IWaitlistRepository, WaitlistRepository>();
services.AddScoped<WaitlistService>();
services.AddHostedService<WaitlistMatchingWorker>();
services.AddHostedService<ClaimWindowExpiryWorker>();
```

## Current Project State

```text
propelIQ/
└── server/
    └── src/
        ├── PropelIQ.Api/
        │   └── Controllers/
        │       ├── BookingController.cs           (existing from US_021/US_022)
        │       └── WaitlistController.cs          (new)
        ├── PropelIQ.Application/
        │   ├── Booking/                           (existing from US_021/US_022)
        │   ├── Waitlist/                          (new module)
        │   │   ├── WaitlistService.cs
        │   │   ├── Dto/
        │   │   │   └── WaitlistDto.cs
        │   │   └── Validators/
        │   │       └── JoinWaitlistValidator.cs
        │   └── Abstractions/
        │       ├── IBookingRepository.cs          (existing)
        │       └── IWaitlistRepository.cs         (new)
        ├── PropelIQ.Domain/
        │   ├── Entities/
        │   │   ├── Appointment.cs                 (existing)
        │   │   ├── AppointmentSlot.cs             (existing)
        │   │   └── WaitlistEntry.cs               (new)
        │   ├── Enums/
        │   │   ├── AppointmentStatus.cs           (existing)
        │   │   └── WaitlistStatus.cs              (new)
        │   └── Events/
        │       ├── BookingConfirmedEvent.cs        (existing)
        │       ├── BookingCancelledEvent.cs        (existing)
        │       ├── SlotOfferedEvent.cs             (new)
        │       └── ClaimExpiredEvent.cs            (new)
        └── PropelIQ.Infrastructure/
            ├── Waitlist/                          (new module)
            │   ├── WaitlistRepository.cs
            │   ├── WaitlistMatchingWorker.cs
            │   └── ClaimWindowExpiryWorker.cs
            ├── AppDbContext.cs                    (modify)
            └── DependencyInjection.cs             (modify)
```

> Placeholder: Update on execution based on US_021 and US_022 task completion state.

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | server/src/PropelIQ.Domain/Entities/WaitlistEntry.cs | Waitlist aggregate with preferred slot parameters, offer tracking, FIFO position |
| CREATE | server/src/PropelIQ.Domain/Enums/WaitlistStatus.cs | Enum: Active, Offered, Claimed, Expired, Cancelled |
| CREATE | server/src/PropelIQ.Domain/Events/SlotOfferedEvent.cs | Notification event for matched waitlist patient |
| CREATE | server/src/PropelIQ.Domain/Events/ClaimExpiredEvent.cs | Event for expired claim window notification |
| CREATE | server/src/PropelIQ.Application/Waitlist/Dto/WaitlistDto.cs | Join, entry response, claim request/response DTOs |
| CREATE | server/src/PropelIQ.Application/Waitlist/Validators/JoinWaitlistValidator.cs | FluentValidation for date range, duration, appointment type |
| CREATE | server/src/PropelIQ.Application/Waitlist/WaitlistService.cs | Join, match, claim, expire orchestration with BookingService reuse |
| CREATE | server/src/PropelIQ.Application/Abstractions/IWaitlistRepository.cs | Repository abstraction with FIFO matching and expiry queries |
| CREATE | server/src/PropelIQ.Infrastructure/Waitlist/WaitlistRepository.cs | EF Core implementation with composite indexes |
| CREATE | server/src/PropelIQ.Infrastructure/Waitlist/WaitlistMatchingWorker.cs | Background worker consuming slot-released channel for 5-min matching |
| CREATE | server/src/PropelIQ.Infrastructure/Waitlist/ClaimWindowExpiryWorker.cs | 1-min interval worker expiring 2-hour claim windows and rotating |
| CREATE | server/src/PropelIQ.Api/Controllers/WaitlistController.cs | POST join, GET entries, POST claim, DELETE cancel endpoints |
| MODIFY | server/src/PropelIQ.Infrastructure/AppDbContext.cs | Add WaitlistEntry DbSet with status+position and status+expiry indexes |
| MODIFY | server/src/PropelIQ.Infrastructure/DependencyInjection.cs | Register waitlist repo, service, matching worker, expiry worker |

## External References

- EF Core Background Services: https://learn.microsoft.com/en-us/aspnet/core/fundamentals/host/hosted-services
- System.Threading.Channels: https://learn.microsoft.com/en-us/dotnet/core/extensions/channels
- EF Core Composite Indexes: https://learn.microsoft.com/en-us/ef/core/modeling/indexes

## Build Commands

```bash
# Build backend
cd server/src/PropelIQ.Api
dotnet build

# Run backend
dotnet run

# Test join waitlist (AC-1)
curl -X POST "http://localhost:5000/api/v1/waitlist" \
  -H "Authorization: Bearer <jwt>" \
  -H "Content-Type: application/json" \
  -d '{"preferredDateStart":"2026-04-20","preferredDateEnd":"2026-04-25","preferredDurationMinutes":30,"preferredAppointmentType":"General"}'
# Expected: 201 Created with waitlist entry

# Test claim offered slot (AC-3)
curl -X POST "http://localhost:5000/api/v1/waitlist/<entry-guid>/claim" \
  -H "Authorization: Bearer <jwt>"
# Expected: 200 OK with booking confirmation

# Test get waitlist entries
curl -X GET "http://localhost:5000/api/v1/waitlist" \
  -H "Authorization: Bearer <jwt>"
# Expected: 200 OK with list of active/offered entries
```

## Implementation Validation Strategy

- [ ] `POST /api/v1/waitlist` persists preferred slot parameters with FIFO position (AC-1)
- [ ] `WaitlistMatchingWorker` matches released slots to first eligible patient within 5 minutes (AC-2)
- [ ] Matching uses FIFO ordering by position then creation time
- [ ] `SlotOfferedEvent` dispatched with 2-hour claim expiry timestamp (AC-2)
- [ ] `POST /api/v1/waitlist/{id}/claim` atomically reserves offered slot via `BookingService` (AC-3)
- [ ] Successful claim dispatches standard `BookingConfirmedEvent` for PDF/QR/ICS artifacts (AC-3)
- [ ] `ClaimWindowExpiryWorker` expires unclaimed offers after 2 hours (AC-4)
- [ ] Expired entries rotate the slot to the next eligible patient (AC-4)
- [ ] Concurrent claim attempts: first succeeds, second returns error and patient stays on waitlist (edge case)

## Implementation Checklist

- [ ] Create `WaitlistEntry` entity with preferred slot parameters and offer tracking
- [ ] Create `WaitlistStatus` enum (Active, Offered, Claimed, Expired, Cancelled)
- [ ] Create `WaitlistService` with join, match, claim, and expire-rotate methods
- [ ] Create `WaitlistMatchingWorker` consuming slot-released channel for 5-min matching SLA
- [ ] Create `ClaimWindowExpiryWorker` with 1-min polling interval for 2-hour expiry
- [ ] Create `WaitlistController` with POST join, GET entries, POST claim endpoints
- [ ] Add `WaitlistEntry` DbSet with composite indexes for status+position and status+expiry
- [ ] Reuse `BookingService.CreateBookingAsync` for atomic claim reservation
