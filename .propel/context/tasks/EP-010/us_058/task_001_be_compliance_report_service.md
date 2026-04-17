# Task - TASK_001

## Requirement Reference

- User Story: us_058
- Story Location: .propel/context/tasks/EP-010/us_058/us_058.md
- Acceptance Criteria:
  - AC-1: Given a compliance report schedule is configured, When the scheduled time triggers, Then a HIPAA compliance report is generated covering access log summaries, audit event counts by type, and any detected anomalies for the reporting period.
  - AC-2: Given a compliance report is generated, When I access the reports section, Then the report is available for PDF download with the period, report date, and key metrics clearly labeled.
  - AC-3: Given a distribution list is configured, When a report is generated, Then it is automatically emailed as a PDF attachment to all recipients on the list.
  - AC-4: Given I want an on-demand report, When I trigger manual generation with a selected date range, Then the report is generated and available within 2 minutes for that range.
- Edge Cases:
  - What happens if report generation exceeds 2 minutes for a large date range? An async job is created; user is notified by email when the report is ready; a progress indicator is shown in the UI.
  - How does the system handle distribution list failures (bounced emails)? Delivery failures are logged; a retry is attempted once; persistent failures are surfaced in the admin notifications panel.

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
| Library | EF Core | 8.x |
| Library | QuestPDF | 2024.x |
| Library | Polly | 8.x |
| Library | FluentValidation | 11.x |
| Library | System.Threading.Channels | 8.x |
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

Implement the backend compliance report generation service, scheduled execution worker, PDF rendering, and email distribution pipeline for HIPAA-oriented compliance reports. A `IComplianceReportService` contract in the Application layer provides `GenerateAsync(ReportRequest)` for on-demand generation (AC-4) and is consumed by both the API controller and the scheduled worker. The `ComplianceReportGenerator` aggregates data from the `audit_records` table (US_056 foundation) to produce three report sections: access log summaries grouped by actor and resource type, audit event counts by event type for the period, and anomaly detection flags (unusual access volume, off-hours access, repeated failed attempts). The generator produces a structured `ComplianceReportData` object that feeds into a `ComplianceReportPdfRenderer` using QuestPDF to create a branded PDF with period label, generation date, and key metric summary (AC-2). A `ComplianceReportScheduleWorker` BackgroundService reads configured schedules from the `compliance_report_schedules` table and triggers generation at the configured intervals (AC-1). After generation, a `ComplianceReportDistributor` reads the `compliance_distribution_lists` table and emails the PDF attachment to all active recipients via the existing `IEmailSender` (AC-3). Delivery failures are logged to the `compliance_distribution_log` table; a single retry is attempted; persistent failures generate an admin notification (edge case 2). When on-demand generation is estimated to exceed 2 minutes (based on date range span heuristic), the endpoint returns 202 Accepted with a job ID; the `ComplianceReportJobWorker` processes it asynchronously and emails the admin when complete (edge case 1). Endpoints: `POST /api/v1/admin/reports` (trigger generation), `GET /api/v1/admin/reports` (list reports with pagination), `GET /api/v1/admin/reports/{id}` (report metadata), `GET /api/v1/admin/reports/{id}/download` (PDF download), `GET /api/v1/admin/reports/{id}/status` (job status for async reports).

## Dependent Tasks

- US_056 task_001 (requires `IAuditRecordService`, `audit_records` table, and query infrastructure)
- US_056 task_002 (requires partitioned audit table and retention policy for historical data access)
- US_058 task_002 (requires `compliance_reports`, `compliance_report_schedules`, `compliance_distribution_lists`, and `compliance_distribution_log` tables)
- US_015 task_001 (requires Admin role authorization via RBAC)

## Impacted Components

- New: `server/src/PropelIQ.Application/Compliance/IComplianceReportService.cs` (service contract)
- New: `server/src/PropelIQ.Application/Compliance/ComplianceReportData.cs` (report data model)
- New: `server/src/PropelIQ.Application/Compliance/ReportRequest.cs` (generation request DTO)
- New: `server/src/PropelIQ.Application/Compliance/ReportSummary.cs` (list response DTO)
- New: `server/src/PropelIQ.Infrastructure/Compliance/ComplianceReportGenerator.cs` (data aggregation)
- New: `server/src/PropelIQ.Infrastructure/Compliance/ComplianceReportPdfRenderer.cs` (QuestPDF rendering)
- New: `server/src/PropelIQ.Infrastructure/Compliance/ComplianceReportDistributor.cs` (email pipeline)
- New: `server/src/PropelIQ.Infrastructure/Compliance/ComplianceReportScheduleWorker.cs` (scheduled BackgroundService)
- New: `server/src/PropelIQ.Infrastructure/Compliance/ComplianceReportJobWorker.cs` (async job processor)
- New: `server/src/PropelIQ.Api/Controllers/Admin/ComplianceReportController.cs` (REST endpoints)
- New: `server/src/PropelIQ.Application/Compliance/Validators/ReportRequestValidator.cs` (FluentValidation)
- Modify: `server/src/PropelIQ.Api/Program.cs` (register compliance services and background workers)

## Implementation Plan

1. **Define the `ComplianceReportData` model and `IComplianceReportService` contract** in the Application layer:

```csharp
// server/src/PropelIQ.Application/Compliance/
//   ComplianceReportData.cs
namespace PropelIQ.Application.Compliance;

public sealed record ComplianceReportData
{
    public required Guid ReportId { get; init; }
    public required string ReportType { get; init; }
    public required DateTime PeriodStartUtc { get; init; }
    public required DateTime PeriodEndUtc { get; init; }
    public required DateTime GeneratedAtUtc { get; init; }
    public required AccessLogSummary AccessSummary
        { get; init; }
    public required IReadOnlyList<EventTypeCount>
        EventCounts { get; init; }
    public required IReadOnlyList<AnomalyFlag>
        Anomalies { get; init; }
    public required ReportMetrics KeyMetrics
        { get; init; }
}

public sealed record AccessLogSummary
{
    public required int TotalAccessEvents { get; init; }
    public required IReadOnlyList<ActorAccessGroup>
        ByActor { get; init; }
    public required IReadOnlyList<ResourceAccessGroup>
        ByResource { get; init; }
}

public sealed record ActorAccessGroup
{
    public required string ActorName { get; init; }
    public required string Role { get; init; }
    public required int AccessCount { get; init; }
}

public sealed record ResourceAccessGroup
{
    public required string ResourceType { get; init; }
    public required int AccessCount { get; init; }
}

public sealed record EventTypeCount
{
    public required string EventType { get; init; }
    public required int Count { get; init; }
}

public sealed record AnomalyFlag
{
    public required string AnomalyType { get; init; }
    public required string Description { get; init; }
    public required string Severity { get; init; }
    public required DateTime DetectedAtUtc { get; init; }
}

public sealed record ReportMetrics
{
    public required int TotalAuditEvents { get; init; }
    public required int UniqueActors { get; init; }
    public required int AnomalyCount { get; init; }
    public required int FailedAccessAttempts
        { get; init; }
}
```

```csharp
// server/src/PropelIQ.Application/Compliance/
//   IComplianceReportService.cs
namespace PropelIQ.Application.Compliance;

public interface IComplianceReportService
{
    Task<ReportGenerationResult> GenerateAsync(
        ReportRequest request,
        CancellationToken ct = default);

    Task<PagedResult<ReportSummary>> ListAsync(
        int page, int pageSize,
        CancellationToken ct = default);

    Task<ReportSummary?> GetAsync(
        Guid reportId,
        CancellationToken ct = default);

    Task<Stream?> DownloadPdfAsync(
        Guid reportId,
        CancellationToken ct = default);

    Task<ReportJobStatus?> GetJobStatusAsync(
        Guid jobId,
        CancellationToken ct = default);
}

public sealed record ReportGenerationResult
{
    public required Guid Id { get; init; }
    public required bool IsAsync { get; init; }
    public Guid? JobId { get; init; }
}
```

2. **Implement `ComplianceReportGenerator`** in the Infrastructure layer to aggregate audit data from the partitioned `audit_records` table. The generator queries three data sets using EF Core: (a) access log events grouped by actor and resource type, (b) all audit event counts grouped by event type, (c) anomaly detection via SQL queries checking for unusual access volume (> 2 standard deviations from mean), off-hours access (outside 06:00–22:00 local time), and repeated failed authentication attempts (> 5 in 10 minutes). Results are composed into `ComplianceReportData`. A date range span heuristic (> 90 days) determines if the report should be processed asynchronously.

3. **Implement `ComplianceReportPdfRenderer`** using QuestPDF to render `ComplianceReportData` into a branded PDF. The PDF includes: cover page with practice name, report type, period, and generation date; executive summary with `ReportMetrics` key figures (AC-2); access log summary table grouped by actor/role; event type counts bar chart; anomalies section with severity indicators; footer with page numbers and report ID. The renderer returns a `byte[]` persisted to the `compliance_reports.pdf_content` column.

4. **Implement `ComplianceReportScheduleWorker`** as a `BackgroundService` that polls `compliance_report_schedules` at startup and recalculates next-run times. Uses `PeriodicTimer` with 1-minute resolution to check if any schedule's next-run-time has passed. When triggered, invokes `IComplianceReportService.GenerateAsync` with the schedule's configured report type and period (AC-1). After successful generation, triggers distribution and updates the schedule's `last_run_at` and `next_run_at` timestamps. Supports daily, weekly, and monthly recurrence patterns.

5. **Implement `ComplianceReportDistributor`** that reads active recipients from `compliance_distribution_lists`, constructs an email with the PDF attachment and a summary body (report type, period, key metrics), and sends via `IEmailSender` (AC-3). Each delivery attempt is logged to `compliance_distribution_log` with status (Sent, Failed, Retried). On failure, a single retry is attempted after 60 seconds using Polly. Persistent failures (both attempts failed) create an entry in the admin notification system (edge case 2).

6. **Implement `ComplianceReportJobWorker`** as a `BackgroundService` consuming from a `System.Threading.Channels` bounded channel (capacity 50). When on-demand generation is estimated to exceed 2 minutes, the controller writes a job to the channel. The worker generates the report, persists the PDF, triggers distribution if configured, and sends a completion email to the requesting admin (edge case 1).

7. **Implement `ComplianceReportController`** with Admin-only endpoints:

```csharp
// server/src/PropelIQ.Api/Controllers/Admin/
//   ComplianceReportController.cs
namespace PropelIQ.Api.Controllers.Admin;

[ApiController]
[Route("api/v1/admin/reports")]
[Authorize(Roles = "Admin")]
public sealed class ComplianceReportController
    : ControllerBase
{
    private readonly IComplianceReportService _service;

    public ComplianceReportController(
        IComplianceReportService service)
        => _service = service;

    // POST /api/v1/admin/reports
    // Triggers report generation (AC-4)
    // Returns 200 with report ID for quick reports
    // Returns 202 with job ID for async (edge case 1)
    [HttpPost]
    public async Task<IActionResult> Generate(
        [FromBody] ReportRequest request,
        CancellationToken ct)
    {
        var result = await _service
            .GenerateAsync(request, ct);

        if (result.IsAsync)
            return Accepted(new
            {
                result.JobId,
                Status = "Generating"
            });

        return Ok(new
        {
            result.Id,
            Status = "Completed"
        });
    }

    // GET /api/v1/admin/reports
    // Lists reports with pagination (AC-2)
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken ct = default)
    {
        var result = await _service
            .ListAsync(page, pageSize, ct);
        return Ok(result);
    }

    // GET /api/v1/admin/reports/{id}
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(
        Guid id, CancellationToken ct)
    {
        var report = await _service
            .GetAsync(id, ct);
        return report is null
            ? NotFound() : Ok(report);
    }

    // GET /api/v1/admin/reports/{id}/download
    // Returns PDF file (AC-2)
    [HttpGet("{id:guid}/download")]
    public async Task<IActionResult> Download(
        Guid id, CancellationToken ct)
    {
        var stream = await _service
            .DownloadPdfAsync(id, ct);
        if (stream is null) return NotFound();

        return File(stream,
            "application/pdf",
            $"compliance-report-{id}.pdf");
    }

    // GET /api/v1/admin/reports/{id}/status
    // Job status for async reports (edge case 1)
    [HttpGet("{id:guid}/status")]
    public async Task<IActionResult> Status(
        Guid id, CancellationToken ct)
    {
        var status = await _service
            .GetJobStatusAsync(id, ct);
        return status is null
            ? NotFound() : Ok(status);
    }
}
```

8. **Register all services in `Program.cs`**: `IComplianceReportService` as scoped, `ComplianceReportPdfRenderer` as singleton, `ComplianceReportDistributor` as scoped, `ComplianceReportScheduleWorker` and `ComplianceReportJobWorker` as hosted services. Add FluentValidation `ReportRequestValidator` ensuring `ReportType` is a valid enum value and date range does not exceed 2 years.

## Current Project State

```text
propelIQ/
└── server/
    └── src/
        ├── PropelIQ.Api/
        │   ├── Controllers/
        │   │   └── Admin/
        │   │       ├── AuditLogController.cs          (US_056)
        │   │       └── ComplianceReportController.cs   (new)
        │   └── Program.cs                              (modify)
        ├── PropelIQ.Application/
        │   ├── Audit/
        │   │   ├── IAuditRecordService.cs             (US_056)
        │   │   └── AuditEvent.cs                      (US_056)
        │   └── Compliance/
        │       ├── IComplianceReportService.cs         (new)
        │       ├── ComplianceReportData.cs             (new)
        │       ├── ReportRequest.cs                    (new)
        │       ├── ReportSummary.cs                    (new)
        │       └── Validators/
        │           └── ReportRequestValidator.cs       (new)
        └── PropelIQ.Infrastructure/
            ├── Audit/
            │   ├── AuditRecordService.cs              (US_056)
            │   └── AuditRecordWriterWorker.cs         (US_056)
            └── Compliance/
                ├── ComplianceReportGenerator.cs        (new)
                ├── ComplianceReportPdfRenderer.cs      (new)
                ├── ComplianceReportDistributor.cs      (new)
                ├── ComplianceReportScheduleWorker.cs   (new)
                └── ComplianceReportJobWorker.cs        (new)
```

> Placeholder: Update on execution based on US_056 and US_058 task_002 completion state.

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | server/src/PropelIQ.Application/Compliance/IComplianceReportService.cs | Service contract with Generate, List, Get, Download, GetJobStatus |
| CREATE | server/src/PropelIQ.Application/Compliance/ComplianceReportData.cs | Report data model with access summaries, event counts, anomalies, metrics |
| CREATE | server/src/PropelIQ.Application/Compliance/ReportRequest.cs | Generation request DTO with reportType, periodStartUtc, periodEndUtc |
| CREATE | server/src/PropelIQ.Application/Compliance/ReportSummary.cs | List response DTO with report metadata |
| CREATE | server/src/PropelIQ.Application/Compliance/Validators/ReportRequestValidator.cs | FluentValidation for report type and date range |
| CREATE | server/src/PropelIQ.Infrastructure/Compliance/ComplianceReportGenerator.cs | Aggregates audit data into ComplianceReportData |
| CREATE | server/src/PropelIQ.Infrastructure/Compliance/ComplianceReportPdfRenderer.cs | QuestPDF renderer producing branded PDF |
| CREATE | server/src/PropelIQ.Infrastructure/Compliance/ComplianceReportDistributor.cs | Email distribution with retry and failure logging |
| CREATE | server/src/PropelIQ.Infrastructure/Compliance/ComplianceReportScheduleWorker.cs | BackgroundService polling schedule table |
| CREATE | server/src/PropelIQ.Infrastructure/Compliance/ComplianceReportJobWorker.cs | Async job processor via bounded channel |
| CREATE | server/src/PropelIQ.Api/Controllers/Admin/ComplianceReportController.cs | Admin-only REST endpoints for report generation and retrieval |
| MODIFY | server/src/PropelIQ.Api/Program.cs | Register compliance services, validators, and background workers |

## External References

- QuestPDF Documentation: https://www.questpdf.com/getting-started.html
- ASP.NET Core BackgroundService: https://learn.microsoft.com/en-us/aspnet/core/fundamentals/host/hosted-services
- System.Threading.Channels: https://learn.microsoft.com/en-us/dotnet/core/extensions/channels
- Polly Retry Policies: https://github.com/App-vNext/Polly#retry
- FluentValidation ASP.NET Integration: https://docs.fluentvalidation.net/en/latest/aspnet.html

## Build Commands

```bash
# Build backend
cd server
dotnet build

# Run backend
dotnet run --project src/PropelIQ.Api

# Test report generation:
# 1. POST /api/v1/admin/reports with
#    { "reportType": "HIPAA", "periodStartUtc": "...",
#      "periodEndUtc": "..." }
# 2. GET /api/v1/admin/reports to list
# 3. GET /api/v1/admin/reports/{id}/download for PDF
# 4. GET /api/v1/admin/reports/{id}/status for async jobs
```

## Implementation Validation Strategy

- [ ] On-demand report generation completes within 2 minutes for standard date ranges (AC-4)
- [ ] Generated PDF includes period, report date, and key metrics (AC-2)
- [ ] Scheduled worker triggers generation at configured intervals (AC-1)
- [ ] Distribution emails are sent with PDF attachment to all active recipients (AC-3)
- [ ] Async job returns 202 and notifies admin on completion (edge case 1)
- [ ] Email delivery failures are logged, retried once, and surfaced as admin notification (edge case 2)
- [ ] All endpoints restricted to Admin role

## Implementation Checklist

- [ ] Define ComplianceReportData, ReportRequest, ReportSummary DTOs and IComplianceReportService contract
- [ ] Implement ComplianceReportGenerator aggregating access logs, event counts, and anomalies from audit_records
- [ ] Implement ComplianceReportPdfRenderer using QuestPDF with branded cover page, metrics summary, and tables
- [ ] Implement ComplianceReportScheduleWorker BackgroundService with daily/weekly/monthly recurrence
- [ ] Implement ComplianceReportDistributor with email delivery, retry, and failure notification
- [ ] Implement ComplianceReportJobWorker for async generation via bounded channel
- [ ] Create ComplianceReportController with POST generate, GET list, GET download, GET status endpoints
- [ ] Register all services and background workers in Program.cs with FluentValidation
