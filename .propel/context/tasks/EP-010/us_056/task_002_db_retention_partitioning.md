# Task - TASK_002

## Requirement Reference

- User Story: us_056
- Story Location: .propel/context/tasks/EP-010/us_056/us_056.md
- Acceptance Criteria:
  - AC-2: Given an AuditRecord is written, When any role (including DBA) attempts to UPDATE or DELETE it, Then the operation is rejected by the database write-restriction policy and the attempt is itself logged.
  - AC-3: Given an audit record is 7 years old, When the retention policy evaluation runs, Then the record is transitioned to archival cold storage; records within the 7-year window remain accessible and queryable.
- Edge Cases:
  - How does the system handle export of large audit log batches for external compliance review? Async export is triggered; when complete, the file is delivered via secure download link to the requesting admin. (Partition-aware export leverages partition pruning for date-range queries.)

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

Implement PostgreSQL range partitioning by year on the `app.audit_records` table, a scheduled retention evaluation job, and cold storage archival for records exceeding the 7-year retention window (AC-3). The existing `app.audit_records` table (from US_010 task_001) is converted to a partitioned table with yearly child partitions, enabling efficient date-range queries for the admin audit log viewer (AC-4 from task_001) and partition-level archival operations. A `RetentionPolicyWorker` background service runs daily, identifies partitions older than 7 years, detaches them from the parent table, and moves the data to a `app.audit_records_archive` cold storage table. Archived partitions remain queryable via a `UNION ALL` view for compliance review but are excluded from the default query path for performance. The immutability trigger from US_010 (`trg_audit_records_immutable`) is re-applied to each new partition as PostgreSQL requires triggers on partitioned tables to be defined on child partitions. An `audit_mutation_attempts` log table captures rejected UPDATE/DELETE attempts at the application layer (AC-2 complement to US_010's pgaudit logging), providing a queryable record of tampering attempts accessible to the admin audit log viewer. Composite indexes on `(created_at, event_type)` and `(user_id, created_at)` are added to support the 3-second query target from AC-4.

## Dependent Tasks

- US_010 task_001 (requires `audit_records` table, immutability trigger, and pgaudit configuration)
- US_010 task_002 (requires transactional integrity infrastructure)
- US_056 task_001 (requires `AuditRecordWriterWorker` to populate records into partitioned table)

## Impacted Components

- New: `server/src/PropelIQ.Infrastructure/Persistence/Migrations/<timestamp>_AuditPartitioning.cs` (partition conversion migration)
- New: `server/src/PropelIQ.Infrastructure/Audit/RetentionPolicyWorker.cs` (daily archival BackgroundService)
- New: `server/src/PropelIQ.Infrastructure/Audit/PartitionMaintenanceService.cs` (yearly partition creation)
- Modify: `server/src/PropelIQ.Infrastructure/Persistence/AppDbContext.cs` (partition-aware configuration)
- Modify: `docker-compose.yml` (pg_cron extension for scheduled partition maintenance — optional)

## Implementation Plan

1. **Create the partitioning migration** that converts `app.audit_records` to a range-partitioned table by `created_at`. Since PostgreSQL does not support in-place conversion of existing tables to partitioned tables, the migration creates the new partitioned structure, copies data, and swaps:

```sql
-- Step 1: Rename existing table
ALTER TABLE app.audit_records
    RENAME TO audit_records_legacy;

-- Step 2: Drop the immutability trigger on legacy
DROP TRIGGER IF EXISTS trg_audit_records_immutable
    ON app.audit_records_legacy;

-- Step 3: Create partitioned parent table
CREATE TABLE app.audit_records (
    audit_id UUID NOT NULL,
    user_id UUID NOT NULL,
    event_type VARCHAR(100) NOT NULL,
    entity_type VARCHAR(100) NOT NULL,
    entity_id UUID,
    details JSONB NOT NULL DEFAULT '{}',
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT pk_audit_records
        PRIMARY KEY (audit_id, created_at)
) PARTITION BY RANGE (created_at);

-- Step 4: Create yearly partitions (current + next)
CREATE TABLE app.audit_records_y2026
    PARTITION OF app.audit_records
    FOR VALUES FROM ('2026-01-01')
                 TO ('2027-01-01');

CREATE TABLE app.audit_records_y2027
    PARTITION OF app.audit_records
    FOR VALUES FROM ('2027-01-01')
                 TO ('2028-01-01');

-- Step 5: Create default partition for overflow
CREATE TABLE app.audit_records_default
    PARTITION OF app.audit_records DEFAULT;

-- Step 6: Copy legacy data into partitioned table
INSERT INTO app.audit_records
    SELECT * FROM app.audit_records_legacy;

-- Step 7: Drop legacy table
DROP TABLE app.audit_records_legacy;

-- Step 8: Re-apply immutability trigger
--   on each partition
CREATE TRIGGER trg_audit_records_immutable_y2026
    BEFORE UPDATE OR DELETE
    ON app.audit_records_y2026
    FOR EACH ROW
    EXECUTE FUNCTION app.fn_prevent_audit_mutation();

CREATE TRIGGER trg_audit_records_immutable_y2027
    BEFORE UPDATE OR DELETE
    ON app.audit_records_y2027
    FOR EACH ROW
    EXECUTE FUNCTION app.fn_prevent_audit_mutation();

CREATE TRIGGER trg_audit_records_immutable_default
    BEFORE UPDATE OR DELETE
    ON app.audit_records_default
    FOR EACH ROW
    EXECUTE FUNCTION app.fn_prevent_audit_mutation();

-- Step 9: Re-apply GRANT restrictions
REVOKE ALL ON app.audit_records FROM app_user;
GRANT INSERT, SELECT ON app.audit_records
    TO app_user;
REVOKE ALL ON app.audit_records_y2026
    FROM app_user;
GRANT INSERT, SELECT ON app.audit_records_y2026
    TO app_user;
REVOKE ALL ON app.audit_records_y2027
    FROM app_user;
GRANT INSERT, SELECT ON app.audit_records_y2027
    TO app_user;
```

The primary key includes `created_at` as required by PostgreSQL for range partition keys. Foreign key from `user_id` to `app.users` is maintained.

2. **Add composite indexes** for query performance (AC-4, 3-second target):

```sql
-- Index for date-range + event-type filtering
CREATE INDEX ix_audit_records_created_type
    ON app.audit_records (created_at DESC, event_type);

-- Index for actor-based filtering
CREATE INDEX ix_audit_records_user_created
    ON app.audit_records (user_id, created_at DESC);

-- Index for resource-based filtering
CREATE INDEX ix_audit_records_entity
    ON app.audit_records (entity_id, created_at DESC)
    WHERE entity_id IS NOT NULL;
```

PostgreSQL automatically creates these indexes on each child partition via the partition inheritance mechanism.

3. **Create the `audit_records_archive` cold storage table** for records older than 7 years (AC-3):

```sql
-- Cold storage: non-partitioned append-only archive
CREATE TABLE app.audit_records_archive (
    audit_id UUID NOT NULL,
    user_id UUID NOT NULL,
    event_type VARCHAR(100) NOT NULL,
    entity_type VARCHAR(100) NOT NULL,
    entity_id UUID,
    details JSONB NOT NULL DEFAULT '{}',
    created_at TIMESTAMPTZ NOT NULL,
    archived_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT pk_audit_records_archive
        PRIMARY KEY (audit_id)
);

-- Immutability trigger on archive
CREATE TRIGGER trg_audit_archive_immutable
    BEFORE UPDATE OR DELETE
    ON app.audit_records_archive
    FOR EACH ROW
    EXECUTE FUNCTION app.fn_prevent_audit_mutation();

-- Read-only GRANT
GRANT SELECT ON app.audit_records_archive
    TO app_user;
```

4. **Create the `audit_mutation_attempts` log table** for AC-2 application-layer tracking:

```sql
CREATE TABLE app.audit_mutation_attempts (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    attempted_by TEXT NOT NULL,
    operation TEXT NOT NULL,
    target_audit_id UUID,
    error_message TEXT NOT NULL,
    occurred_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    source_ip INET
);

CREATE INDEX ix_mutation_attempts_occurred
    ON app.audit_mutation_attempts (occurred_at DESC);
```

5. **Implement `RetentionPolicyWorker`** as a daily `BackgroundService`:

```csharp
// server/src/PropelIQ.Infrastructure/Audit/
//   RetentionPolicyWorker.cs
public sealed class RetentionPolicyWorker
    : BackgroundService
{
    private static readonly TimeSpan RunInterval =
        TimeSpan.FromHours(24);

    protected override async Task ExecuteAsync(
        CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            await Task.Delay(RunInterval, ct);
            await EvaluateRetentionAsync(ct);
        }
    }

    private async Task EvaluateRetentionAsync(
        CancellationToken ct)
    {
        // AC-3: Find partitions with data older
        //   than 7 years
        // 1. Query pg_inherits + pg_class to list
        //   child partitions
        // 2. For each partition, check range bounds
        //   via pg_catalog.pg_partition_range
        // 3. If upper bound < (now - 7 years):
        //   a. INSERT INTO audit_records_archive
        //      SELECT * FROM <partition>
        //   b. ALTER TABLE audit_records
        //      DETACH PARTITION <partition>
        //   c. DROP TABLE <partition>
        //   d. Log archival event with count
        // 4. Partitions within 7 years remain
        //   untouched and queryable
    }
}
```

6. **Implement `PartitionMaintenanceService`** that pre-creates yearly partitions:

```csharp
// server/src/PropelIQ.Infrastructure/Audit/
//   PartitionMaintenanceService.cs
public sealed class PartitionMaintenanceService
{
    // Called by RetentionPolicyWorker after archival
    // 1. Check if partition for next year exists
    // 2. If not: CREATE TABLE
    //    app.audit_records_y{year}
    //    PARTITION OF app.audit_records
    //    FOR VALUES FROM ('{year}-01-01')
    //                 TO ('{year+1}-01-01')
    // 3. Apply immutability trigger
    // 4. Apply GRANT restrictions
    // 5. Log partition creation event
}
```

7. **Create unified view** for compliance queries spanning active and archived data:

```sql
CREATE OR REPLACE VIEW app.audit_records_full AS
SELECT audit_id, user_id, event_type, entity_type,
       entity_id, details, created_at
FROM app.audit_records
UNION ALL
SELECT audit_id, user_id, event_type, entity_type,
       entity_id, details, created_at
FROM app.audit_records_archive;
```

The default admin query endpoint uses `app.audit_records` (active partitions only) for fast performance. A separate `include_archived=true` parameter switches to the `audit_records_full` view for compliance review.

## Current Project State

```text
propelIQ/
├── server/
│   └── src/
│       ├── PropelIQ.Infrastructure/
│       │   ├── Audit/
│       │   │   ├── AuditRecordService.cs              (from task_001)
│       │   │   ├── AuditRecordWriterWorker.cs         (from task_001)
│       │   │   ├── DeadLetterRetryWorker.cs           (from task_001)
│       │   │   ├── RetentionPolicyWorker.cs           (new)
│       │   │   └── PartitionMaintenanceService.cs     (new)
│       │   └── Persistence/
│       │       ├── AppDbContext.cs                     (modify)
│       │       └── Migrations/
│       │           ├── <timestamp>_AuditTableImmutability.cs (from US_010)
│       │           ├── <timestamp>_AuditDeadLetterTable.cs   (from task_001)
│       │           └── <timestamp>_AuditPartitioning.cs      (new)
│       └── PropelIQ.Api/
│           └── Program.cs                             (modify)
└── docker-compose.yml
```

> Placeholder: Update on execution based on US_010 task_001 and US_056 task_001 completion state.

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | server/src/PropelIQ.Infrastructure/Persistence/Migrations/\<timestamp\>_AuditPartitioning.cs | Range partitioning migration: table conversion, yearly partitions, indexes, archive table, mutation attempts table, unified view |
| CREATE | server/src/PropelIQ.Infrastructure/Audit/RetentionPolicyWorker.cs | Daily BackgroundService evaluating 7-year retention and archiving expired partitions |
| CREATE | server/src/PropelIQ.Infrastructure/Audit/PartitionMaintenanceService.cs | Yearly partition pre-creation with trigger and GRANT application |
| MODIFY | server/src/PropelIQ.Infrastructure/Persistence/AppDbContext.cs | Add DbSet for AuditMutationAttempt; configure partition-aware entity mapping |
| MODIFY | server/src/PropelIQ.Api/Program.cs | Register RetentionPolicyWorker and PartitionMaintenanceService |

## External References

- PostgreSQL table partitioning: https://www.postgresql.org/docs/15/ddl-partitioning.html
- PostgreSQL range partitioning: https://www.postgresql.org/docs/15/ddl-partitioning.html#DDL-PARTITIONING-DECLARATIVE
- PostgreSQL partition maintenance: https://www.postgresql.org/docs/15/ddl-partitioning.html#DDL-PARTITIONING-DECLARATIVE-MAINTENANCE
- PostgreSQL DETACH PARTITION: https://www.postgresql.org/docs/15/sql-altertable.html
- PostgreSQL triggers on partitioned tables: https://www.postgresql.org/docs/15/trigger-definition.html
- EF Core raw SQL in migrations: https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/operations
- DR-005 (7-year retention): .propel/context/docs/design.md
- NFR-010 (immutable audit evidence): .propel/context/docs/design.md

## Build Commands

```bash
# Generate partitioning migration
dotnet ef migrations add AuditPartitioning \
  --project server/src/PropelIQ.Infrastructure \
  --startup-project server/src/PropelIQ.Api \
  --output-dir Persistence/Migrations

# Apply migration
dotnet ef database update \
  --project server/src/PropelIQ.Infrastructure \
  --startup-project server/src/PropelIQ.Api

# Verify partitions exist
docker exec propeliq-postgres psql -U app_user -d propeliq -c \
  "SELECT relname FROM pg_class WHERE relname LIKE 'audit_records_y%';"

# Verify archive table
docker exec propeliq-postgres psql -U app_user -d propeliq -c \
  "SELECT count(*) FROM app.audit_records_archive;"

# Verify indexes
docker exec propeliq-postgres psql -U app_user -d propeliq -c \
  "SELECT indexname FROM pg_indexes WHERE tablename LIKE 'audit_records%';"
```

## Implementation Validation Strategy

- [ ] `audit_records` is a partitioned table with yearly child partitions (AC-3)
- [ ] Immutability trigger exists on each child partition and rejects UPDATE/DELETE (AC-2)
- [ ] Composite indexes on `(created_at, event_type)` and `(user_id, created_at)` exist on partitions
- [ ] `audit_records_archive` table exists with immutability trigger and SELECT-only GRANT
- [ ] RetentionPolicyWorker archives partitions older than 7 years to cold storage (AC-3)
- [ ] Archived records remain queryable via `audit_records_full` view (AC-3)
- [ ] `audit_mutation_attempts` table logs rejected operations (AC-2)
- [ ] New yearly partitions are pre-created with triggers and GRANTs applied

## Implementation Checklist

- [ ] Create partitioning migration converting `audit_records` to range-partitioned table by `created_at`
- [ ] Create yearly child partitions (2026, 2027) with default overflow partition
- [ ] Re-apply immutability triggers and GRANT restrictions on all child partitions
- [ ] Add composite indexes for date-range, actor, and resource filtering
- [ ] Create `audit_records_archive` cold storage table with immutability enforcement
- [ ] Create `audit_mutation_attempts` log table for AC-2 tracking
- [ ] Implement `RetentionPolicyWorker` daily job for 7-year archival evaluation
- [ ] Implement `PartitionMaintenanceService` for yearly partition pre-creation with trigger/GRANT propagation
