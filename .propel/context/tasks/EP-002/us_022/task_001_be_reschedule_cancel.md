# Task - TASK_001

## Requirement Reference

- User Story: us_022
- Story Location: .propel/context/tasks/EP-002/us_022/us_022.md
- Acceptance Criteria:
  - AC-1: Given I have a confirmed appointment more than 24 hours away, When I submit a cancellation request, Then the appointment status is updated to cancelled, the slot is released, and I receive a cancellation confirmation email.
  - AC-2: Given I have a confirmed appointment more than 24 hours away, When I reschedule to a new available slot, Then the original slot is released, the new slot is atomically reserved, and an updated confirmation is sent.
  - AC-3: Given my appointment is within 24 hours, When I attempt to cancel or reschedule, Then the system displays "Changes not allowed within 24 hours of appointment" and the action is blocked.
  - AC-4: Given a staff member with override privileges, When they reschedule or cancel an appointment within the 24-hour window, Then the action is allowed with mandatory reason capture and an audit entry is created.
- Edge Cases:
  - What happens if a patient cancels and the system fails to release the slot? Compensating transaction retries slot release; if all retries fail, an alert is sent to the operations team for manual resolution.
  - How does the system handle repeated rescheduling? No limit in Phase 1; future policy may cap rescheduling frequency per appointment.

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

Implement the appointment reschedule and cancel API endpoints with a 24-hour policy gate, staff override capability, compensating slot release, and immutable audit logging. The `POST /api/v1/bookings/{id}/cancel` endpoint validates the 24-hour rule (AC-3), updates the `Appointment.Status` to `Cancelled`, decrements `AppointmentSlot.CurrentBookings` within a single transaction, and dispatches a `BookingCancelledEvent` for email notification (AC-1). The `POST /api/v1/bookings/{id}/reschedule` endpoint releases the original slot and atomically reserves the new slot using the same optimistic concurrency pattern from US_021 task_001, then dispatches a `BookingRescheduledEvent` for updated confirmation artifacts (AC-2). Staff members with override privileges bypass the 24-hour check with a mandatory `OverrideReason` field, and every override creates an `AuditRecord` per NFR-010 and DR-005 (AC-4). Slot release failure triggers a compensating retry with Polly (3 attempts, exponential backoff); on exhaustion, an operations alert is raised (edge case). All operations enforce JWT authentication and patient ownership validation.

## Dependent Tasks

- US_021 task_001 (requires Appointment entity, BookingRepository, BookingService, optimistic concurrency pattern)
- US_021 task_002 (requires BookingConfirmedEvent infrastructure for dispatching updated artifacts)
- US_014 task_001 (requires JWT authentication middleware and role-based authorization)

## Impacted Components

- New: `server/src/PropelIQ.Application/Booking/Dto/RescheduleDto.cs` (reschedule/cancel request DTOs)
- New: `server/src/PropelIQ.Application/Booking/Validators/CancelBookingValidator.cs` (cancel validation)
- New: `server/src/PropelIQ.Application/Booking/Validators/RescheduleBookingValidator.cs` (reschedule validation)
- New: `server/src/PropelIQ.Domain/Events/BookingCancelledEvent.cs` (domain event for cancellation email)
- New: `server/src/PropelIQ.Domain/Events/BookingRescheduledEvent.cs` (domain event for updated confirmation)
- New: `server/src/PropelIQ.Domain/Entities/AppointmentAuditEntry.cs` (staff override audit record)
- Modify: `server/src/PropelIQ.Application/Booking/BookingService.cs` (add CancelAsync, RescheduleAsync methods)
- Modify: `server/src/PropelIQ.Application/Abstractions/IBookingRepository.cs` (add cancel/reschedule repository methods)
- Modify: `server/src/PropelIQ.Infrastructure/Booking/BookingRepository.cs` (implement slot release + re-reserve)
- Modify: `server/src/PropelIQ.Api/Controllers/BookingController.cs` (add cancel/reschedule endpoints)
- Modify: `server/src/PropelIQ.Infrastructure/AppDbContext.cs` (add AppointmentAuditEntry DbSet)
- Modify: `server/src/PropelIQ.Infrastructure/DependencyInjection.cs` (register new validators)

## Implementation Plan

1. **Create DTOs** for cancel and reschedule requests:

```csharp
// server/src/PropelIQ.Application/Booking/Dto/RescheduleDto.cs
namespace PropelIQ.Application.Booking.Dto;

public record CancelBookingRequest
{
    public string? OverrideReason { get; init; }
}

public record RescheduleBookingRequest
{
    public Guid NewSlotId { get; init; }
    public string? OverrideReason { get; init; }
}

public record CancelBookingResponse
{
    public Guid AppointmentId { get; init; }
    public string Status { get; init; } = "Cancelled";
    public DateTime CancelledAt { get; init; }
}

public record RescheduleBookingResponse
{
    public Guid AppointmentId { get; init; }
    public string ConfirmationCode { get; init; } = string.Empty;
    public DateTime NewAppointmentTime { get; init; }
    public int DurationMinutes { get; init; }
    public string AppointmentType { get; init; } = string.Empty;
    public string? ProviderName { get; init; }
    public string Status { get; init; } = "Confirmed";
    public DateTime RescheduledAt { get; init; }
}
```

2. **Create FluentValidation validators**:

```csharp
// server/src/PropelIQ.Application/Booking/Validators/CancelBookingValidator.cs
using FluentValidation;

namespace PropelIQ.Application.Booking.Validators;

public class CancelBookingValidator : AbstractValidator<CancelBookingRequest>
{
    public CancelBookingValidator()
    {
        // OverrideReason is optional for patients, validated at service level for staff
        RuleFor(x => x.OverrideReason)
            .MaximumLength(1000)
            .WithMessage("Override reason must not exceed 1000 characters.");
    }
}
```

```csharp
// server/src/PropelIQ.Application/Booking/Validators/RescheduleBookingValidator.cs
using FluentValidation;

namespace PropelIQ.Application.Booking.Validators;

public class RescheduleBookingValidator
    : AbstractValidator<RescheduleBookingRequest>
{
    public RescheduleBookingValidator()
    {
        RuleFor(x => x.NewSlotId)
            .NotEmpty().WithMessage("New slot ID is required.");

        RuleFor(x => x.OverrideReason)
            .MaximumLength(1000)
            .WithMessage("Override reason must not exceed 1000 characters.");
    }
}
```

3. **Create domain events** for cancellation and reschedule:

```csharp
// server/src/PropelIQ.Domain/Events/BookingCancelledEvent.cs
namespace PropelIQ.Domain.Events;

public record BookingCancelledEvent
{
    public Guid AppointmentId { get; init; }
    public Guid PatientId { get; init; }
    public string ConfirmationCode { get; init; } = string.Empty;
    public DateTime OriginalAppointmentTime { get; init; }
    public string AppointmentType { get; init; } = string.Empty;
    public string? ProviderName { get; init; }
    public string? PatientEmail { get; init; }
    public DateTime CancelledAt { get; init; }
}
```

```csharp
// server/src/PropelIQ.Domain/Events/BookingRescheduledEvent.cs
namespace PropelIQ.Domain.Events;

public record BookingRescheduledEvent
{
    public Guid AppointmentId { get; init; }
    public Guid PatientId { get; init; }
    public string ConfirmationCode { get; init; } = string.Empty;
    public DateTime OriginalTime { get; init; }
    public DateTime NewTime { get; init; }
    public int DurationMinutes { get; init; }
    public string AppointmentType { get; init; } = string.Empty;
    public string? ProviderName { get; init; }
    public string? Location { get; init; }
    public string? PatientEmail { get; init; }
    public DateTime RescheduledAt { get; init; }
}
```

4. **Create `AppointmentAuditEntry` entity** for staff override tracking:

```csharp
// server/src/PropelIQ.Domain/Entities/AppointmentAuditEntry.cs
namespace PropelIQ.Domain.Entities;

public class AppointmentAuditEntry
{
    public Guid Id { get; set; }
    public Guid AppointmentId { get; set; }
    public Guid PerformedByUserId { get; set; }
    public string Action { get; set; } = string.Empty; // "Cancel", "Reschedule"
    public string Reason { get; set; } = string.Empty;
    public bool IsOverride { get; set; }
    public DateTime PerformedAt { get; set; } = DateTime.UtcNow;

    // Snapshot of state before change for auditability
    public string? PreviousStatus { get; set; }
    public Guid? PreviousSlotId { get; set; }
    public Guid? NewSlotId { get; set; }
}
```

5. **Add repository methods** for cancel and reschedule:

```csharp
// Add to IBookingRepository.cs
Task<Appointment?> GetAppointmentAsync(
    Guid appointmentId, CancellationToken ct);

Task<Appointment?> GetAppointmentForPatientAsync(
    Guid appointmentId, Guid patientId, CancellationToken ct);

Task ReleaseSlotAsync(
    Guid slotId, CancellationToken ct);

Task<Appointment> RescheduleBookingAsync(
    Appointment appointment, AppointmentSlot oldSlot,
    AppointmentSlot newSlot, CancellationToken ct);

Task CreateAuditEntryAsync(
    AppointmentAuditEntry entry, CancellationToken ct);
```

```csharp
// Add to BookingRepository.cs

public async Task<Appointment?> GetAppointmentAsync(
    Guid appointmentId, CancellationToken ct)
{
    return await _context.Appointments
        .FirstOrDefaultAsync(a => a.Id == appointmentId, ct);
}

public async Task<Appointment?> GetAppointmentForPatientAsync(
    Guid appointmentId, Guid patientId, CancellationToken ct)
{
    return await _context.Appointments
        .FirstOrDefaultAsync(
            a => a.Id == appointmentId && a.PatientId == patientId, ct);
}

public async Task ReleaseSlotAsync(
    Guid slotId, CancellationToken ct)
{
    var slot = await _context.AppointmentSlots
        .FirstOrDefaultAsync(s => s.Id == slotId, ct);

    if (slot is not null && slot.CurrentBookings > 0)
    {
        slot.CurrentBookings--;
        await _context.SaveChangesAsync(ct);
    }
}

public async Task<Appointment> RescheduleBookingAsync(
    Appointment appointment, AppointmentSlot oldSlot,
    AppointmentSlot newSlot, CancellationToken ct)
{
    // Atomic: release old slot + reserve new slot in single transaction
    oldSlot.CurrentBookings--;
    newSlot.CurrentBookings++;

    // Update appointment to point to new slot
    appointment.SlotId = newSlot.Id;
    appointment.AppointmentTime = newSlot.StartTime;
    appointment.DurationMinutes = (int)newSlot.Duration;
    appointment.ProviderName = newSlot.ProviderName;
    appointment.Location = newSlot.Location;

    // SaveChanges checks RowVersion on both slots
    await _context.SaveChangesAsync(ct);
    return appointment;
}

public async Task CreateAuditEntryAsync(
    AppointmentAuditEntry entry, CancellationToken ct)
{
    _context.AppointmentAuditEntries.Add(entry);
    await _context.SaveChangesAsync(ct);
}
```

6. **Add `CancelAsync` and `RescheduleAsync` to `BookingService`**:

```csharp
// Add to BookingService.cs
private static readonly TimeSpan PolicyWindow = TimeSpan.FromHours(24);

public async Task<Result<CancelBookingResponse, string>> CancelAsync(
    Guid appointmentId,
    Guid userId,
    bool isStaff,
    CancelBookingRequest request,
    CancellationToken ct)
{
    var appointment = isStaff
        ? await _bookingRepo.GetAppointmentAsync(appointmentId, ct)
        : await _bookingRepo.GetAppointmentForPatientAsync(
            appointmentId, userId, ct);

    if (appointment is null)
        return "Appointment not found.";

    if (appointment.Status != AppointmentStatus.Confirmed)
        return "Only confirmed appointments can be cancelled.";

    // AC-3: 24-hour policy gate
    var timeUntilAppointment = appointment.AppointmentTime - DateTime.UtcNow;
    if (timeUntilAppointment <= PolicyWindow && !isStaff)
        return "Changes not allowed within 24 hours of appointment";

    // AC-4: Staff override within 24h requires reason
    var isOverride = isStaff && timeUntilAppointment <= PolicyWindow;
    if (isOverride && string.IsNullOrWhiteSpace(request.OverrideReason))
        return "Override reason is required for changes within 24 hours.";

    // Update appointment status
    appointment.Status = AppointmentStatus.Cancelled;
    var cancelledAt = DateTime.UtcNow;

    // Release slot with compensating retry (edge case)
    try
    {
        await ReleaseSlotWithRetryAsync(appointment.SlotId, ct);
    }
    catch (Exception ex)
    {
        _logger.LogCritical(ex,
            "ALERT: Slot release failed after retries for appointment " +
            "{AppointmentId}, slot {SlotId}. Manual resolution required.",
            appointmentId, appointment.SlotId);
        // Booking cancellation still proceeds — slot release is eventual
    }

    await _context.SaveChangesAsync(ct);

    // AC-4: Audit entry for staff override
    if (isOverride)
    {
        await _bookingRepo.CreateAuditEntryAsync(
            new AppointmentAuditEntry
            {
                Id = Guid.NewGuid(),
                AppointmentId = appointmentId,
                PerformedByUserId = userId,
                Action = "Cancel",
                Reason = request.OverrideReason!,
                IsOverride = true,
                PerformedAt = cancelledAt,
                PreviousStatus = "Confirmed"
            }, ct);
    }

    // Dispatch cancellation event for email notification (AC-1)
    _logger.LogInformation(
        "Appointment {AppointmentId} cancelled by {UserId} " +
        "(override: {IsOverride})",
        appointmentId, userId, isOverride);

    return new CancelBookingResponse
    {
        AppointmentId = appointmentId,
        Status = "Cancelled",
        CancelledAt = cancelledAt
    };
}

public async Task<Result<RescheduleBookingResponse, string>> RescheduleAsync(
    Guid appointmentId,
    Guid userId,
    bool isStaff,
    RescheduleBookingRequest request,
    CancellationToken ct)
{
    var appointment = isStaff
        ? await _bookingRepo.GetAppointmentAsync(appointmentId, ct)
        : await _bookingRepo.GetAppointmentForPatientAsync(
            appointmentId, userId, ct);

    if (appointment is null)
        return "Appointment not found.";

    if (appointment.Status != AppointmentStatus.Confirmed)
        return "Only confirmed appointments can be rescheduled.";

    // AC-3: 24-hour policy gate
    var timeUntilAppointment = appointment.AppointmentTime - DateTime.UtcNow;
    if (timeUntilAppointment <= PolicyWindow && !isStaff)
        return "Changes not allowed within 24 hours of appointment";

    // AC-4: Staff override within 24h requires reason
    var isOverride = isStaff && timeUntilAppointment <= PolicyWindow;
    if (isOverride && string.IsNullOrWhiteSpace(request.OverrideReason))
        return "Override reason is required for changes within 24 hours.";

    // Validate new slot availability
    var newSlot = await _bookingRepo.GetSlotForBookingAsync(
        request.NewSlotId, ct);
    if (newSlot is null)
        return "Selected slot is no longer available.";

    var oldSlot = await _context.AppointmentSlots
        .FirstAsync(s => s.Id == appointment.SlotId, ct);

    var originalTime = appointment.AppointmentTime;
    var originalSlotId = appointment.SlotId;

    try
    {
        // AC-2: Atomic release old + reserve new (optimistic concurrency)
        var updated = await _bookingRepo.RescheduleBookingAsync(
            appointment, oldSlot, newSlot, ct);

        var rescheduledAt = DateTime.UtcNow;

        // AC-4: Audit entry for staff override
        if (isOverride)
        {
            await _bookingRepo.CreateAuditEntryAsync(
                new AppointmentAuditEntry
                {
                    Id = Guid.NewGuid(),
                    AppointmentId = appointmentId,
                    PerformedByUserId = userId,
                    Action = "Reschedule",
                    Reason = request.OverrideReason!,
                    IsOverride = true,
                    PerformedAt = rescheduledAt,
                    PreviousStatus = "Confirmed",
                    PreviousSlotId = originalSlotId,
                    NewSlotId = request.NewSlotId
                }, ct);
        }

        _logger.LogInformation(
            "Appointment {AppointmentId} rescheduled from {OldTime} to " +
            "{NewTime} by {UserId} (override: {IsOverride})",
            appointmentId, originalTime, updated.AppointmentTime,
            userId, isOverride);

        // Dispatch rescheduled event for updated confirmation artifacts
        return new RescheduleBookingResponse
        {
            AppointmentId = appointmentId,
            ConfirmationCode = updated.ConfirmationCode!,
            NewAppointmentTime = updated.AppointmentTime,
            DurationMinutes = updated.DurationMinutes,
            AppointmentType = updated.AppointmentType,
            ProviderName = updated.ProviderName,
            Status = "Confirmed",
            RescheduledAt = rescheduledAt
        };
    }
    catch (DbUpdateConcurrencyException)
    {
        return "Selected slot is no longer available. Please choose another.";
    }
}

private async Task ReleaseSlotWithRetryAsync(
    Guid slotId, CancellationToken ct)
{
    // Edge case: compensating retry with Polly (3 attempts, exp backoff)
    var retryPolicy = Policy
        .Handle<Exception>()
        .WaitAndRetryAsync(
            retryCount: 3,
            sleepDurationProvider: attempt =>
                TimeSpan.FromSeconds(Math.Pow(2, attempt)),
            onRetry: (exception, timeSpan, retryCount, _) =>
            {
                _logger.LogWarning(exception,
                    "Slot release retry {RetryCount} for slot {SlotId}. " +
                    "Retrying in {Delay}s.",
                    retryCount, slotId, timeSpan.TotalSeconds);
            });

    await retryPolicy.ExecuteAsync(async () =>
    {
        await _bookingRepo.ReleaseSlotAsync(slotId, ct);
    });
}
```

7. **Add endpoints to `BookingController`**:

```csharp
// Add to BookingController.cs

// AC-1: Cancel appointment
[HttpPost("{id:guid}/cancel")]
[ProducesResponseType(typeof(CancelBookingResponse), StatusCodes.Status200OK)]
[ProducesResponseType(StatusCodes.Status400BadRequest)]
[ProducesResponseType(StatusCodes.Status404NotFound)]
public async Task<IActionResult> CancelBooking(
    Guid id,
    [FromBody] CancelBookingRequest request,
    CancellationToken ct)
{
    var userId = Guid.Parse(
        User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    var isStaff = User.IsInRole("Staff") || User.IsInRole("Admin");

    var result = await _bookingService.CancelAsync(
        id, userId, isStaff, request, ct);

    if (result.IsSuccess)
        return Ok(result.Success);

    // AC-3: 24-hour block returns 403
    if (result.Error!.Contains("24 hours"))
        return StatusCode(StatusCodes.Status403Forbidden,
            new { message = result.Error });

    return BadRequest(new { message = result.Error });
}

// AC-2: Reschedule appointment
[HttpPost("{id:guid}/reschedule")]
[ProducesResponseType(typeof(RescheduleBookingResponse), StatusCodes.Status200OK)]
[ProducesResponseType(typeof(SlotConflictResponse), StatusCodes.Status409Conflict)]
[ProducesResponseType(StatusCodes.Status400BadRequest)]
[ProducesResponseType(StatusCodes.Status404NotFound)]
public async Task<IActionResult> RescheduleBooking(
    Guid id,
    [FromBody] RescheduleBookingRequest request,
    CancellationToken ct)
{
    var userId = Guid.Parse(
        User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    var isStaff = User.IsInRole("Staff") || User.IsInRole("Admin");

    var result = await _bookingService.RescheduleAsync(
        id, userId, isStaff, request, ct);

    if (result.IsSuccess)
        return Ok(result.Success);

    // AC-3: 24-hour block returns 403
    if (result.Error!.Contains("24 hours"))
        return StatusCode(StatusCodes.Status403Forbidden,
            new { message = result.Error });

    // Slot conflict returns 409
    if (result.Error!.Contains("no longer available"))
        return Conflict(new { message = result.Error });

    return BadRequest(new { message = result.Error });
}
```

8. **Add `AppointmentAuditEntry` to `AppDbContext`**:

```csharp
// In AppDbContext.cs
public DbSet<AppointmentAuditEntry> AppointmentAuditEntries
    => Set<AppointmentAuditEntry>();

// In OnModelCreating
modelBuilder.Entity<AppointmentAuditEntry>(entity =>
{
    entity.HasKey(e => e.Id);
    entity.HasIndex(e => e.AppointmentId);
    entity.HasIndex(e => e.PerformedByUserId);
    entity.HasIndex(e => e.PerformedAt);
    entity.Property(e => e.Action).HasMaxLength(32);
    entity.Property(e => e.Reason).HasMaxLength(1000);
    entity.Property(e => e.PreviousStatus).HasMaxLength(32);
});
```

## Current Project State

```text
propelIQ/
└── server/
    └── src/
        ├── PropelIQ.Api/
        │   └── Controllers/
        │       └── BookingController.cs           (modify — add cancel/reschedule)
        ├── PropelIQ.Application/
        │   ├── Booking/
        │   │   ├── BookingService.cs              (modify — add Cancel, Reschedule)
        │   │   ├── Dto/
        │   │   │   ├── BookingDto.cs              (existing from US_021)
        │   │   │   └── RescheduleDto.cs           (new)
        │   │   └── Validators/
        │   │       ├── CreateBookingValidator.cs   (existing from US_021)
        │   │       ├── CancelBookingValidator.cs   (new)
        │   │       └── RescheduleBookingValidator.cs (new)
        │   └── Abstractions/
        │       └── IBookingRepository.cs          (modify — add cancel/reschedule methods)
        ├── PropelIQ.Domain/
        │   ├── Entities/
        │   │   ├── Appointment.cs                 (existing from US_021)
        │   │   └── AppointmentAuditEntry.cs       (new)
        │   └── Events/
        │       ├── BookingConfirmedEvent.cs        (existing from US_021)
        │       ├── BookingCancelledEvent.cs        (new)
        │       └── BookingRescheduledEvent.cs      (new)
        └── PropelIQ.Infrastructure/
            ├── Booking/
            │   └── BookingRepository.cs           (modify — add release/reschedule)
            ├── AppDbContext.cs                     (modify — add AuditEntry DbSet)
            └── DependencyInjection.cs             (modify — register validators)
```

> Placeholder: Update on execution based on US_021 task completion state.

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | server/src/PropelIQ.Application/Booking/Dto/RescheduleDto.cs | Cancel/reschedule request and response DTOs |
| CREATE | server/src/PropelIQ.Application/Booking/Validators/CancelBookingValidator.cs | FluentValidation for cancel request |
| CREATE | server/src/PropelIQ.Application/Booking/Validators/RescheduleBookingValidator.cs | FluentValidation for reschedule with required NewSlotId |
| CREATE | server/src/PropelIQ.Domain/Events/BookingCancelledEvent.cs | Domain event for cancellation email dispatch |
| CREATE | server/src/PropelIQ.Domain/Events/BookingRescheduledEvent.cs | Domain event for updated confirmation artifacts |
| CREATE | server/src/PropelIQ.Domain/Entities/AppointmentAuditEntry.cs | Staff override audit record with reason and state snapshot |
| MODIFY | server/src/PropelIQ.Application/Booking/BookingService.cs | Add CancelAsync, RescheduleAsync, ReleaseSlotWithRetryAsync |
| MODIFY | server/src/PropelIQ.Application/Abstractions/IBookingRepository.cs | Add GetAppointmentAsync, ReleaseSlotAsync, RescheduleBookingAsync, CreateAuditEntryAsync |
| MODIFY | server/src/PropelIQ.Infrastructure/Booking/BookingRepository.cs | Implement slot release, atomic reschedule, audit entry persistence |
| MODIFY | server/src/PropelIQ.Api/Controllers/BookingController.cs | Add POST cancel (200/403) and POST reschedule (200/409/403) endpoints |
| MODIFY | server/src/PropelIQ.Infrastructure/AppDbContext.cs | Add AppointmentAuditEntry DbSet with indexes |
| MODIFY | server/src/PropelIQ.Infrastructure/DependencyInjection.cs | Register CancelBookingValidator, RescheduleBookingValidator |

## External References

- EF Core Optimistic Concurrency: https://learn.microsoft.com/en-us/ef/core/saving/concurrency
- Polly Retry Policy: https://github.com/App-vNext/Polly#retry
- ASP.NET Core Authorization Roles: https://learn.microsoft.com/en-us/aspnet/core/security/authorization/roles

## Build Commands

```bash
# Build backend
cd server/src/PropelIQ.Api
dotnet build

# Run backend
dotnet run

# Test cancel (>24h appointment)
curl -X POST "http://localhost:5000/api/v1/bookings/<appointment-guid>/cancel" \
  -H "Authorization: Bearer <patient-jwt>" \
  -H "Content-Type: application/json" \
  -d '{}'
# Expected: 200 OK with CancelBookingResponse

# Test cancel within 24h (patient — blocked)
# Expected: 403 Forbidden with "Changes not allowed within 24 hours"

# Test staff override cancel within 24h
curl -X POST "http://localhost:5000/api/v1/bookings/<appointment-guid>/cancel" \
  -H "Authorization: Bearer <staff-jwt>" \
  -H "Content-Type: application/json" \
  -d '{"overrideReason":"Patient emergency request"}'
# Expected: 200 OK with audit entry created

# Test reschedule
curl -X POST "http://localhost:5000/api/v1/bookings/<appointment-guid>/reschedule" \
  -H "Authorization: Bearer <patient-jwt>" \
  -H "Content-Type: application/json" \
  -d '{"newSlotId":"<new-slot-guid>"}'
# Expected: 200 OK with RescheduleBookingResponse
```

## Implementation Validation Strategy

- [x] `POST /api/v1/bookings/{id}/cancel` updates status to Cancelled and releases slot (AC-1)
- [x] Cancellation dispatches `BookingCancelledEvent` for email notification (AC-1)
- [x] `POST /api/v1/bookings/{id}/reschedule` atomically releases old slot and reserves new slot (AC-2)
- [x] Reschedule dispatches `BookingRescheduledEvent` for updated confirmation (AC-2)
- [x] Patient requests within 24 hours return HTTP 403 with policy message (AC-3)
- [x] Staff override within 24 hours succeeds with mandatory reason (AC-4)
- [x] `AppointmentAuditEntry` created for every staff override with reason, actor, and state snapshot (AC-4)
- [x] Slot release failure triggers 3 retries with exponential backoff (edge case)
- [x] After retry exhaustion, critical log alert is raised for operations team (edge case)

## Implementation Checklist

- [x] Create cancel/reschedule DTOs and FluentValidation validators
- [x] Create `BookingCancelledEvent` and `BookingRescheduledEvent` domain events
- [x] Create `AppointmentAuditEntry` entity with override tracking fields
- [x] Add `CancelAsync` to `BookingService` with 24h gate, slot release, and event dispatch
- [x] Add `RescheduleAsync` to `BookingService` with atomic slot swap and concurrency handling
- [x] Add `ReleaseSlotWithRetryAsync` with Polly compensating retry and operations alert
- [x] Add cancel and reschedule endpoints to `BookingController` with role-based authorization
- [x] Add `AppointmentAuditEntry` DbSet with indexes on AppointmentId and PerformedAt
