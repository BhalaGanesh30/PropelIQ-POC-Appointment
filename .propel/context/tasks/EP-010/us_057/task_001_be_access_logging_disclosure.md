# Task - TASK_001

## Requirement Reference

- User Story: us_057
- Story Location: .propel/context/tasks/EP-010/us_057/us_057.md
- Acceptance Criteria:
  - AC-1: Given any authorized user views a patient record, clinical profile, or document, When the access request is processed, Then an access log entry is written recording the accessor's identity, role, accessed resource, patient ID, and timestamp.
  - AC-2: Given a patient submits a disclosure request for their access records, When the request is received, Then the system compiles all access log entries for the patient's data within the requested date range and prepares a structured disclosure response.
  - AC-3: Given a disclosure response is prepared, When I (as an authorized staff member) review and approve it, Then the disclosure is delivered to the patient via email or secure download link within the configured SLA.
  - AC-4: Given the patient data access log is queried, When I filter by patient ID and date range, Then all access events are returned in chronological order with actor role and resource details.
- Edge Cases:
  - What happens if a patient requests access records for a very long time period? Async job is created; patient is notified when the report is ready; it is available for secure download for 48 hours.
  - How does the system handle bulk access by reporting processes? Automated system-level accesses are flagged with a `System` actor role; they are included in the log but clearly distinguished from human-initiated access.

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
| Library | System.Threading.Channels | 8.x |
| Library | FluentValidation | 11.x |
| Library | Polly | 8.x |
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

Implement the patient data access logging interceptor, disclosure request workflow API, and patient-scoped access log query endpoint. A `PatientDataAccessFilter` (ASP.NET Core action filter) intercepts every controller action that reads patient data — patient records, clinical profiles, documents, and clinical facts — and emits a `DataAccess` audit event via `IAuditRecordService.WriteAsync()` from US_056 task_001 (AC-1). The event includes the accessor's user ID, role claim from JWT, the accessed resource type, entity ID, patient ID, and UTC timestamp. Automated system-level accesses (e.g., reporting, background workers) use a `System` actor role to distinguish them from human-initiated access (edge case 2). A `DisclosureRequest` entity tracks patient-initiated disclosure requests through a state machine: `Submitted` → `Compiling` → `PendingReview` → `Approved` → `Delivered` (or `Rejected`). The `POST /api/v1/patients/me/disclosure-requests` endpoint allows authenticated patients to submit a request with a date range (AC-2). A `DisclosureCompilationWorker` background service compiles all `DataAccess` audit records for the patient within the requested date range into a structured `DisclosureReport` JSON document stored in the `disclosure_reports` table. For long time periods (edge case 1), compilation is asynchronous — the patient receives a notification when the report is ready with a secure download link valid for 48 hours. A `PUT /api/v1/admin/disclosure-requests/{id}/review` endpoint allows authorized staff to approve or reject the disclosure (AC-3). On approval, the disclosure is delivered via email (secure download link generated with HMAC-signed token, reusing `IReminderTokenService` from US_027 task_002) or direct secure download. A `GET /api/v1/admin/access-logs` endpoint restricted to Admin/Staff roles returns patient-scoped access logs filtered by patient ID and date range with chronological ordering and pagination (AC-4).

## Dependent Tasks

- US_056 task_001 (requires `IAuditRecordService` with `WriteAsync` for audit event emission and `AuditRecordWriterWorker`)
- US_056 task_002 (requires partitioned `audit_records` table with indexes for date-range queries)
- US_015 task_001 (requires RBAC policies for Admin and Staff role authorization)
- US_014 task_001 (requires JWT bearer authentication with role claims)

## Impacted Components

- New: `server/src/PropelIQ.Api/Filters/PatientDataAccessFilter.cs` (action filter emitting DataAccess audit events)
- New: `server/src/PropelIQ.Domain/Entities/DisclosureRequest.cs` (entity with state machine)
- New: `server/src/PropelIQ.Domain/Entities/DisclosureReport.cs` (compiled report entity)
- New: `server/src/PropelIQ.Application/Disclosure/IDisclosureService.cs` (service contract)
- New: `server/src/PropelIQ.Infrastructure/Disclosure/DisclosureService.cs` (service implementation)
- New: `server/src/PropelIQ.Infrastructure/Disclosure/DisclosureCompilationWorker.cs` (async compilation BackgroundService)
- New: `server/src/PropelIQ.Api/Controllers/Patient/DisclosureRequestController.cs` (patient-facing endpoints)
- New: `server/src/PropelIQ.Api/Controllers/Admin/AccessLogController.cs` (admin access log query)
- New: `server/src/PropelIQ.Api/Controllers/Admin/DisclosureReviewController.cs` (staff review/approve)
- New: `server/src/PropelIQ.Application/Disclosure/DisclosureRequestValidator.cs` (FluentValidation)
- New: `server/src/PropelIQ.Infrastructure/Persistence/Migrations/<timestamp>_DisclosureRequestTables.cs` (migration)
- Modify: `server/src/PropelIQ.Infrastructure/Persistence/AppDbContext.cs` (add DbSets for DisclosureRequest, DisclosureReport)
- Modify: `server/src/PropelIQ.Api/Program.cs` (register filter, services, and background worker)

## Implementation Plan

1. **Create the `PatientDataAccessFilter`** action filter that intercepts patient data read operations and emits audit events (AC-1):

```csharp
// server/src/PropelIQ.Api/Filters/
//   PatientDataAccessFilter.cs
using PropelIQ.Application.Audit;

public sealed class PatientDataAccessFilter
    : IAsyncActionFilter
{
    private readonly IAuditRecordService _audit;

    public PatientDataAccessFilter(
        IAuditRecordService audit)
    {
        _audit = audit;
    }

    public async Task OnActionExecutionAsync(
        ActionExecutingContext context,
        ActionExecutionDelegate next)
    {
        var result = await next();

        // Only log successful reads (2xx responses)
        if (result.Result is ObjectResult obj
            && obj.StatusCode >= 200
            && obj.StatusCode < 300)
        {
            var user = context.HttpContext.User;
            var userId = Guid.Parse(
                user.FindFirstValue(
                    ClaimTypes.NameIdentifier)!);
            var role = user.FindFirstValue(
                ClaimTypes.Role) ?? "Unknown";

            // Extract patient ID from route or action
            //   arguments
            var patientId = ExtractPatientId(context);
            if (patientId == null) return;

            var resourceType =
                context.Controller.GetType().Name
                    .Replace("Controller", "");
            var entityId =
                ExtractEntityId(context);

            await _audit.WriteAsync(new AuditEvent
            {
                UserId = userId,
                EventType = "DataAccess",
                EntityType = resourceType,
                EntityId = entityId,
                Details = new Dictionary<string, object>
                {
                    ["patientId"] =
                        patientId.Value.ToString(),
                    ["accessorRole"] = role,
                    ["httpMethod"] =
                        context.HttpContext.Request.Method,
                    ["path"] =
                        context.HttpContext.Request.Path
                            .Value ?? ""
                }
            });
        }
    }

    private static Guid? ExtractPatientId(
        ActionExecutingContext context)
    {
        // Check route values first
        if (context.ActionArguments
            .TryGetValue("patientId", out var pid)
            && pid is Guid patientGuid)
            return patientGuid;

        // Check "me" pattern — patient accessing
        //   own data
        var path = context.HttpContext.Request.Path
            .Value ?? "";
        if (path.Contains("/patients/me"))
        {
            var userId = context.HttpContext.User
                .FindFirstValue(
                    ClaimTypes.NameIdentifier);
            return userId != null
                ? Guid.Parse(userId) : null;
        }

        return null;
    }

    private static Guid? ExtractEntityId(
        ActionExecutingContext context)
    {
        if (context.ActionArguments
            .TryGetValue("id", out var id)
            && id is Guid entityGuid)
            return entityGuid;
        return null;
    }
}
```

Apply the filter to patient-data controllers via `[ServiceFilter(typeof(PatientDataAccessFilter))]` on controllers: `PatientController`, `ClinicalDocumentController`, `ClinicalFactController`, `IntakeController`, `InsuranceProfileController`. For system-level bulk access (edge case 2), the filter detects a `System` role from the service account JWT claim and records it as actor role `System`.

2. **Create `DisclosureRequest` and `DisclosureReport` entities** with the state machine:

```csharp
// server/src/PropelIQ.Domain/Entities/
//   DisclosureRequest.cs
public sealed class DisclosureRequest
{
    public Guid Id { get; set; }
    public Guid PatientId { get; set; }
    public DateTime FromDateUtc { get; set; }
    public DateTime ToDateUtc { get; set; }
    public DisclosureStatus Status { get; set; }
    public DateTime RequestedAt { get; set; }
    public DateTime? CompiledAt { get; set; }
    public Guid? ReviewedBy { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? ReviewNotes { get; set; }
    public DateTime? DeliveredAt { get; set; }
    public string? DeliveryMethod { get; set; }
    public Guid? ReportId { get; set; }
    public DisclosureReport? Report { get; set; }
}

public enum DisclosureStatus
{
    Submitted,
    Compiling,
    PendingReview,
    Approved,
    Delivered,
    Rejected
}

// DisclosureReport.cs
public sealed class DisclosureReport
{
    public Guid Id { get; set; }
    public Guid DisclosureRequestId { get; set; }
    public string ReportJson { get; set; } = "";
    public int AccessEventCount { get; set; }
    public DateTime GeneratedAt { get; set; }
    public string? DownloadToken { get; set; }
    public DateTime? DownloadExpiresAt { get; set; }
}
```

3. **Create the database migration** for disclosure tables:

```sql
-- In EF Core migration via migrationBuilder.Sql()
CREATE TABLE app.disclosure_requests (
    id UUID PRIMARY KEY,
    patient_id UUID NOT NULL
        REFERENCES app.patients(id),
    from_date_utc TIMESTAMPTZ NOT NULL,
    to_date_utc TIMESTAMPTZ NOT NULL,
    status VARCHAR(20) NOT NULL DEFAULT 'Submitted',
    requested_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    compiled_at TIMESTAMPTZ,
    reviewed_by UUID REFERENCES app.users(id),
    reviewed_at TIMESTAMPTZ,
    review_notes TEXT,
    delivered_at TIMESTAMPTZ,
    delivery_method VARCHAR(20),
    report_id UUID
);

CREATE INDEX ix_disclosure_requests_patient
    ON app.disclosure_requests (patient_id, requested_at DESC);
CREATE INDEX ix_disclosure_requests_status
    ON app.disclosure_requests (status)
    WHERE status NOT IN ('Delivered', 'Rejected');

CREATE TABLE app.disclosure_reports (
    id UUID PRIMARY KEY,
    disclosure_request_id UUID NOT NULL
        REFERENCES app.disclosure_requests(id),
    report_json JSONB NOT NULL,
    access_event_count INT NOT NULL,
    generated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    download_token TEXT,
    download_expires_at TIMESTAMPTZ
);

ALTER TABLE app.disclosure_requests
    ADD CONSTRAINT fk_disclosure_report
    FOREIGN KEY (report_id)
    REFERENCES app.disclosure_reports(id);
```

4. **Implement `DisclosureCompilationWorker`** as a BackgroundService that processes submitted disclosure requests (AC-2):

```csharp
// server/src/PropelIQ.Infrastructure/Disclosure/
//   DisclosureCompilationWorker.cs
public sealed class DisclosureCompilationWorker
    : BackgroundService
{
    // Polls every 30 seconds for Submitted requests
    // For each Submitted request:
    // 1. Transition status to Compiling
    // 2. Query audit_records WHERE
    //    details->>'patientId' = patientId
    //    AND event_type = 'DataAccess'
    //    AND created_at BETWEEN from_date AND to_date
    //    ORDER BY created_at ASC
    // 3. Compile into DisclosureReport JSON with:
    //    - accessor name, role, resource type
    //    - entity ID, access timestamp
    //    - access purpose (from details)
    // 4. Store report in disclosure_reports table
    // 5. Transition status to PendingReview
    // 6. Log compilation audit event
    // Edge case 1: If query returns > 10,000 rows,
    //   stream in batches of 1,000 to avoid memory
    //   pressure
}
```

5. **Implement patient-facing disclosure endpoints** in `DisclosureRequestController`:

```csharp
// server/src/PropelIQ.Api/Controllers/Patient/
//   DisclosureRequestController.cs
[ApiController]
[Route("api/v1/patients/me/disclosure-requests")]
[Authorize(Policy = "PatientOnly")]
public sealed class DisclosureRequestController
    : ControllerBase
{
    private readonly IDisclosureService _service;

    // AC-2: Submit disclosure request
    [HttpPost]
    public async Task<IActionResult> Submit(
        [FromBody] SubmitDisclosureRequest request,
        CancellationToken ct)
    {
        var patientId = GetPatientId();
        var id = await _service.SubmitAsync(
            patientId,
            request.FromDateUtc,
            request.ToDateUtc,
            ct);
        return CreatedAtAction(
            nameof(GetStatus), new { id },
            new { id, status = "Submitted" });
    }

    // Patient checks request status
    [HttpGet("{id}")]
    public async Task<IActionResult> GetStatus(
        Guid id, CancellationToken ct)
    {
        var request = await _service
            .GetByIdForPatientAsync(
                GetPatientId(), id, ct);
        if (request is null) return NotFound();
        return Ok(request);
    }

    // Patient lists their disclosure requests
    [HttpGet]
    public async Task<IActionResult> List(
        CancellationToken ct)
    {
        var requests = await _service
            .ListForPatientAsync(
                GetPatientId(), ct);
        return Ok(requests);
    }

    // Edge case 1: Secure download of compiled
    //   report (48-hour link)
    [HttpGet("{id}/download")]
    public async Task<IActionResult> Download(
        Guid id,
        [FromQuery] string token,
        CancellationToken ct)
    {
        var report = await _service
            .GetReportForDownloadAsync(
                GetPatientId(), id, token, ct);
        if (report is null)
            return NotFound();
        if (report.IsExpired)
            return StatusCode(410,
                new { message =
                    "Download link has expired" });

        return File(
            report.Content,
            "application/json",
            $"disclosure-report-{id}.json");
    }

    private Guid GetPatientId() =>
        Guid.Parse(User.FindFirstValue(
            ClaimTypes.NameIdentifier)!);
}
```

6. **Implement staff review/approve endpoints** in `DisclosureReviewController` (AC-3):

```csharp
// server/src/PropelIQ.Api/Controllers/Admin/
//   DisclosureReviewController.cs
[ApiController]
[Route("api/v1/admin/disclosure-requests")]
[Authorize(Policy = "StaffOrAdmin")]
public sealed class DisclosureReviewController
    : ControllerBase
{
    private readonly IDisclosureService _service;

    // List pending disclosure requests
    [HttpGet]
    public async Task<IActionResult> ListPending(
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken ct = default)
    {
        var result = await _service
            .ListForReviewAsync(
                status, page, pageSize, ct);
        return Ok(result);
    }

    // AC-3: Review and approve/reject
    [HttpPut("{id}/review")]
    public async Task<IActionResult> Review(
        Guid id,
        [FromBody] ReviewDisclosureRequest request,
        CancellationToken ct)
    {
        var reviewerId = GetUserId();
        var success = await _service.ReviewAsync(
            id, reviewerId,
            request.Approved,
            request.Notes,
            ct);
        if (!success) return NotFound();
        return Ok(new
        {
            status = request.Approved
                ? "Approved" : "Rejected"
        });
    }

    // View compiled report before approval
    [HttpGet("{id}/report")]
    public async Task<IActionResult> ViewReport(
        Guid id, CancellationToken ct)
    {
        var report = await _service
            .GetReportForReviewAsync(id, ct);
        if (report is null) return NotFound();
        return Ok(report);
    }

    private Guid GetUserId() =>
        Guid.Parse(User.FindFirstValue(
            ClaimTypes.NameIdentifier)!);
}
```

7. **Implement `AccessLogController`** for admin patient-scoped access log queries (AC-4):

```csharp
// server/src/PropelIQ.Api/Controllers/Admin/
//   AccessLogController.cs
[ApiController]
[Route("api/v1/admin/access-logs")]
[Authorize(Policy = "StaffOrAdmin")]
public sealed class AccessLogController
    : ControllerBase
{
    private readonly AppDbContext _db;

    // AC-4: Filter by patient ID and date range
    [HttpGet]
    public async Task<IActionResult> Query(
        [FromQuery] Guid patientId,
        [FromQuery] DateTime? fromUtc,
        [FromQuery] DateTime? toUtc,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken ct = default)
    {
        var query = _db.AuditRecords
            .Where(r =>
                r.EventType == "DataAccess"
                && EF.Functions.JsonContains(
                    r.Details,
                    $"{{\"patientId\":\"{patientId}\"}}"
                ));

        if (fromUtc.HasValue)
            query = query.Where(
                r => r.CreatedAt >= fromUtc);
        if (toUtc.HasValue)
            query = query.Where(
                r => r.CreatedAt <= toUtc);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderBy(r => r.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return Ok(new { total, items });
    }
}
```

The chronological ordering (AC-4) uses `OrderBy(r => r.CreatedAt)` ascending. The JSONB `details->>'patientId'` filter leverages the GIN index from US_056 task_002 for partition pruning on `created_at` combined with JSONB containment.

8. **Implement `DisclosureService`** with delivery logic (AC-3). On approval, the service generates an HMAC-signed download token (reusing `IReminderTokenService` pattern from US_027 task_002) with 48-hour expiry, sends an email with the secure download link to the patient, and transitions the disclosure request to `Delivered` status. Register all services, the filter, and the background worker in `Program.cs`.

## Current Project State

```text
propelIQ/
├── server/
│   └── src/
│       ├── PropelIQ.Api/
│       │   ├── Program.cs                              (modify)
│       │   ├── Filters/
│       │   │   └── PatientDataAccessFilter.cs          (new)
│       │   └── Controllers/
│       │       ├── Patient/
│       │       │   └── DisclosureRequestController.cs  (new)
│       │       └── Admin/
│       │           ├── AccessLogController.cs          (new)
│       │           └── DisclosureReviewController.cs   (new)
│       ├── PropelIQ.Application/
│       │   └── Disclosure/
│       │       ├── IDisclosureService.cs               (new)
│       │       └── DisclosureRequestValidator.cs       (new)
│       ├── PropelIQ.Domain/
│       │   └── Entities/
│       │       ├── DisclosureRequest.cs                (new)
│       │       └── DisclosureReport.cs                 (new)
│       └── PropelIQ.Infrastructure/
│           ├── Disclosure/
│           │   ├── DisclosureService.cs                (new)
│           │   └── DisclosureCompilationWorker.cs      (new)
│           └── Persistence/
│               ├── AppDbContext.cs                      (modify)
│               └── Migrations/
│                   └── <timestamp>_DisclosureRequestTables.cs (new)
└── docker-compose.yml
```

> Placeholder: Update on execution based on US_056 task completion state.

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | server/src/PropelIQ.Api/Filters/PatientDataAccessFilter.cs | Action filter emitting DataAccess audit events on patient data reads |
| CREATE | server/src/PropelIQ.Domain/Entities/DisclosureRequest.cs | Entity with state machine (Submitted→Compiling→PendingReview→Approved→Delivered) |
| CREATE | server/src/PropelIQ.Domain/Entities/DisclosureReport.cs | Compiled report entity with JSONB content and download token |
| CREATE | server/src/PropelIQ.Application/Disclosure/IDisclosureService.cs | Service contract for disclosure submission, compilation, review, and delivery |
| CREATE | server/src/PropelIQ.Application/Disclosure/DisclosureRequestValidator.cs | FluentValidation for date range, max range limit |
| CREATE | server/src/PropelIQ.Infrastructure/Disclosure/DisclosureService.cs | Implementation with HMAC download token and email delivery |
| CREATE | server/src/PropelIQ.Infrastructure/Disclosure/DisclosureCompilationWorker.cs | BackgroundService compiling access logs into disclosure reports |
| CREATE | server/src/PropelIQ.Api/Controllers/Patient/DisclosureRequestController.cs | Patient-facing POST submit, GET status, GET download endpoints |
| CREATE | server/src/PropelIQ.Api/Controllers/Admin/DisclosureReviewController.cs | Staff PUT review/approve, GET pending list, GET report preview |
| CREATE | server/src/PropelIQ.Api/Controllers/Admin/AccessLogController.cs | Admin GET access logs filtered by patient ID and date range |
| CREATE | server/src/PropelIQ.Infrastructure/Persistence/Migrations/\<timestamp\>_DisclosureRequestTables.cs | Migration for disclosure_requests and disclosure_reports tables |
| MODIFY | server/src/PropelIQ.Infrastructure/Persistence/AppDbContext.cs | Add DbSets for DisclosureRequest and DisclosureReport |
| MODIFY | server/src/PropelIQ.Api/Program.cs | Register PatientDataAccessFilter, disclosure services, and compilation worker |

## External References

- ASP.NET Core action filters: https://learn.microsoft.com/en-us/aspnet/core/mvc/controllers/filters?view=aspnetcore-8.0
- ASP.NET Core BackgroundService: https://learn.microsoft.com/en-us/aspnet/core/fundamentals/host/hosted-services?view=aspnetcore-8.0
- EF Core JSONB querying (Npgsql): https://www.npgsql.org/efcore/mapping/json.html
- PostgreSQL JSONB containment: https://www.postgresql.org/docs/15/datatype-json.html#JSON-CONTAINMENT
- FluentValidation: https://docs.fluentvalidation.net/en/latest/
- HMAC-SHA256 in .NET: https://learn.microsoft.com/en-us/dotnet/api/system.security.cryptography.hmacsha256
- FR-AC-002 (access logging + disclosure): .propel/context/docs/spec.md
- NFR-010 (immutable audit evidence): .propel/context/docs/design.md

## Build Commands

```bash
# Generate migration
dotnet ef migrations add DisclosureRequestTables \
  --project server/src/PropelIQ.Infrastructure \
  --startup-project server/src/PropelIQ.Api \
  --output-dir Persistence/Migrations

# Apply migration
dotnet ef database update \
  --project server/src/PropelIQ.Infrastructure \
  --startup-project server/src/PropelIQ.Api

# Build backend
dotnet build server/PropelIQ.sln

# Run API
dotnet run --project server/src/PropelIQ.Api
```

## Implementation Validation Strategy

- [ ] Patient data access emits DataAccess audit event with accessor identity, role, resource, patient ID, and timestamp (AC-1)
- [ ] System-level bulk accesses are logged with `System` actor role (edge case 2)
- [ ] Patient can submit disclosure request with date range and receives 201 Created (AC-2)
- [ ] DisclosureCompilationWorker compiles access logs into structured report (AC-2)
- [ ] Staff can review and approve disclosure; approved disclosure delivers download link to patient (AC-3)
- [ ] Long-period requests produce async compilation with 48-hour download link (edge case 1)
- [ ] Access log query returns chronologically ordered results filtered by patient ID and date range (AC-4)
- [ ] All disclosure state transitions are audit-logged via IAuditRecordService

## Implementation Checklist

- [ ] Create PatientDataAccessFilter emitting DataAccess audit events on successful patient data reads
- [ ] Create DisclosureRequest and DisclosureReport domain entities with state machine
- [ ] Create database migration for disclosure_requests and disclosure_reports tables with indexes
- [ ] Implement DisclosureCompilationWorker BackgroundService compiling access logs into reports
- [ ] Implement patient-facing endpoints: submit, status, list, and secure download
- [ ] Implement staff review/approve endpoints with disclosure delivery (email + HMAC download link)
- [ ] Implement AccessLogController with patient ID and date range filtering (AC-4)
- [ ] Register filter, services, and background worker in Program.cs
