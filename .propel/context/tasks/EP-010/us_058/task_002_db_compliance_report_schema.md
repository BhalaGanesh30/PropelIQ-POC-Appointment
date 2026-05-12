# Task - TASK_002

## Requirement Reference

- User Story: us_058
- Story Location: .propel/context/tasks/EP-010/us_058/us_058.md
- Acceptance Criteria:
  - AC-1: Given a compliance report schedule is configured, When the scheduled time triggers, Then a HIPAA compliance report is generated covering access log summaries, audit event counts by type, and any detected anomalies for the reporting period.
  - AC-2: Given a compliance report is generated, When I access the reports section, Then the report is available for PDF download with the period, report date, and key metrics clearly labeled.
  - AC-3: Given a distribution list is configured, When a report is generated, Then it is automatically emailed as a PDF attachment to all recipients on the list.
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
| Backend | N/A | N/A |
| Database | PostgreSQL with pgvector | 15.x |
| Library | EF Core | 8.x |
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

Create the database schema and EF Core migration for compliance report storage, scheduling configuration, email distribution lists, and delivery tracking. The `compliance_reports` table stores generated report metadata and PDF content with status tracking (Pending, Generating, Completed, Failed) enabling the report listing and download endpoints (AC-2). The `compliance_report_schedules` table stores admin-configured schedules with recurrence pattern (daily, weekly, monthly), report type, and next-run timestamp enabling the scheduled generation worker (AC-1). The `compliance_distribution_lists` table stores email recipients per schedule with active/inactive status enabling automatic distribution (AC-3). The `compliance_distribution_log` table records every delivery attempt with status (Sent, Failed, Retried), error details, and timestamps enabling failure tracking (edge case 2). The `compliance_report_jobs` table tracks async generation jobs with status and progress percentage enabling the progress indicator for long-running reports (edge case 1). Composite indexes support the query patterns used by the report listing API, schedule polling, and distribution logging.

## Dependent Tasks

- US_056 task_002 (requires partitioned `audit_records` table as the data source for report aggregation)
- US_010 task_001 (requires `audit_records` base table foundation)

## Impacted Components

- New: `server/src/PropelIQ.Infrastructure/Persistence/Migrations/<timestamp>_AddComplianceReportSchema.cs` (EF Core migration)
- New: `server/src/PropelIQ.Domain/Compliance/ComplianceReport.cs` (entity)
- New: `server/src/PropelIQ.Domain/Compliance/ComplianceReportSchedule.cs` (entity)
- New: `server/src/PropelIQ.Domain/Compliance/DistributionListEntry.cs` (entity)
- New: `server/src/PropelIQ.Domain/Compliance/DistributionLog.cs` (entity)
- New: `server/src/PropelIQ.Domain/Compliance/ComplianceReportJob.cs` (entity)
- Modify: `server/src/PropelIQ.Infrastructure/Persistence/AppDbContext.cs` (add DbSets and Fluent API configuration)

## Implementation Plan

1. **Create `ComplianceReport` entity** in the Domain layer:

```csharp
// server/src/PropelIQ.Domain/Compliance/
//   ComplianceReport.cs
namespace PropelIQ.Domain.Compliance;

public sealed class ComplianceReport
{
    public Guid Id { get; set; }
    public string ReportType { get; set; } = default!;
    public DateTime PeriodStartUtc { get; set; }
    public DateTime PeriodEndUtc { get; set; }
    public string Status { get; set; } = "Pending";
    public DateTime GeneratedAtUtc { get; set; }
    public Guid GeneratedByUserId { get; set; }
    public byte[]? PdfContent { get; set; }
    public int TotalAuditEvents { get; set; }
    public int UniqueActors { get; set; }
    public int AnomalyCount { get; set; }
    public Guid? ScheduleId { get; set; }
    public ComplianceReportSchedule? Schedule
        { get; set; }
}
```

2. **Create `ComplianceReportSchedule` entity** for recurrence configuration:

```csharp
// server/src/PropelIQ.Domain/Compliance/
//   ComplianceReportSchedule.cs
namespace PropelIQ.Domain.Compliance;

public sealed class ComplianceReportSchedule
{
    public Guid Id { get; set; }
    public string ReportType { get; set; } = default!;
    public string RecurrencePattern { get; set; }
        = "Monthly";
    public int DayOfMonth { get; set; } = 1;
    public int? DayOfWeek { get; set; }
    public TimeOnly ScheduledTimeUtc { get; set; }
    public DateTime? LastRunAtUtc { get; set; }
    public DateTime NextRunAtUtc { get; set; }
    public bool IsActive { get; set; } = true;
    public Guid CreatedByUserId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public ICollection<DistributionListEntry>
        DistributionList { get; set; } = [];
    public ICollection<ComplianceReport>
        Reports { get; set; } = [];
}
```

3. **Create `DistributionListEntry` entity** for email recipients:

```csharp
// server/src/PropelIQ.Domain/Compliance/
//   DistributionListEntry.cs
namespace PropelIQ.Domain.Compliance;

public sealed class DistributionListEntry
{
    public Guid Id { get; set; }
    public Guid ScheduleId { get; set; }
    public string Email { get; set; } = default!;
    public string DisplayName { get; set; } = default!;
    public bool IsActive { get; set; } = true;
    public DateTime AddedAtUtc { get; set; }
    public ComplianceReportSchedule Schedule
        { get; set; } = default!;
}
```

4. **Create `DistributionLog` entity** for delivery tracking:

```csharp
// server/src/PropelIQ.Domain/Compliance/
//   DistributionLog.cs
namespace PropelIQ.Domain.Compliance;

public sealed class DistributionLog
{
    public Guid Id { get; set; }
    public Guid ReportId { get; set; }
    public string RecipientEmail { get; set; }
        = default!;
    public string Status { get; set; } = "Pending";
    public string? ErrorDetails { get; set; }
    public int AttemptCount { get; set; } = 0;
    public DateTime AttemptedAtUtc { get; set; }
    public DateTime? DeliveredAtUtc { get; set; }
    public ComplianceReport Report { get; set; }
        = default!;
}
```

5. **Create `ComplianceReportJob` entity** for async job tracking:

```csharp
// server/src/PropelIQ.Domain/Compliance/
//   ComplianceReportJob.cs
namespace PropelIQ.Domain.Compliance;

public sealed class ComplianceReportJob
{
    public Guid Id { get; set; }
    public Guid? ReportId { get; set; }
    public string ReportType { get; set; } = default!;
    public DateTime PeriodStartUtc { get; set; }
    public DateTime PeriodEndUtc { get; set; }
    public string Status { get; set; } = "Queued";
    public int ProgressPercent { get; set; } = 0;
    public Guid RequestedByUserId { get; set; }
    public DateTime RequestedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public string? ErrorMessage { get; set; }
    public ComplianceReport? Report { get; set; }
}
```

6. **Create EF Core migration** with the following SQL schema:

```sql
-- compliance_reports table
CREATE TABLE app.compliance_reports (
    id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    report_type     varchar(50) NOT NULL,
    period_start_utc timestamptz NOT NULL,
    period_end_utc  timestamptz NOT NULL,
    status          varchar(20) NOT NULL DEFAULT 'Pending',
    generated_at_utc timestamptz NOT NULL DEFAULT now(),
    generated_by_user_id uuid NOT NULL
        REFERENCES app.users(id),
    pdf_content     bytea,
    total_audit_events int NOT NULL DEFAULT 0,
    unique_actors   int NOT NULL DEFAULT 0,
    anomaly_count   int NOT NULL DEFAULT 0,
    schedule_id     uuid
        REFERENCES app.compliance_report_schedules(id)
);

CREATE INDEX ix_compliance_reports_status_generated
    ON app.compliance_reports (status, generated_at_utc DESC);
CREATE INDEX ix_compliance_reports_schedule
    ON app.compliance_reports (schedule_id, generated_at_utc DESC)
    WHERE schedule_id IS NOT NULL;

-- compliance_report_schedules table
CREATE TABLE app.compliance_report_schedules (
    id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    report_type     varchar(50) NOT NULL,
    recurrence_pattern varchar(20) NOT NULL
        DEFAULT 'Monthly',
    day_of_month    int NOT NULL DEFAULT 1,
    day_of_week     int,
    scheduled_time_utc time NOT NULL,
    last_run_at_utc timestamptz,
    next_run_at_utc timestamptz NOT NULL,
    is_active       boolean NOT NULL DEFAULT true,
    created_by_user_id uuid NOT NULL
        REFERENCES app.users(id),
    created_at_utc  timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX ix_schedules_next_run
    ON app.compliance_report_schedules
    (next_run_at_utc)
    WHERE is_active = true;

-- compliance_distribution_lists table
CREATE TABLE app.compliance_distribution_lists (
    id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    schedule_id     uuid NOT NULL
        REFERENCES app.compliance_report_schedules(id)
        ON DELETE CASCADE,
    email           varchar(256) NOT NULL,
    display_name    varchar(200) NOT NULL,
    is_active       boolean NOT NULL DEFAULT true,
    added_at_utc    timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX ix_distribution_schedule_active
    ON app.compliance_distribution_lists
    (schedule_id)
    WHERE is_active = true;

-- compliance_distribution_log table
CREATE TABLE app.compliance_distribution_log (
    id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    report_id       uuid NOT NULL
        REFERENCES app.compliance_reports(id),
    recipient_email varchar(256) NOT NULL,
    status          varchar(20) NOT NULL DEFAULT 'Pending',
    error_details   text,
    attempt_count   int NOT NULL DEFAULT 0,
    attempted_at_utc timestamptz NOT NULL DEFAULT now(),
    delivered_at_utc timestamptz
);

CREATE INDEX ix_distribution_log_report
    ON app.compliance_distribution_log
    (report_id, attempted_at_utc DESC);

-- compliance_report_jobs table
CREATE TABLE app.compliance_report_jobs (
    id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    report_id       uuid
        REFERENCES app.compliance_reports(id),
    report_type     varchar(50) NOT NULL,
    period_start_utc timestamptz NOT NULL,
    period_end_utc  timestamptz NOT NULL,
    status          varchar(20) NOT NULL DEFAULT 'Queued',
    progress_percent int NOT NULL DEFAULT 0,
    requested_by_user_id uuid NOT NULL
        REFERENCES app.users(id),
    requested_at_utc timestamptz NOT NULL DEFAULT now(),
    completed_at_utc timestamptz,
    error_message   text
);

CREATE INDEX ix_report_jobs_status
    ON app.compliance_report_jobs (status)
    WHERE status IN ('Queued', 'Processing');
```

## Current Project State

```text
propelIQ/
└── server/
    └── src/
        ├── PropelIQ.Domain/
        │   └── Compliance/
        │       ├── ComplianceReport.cs                  (new)
        │       ├── ComplianceReportSchedule.cs          (new)
        │       ├── DistributionListEntry.cs             (new)
        │       ├── DistributionLog.cs                   (new)
        │       └── ComplianceReportJob.cs               (new)
        └── PropelIQ.Infrastructure/
            └── Persistence/
                ├── AppDbContext.cs                       (modify)
                └── Migrations/
                    └── <timestamp>_AddComplianceReportSchema.cs (new)
```

> Placeholder: Update on execution based on US_056 task_002 completion state.

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | server/src/PropelIQ.Domain/Compliance/ComplianceReport.cs | Report entity with status, PDF content, metrics |
| CREATE | server/src/PropelIQ.Domain/Compliance/ComplianceReportSchedule.cs | Schedule entity with recurrence pattern and next-run time |
| CREATE | server/src/PropelIQ.Domain/Compliance/DistributionListEntry.cs | Recipient entity linked to schedule |
| CREATE | server/src/PropelIQ.Domain/Compliance/DistributionLog.cs | Delivery tracking entity with retry metadata |
| CREATE | server/src/PropelIQ.Domain/Compliance/ComplianceReportJob.cs | Async job entity with progress tracking |
| CREATE | server/src/PropelIQ.Infrastructure/Persistence/Migrations/*_AddComplianceReportSchema.cs | EF Core migration for all 5 tables with indexes |
| MODIFY | server/src/PropelIQ.Infrastructure/Persistence/AppDbContext.cs | Add DbSets and Fluent API configuration for compliance entities |

## External References

- EF Core Migrations: https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations
- PostgreSQL Index Types: https://www.postgresql.org/docs/15/indexes-types.html
- PostgreSQL Partial Indexes: https://www.postgresql.org/docs/15/indexes-partial.html
- EF Core Fluent API: https://learn.microsoft.com/en-us/ef/core/modeling

## Build Commands

```bash
# Generate migration
cd server
dotnet ef migrations add AddComplianceReportSchema \
    --project src/PropelIQ.Infrastructure \
    --startup-project src/PropelIQ.Api

# Apply migration
dotnet ef database update \
    --project src/PropelIQ.Infrastructure \
    --startup-project src/PropelIQ.Api

# Verify tables
psql -d propeliq -c "\dt app.compliance_*"
```

## Implementation Validation Strategy

- [x] All 5 tables created with correct columns, types, and constraints
- [x] Foreign key relationships correctly reference parent tables
- [x] Composite and partial indexes created for schedule polling, report listing, and distribution log queries
- [x] Migration is reversible with proper rollback support
- [x] Default values applied for status, timestamps, and boolean fields

## Implementation Checklist

- [x] Create ComplianceReport entity with status tracking, PDF content, and metric fields
- [x] Create ComplianceReportSchedule entity with recurrence pattern and next-run timestamp
- [x] Create DistributionListEntry entity linked to schedule with active/inactive status
- [x] Create DistributionLog entity with delivery status, retry count, and error details
- [x] Create ComplianceReportJob entity with progress percentage and status tracking
- [x] Create EF Core migration with all 5 tables, foreign keys, and composite/partial indexes
