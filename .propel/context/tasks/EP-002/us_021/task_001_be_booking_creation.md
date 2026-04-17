# Task - TASK_001

## Requirement Reference

- User Story: us_021
- Story Location: .propel/context/tasks/EP-002/us_021/us_021.md
- Acceptance Criteria:
  - AC-1: Given I confirm my appointment selection with completed intake, When the booking request is submitted, Then the slot is atomically reserved, the appointment record is persisted, and I receive a confirmation within 1 minute.
  - AC-4: Given two patients attempt to book the same slot simultaneously, When the concurrent requests are processed, Then only one booking succeeds using optimistic concurrency control; the second patient receives "Slot no longer available" and is offered the next available slot.
- Edge Cases:
  - What happens if the confirmation email fails to send? Booking is still persisted; email delivery is retried up to 3 times with exponential backoff; failure is logged and patient can access confirmation from their dashboard.

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
| Library | MediatR | latest stable |
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

Implement the booking creation API with atomic slot reservation using optimistic concurrency control (AC-1, AC-4). The `POST /api/v1/bookings` endpoint receives a slot ID and intake record ID, validates the slot is still available, atomically reserves it within a database transaction using the `AppointmentSlot.RowVersion` concurrency token (from US_019 task_001), creates an `Appointment` record with status `Confirmed`, links the `IntakeRecord`, and dispatches a `BookingConfirmedEvent` for asynchronous artifact generation and notification. When a concurrent booking race occurs (AC-4), the `DbUpdateConcurrencyException` is caught and the patient receives HTTP 409 with "Slot no longer available" and a suggestion of the next available slot. The booking must complete within the 500 ms API p95 target (NFR-002). The email/SMS notification is dispatched asynchronously — booking persists even if notification dispatch fails (edge case). All booking operations are audited per NFR-010 and enforce referential integrity per DR-002.

## Dependent Tasks

- US_019 task_001 (requires AppointmentSlot entity with RowVersion concurrency token)
- US_020 task_001 (requires IntakeRecord entity linked to appointment)
- US_014 task_001 (requires JWT authentication middleware)

## Impacted Components

- New: `server/src/PropelIQ.Domain/Entities/Appointment.cs` (appointment aggregate with slot, patient, intake, status)
- New: `server/src/PropelIQ.Domain/Enums/AppointmentStatus.cs` (enum: Confirmed, Cancelled, Completed, NoShow)
- New: `server/src/PropelIQ.Domain/Events/BookingConfirmedEvent.cs` (domain event for async processing)
- New: `server/src/PropelIQ.Application/Booking/BookingService.cs` (atomic reserve + persist orchestration)
- New: `server/src/PropelIQ.Application/Booking/Dto/BookingDto.cs` (request/response DTOs)
- New: `server/src/PropelIQ.Application/Booking/Validators/CreateBookingValidator.cs` (FluentValidation)
- New: `server/src/PropelIQ.Application/Abstractions/IBookingRepository.cs` (repository abstraction)
- New: `server/src/PropelIQ.Infrastructure/Booking/BookingRepository.cs` (EF Core with transaction)
- New: `server/src/PropelIQ.Api/Controllers/BookingController.cs` (booking endpoint)
- Modify: `server/src/PropelIQ.Infrastructure/AppDbContext.cs` (add Appointment DbSet, entity config)
- Modify: `server/src/PropelIQ.Infrastructure/DependencyInjection.cs` (register booking services)

## Implementation Plan

1. **Create domain entities** for appointment and booking events:

```csharp
// server/src/PropelIQ.Domain/Enums/AppointmentStatus.cs
namespace PropelIQ.Domain.Enums;

public enum AppointmentStatus
{
    Confirmed,
    Cancelled,
    Completed,
    NoShow
}
```

```csharp
// server/src/PropelIQ.Domain/Entities/Appointment.cs
namespace PropelIQ.Domain.Entities;

public class Appointment
{
    public Guid Id { get; set; }
    public Guid PatientId { get; set; }
    public Guid SlotId { get; set; }
    public Guid? IntakeRecordId { get; set; }
    public AppointmentStatus Status { get; set; } = AppointmentStatus.Confirmed;
    public DateTime BookedAt { get; set; } = DateTime.UtcNow;

    // Denormalized from slot for query convenience
    public DateTime AppointmentTime { get; set; }
    public int DurationMinutes { get; set; }
    public string AppointmentType { get; set; } = string.Empty;
    public string? ProviderName { get; set; }
    public string? Location { get; set; }

    // Confirmation artifact references
    public string? ConfirmationCode { get; set; }
    public bool ArtifactsGenerated { get; set; } = false;
}
```

```csharp
// server/src/PropelIQ.Domain/Events/BookingConfirmedEvent.cs
namespace PropelIQ.Domain.Events;

public record BookingConfirmedEvent
{
    public Guid AppointmentId { get; init; }
    public Guid PatientId { get; init; }
    public string ConfirmationCode { get; init; } = string.Empty;
    public DateTime AppointmentTime { get; init; }
    public int DurationMinutes { get; init; }
    public string AppointmentType { get; init; } = string.Empty;
    public string? ProviderName { get; init; }
    public string? Location { get; init; }
    public string? PatientEmail { get; init; }
    public string? PatientPhone { get; init; }
}
```

2. **Create booking DTOs** and validator:

```csharp
// server/src/PropelIQ.Application/Booking/Dto/BookingDto.cs
namespace PropelIQ.Application.Booking.Dto;

public record CreateBookingRequest
{
    public Guid SlotId { get; init; }
    public Guid IntakeRecordId { get; init; }
}

public record BookingResponse
{
    public Guid AppointmentId { get; init; }
    public string ConfirmationCode { get; init; } = string.Empty;
    public DateTime AppointmentTime { get; init; }
    public int DurationMinutes { get; init; }
    public string AppointmentType { get; init; } = string.Empty;
    public string? ProviderName { get; init; }
    public string? Location { get; init; }
    public string Status { get; init; } = string.Empty;
    public DateTime BookedAt { get; init; }
}

public record SlotConflictResponse
{
    public string Message { get; init; } = "Slot no longer available";
    public Guid? NextAvailableSlotId { get; init; }
    public DateTime? NextAvailableTime { get; init; }
}
```

```csharp
// server/src/PropelIQ.Application/Booking/Validators/CreateBookingValidator.cs
using FluentValidation;

namespace PropelIQ.Application.Booking.Validators;

public class CreateBookingValidator : AbstractValidator<CreateBookingRequest>
{
    public CreateBookingValidator()
    {
        RuleFor(x => x.SlotId)
            .NotEmpty().WithMessage("Slot ID is required.");

        RuleFor(x => x.IntakeRecordId)
            .NotEmpty().WithMessage("Intake record ID is required.");
    }
}
```

3. **Create repository abstraction and implementation** with optimistic concurrency:

```csharp
// server/src/PropelIQ.Application/Abstractions/IBookingRepository.cs
namespace PropelIQ.Application.Abstractions;

public interface IBookingRepository
{
    Task<AppointmentSlot?> GetSlotForBookingAsync(
        Guid slotId, CancellationToken ct);

    Task<AppointmentSlot?> GetNextAvailableSlotAsync(
        DateTime afterTime, AppointmentType? type, CancellationToken ct);

    Task<Appointment> CreateBookingAsync(
        Appointment appointment, AppointmentSlot slot, CancellationToken ct);
}
```

```csharp
// server/src/PropelIQ.Infrastructure/Booking/BookingRepository.cs
using Microsoft.EntityFrameworkCore;

namespace PropelIQ.Infrastructure.Booking;

public class BookingRepository : IBookingRepository
{
    private readonly AppDbContext _context;

    public BookingRepository(AppDbContext context)
        => _context = context;

    public async Task<AppointmentSlot?> GetSlotForBookingAsync(
        Guid slotId, CancellationToken ct)
    {
        return await _context.AppointmentSlots
            .FirstOrDefaultAsync(
                s => s.Id == slotId
                  && s.StartTime > DateTime.UtcNow
                  && s.CurrentBookings < s.MaxCapacity, ct);
    }

    public async Task<AppointmentSlot?> GetNextAvailableSlotAsync(
        DateTime afterTime, AppointmentType? type, CancellationToken ct)
    {
        var query = _context.AppointmentSlots
            .Where(s => s.StartTime > afterTime
                     && s.CurrentBookings < s.MaxCapacity);

        if (type.HasValue)
            query = query.Where(s => s.Type == type.Value);

        return await query
            .OrderBy(s => s.StartTime)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<Appointment> CreateBookingAsync(
        Appointment appointment, AppointmentSlot slot, CancellationToken ct)
    {
        // Atomic reservation within a single transaction
        // Increment booking count on slot (triggers RowVersion check)
        slot.CurrentBookings++;

        _context.Appointments.Add(appointment);

        // SaveChanges checks RowVersion — throws
        // DbUpdateConcurrencyException on race condition (AC-4)
        await _context.SaveChangesAsync(ct);

        return appointment;
    }
}
```

4. **Create `BookingService`** with atomic reservation and conflict handling:

```csharp
// server/src/PropelIQ.Application/Booking/BookingService.cs
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace PropelIQ.Application.Booking;

public class BookingService
{
    private readonly IBookingRepository _bookingRepo;
    private readonly AppDbContext _context;
    private readonly ILogger<BookingService> _logger;

    public BookingService(
        IBookingRepository bookingRepo,
        AppDbContext context,
        ILogger<BookingService> logger)
    {
        _bookingRepo = bookingRepo;
        _context = context;
        _logger = logger;
    }

    public async Task<Result<BookingResponse, SlotConflictResponse>>
        CreateBookingAsync(
            Guid patientId,
            CreateBookingRequest request,
            CancellationToken ct)
    {
        // Validate slot availability
        var slot = await _bookingRepo.GetSlotForBookingAsync(
            request.SlotId, ct);

        if (slot is null)
        {
            return await BuildConflictResponse(
                DateTime.UtcNow, null, ct);
        }

        // Generate unique confirmation code
        var confirmationCode = GenerateConfirmationCode();

        var appointment = new Appointment
        {
            Id = Guid.NewGuid(),
            PatientId = patientId,
            SlotId = request.SlotId,
            IntakeRecordId = request.IntakeRecordId,
            Status = AppointmentStatus.Confirmed,
            BookedAt = DateTime.UtcNow,
            AppointmentTime = slot.StartTime,
            DurationMinutes = (int)slot.Duration,
            AppointmentType = slot.Type.ToString(),
            ProviderName = slot.ProviderName,
            Location = slot.Location,
            ConfirmationCode = confirmationCode
        };

        try
        {
            // Atomic reservation with optimistic concurrency (AC-1, AC-4)
            var created = await _bookingRepo.CreateBookingAsync(
                appointment, slot, ct);

            _logger.LogInformation(
                "Booking created: {AppointmentId} for patient {PatientId}, " +
                "slot {SlotId}, code {ConfirmationCode}",
                created.Id, patientId, request.SlotId, confirmationCode);

            // Dispatch async event for artifact generation + notification
            // Event handler in task_002 generates PDF, QR, ICS and sends email
            await DispatchBookingConfirmedEventAsync(created, patientId, ct);

            return new BookingResponse
            {
                AppointmentId = created.Id,
                ConfirmationCode = confirmationCode,
                AppointmentTime = created.AppointmentTime,
                DurationMinutes = created.DurationMinutes,
                AppointmentType = created.AppointmentType,
                ProviderName = created.ProviderName,
                Location = created.Location,
                Status = created.Status.ToString(),
                BookedAt = created.BookedAt
            };
        }
        catch (DbUpdateConcurrencyException)
        {
            // AC-4: Concurrent booking race — slot taken by another patient
            _logger.LogWarning(
                "Booking conflict: slot {SlotId} taken during concurrent " +
                "booking attempt by patient {PatientId}",
                request.SlotId, patientId);

            return await BuildConflictResponse(
                slot.StartTime, slot.Type, ct);
        }
    }

    private async Task<SlotConflictResponse> BuildConflictResponse(
        DateTime afterTime, AppointmentType? type, CancellationToken ct)
    {
        var nextSlot = await _bookingRepo.GetNextAvailableSlotAsync(
            afterTime, type, ct);

        return new SlotConflictResponse
        {
            Message = "Slot no longer available",
            NextAvailableSlotId = nextSlot?.Id,
            NextAvailableTime = nextSlot?.StartTime
        };
    }

    private async Task DispatchBookingConfirmedEventAsync(
        Appointment appointment, Guid patientId, CancellationToken ct)
    {
        // Publish domain event for async processing
        // Consumed by ConfirmationArtifactService (task_002)
        var evt = new BookingConfirmedEvent
        {
            AppointmentId = appointment.Id,
            PatientId = patientId,
            ConfirmationCode = appointment.ConfirmationCode!,
            AppointmentTime = appointment.AppointmentTime,
            DurationMinutes = appointment.DurationMinutes,
            AppointmentType = appointment.AppointmentType,
            ProviderName = appointment.ProviderName,
            Location = appointment.Location
        };

        // Dispatch via channel/queue for background processing
        // Implementation depends on messaging infrastructure
        _logger.LogInformation(
            "BookingConfirmedEvent dispatched for {AppointmentId}",
            appointment.Id);

        await Task.CompletedTask;
    }

    private static string GenerateConfirmationCode()
    {
        // 8-character alphanumeric code
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        Span<byte> bytes = stackalloc byte[8];
        RandomNumberGenerator.Fill(bytes);
        return string.Create(8, bytes.ToArray(), (span, data) =>
        {
            for (int i = 0; i < span.Length; i++)
                span[i] = chars[data[i] % chars.Length];
        });
    }
}

// Simple result type for success/conflict differentiation
public class Result<TSuccess, TError>
{
    public TSuccess? Success { get; private init; }
    public TError? Error { get; private init; }
    public bool IsSuccess { get; private init; }

    public static implicit operator Result<TSuccess, TError>(TSuccess success) =>
        new() { Success = success, IsSuccess = true };

    public static implicit operator Result<TSuccess, TError>(TError error) =>
        new() { Error = error, IsSuccess = false };
}
```

5. **Create `BookingController`**:

```csharp
// server/src/PropelIQ.Api/Controllers/BookingController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace PropelIQ.Api.Controllers;

[ApiController]
[Route("api/v1/bookings")]
[Authorize]
public class BookingController : ControllerBase
{
    private readonly BookingService _bookingService;

    public BookingController(BookingService bookingService)
        => _bookingService = bookingService;

    // AC-1: Create booking with atomic slot reservation
    [HttpPost]
    [ProducesResponseType(typeof(BookingResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(SlotConflictResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateBooking(
        [FromBody] CreateBookingRequest request,
        CancellationToken ct)
    {
        var patientId = Guid.Parse(
            User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var result = await _bookingService.CreateBookingAsync(
            patientId, request, ct);

        if (result.IsSuccess)
        {
            return CreatedAtAction(
                nameof(GetBooking),
                new { id = result.Success!.AppointmentId },
                result.Success);
        }

        // AC-4: Concurrent conflict — suggest next slot
        return Conflict(result.Error);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(BookingResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetBooking(
        Guid id, CancellationToken ct)
    {
        // Retrieve by appointment ID — implementation delegated to repository
        return Ok(); // Placeholder: wire to repository
    }
}
```

6. **Add entity configuration** to `AppDbContext`:

```csharp
// In AppDbContext.cs
public DbSet<Appointment> Appointments => Set<Appointment>();

// In OnModelCreating
modelBuilder.Entity<Appointment>(entity =>
{
    entity.HasKey(e => e.Id);
    entity.HasIndex(e => e.PatientId);
    entity.HasIndex(e => e.SlotId).IsUnique();
    entity.HasIndex(e => e.ConfirmationCode).IsUnique();
    entity.HasIndex(e => e.IntakeRecordId);
    entity.Property(e => e.AppointmentType).HasMaxLength(64);
    entity.Property(e => e.ProviderName).HasMaxLength(256);
    entity.Property(e => e.Location).HasMaxLength(256);
    entity.Property(e => e.ConfirmationCode).HasMaxLength(16);
});
```

7. **Register services**:

```csharp
// In DependencyInjection.cs
services.AddScoped<IBookingRepository, BookingRepository>();
services.AddScoped<BookingService>();
```

## Current Project State

```text
propelIQ/
└── server/
    └── src/
        ├── PropelIQ.Api/
        │   └── Controllers/
        │       ├── AuthController.cs
        │       ├── AppointmentController.cs
        │       ├── IntakeController.cs
        │       └── BookingController.cs         (new)
        ├── PropelIQ.Application/
        │   ├── Scheduling/
        │   ├── Intake/
        │   ├── Booking/                          (new module)
        │   └── Abstractions/
        ├── PropelIQ.Domain/
        │   ├── Entities/
        │   │   ├── AppointmentSlot.cs           (US_019 — has RowVersion)
        │   │   ├── IntakeRecord.cs              (US_020)
        │   │   └── Appointment.cs               (new)
        │   ├── Enums/
        │   └── Events/
        └── PropelIQ.Infrastructure/
            ├── Booking/                          (new module)
            ├── AppDbContext.cs
            └── DependencyInjection.cs
```

> Placeholder: Update on execution based on US_019 and US_020 task completion state.

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | server/src/PropelIQ.Domain/Entities/Appointment.cs | Appointment aggregate with slot, patient, intake, status, confirmation code |
| CREATE | server/src/PropelIQ.Domain/Enums/AppointmentStatus.cs | Enum: Confirmed, Cancelled, Completed, NoShow |
| CREATE | server/src/PropelIQ.Domain/Events/BookingConfirmedEvent.cs | Domain event dispatched for async artifact generation |
| CREATE | server/src/PropelIQ.Application/Booking/Dto/BookingDto.cs | Create request, booking response, slot conflict response DTOs |
| CREATE | server/src/PropelIQ.Application/Booking/Validators/CreateBookingValidator.cs | FluentValidation for slot ID and intake record ID |
| CREATE | server/src/PropelIQ.Application/Booking/BookingService.cs | Atomic reservation with optimistic concurrency, conflict suggestion |
| CREATE | server/src/PropelIQ.Application/Abstractions/IBookingRepository.cs | Repository abstraction for slot lookup and booking creation |
| CREATE | server/src/PropelIQ.Infrastructure/Booking/BookingRepository.cs | EF Core transactional booking with RowVersion concurrency check |
| CREATE | server/src/PropelIQ.Api/Controllers/BookingController.cs | POST /api/v1/bookings with 201/409 responses |
| MODIFY | server/src/PropelIQ.Infrastructure/AppDbContext.cs | Add Appointment DbSet with unique indexes on SlotId and ConfirmationCode |
| MODIFY | server/src/PropelIQ.Infrastructure/DependencyInjection.cs | Register BookingRepository and BookingService |

## External References

- EF Core Optimistic Concurrency: https://learn.microsoft.com/en-us/ef/core/saving/concurrency
- ASP.NET Core REST API 201 Created: https://learn.microsoft.com/en-us/aspnet/core/web-api/action-return-types
- RandomNumberGenerator: https://learn.microsoft.com/en-us/dotnet/api/system.security.cryptography.randomnumbergenerator

## Build Commands

```bash
# Build backend
cd server/src/PropelIQ.Api
dotnet build

# Run backend
dotnet run

# Test successful booking
curl -X POST "http://localhost:5000/api/v1/bookings" \
  -H "Authorization: Bearer <jwt>" \
  -H "Content-Type: application/json" \
  -d '{"slotId":"<slot-guid>","intakeRecordId":"<intake-guid>"}'

# Expected: 201 Created with confirmation code

# Test concurrent conflict (simulate with same slotId from two sessions)
# Expected: 409 Conflict with next available slot suggestion
```

## Implementation Validation Strategy

- [ ] `POST /api/v1/bookings` atomically reserves slot and creates Appointment record (AC-1)
- [ ] Booking completes within 500 ms p95 (NFR-002)
- [ ] Unique 8-character confirmation code is generated per booking
- [ ] `AppointmentSlot.CurrentBookings` is incremented within the same transaction as Appointment creation
- [ ] `DbUpdateConcurrencyException` is caught and returns HTTP 409 with next available slot (AC-4)
- [ ] `Appointment.SlotId` has unique index preventing double-booking at DB level
- [ ] `BookingConfirmedEvent` is dispatched for async artifact generation after successful booking
- [ ] Booking persists even if event dispatch fails (edge case: email failure)
- [ ] Endpoint requires JWT bearer authentication
- [ ] Patient ID is extracted from JWT claims — cannot book for another patient

## Implementation Checklist

- [ ] Create `AppointmentStatus` enum (Confirmed, Cancelled, Completed, NoShow)
- [ ] Create `Appointment` entity with slot, patient, intake references and confirmation code
- [ ] Create `BookingConfirmedEvent` domain event for async artifact/notification processing
- [ ] Create `BookingService` with atomic reservation, `DbUpdateConcurrencyException` handling, and next-slot suggestion
- [ ] Create `BookingController` with `POST /api/v1/bookings` returning 201 or 409
- [ ] Create `BookingRepository` with transactional slot increment and appointment persist
- [ ] Add `Appointment` DbSet with unique indexes on `SlotId` and `ConfirmationCode`
- [ ] Generate cryptographically random 8-character confirmation codes
