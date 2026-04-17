# Task - TASK_001

## Requirement Reference

- User Story: us_025
- Story Location: .propel/context/tasks/EP-002/us_025/us_025.md
- Acceptance Criteria:
  - AC-1: Given I am authenticated as a patient, When I navigate to "My Appointments," Then a list of all my past and upcoming appointments is displayed sorted by date descending with status labels.
  - AC-2: Given the appointment history is displayed, When I apply a status filter (e.g., Completed, Cancelled, No-Show), Then the list updates within 500 ms to show only appointments matching the filter.
  - AC-3: Given I apply a date range filter, When the filter is applied, Then only appointments within the specified date range are shown.
  - AC-4: Given I click "Export PDF," When the export is processed, Then a PDF containing my filtered appointment history downloads within 5 seconds.
- Edge Cases:
  - What happens if a patient has hundreds of appointments? Pagination is applied with 20 records per page; the export PDF includes all filtered records regardless of pagination.
  - How does the system handle an empty appointment history? An empty state message is displayed: "No appointments found. Book your first appointment."

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
| Library | QuestPDF | latest stable |
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

Implement the appointment history API with server-side filtering, cursor-based pagination, and PDF export. The `GET /api/v1/appointments/history` endpoint returns the authenticated patient's appointments sorted by date descending (AC-1), with optional query parameters for status filter (AC-2) and date range filter (AC-3). The response uses cursor-based pagination with 20 records per page (edge case) and includes total count for frontend pagination controls. The `GET /api/v1/appointments/history/export` endpoint generates a PDF containing all filtered records regardless of pagination (AC-4), using QuestPDF (same library as US_021 task_002 confirmation artifacts) with a table layout showing date, time, provider, type, status, and duration. The PDF generation MUST complete within 5 seconds (AC-4) even for hundreds of records — achieved by streaming the query with `AsAsyncEnumerable()` and writing rows incrementally. The API enforces JWT authentication with patient ownership validation, and queries use a composite index on `(PatientId, AppointmentDate DESC, Status)` to meet the NFR-002 500ms p95 SLA (AC-2). Empty results return a 200 with an empty array and `totalCount: 0` (edge case).

## Dependent Tasks

- US_021 task_001 (requires Appointment entity with status, date, provider, and type fields)
- US_019 task_001 (requires AppointmentSlot entity with provider and duration)
- US_014 task_001 (requires JWT authentication middleware)

## Impacted Components

- New: `server/src/PropelIQ.Application/Appointments/AppointmentHistoryService.cs` (filtered query, pagination, PDF orchestration)
- New: `server/src/PropelIQ.Application/Appointments/Dto/AppointmentHistoryDto.cs` (request/response DTOs)
- New: `server/src/PropelIQ.Application/Appointments/Validators/HistoryFilterValidator.cs` (FluentValidation)
- New: `server/src/PropelIQ.Application/Appointments/AppointmentHistoryPdfGenerator.cs` (QuestPDF table layout)
- New: `server/src/PropelIQ.Application/Abstractions/IAppointmentHistoryRepository.cs` (repository abstraction)
- New: `server/src/PropelIQ.Infrastructure/Appointments/AppointmentHistoryRepository.cs` (EF Core with composite index)
- New: `server/src/PropelIQ.Api/Controllers/AppointmentHistoryController.cs` (history + export endpoints)
- Modify: `server/src/PropelIQ.Infrastructure/AppDbContext.cs` (add composite index on Appointment)
- Modify: `server/src/PropelIQ.Infrastructure/DependencyInjection.cs` (register history services)

## Implementation Plan

1. **Create DTOs for appointment history**:

```csharp
// server/src/PropelIQ.Application/Appointments/Dto/AppointmentHistoryDto.cs
namespace PropelIQ.Application.Appointments.Dto;

public record AppointmentHistoryFilter
{
    public string? Status { get; init; }
    public DateTime? DateFrom { get; init; }
    public DateTime? DateTo { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}

public record AppointmentHistoryItem
{
    public Guid Id { get; init; }
    public DateTime AppointmentDate { get; init; }
    public TimeSpan AppointmentTime { get; init; }
    public int DurationMinutes { get; init; }
    public string AppointmentType { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string? ProviderName { get; init; }
    public string? Location { get; init; }
    public string ConfirmationCode { get; init; } = string.Empty;
}

public record AppointmentHistoryResponse
{
    public List<AppointmentHistoryItem> Items { get; init; } = [];
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalPages { get; init; }
}
```

2. **Create FluentValidation for filter parameters**:

```csharp
// server/src/PropelIQ.Application/Appointments/Validators/HistoryFilterValidator.cs
using FluentValidation;

namespace PropelIQ.Application.Appointments.Validators;

public class HistoryFilterValidator
    : AbstractValidator<AppointmentHistoryFilter>
{
    private static readonly string[] ValidStatuses =
        ["Confirmed", "Completed", "Cancelled", "No-Show", "Rescheduled"];

    public HistoryFilterValidator()
    {
        RuleFor(x => x.Status)
            .Must(s => s is null || ValidStatuses.Contains(s))
            .WithMessage(
                "Status must be one of: Confirmed, Completed, " +
                "Cancelled, No-Show, Rescheduled.");

        RuleFor(x => x.DateTo)
            .GreaterThanOrEqualTo(x => x.DateFrom)
            .When(x => x.DateFrom.HasValue && x.DateTo.HasValue)
            .WithMessage("End date must be on or after start date.");

        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1)
            .WithMessage("Page must be 1 or greater.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100)
            .WithMessage("Page size must be between 1 and 100.");
    }
}
```

3. **Create repository abstraction and implementation**:

```csharp
// server/src/PropelIQ.Application/Abstractions/IAppointmentHistoryRepository.cs
namespace PropelIQ.Application.Abstractions;

public interface IAppointmentHistoryRepository
{
    Task<(List<Appointment> Items, int TotalCount)>
        GetFilteredAsync(
            Guid patientId,
            AppointmentHistoryFilter filter,
            CancellationToken ct);

    IAsyncEnumerable<Appointment> StreamFilteredAsync(
        Guid patientId,
        AppointmentHistoryFilter filter,
        CancellationToken ct);
}
```

```csharp
// server/src/PropelIQ.Infrastructure/Appointments/AppointmentHistoryRepository.cs
using Microsoft.EntityFrameworkCore;

namespace PropelIQ.Infrastructure.Appointments;

public class AppointmentHistoryRepository
    : IAppointmentHistoryRepository
{
    private readonly AppDbContext _context;

    public AppointmentHistoryRepository(AppDbContext context)
        => _context = context;

    // AC-1, AC-2, AC-3: Filtered paginated query
    public async Task<(List<Appointment> Items, int TotalCount)>
        GetFilteredAsync(
            Guid patientId,
            AppointmentHistoryFilter filter,
            CancellationToken ct)
    {
        var query = BuildFilteredQuery(patientId, filter);

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(a => a.AppointmentDate)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .AsNoTracking()
            .ToListAsync(ct);

        return (items, totalCount);
    }

    // AC-4: Stream all filtered records for PDF (no pagination)
    public async IAsyncEnumerable<Appointment> StreamFilteredAsync(
        Guid patientId,
        AppointmentHistoryFilter filter,
        [EnumeratorCancellation] CancellationToken ct)
    {
        var query = BuildFilteredQuery(patientId, filter)
            .OrderByDescending(a => a.AppointmentDate)
            .AsNoTracking();

        await foreach (var item in query.AsAsyncEnumerable()
            .WithCancellation(ct))
        {
            yield return item;
        }
    }

    private IQueryable<Appointment> BuildFilteredQuery(
        Guid patientId, AppointmentHistoryFilter filter)
    {
        var query = _context.Appointments
            .Where(a => a.PatientId == patientId);

        // AC-2: Status filter
        if (!string.IsNullOrEmpty(filter.Status))
        {
            query = query.Where(a =>
                a.Status.ToString() == filter.Status);
        }

        // AC-3: Date range filter
        if (filter.DateFrom.HasValue)
        {
            query = query.Where(a =>
                a.AppointmentDate >= filter.DateFrom.Value);
        }

        if (filter.DateTo.HasValue)
        {
            query = query.Where(a =>
                a.AppointmentDate <= filter.DateTo.Value);
        }

        return query;
    }
}
```

4. **Create PDF generator for appointment history export**:

```csharp
// server/src/PropelIQ.Application/Appointments/AppointmentHistoryPdfGenerator.cs
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace PropelIQ.Application.Appointments;

public class AppointmentHistoryPdfGenerator
{
    // AC-4: Generate PDF of filtered appointment history
    public byte[] GeneratePdf(
        List<AppointmentHistoryItem> appointments,
        string patientName,
        AppointmentHistoryFilter filter)
    {
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(1, Unit.Centimetre);

                page.Header().Column(col =>
                {
                    col.Item().Text("Appointment History")
                        .FontSize(18).Bold();
                    col.Item().Text($"Patient: {patientName}")
                        .FontSize(12);

                    if (filter.Status is not null
                        || filter.DateFrom.HasValue)
                    {
                        var filterText = "Filters: ";
                        if (filter.Status is not null)
                            filterText += $"Status: {filter.Status} ";
                        if (filter.DateFrom.HasValue)
                            filterText +=
                                $"From: {filter.DateFrom:MMM d, yyyy} ";
                        if (filter.DateTo.HasValue)
                            filterText +=
                                $"To: {filter.DateTo:MMM d, yyyy}";

                        col.Item().Text(filterText)
                            .FontSize(10).Italic();
                    }

                    col.Item().PaddingBottom(10);
                });

                page.Content().Table(table =>
                {
                    table.ColumnsDefinition(cols =>
                    {
                        cols.RelativeColumn(2); // Date
                        cols.RelativeColumn(1); // Time
                        cols.RelativeColumn(2); // Type
                        cols.RelativeColumn(2); // Provider
                        cols.RelativeColumn(1); // Duration
                        cols.RelativeColumn(1); // Status
                    });

                    // Header row
                    table.Header(header =>
                    {
                        foreach (var h in new[]
                        {
                            "Date", "Time", "Type",
                            "Provider", "Duration", "Status"
                        })
                        {
                            header.Cell()
                                .Background(Colors.Grey.Lighten3)
                                .Padding(5)
                                .Text(h).Bold().FontSize(10);
                        }
                    });

                    // Data rows
                    foreach (var apt in appointments)
                    {
                        table.Cell().Padding(4)
                            .Text(apt.AppointmentDate
                                .ToString("MMM d, yyyy"))
                            .FontSize(9);
                        table.Cell().Padding(4)
                            .Text(apt.AppointmentTime
                                .ToString(@"hh\:mm"))
                            .FontSize(9);
                        table.Cell().Padding(4)
                            .Text(apt.AppointmentType)
                            .FontSize(9);
                        table.Cell().Padding(4)
                            .Text(apt.ProviderName ?? "—")
                            .FontSize(9);
                        table.Cell().Padding(4)
                            .Text($"{apt.DurationMinutes} min")
                            .FontSize(9);
                        table.Cell().Padding(4)
                            .Text(apt.Status)
                            .FontSize(9);
                    }
                });

                page.Footer().AlignCenter()
                    .Text(x =>
                    {
                        x.Span("Page ").FontSize(8);
                        x.CurrentPageNumber().FontSize(8);
                        x.Span(" of ").FontSize(8);
                        x.TotalPages().FontSize(8);
                    });
            });
        });

        return document.GeneratePdf();
    }
}
```

5. **Create `AppointmentHistoryService`**:

```csharp
// server/src/PropelIQ.Application/Appointments/AppointmentHistoryService.cs
using Microsoft.Extensions.Logging;

namespace PropelIQ.Application.Appointments;

public class AppointmentHistoryService
{
    private readonly IAppointmentHistoryRepository _repo;
    private readonly AppointmentHistoryPdfGenerator _pdfGenerator;
    private readonly ILogger<AppointmentHistoryService> _logger;

    public AppointmentHistoryService(
        IAppointmentHistoryRepository repo,
        AppointmentHistoryPdfGenerator pdfGenerator,
        ILogger<AppointmentHistoryService> logger)
    {
        _repo = repo;
        _pdfGenerator = pdfGenerator;
        _logger = logger;
    }

    // AC-1, AC-2, AC-3: Filtered paginated history
    public async Task<AppointmentHistoryResponse> GetHistoryAsync(
        Guid patientId,
        AppointmentHistoryFilter filter,
        CancellationToken ct)
    {
        var (items, totalCount) = await _repo.GetFilteredAsync(
            patientId, filter, ct);

        var totalPages = (int)Math.Ceiling(
            (double)totalCount / filter.PageSize);

        return new AppointmentHistoryResponse
        {
            Items = items.Select(MapToDto).ToList(),
            TotalCount = totalCount,
            Page = filter.Page,
            PageSize = filter.PageSize,
            TotalPages = totalPages
        };
    }

    // AC-4: PDF export of all filtered records
    public async Task<byte[]> ExportPdfAsync(
        Guid patientId,
        string patientName,
        AppointmentHistoryFilter filter,
        CancellationToken ct)
    {
        // Stream all filtered records (no pagination limit)
        var allItems = new List<AppointmentHistoryItem>();
        await foreach (var apt in _repo.StreamFilteredAsync(
            patientId, filter, ct))
        {
            allItems.Add(MapToDto(apt));
        }

        _logger.LogInformation(
            "Generating PDF for patient {PatientId} with " +
            "{Count} appointment records",
            patientId, allItems.Count);

        return _pdfGenerator.GeneratePdf(
            allItems, patientName, filter);
    }

    private static AppointmentHistoryItem MapToDto(Appointment a)
    {
        return new AppointmentHistoryItem
        {
            Id = a.Id,
            AppointmentDate = a.AppointmentDate,
            AppointmentTime = a.AppointmentTime,
            DurationMinutes = a.DurationMinutes,
            AppointmentType = a.AppointmentType,
            Status = a.Status.ToString(),
            ProviderName = a.ProviderName,
            Location = a.Location,
            ConfirmationCode = a.ConfirmationCode
        };
    }
}
```

6. **Create `AppointmentHistoryController`**:

```csharp
// server/src/PropelIQ.Api/Controllers/AppointmentHistoryController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace PropelIQ.Api.Controllers;

[ApiController]
[Route("api/v1/appointments/history")]
[Authorize]
public class AppointmentHistoryController : ControllerBase
{
    private readonly AppointmentHistoryService _historyService;

    public AppointmentHistoryController(
        AppointmentHistoryService historyService)
        => _historyService = historyService;

    // AC-1, AC-2, AC-3: Paginated filtered history
    [HttpGet]
    [ProducesResponseType(typeof(AppointmentHistoryResponse),
        StatusCodes.Status200OK)]
    public async Task<IActionResult> GetHistory(
        [FromQuery] AppointmentHistoryFilter filter,
        CancellationToken ct)
    {
        var patientId = Guid.Parse(
            User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var result = await _historyService.GetHistoryAsync(
            patientId, filter, ct);

        return Ok(result);
    }

    // AC-4: PDF export of filtered history
    [HttpGet("export")]
    [ProducesResponseType(typeof(FileContentResult),
        StatusCodes.Status200OK)]
    public async Task<IActionResult> ExportPdf(
        [FromQuery] AppointmentHistoryFilter filter,
        CancellationToken ct)
    {
        var patientId = Guid.Parse(
            User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var patientName = User.FindFirstValue(ClaimTypes.Name)
            ?? "Patient";

        var pdfBytes = await _historyService.ExportPdfAsync(
            patientId, patientName, filter, ct);

        return File(
            pdfBytes,
            "application/pdf",
            $"appointment-history-{DateTime.UtcNow:yyyyMMdd}.pdf");
    }
}
```

7. **Add composite index for performance** (NFR-002):

```csharp
// In AppDbContext.OnModelCreating — add to Appointment configuration
// NFR-002: 500ms p95 for filtered history queries
entity.HasIndex(e => new
{
    e.PatientId,
    e.AppointmentDate,
    e.Status
}).HasDatabaseName("IX_Appointment_Patient_Date_Status");
```

8. **Register services in DI**:

```csharp
// In DependencyInjection.cs
services.AddScoped<IAppointmentHistoryRepository,
    AppointmentHistoryRepository>();
services.AddScoped<AppointmentHistoryService>();
services.AddSingleton<AppointmentHistoryPdfGenerator>();
```

## Current Project State

```text
propelIQ/
└── server/
    └── src/
        ├── PropelIQ.Api/
        │   └── Controllers/
        │       ├── BookingController.cs              (existing)
        │       ├── WaitlistController.cs             (existing from US_023)
        │       └── AppointmentHistoryController.cs   (new)
        ├── PropelIQ.Application/
        │   ├── Booking/                              (existing)
        │   ├── Waitlist/                             (existing from US_023)
        │   ├── Appointments/                         (new module)
        │   │   ├── AppointmentHistoryService.cs
        │   │   ├── AppointmentHistoryPdfGenerator.cs
        │   │   ├── Dto/
        │   │   │   └── AppointmentHistoryDto.cs
        │   │   └── Validators/
        │   │       └── HistoryFilterValidator.cs
        │   └── Abstractions/
        │       ├── IBookingRepository.cs             (existing)
        │       ├── IWaitlistRepository.cs            (existing)
        │       └── IAppointmentHistoryRepository.cs  (new)
        ├── PropelIQ.Domain/
        │   └── Entities/
        │       └── Appointment.cs                    (existing)
        └── PropelIQ.Infrastructure/
            ├── Appointments/                         (new)
            │   └── AppointmentHistoryRepository.cs
            ├── AppDbContext.cs                        (modify — composite index)
            └── DependencyInjection.cs                (modify — register services)
```

> Placeholder: Update on execution based on US_021 and US_022 task completion state.

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | server/src/PropelIQ.Application/Appointments/Dto/AppointmentHistoryDto.cs | Filter, item, and paginated response DTOs |
| CREATE | server/src/PropelIQ.Application/Appointments/Validators/HistoryFilterValidator.cs | FluentValidation for status, date range, page size |
| CREATE | server/src/PropelIQ.Application/Abstractions/IAppointmentHistoryRepository.cs | Repository abstraction with filtered query and async stream |
| CREATE | server/src/PropelIQ.Infrastructure/Appointments/AppointmentHistoryRepository.cs | EF Core with composite index, pagination, streaming |
| CREATE | server/src/PropelIQ.Application/Appointments/AppointmentHistoryPdfGenerator.cs | QuestPDF table layout with header, filters summary, pagination |
| CREATE | server/src/PropelIQ.Application/Appointments/AppointmentHistoryService.cs | History query orchestration and PDF export |
| CREATE | server/src/PropelIQ.Api/Controllers/AppointmentHistoryController.cs | GET history and GET export endpoints |
| MODIFY | server/src/PropelIQ.Infrastructure/AppDbContext.cs | Add composite index (PatientId, AppointmentDate, Status) |
| MODIFY | server/src/PropelIQ.Infrastructure/DependencyInjection.cs | Register history repository, service, PDF generator |

## External References

- QuestPDF Tables: https://www.questpdf.com/api-reference/table.html
- EF Core Indexes: https://learn.microsoft.com/en-us/ef/core/modeling/indexes
- AsAsyncEnumerable: https://learn.microsoft.com/en-us/ef/core/querying/streaming

## Build Commands

```bash
# Build backend
cd server/src/PropelIQ.Api
dotnet build

# Run backend
dotnet run

# Test filtered history (AC-1, AC-2, AC-3)
curl -X GET "http://localhost:5000/api/v1/appointments/history?status=Completed&dateFrom=2026-01-01&dateTo=2026-04-17&page=1&pageSize=20" \
  -H "Authorization: Bearer <jwt>"
# Expected: 200 OK with paginated appointment list

# Test PDF export (AC-4)
curl -X GET "http://localhost:5000/api/v1/appointments/history/export?status=Completed" \
  -H "Authorization: Bearer <jwt>" \
  -o appointment-history.pdf
# Expected: PDF file downloads within 5 seconds
```

## Implementation Validation Strategy

- [ ] `GET /api/v1/appointments/history` returns patient's appointments sorted date descending (AC-1)
- [ ] Status filter returns only matching appointments within 500ms p95 (AC-2, NFR-002)
- [ ] Date range filter returns only appointments within specified range (AC-3)
- [ ] `GET /api/v1/appointments/history/export` returns PDF with all filtered records (AC-4)
- [ ] PDF generation completes within 5 seconds for 500+ records (AC-4)
- [ ] Pagination returns 20 records per page with total count (edge case)
- [ ] Empty history returns 200 with empty array and totalCount: 0 (edge case)
- [ ] Composite index `(PatientId, AppointmentDate, Status)` exists for query performance

## Implementation Checklist

- [ ] Create `AppointmentHistoryDto` with filter, item, and paginated response records
- [ ] Create `HistoryFilterValidator` with status enum check and date range validation
- [ ] Create `AppointmentHistoryRepository` with filtered query and async stream
- [ ] Create `AppointmentHistoryPdfGenerator` with QuestPDF table layout
- [ ] Create `AppointmentHistoryService` with pagination and PDF export orchestration
- [ ] Create `AppointmentHistoryController` with GET history and GET export endpoints
- [ ] Add composite index `(PatientId, AppointmentDate, Status)` to AppDbContext
- [ ] Register services in DependencyInjection.cs
