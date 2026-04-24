# Task - TASK_001

## Requirement Reference

- User Story: us_019
- Story Location: .propel/context/tasks/EP-002/us_019/us_019.md
- Acceptance Criteria:
  - AC-1: Given I am authenticated, When I submit a slot search request with date range (within 30 days), duration (15, 30, or 60 minutes), and appointment type, Then the system returns available slots within 1 second, sourced from Redis cache where available.
  - AC-2: Given slot search results are returned, When I view the results, Then only future available slots are displayed; fully booked slots are excluded from results.
  - AC-3: Given no slots match my search criteria, When the search completes, Then the system displays "No slots available" and presents the option to join the preferred-slot waitlist.
  - AC-4: Given a date range beyond 30 days is submitted, When the API validates the request, Then the API returns HTTP 400 with a validation error message: "Slot search is limited to the next 30 days."
- Edge Cases:
  - What happens if a slot becomes unavailable between search and booking? Slot reservation uses optimistic concurrency; if the slot is taken, the user is shown an updated availability view.
  - How does the system handle a Redis cache miss? Request falls through to the database; cache is repopulated with the result and a bounded TTL.

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
| Library | StackExchange.Redis | latest stable |
| Library | FluentValidation | latest stable |
| Library | Microsoft.Extensions.Caching.StackExchangeRedis | latest stable |
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

Implement the backend slot search API with Redis cache-first pattern, 30-day window validation, and booked-slot exclusion. The `GET /api/v1/appointments/slots` endpoint accepts date range, duration (15/30/60 minutes), and appointment type as query parameters, validates the 30-day window constraint (AC-4), and returns available time slots within 1 second (AC-1, NFR-002). The service layer implements a cache-aside pattern per TR-004: on each search request, Redis is checked first with a composite cache key (`slots:{date}:{duration}:{type}`); on cache miss, the database is queried and results are written to Redis with a bounded TTL (5 minutes to balance freshness and performance). The `SlotSearchService` queries the `AppointmentSlot` table, excludes slots that are fully booked or in the past (AC-2), and returns a `SlotSearchResponse` with grouped time slots. A `SlotTemplate` configuration entity defines the base availability patterns (working hours, break windows) from which concrete slot instances are derived. The API supports role-based access for both Patient and Staff personas. Optimistic concurrency is built into the slot entity via a `RowVersion` column for booking race condition handling (edge case). All slot search operations are traced via OpenTelemetry per NFR-011.

## Dependent Tasks

- US_009 task_001 (requires Appointment entity, AppointmentSlot entity, and database migrations)
- EP-TECH infrastructure tasks (requires Redis provisioned and accessible per TR-004)

## Impacted Components

- New: `server/src/PropelIQ.Domain/Entities/AppointmentSlot.cs` (slot entity with availability, duration, type, booking state, concurrency token)
- New: `server/src/PropelIQ.Domain/Entities/SlotTemplate.cs` (slot availability template — working hours, break windows, recurrence)
- New: `server/src/PropelIQ.Domain/Enums/AppointmentType.cs` (enum for appointment types)
- New: `server/src/PropelIQ.Domain/Enums/SlotDuration.cs` (enum for 15/30/60 minute durations)
- New: `server/src/PropelIQ.Application/Scheduling/SlotSearchService.cs` (cache-first slot search logic)
- New: `server/src/PropelIQ.Application/Scheduling/SlotSearchQuery.cs` (search request/response DTOs)
- New: `server/src/PropelIQ.Application/Scheduling/Validators/SlotSearchQueryValidator.cs` (30-day window, duration, date range validation)
- New: `server/src/PropelIQ.Application/Abstractions/ISlotSearchService.cs` (search service abstraction)
- New: `server/src/PropelIQ.Application/Abstractions/ISlotRepository.cs` (slot repository abstraction)
- New: `server/src/PropelIQ.Infrastructure/Scheduling/SlotRepository.cs` (slot query with booked-slot exclusion)
- New: `server/src/PropelIQ.Infrastructure/Caching/SlotCacheService.cs` (Redis cache-aside wrapper for slot search)
- New: `server/src/PropelIQ.Api/Controllers/AppointmentController.cs` (slot search endpoint)
- Modify: `server/src/PropelIQ.Infrastructure/AppDbContext.cs` (add DbSets for AppointmentSlot, SlotTemplate)
- Modify: `server/src/PropelIQ.Infrastructure/DependencyInjection.cs` (register slot services, Redis cache)
- Modify: `server/src/PropelIQ.Api/Program.cs` (configure Redis distributed cache)

## Implementation Plan

1. **Create domain entities** for appointment slots and slot templates:

```csharp
// server/src/PropelIQ.Domain/Enums/AppointmentType.cs
namespace PropelIQ.Domain.Enums;

public enum AppointmentType
{
    General,
    Specialist,
    FollowUp,
    Urgent
}
```

```csharp
// server/src/PropelIQ.Domain/Enums/SlotDuration.cs
namespace PropelIQ.Domain.Enums;

public enum SlotDuration
{
    Fifteen = 15,
    Thirty = 30,
    Sixty = 60
}
```

```csharp
// server/src/PropelIQ.Domain/Entities/AppointmentSlot.cs
namespace PropelIQ.Domain.Entities;

public class AppointmentSlot
{
    public Guid Id { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public SlotDuration Duration { get; set; }
    public AppointmentType Type { get; set; }
    public int MaxCapacity { get; set; } = 1;
    public int CurrentBookings { get; set; } = 0;
    public bool IsAvailable => CurrentBookings < MaxCapacity
                               && StartTime > DateTime.UtcNow;
    public Guid? ProviderId { get; set; }
    public string? ProviderName { get; set; }
    public string? Location { get; set; }

    // Optimistic concurrency (edge case: booking race condition)
    public uint RowVersion { get; set; }
}
```

```csharp
// server/src/PropelIQ.Domain/Entities/SlotTemplate.cs
namespace PropelIQ.Domain.Entities;

public class SlotTemplate
{
    public Guid Id { get; set; }
    public DayOfWeek DayOfWeek { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public SlotDuration DefaultDuration { get; set; }
    public AppointmentType Type { get; set; }
    public int MaxCapacity { get; set; } = 1;
    public Guid? ProviderId { get; set; }
    public string? Location { get; set; }
    public bool IsActive { get; set; } = true;
}
```

2. **Create search DTOs and validator** with 30-day window enforcement:

```csharp
// server/src/PropelIQ.Application/Scheduling/SlotSearchQuery.cs
namespace PropelIQ.Application.Scheduling;

public record SlotSearchQuery
{
    public DateTime DateFrom { get; init; }
    public DateTime DateTo { get; init; }
    public SlotDuration? Duration { get; init; }
    public AppointmentType? Type { get; init; }
}

public record SlotSearchResponse
{
    public List<SlotGroupDto> Days { get; init; } = [];
    public int TotalAvailableSlots { get; init; }
    public bool HasResults => TotalAvailableSlots > 0;
}

public record SlotGroupDto
{
    public DateOnly Date { get; init; }
    public List<SlotDto> Slots { get; init; } = [];
}

public record SlotDto
{
    public Guid Id { get; init; }
    public DateTime StartTime { get; init; }
    public DateTime EndTime { get; init; }
    public int DurationMinutes { get; init; }
    public string Type { get; init; } = string.Empty;
    public string? ProviderName { get; init; }
    public string? Location { get; init; }
    public int AvailableCapacity { get; init; }
}
```

```csharp
// server/src/PropelIQ.Application/Scheduling/Validators/SlotSearchQueryValidator.cs
using FluentValidation;

namespace PropelIQ.Application.Scheduling.Validators;

public class SlotSearchQueryValidator : AbstractValidator<SlotSearchQuery>
{
    private const int MaxSearchWindowDays = 30;

    public SlotSearchQueryValidator()
    {
        RuleFor(x => x.DateFrom)
            .GreaterThanOrEqualTo(DateTime.UtcNow.Date)
                .WithMessage("Start date cannot be in the past.");

        RuleFor(x => x.DateTo)
            .GreaterThanOrEqualTo(x => x.DateFrom)
                .WithMessage("End date must be on or after the start date.");

        RuleFor(x => x)
            .Must(q => (q.DateTo.Date - q.DateFrom.Date).TotalDays <= MaxSearchWindowDays)
                .WithMessage("Slot search is limited to the next 30 days.")
                .WithName("DateRange");

        RuleFor(x => x.DateTo)
            .LessThanOrEqualTo(DateTime.UtcNow.Date.AddDays(MaxSearchWindowDays))
                .WithMessage("Slot search is limited to the next 30 days.");

        RuleFor(x => x.Duration)
            .IsInEnum()
                .When(x => x.Duration.HasValue)
                .WithMessage("Duration must be 15, 30, or 60 minutes.");

        RuleFor(x => x.Type)
            .IsInEnum()
                .When(x => x.Type.HasValue)
                .WithMessage("Invalid appointment type.");
    }
}
```

3. **Create `ISlotRepository` and `SlotRepository`** for database queries with booked-slot exclusion:

```csharp
// server/src/PropelIQ.Application/Abstractions/ISlotRepository.cs
namespace PropelIQ.Application.Abstractions;

public interface ISlotRepository
{
    Task<List<AppointmentSlot>> SearchAvailableSlotsAsync(
        DateTime dateFrom,
        DateTime dateTo,
        SlotDuration? duration,
        AppointmentType? type,
        CancellationToken ct);
}
```

```csharp
// server/src/PropelIQ.Infrastructure/Scheduling/SlotRepository.cs
namespace PropelIQ.Infrastructure.Scheduling;

public class SlotRepository : ISlotRepository
{
    private readonly AppDbContext _context;

    public SlotRepository(AppDbContext context)
        => _context = context;

    public async Task<List<AppointmentSlot>> SearchAvailableSlotsAsync(
        DateTime dateFrom,
        DateTime dateTo,
        SlotDuration? duration,
        AppointmentType? type,
        CancellationToken ct)
    {
        var query = _context.AppointmentSlots
            .AsNoTracking()
            .Where(s => s.StartTime >= dateFrom
                     && s.StartTime <= dateTo
                     && s.StartTime > DateTime.UtcNow   // Future only (AC-2)
                     && s.CurrentBookings < s.MaxCapacity); // Exclude booked (AC-2)

        if (duration.HasValue)
            query = query.Where(s => s.Duration == duration.Value);

        if (type.HasValue)
            query = query.Where(s => s.Type == type.Value);

        return await query
            .OrderBy(s => s.StartTime)
            .ToListAsync(ct);
    }
}
```

4. **Create `SlotCacheService`** implementing Redis cache-aside pattern per TR-004:

```csharp
// server/src/PropelIQ.Infrastructure/Caching/SlotCacheService.cs
using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace PropelIQ.Infrastructure.Caching;

public class SlotCacheService
{
    private const int CacheTtlMinutes = 5;
    private const string CacheKeyPrefix = "slots";

    private readonly IDistributedCache _cache;
    private readonly ILogger<SlotCacheService> _logger;

    public SlotCacheService(
        IDistributedCache cache,
        ILogger<SlotCacheService> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public string BuildCacheKey(SlotSearchQuery query)
    {
        var dateFrom = query.DateFrom.ToString("yyyyMMdd");
        var dateTo = query.DateTo.ToString("yyyyMMdd");
        var duration = query.Duration?.ToString() ?? "any";
        var type = query.Type?.ToString() ?? "any";
        return $"{CacheKeyPrefix}:{dateFrom}:{dateTo}:{duration}:{type}";
    }

    public async Task<List<AppointmentSlot>?> GetAsync(
        string cacheKey, CancellationToken ct)
    {
        try
        {
            var cached = await _cache.GetStringAsync(cacheKey, ct);
            if (cached is null) return null;

            return JsonSerializer.Deserialize<List<AppointmentSlot>>(cached);
        }
        catch (Exception ex)
        {
            // Cache failure should not break the search — fallback to DB
            _logger.LogWarning(ex,
                "Redis cache read failed for key {CacheKey}", cacheKey);
            return null;
        }
    }

    public async Task SetAsync(
        string cacheKey,
        List<AppointmentSlot> slots,
        CancellationToken ct)
    {
        try
        {
            var serialized = JsonSerializer.Serialize(slots);
            var options = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow =
                    TimeSpan.FromMinutes(CacheTtlMinutes)
            };

            await _cache.SetStringAsync(
                cacheKey, serialized, options, ct);
        }
        catch (Exception ex)
        {
            // Cache write failure is non-critical — log and continue
            _logger.LogWarning(ex,
                "Redis cache write failed for key {CacheKey}", cacheKey);
        }
    }

    public async Task InvalidateByDateAsync(
        DateTime date, CancellationToken ct)
    {
        // Pattern-based invalidation for slot cache on booking changes
        // Redis does not natively support wildcard delete — use key tracking
        // or accept TTL-based eventual consistency for Phase 1
        _logger.LogInformation(
            "Cache invalidation requested for date {Date} — relying on TTL expiry",
            date.ToString("yyyy-MM-dd"));
        await Task.CompletedTask;
    }
}
```

5. **Create `ISlotSearchService` and `SlotSearchService`** with cache-first orchestration:

```csharp
// server/src/PropelIQ.Application/Abstractions/ISlotSearchService.cs
namespace PropelIQ.Application.Abstractions;

public interface ISlotSearchService
{
    Task<SlotSearchResponse> SearchAsync(
        SlotSearchQuery query, CancellationToken ct);
}
```

```csharp
// server/src/PropelIQ.Application/Scheduling/SlotSearchService.cs
using System.Diagnostics;

namespace PropelIQ.Application.Scheduling;

public class SlotSearchService : ISlotSearchService
{
    private static readonly ActivitySource ActivitySource =
        new("PropelIQ.Scheduling.SlotSearch");

    private readonly ISlotRepository _slotRepo;
    private readonly SlotCacheService _cacheService;

    public SlotSearchService(
        ISlotRepository slotRepo,
        SlotCacheService cacheService)
    {
        _slotRepo = slotRepo;
        _cacheService = cacheService;
    }

    public async Task<SlotSearchResponse> SearchAsync(
        SlotSearchQuery query, CancellationToken ct)
    {
        using var activity = ActivitySource.StartActivity("SlotSearch");
        activity?.SetTag("search.date_from", query.DateFrom.ToString("O"));
        activity?.SetTag("search.date_to", query.DateTo.ToString("O"));
        activity?.SetTag("search.duration", query.Duration?.ToString());
        activity?.SetTag("search.type", query.Type?.ToString());

        // Cache-first pattern (AC-1, TR-004)
        var cacheKey = _cacheService.BuildCacheKey(query);
        var cachedSlots = await _cacheService.GetAsync(cacheKey, ct);

        List<AppointmentSlot> slots;

        if (cachedSlots is not null)
        {
            activity?.SetTag("search.cache_hit", true);
            // Re-filter cached results for freshness (exclude now-past slots)
            slots = cachedSlots
                .Where(s => s.StartTime > DateTime.UtcNow
                         && s.CurrentBookings < s.MaxCapacity)
                .ToList();
        }
        else
        {
            activity?.SetTag("search.cache_hit", false);

            // Database fallback (edge case: cache miss)
            slots = await _slotRepo.SearchAvailableSlotsAsync(
                query.DateFrom,
                query.DateTo,
                query.Duration,
                query.Type,
                ct);

            // Repopulate cache with bounded TTL
            await _cacheService.SetAsync(cacheKey, slots, ct);
        }

        // Group by date for frontend consumption
        var grouped = slots
            .GroupBy(s => DateOnly.FromDateTime(s.StartTime))
            .OrderBy(g => g.Key)
            .Select(g => new SlotGroupDto
            {
                Date = g.Key,
                Slots = g.OrderBy(s => s.StartTime)
                    .Select(s => new SlotDto
                    {
                        Id = s.Id,
                        StartTime = s.StartTime,
                        EndTime = s.EndTime,
                        DurationMinutes = (int)s.Duration,
                        Type = s.Type.ToString(),
                        ProviderName = s.ProviderName,
                        Location = s.Location,
                        AvailableCapacity =
                            s.MaxCapacity - s.CurrentBookings
                    })
                    .ToList()
            })
            .ToList();

        activity?.SetTag("search.result_count", slots.Count);

        return new SlotSearchResponse
        {
            Days = grouped,
            TotalAvailableSlots = slots.Count
        };
    }
}
```

6. **Create slot search endpoint** in `AppointmentController.cs`:

```csharp
// server/src/PropelIQ.Api/Controllers/AppointmentController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace PropelIQ.Api.Controllers;

[ApiController]
[Route("api/v1/appointments")]
[Authorize]
public class AppointmentController : ControllerBase
{
    private readonly ISlotSearchService _slotSearchService;

    public AppointmentController(ISlotSearchService slotSearchService)
        => _slotSearchService = slotSearchService;

    [HttpGet("slots")]
    [ProducesResponseType(typeof(SlotSearchResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SearchSlots(
        [FromQuery] SlotSearchQuery query,
        CancellationToken ct)
    {
        // FluentValidation handles 30-day window check (AC-4)
        // via the validation pipeline middleware

        var result = await _slotSearchService.SearchAsync(query, ct);

        return Ok(result);
    }
}
```

7. **Configure Redis distributed cache** in `Program.cs`:

```csharp
// In Program.cs — Redis configuration
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration =
        builder.Configuration.GetConnectionString("Redis");
    options.InstanceName = "PropelIQ:";
});
```

Configuration in `appsettings.json`:
```json
{
  "ConnectionStrings": {
    "Redis": "localhost:6379"
  }
}
```

8. **Add entity configurations** to `AppDbContext`:

```csharp
// In AppDbContext.cs
public DbSet<AppointmentSlot> AppointmentSlots => Set<AppointmentSlot>();
public DbSet<SlotTemplate> SlotTemplates => Set<SlotTemplate>();

// In OnModelCreating
modelBuilder.Entity<AppointmentSlot>(entity =>
{
    entity.HasKey(e => e.Id);
    entity.HasIndex(e => new { e.StartTime, e.Type, e.Duration });
    entity.HasIndex(e => e.ProviderId);
    entity.Property(e => e.ProviderName).HasMaxLength(256);
    entity.Property(e => e.Location).HasMaxLength(256);
    entity.Property(e => e.RowVersion)
        .IsRowVersion(); // Optimistic concurrency
});

modelBuilder.Entity<SlotTemplate>(entity =>
{
    entity.HasKey(e => e.Id);
    entity.HasIndex(e => new { e.DayOfWeek, e.Type, e.IsActive });
    entity.Property(e => e.Location).HasMaxLength(256);
});
```

9. **Register services** in `DependencyInjection.cs`:

```csharp
// In DependencyInjection.cs
services.AddScoped<ISlotRepository, SlotRepository>();
services.AddScoped<ISlotSearchService, SlotSearchService>();
services.AddSingleton<SlotCacheService>();
```

## Current Project State

```text
propelIQ/
├── docker-compose.yml
├── .env.example
└── server/
    └── src/
        ├── PropelIQ.Api/
        │   ├── Program.cs
        │   └── Controllers/
        │       └── AuthController.cs
        ├── PropelIQ.Application/
        │   ├── Auth/
        │   ├── Sessions/
        │   ├── Scheduling/
        │   └── Abstractions/
        ├── PropelIQ.Domain/
        │   ├── Entities/
        │   │   ├── Appointment.cs       (from US_009)
        │   │   └── ...
        │   └── Enums/
        └── PropelIQ.Infrastructure/
            ├── Identity/
            ├── Sessions/
            ├── Scheduling/
            ├── Caching/
            ├── AppDbContext.cs
            └── DependencyInjection.cs
```

> Placeholder: Update on execution based on US_009 task completion state.

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | server/src/PropelIQ.Domain/Entities/AppointmentSlot.cs | Slot entity with capacity, duration, type, optimistic concurrency RowVersion |
| CREATE | server/src/PropelIQ.Domain/Entities/SlotTemplate.cs | Slot availability template defining working hours and recurrence patterns |
| CREATE | server/src/PropelIQ.Domain/Enums/AppointmentType.cs | Enum: General, Specialist, FollowUp, Urgent |
| CREATE | server/src/PropelIQ.Domain/Enums/SlotDuration.cs | Enum: Fifteen (15), Thirty (30), Sixty (60) |
| CREATE | server/src/PropelIQ.Application/Scheduling/SlotSearchQuery.cs | Search request/response DTOs with grouped slot structure |
| CREATE | server/src/PropelIQ.Application/Scheduling/Validators/SlotSearchQueryValidator.cs | 30-day window, duration enum, date range validation |
| CREATE | server/src/PropelIQ.Application/Scheduling/SlotSearchService.cs | Cache-first search with DB fallback, date grouping, OpenTelemetry tracing |
| CREATE | server/src/PropelIQ.Application/Abstractions/ISlotSearchService.cs | Search service abstraction |
| CREATE | server/src/PropelIQ.Application/Abstractions/ISlotRepository.cs | Repository abstraction for slot queries |
| CREATE | server/src/PropelIQ.Infrastructure/Scheduling/SlotRepository.cs | EF Core query filtering past/booked slots, duration/type filtering |
| CREATE | server/src/PropelIQ.Infrastructure/Caching/SlotCacheService.cs | Redis cache-aside with 5-minute TTL, graceful failure on cache errors |
| CREATE | server/src/PropelIQ.Api/Controllers/AppointmentController.cs | GET /api/v1/appointments/slots endpoint with authorization |
| MODIFY | server/src/PropelIQ.Infrastructure/AppDbContext.cs | Add DbSets for AppointmentSlot and SlotTemplate with indexes |
| MODIFY | server/src/PropelIQ.Infrastructure/DependencyInjection.cs | Register SlotRepository, SlotSearchService, SlotCacheService |
| MODIFY | server/src/PropelIQ.Api/Program.cs | Configure AddStackExchangeRedisCache with connection string |

## External References

- ASP.NET Core distributed caching with Redis: https://learn.microsoft.com/en-us/aspnet/core/performance/caching/distributed
- StackExchange.Redis: https://stackexchange.github.io/StackExchange.Redis/
- EF Core optimistic concurrency: https://learn.microsoft.com/en-us/ef/core/saving/concurrency
- Cache-aside pattern: https://learn.microsoft.com/en-us/azure/architecture/patterns/cache-aside
- OpenTelemetry .NET: https://opentelemetry.io/docs/languages/dotnet/

## Build Commands

```bash
# Build backend
cd server/src/PropelIQ.Api
dotnet build

# Run backend (requires Redis running)
dotnet run

# Test slot search
curl -X GET "http://localhost:5000/api/v1/appointments/slots?dateFrom=2026-04-17&dateTo=2026-04-24&duration=30&type=General" \
  -H "Authorization: Bearer <jwt>"

# Test 30-day validation (should return 400)
curl -X GET "http://localhost:5000/api/v1/appointments/slots?dateFrom=2026-04-17&dateTo=2026-06-17" \
  -H "Authorization: Bearer <jwt>"

# Test empty results
curl -X GET "http://localhost:5000/api/v1/appointments/slots?dateFrom=2026-04-17&dateTo=2026-04-17&duration=60&type=Urgent" \
  -H "Authorization: Bearer <jwt>"
```

## Implementation Validation Strategy

- [x] `GET /api/v1/appointments/slots` returns available slots grouped by date within 1 second (AC-1)
- [x] Redis cache is checked first; on hit, results are served from cache (AC-1, TR-004)
- [x] On cache miss, database is queried and results are cached with 5-minute TTL (edge case)
- [x] Only future slots with available capacity are returned; booked/past slots are excluded (AC-2)
- [x] Empty result set returns `totalAvailableSlots: 0` and `hasResults: false` (AC-3)
- [x] Date range exceeding 30 days returns HTTP 400 with "Slot search is limited to the next 30 days." (AC-4)
- [x] Duration validates against 15, 30, 60 values only
- [x] `AppointmentSlot.RowVersion` is configured for optimistic concurrency (edge case: booking race)
- [x] Redis cache failures are logged as warnings and do not break the search flow
- [x] OpenTelemetry activity traces cache hit/miss, result count, and search parameters (NFR-011)
- [x] Endpoint requires JWT bearer authentication
- [x] Composite index on `(StartTime, Type, Duration)` exists for query performance

## Implementation Checklist

- [x] Create `AppointmentType` enum (General, Specialist, FollowUp, Urgent)
- [x] Create `SlotDuration` enum (Fifteen=15, Thirty=30, Sixty=60)
- [x] Create `AppointmentSlot` entity with `Id`, `StartTime`, `EndTime`, `Duration`, `Type`, `MaxCapacity`, `CurrentBookings`, `ProviderId`, `ProviderName`, `Location`, `RowVersion`
- [x] Create `SlotTemplate` entity for base availability patterns
- [x] Create `SlotSearchQuery` and `SlotSearchResponse`/`SlotGroupDto`/`SlotDto` DTOs
- [x] Create `SlotSearchQueryValidator` with 30-day window, future date, and enum validation
- [x] Create `ISlotRepository` with `SearchAvailableSlotsAsync` filtering past and booked slots
- [x] Create `SlotRepository` implementation with EF Core query
- [x] Create `SlotCacheService` with cache-aside pattern, 5-minute TTL, and graceful failure
- [x] Create `ISlotSearchService` and `SlotSearchService` with cache-first orchestration and OpenTelemetry
- [x] Create `AppointmentController` with `GET /api/v1/appointments/slots` endpoint
- [x] Add `AppointmentSlot` and `SlotTemplate` DbSets with entity configuration and indexes
- [x] Configure `AddStackExchangeRedisCache` in `Program.cs`
- [x] Register all scheduling services in `DependencyInjection.cs`
