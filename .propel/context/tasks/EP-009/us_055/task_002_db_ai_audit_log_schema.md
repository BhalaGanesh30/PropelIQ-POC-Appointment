---
task_id: task_002
user_story: us_055
epic: EP-009
layer: Database
status: not-started
effort_hours: 3
---

# Task - task_002

## Requirement Reference

- **User Story**: [us_055] AI Prompt and Response Audit Logging
- **Story Location**: [.propel/context/tasks/EP-009/us_055/us_055.md](.propel/context/tasks/EP-009/us_055/us_055.md)
- **Acceptance Criteria**:
  - AC-3: Given the audit record is written, When it is stored, Then it is persisted in the append-only audit table with no UPDATE or DELETE permissions and a 7-year retention policy enforced.
  - AC-4: Given an admin queries the AI audit log, When they filter by date range and clinician, Then all matching records are returned with full structured metadata.
- **Edge Cases**:
  - Edge Case 1: Audit log write failure — `ai_audit_outbox` table stores compensating retry payloads; `retry_count` column enables `AiAuditOutboxProcessor` (task_001) to manage retries with escalation.
  - Edge Case 2: 7-year storage growth — PostgreSQL native range partitioning by year applied to `ai_audit_logs`; partitions older than 3 years are moved to cold storage (lower-cost tablespace) while remaining queryable via partition pruning.

---

## Design References (Database Task)

| Reference Type | Value |
|----------------|-------|
| **UI Impact** | No |
| **Figma URL** | N/A |
| **Wireframe Status** | N/A (database task) |
| **Wireframe Type** | N/A |
| **Wireframe Path/URL** | N/A |
| **Screen Spec** | N/A |
| **UXR Requirements** | N/A |
| **Design Tokens** | N/A |

---

## Applicable Technology Stack

| Layer | Technology | Version |
|-------|------------|---------|
| Backend | ASP.NET Core Web API (EF Core migrations) | 8.x |
| ORM | Entity Framework Core | 8.x |
| Database | PostgreSQL with pgvector | 15.x |
| Cache | N/A | N/A |
| Observability | N/A | N/A |
| Frontend | N/A | N/A |
| AI/ML | N/A | N/A |
| AI Gateway | N/A | N/A |
| Mobile | N/A | N/A |

---

## AI References (AI Tasks Only)

| Reference Type | Value |
|----------------|-------|
| **AI Impact** | No |
| **AIR Requirements** | N/A |
| **AI Pattern** | N/A |
| **Prompt Template Path** | N/A |
| **Guardrails Config** | N/A |
| **Model Provider** | N/A |

---

## Mobile References (Mobile Tasks Only)

| Reference Type | Value |
|----------------|-------|
| **Mobile Impact** | No |
| **Platform Target** | N/A |
| **Min OS Version** | N/A |
| **Mobile Framework** | N/A |

---

## Task Overview

Create the database schema for AI audit logging to satisfy AIR-011, NFR-010, and DR-005. This migration creates **three new tables** alongside the existing `audit_records` table (which handles general access/override events):

1. **`ai_audit_logs`**: Primary append-only AI audit table. Contains all AIR-011 structured fields. Range-partitioned by year on `request_timestamp` to support 7-year retention lifecycle (Edge Case 2). `REVOKE UPDATE, DELETE` from `app_user` role enforces append-only constraint (AC-3, DR-005).

2. **`ai_audit_log_outcomes`**: Sister table for reviewer decisions — INSERT-only, linked to `ai_audit_logs` by `ai_request_id`. Separate table avoids any UPDATE on the base `ai_audit_logs` record when decisions are appended (AC-2 integrity, AC-3 append-only).

3. **`ai_audit_outbox`**: Compensating retry buffer. Populated when primary `ai_audit_logs` write fails (Edge Case 1, task_001). Holds serialized `AiAuditEntry` JSON with retry tracking columns.

Additionally, add a nullable `ai_request_id UUID` column to `coding_decisions` (additive migration) so `CodingDecisionService` can link decisions to their originating AI request when calling `AppendReviewerOutcomeAsync`.

The existing `audit_records` table and its EP-004 columns are **not modified** — this migration is fully additive per DR-007.

---

## Dependent Tasks

- **us_055/task_001** — `AiAuditService`, `AiAuditOutboxProcessor`, and EF Core entities depend on all tables created here.

---

## Impacted Components

| Component / Module | Action | Notes |
|--------------------|--------|-------|
| `ai_audit_logs` table | CREATE | Primary AIR-011 audit table; range-partitioned by year; REVOKE UPDATE/DELETE (AC-3, DR-005) |
| `ai_audit_log_outcomes` table | CREATE | Reviewer decision outcomes; INSERT-only; FK to `ai_audit_logs.ai_request_id` (AC-2, AC-3) |
| `ai_audit_outbox` table | CREATE | Compensating retry buffer; `retry_count`, `last_attempt_at`, serialized `payload` (Edge Case 1) |
| `coding_decisions.ai_request_id` column | CREATE | Nullable UUID FK to `ai_audit_logs.ai_request_id`; additive migration (DR-007) |
| `ix_ai_audit_logs_clinician_timestamp` index | CREATE | B-tree on `(clinician_id, request_timestamp DESC)` — supports AC-4 admin query |
| `ix_ai_audit_logs_timestamp` index | CREATE | B-tree on `request_timestamp DESC` — supports AC-4 date-range-only queries and partition pruning |
| `ix_ai_audit_log_outcomes_request_id` index | CREATE | B-tree on `ai_request_id` — supports JOIN from admin query |
| `ix_ai_audit_outbox_retry_due` index | CREATE | B-tree on `(retry_count, last_attempt_at)` — supports outbox processor poll query (Edge Case 1) |
| `ai_audit_logs` year partitions | CREATE | Initial partition set: `ai_audit_logs_2026`, `ai_audit_logs_2027` ... `ai_audit_logs_2032` (7-year set) |

---

## Implementation Plan

1. **Create `ai_audit_logs` (partitioned parent table)**:
   ```sql
   CREATE TABLE ai_audit_logs (
       ai_request_id     UUID         NOT NULL,
       request_timestamp TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
       clinician_id      UUID         NOT NULL REFERENCES users(user_id),
       prompt_hash       VARCHAR(64)  NOT NULL,         -- SHA-256 hex of redacted prompt
       context_refs      JSONB        NOT NULL DEFAULT '[]',
       model_name        VARCHAR(100) NOT NULL,
       response_payload  JSONB        NOT NULL DEFAULT '{}',
       confidence_scores JSONB        NOT NULL DEFAULT '{}',
       latency_ms        INTEGER      NOT NULL,
       fallback_reason   VARCHAR(255) NULL,
       created_at        TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
       PRIMARY KEY (ai_request_id, request_timestamp)
   ) PARTITION BY RANGE (request_timestamp);

   COMMENT ON TABLE ai_audit_logs IS 'AIR-011: Append-only AI request audit log. 7-year retention per DR-005. REVOKE UPDATE/DELETE enforced.';
   ```
   Primary key is composite `(ai_request_id, request_timestamp)` — required for PostgreSQL range partitioning; `ai_request_id` alone is unique per row since each request produces exactly one record.

2. **Create year partitions** (2026–2032 — 7-year initial set):
   ```sql
   CREATE TABLE ai_audit_logs_2026 PARTITION OF ai_audit_logs
       FOR VALUES FROM ('2026-01-01') TO ('2027-01-01');

   CREATE TABLE ai_audit_logs_2027 PARTITION OF ai_audit_logs
       FOR VALUES FROM ('2027-01-01') TO ('2028-01-01');
   -- ... repeat through 2032
   ```
   EF Core migration creates each partition as a separate `migrationBuilder.Sql(...)` statement. Cold storage migration (partitions older than 3 years moved to `cold_tablespace`) is a DBA runbook concern, not a code migration — document in migration comment (Edge Case 2).

3. **REVOKE append-only permissions**:
   ```sql
   REVOKE UPDATE, DELETE ON ai_audit_logs FROM app_user;
   -- Also apply to each partition:
   REVOKE UPDATE, DELETE ON ai_audit_logs_2026 FROM app_user;
   -- ... repeat per partition
   ```
   `app_user` is the application database role used by the EF Core connection string. `app_admin` (migrations role) retains all privileges. This enforces DR-005 and AC-3.

4. **Create `ai_audit_log_outcomes`**:
   ```sql
   CREATE TABLE ai_audit_log_outcomes (
       outcome_id        UUID         NOT NULL DEFAULT gen_random_uuid() PRIMARY KEY,
       ai_request_id     UUID         NOT NULL,
       reviewer_action   VARCHAR(20)  NOT NULL CHECK (reviewer_action IN ('accepted','modified','rejected')),
       reviewer_note     TEXT         NULL,
       decided_at        TIMESTAMPTZ  NOT NULL DEFAULT NOW()
   );

   COMMENT ON TABLE ai_audit_log_outcomes IS 'AIR-011: Append-only reviewer decision outcomes linked to ai_audit_logs. No FK enforced to support partitioned parent.';
   -- Note: FK to ai_audit_logs omitted intentionally — PostgreSQL does not support FKs to partitioned tables with composite PKs; referential integrity enforced at application layer.

   REVOKE UPDATE, DELETE ON ai_audit_log_outcomes FROM app_user;
   ```

5. **Create `ai_audit_outbox`**:
   ```sql
   CREATE TABLE ai_audit_outbox (
       outbox_id        UUID        NOT NULL DEFAULT gen_random_uuid() PRIMARY KEY,
       payload          JSONB       NOT NULL,
       retry_count      INTEGER     NOT NULL DEFAULT 0,
       last_attempt_at  TIMESTAMPTZ NULL,
       created_at       TIMESTAMPTZ NOT NULL DEFAULT NOW()
   );

   COMMENT ON TABLE ai_audit_outbox IS 'Edge Case 1: Compensating write buffer for ai_audit_logs failures. AiAuditOutboxProcessor retries up to 3 times.';
   ```

6. **Add `ai_request_id` to `coding_decisions`**:
   ```sql
   ALTER TABLE coding_decisions
       ADD COLUMN ai_request_id UUID NULL;

   COMMENT ON COLUMN coding_decisions.ai_request_id IS 'AIR-011: Links coding decision to originating ai_audit_logs record.';
   ```
   Nullable — existing rows unaffected; backfill not required (forward-only from migration date per DR-007).

7. **Create indexes**:
   ```sql
   -- Admin query: clinician + date range (AC-4)
   CREATE INDEX ix_ai_audit_logs_clinician_timestamp
       ON ai_audit_logs (clinician_id, request_timestamp DESC);

   -- Admin query: date range only (AC-4)
   CREATE INDEX ix_ai_audit_logs_timestamp
       ON ai_audit_logs (request_timestamp DESC);

   -- Outcomes join from admin query
   CREATE INDEX ix_ai_audit_log_outcomes_request_id
       ON ai_audit_log_outcomes (ai_request_id);

   -- Outbox processor poll
   CREATE INDEX ix_ai_audit_outbox_retry_due
       ON ai_audit_outbox (retry_count, last_attempt_at NULLS FIRST);
   ```
   Indexes on the partitioned parent automatically propagate to each partition in PostgreSQL 15.

---

## Current Project State

```
src/
├── Data/
│   └── Migrations/
│       └── YYYYMMDDHHMMSS_AddAiAuditLogSchema.cs     ← CREATE
├── [existing schema tables unchanged]
│   ├── audit_records                                 ← NOT MODIFIED (EP-DATA foundation)
│   ├── coding_decisions                              ← ADD ai_request_id NULL column (additive)
│   ├── users
│   └── [all other existing tables...]
```

> Placeholder: Update this tree after dependent tasks are complete and the actual migration folder structure is confirmed.

---

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | `Data/Migrations/YYYYMMDDHHMMSS_AddAiAuditLogSchema.cs` | EF Core migration: ai_audit_logs (partitioned), ai_audit_log_outcomes, ai_audit_outbox; coding_decisions.ai_request_id column; REVOKE UPDATE/DELETE; year partitions 2026–2032; all indexes |

---

## External References

- PostgreSQL 15 declarative partitioning: https://www.postgresql.org/docs/15/ddl-partitioning.html
- PostgreSQL REVOKE (append-only patterns): https://www.postgresql.org/docs/15/sql-revoke.html
- EF Core 8 raw SQL migrations: https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/
- NFR-010: Immutable audit evidence — minimum 7-year retention
- DR-005: Retain immutable audit and access logs for 7 years with append-only write constraints
- DR-007: Schema migration with backward-compatible, zero-downtime rollouts for additive changes
- AIR-011: Log prompts, context references, model responses, confidence values, and reviewer outcomes with 7-year retention

---

## Build Commands

```bash
# Restore packages
dotnet restore

# Build to validate migration compiles
dotnet build --no-restore

# Generate EF Core migration (if using code-first)
dotnet ef migrations add AddAiAuditLogSchema --project src/Data --startup-project src/Api

# Apply migration to dev database
dotnet ef database update --project src/Data --startup-project src/Api

# Verify tables created (psql)
psql -c "\d+ ai_audit_logs"
psql -c "\d ai_audit_log_outcomes"
psql -c "\d ai_audit_outbox"
psql -c "\d+ coding_decisions"
```

---

## Implementation Validation Strategy

- [ ] `ai_audit_logs` table created as PARTITION BY RANGE on `request_timestamp`; 7 partitions (2026–2032) created (Edge Case 2)
- [ ] `REVOKE UPDATE, DELETE ON ai_audit_logs FROM app_user` enforced — INSERT via `app_user` succeeds; UPDATE/DELETE fails with permission error (AC-3, DR-005)
- [ ] Same REVOKE applied to `ai_audit_log_outcomes` — outcomes can only be INSERTed (AC-3)
- [ ] `ai_audit_outbox` table created with `retry_count INT DEFAULT 0` and `last_attempt_at TIMESTAMPTZ NULL` (Edge Case 1)
- [ ] `coding_decisions.ai_request_id UUID NULL` column added; existing rows unaffected; no FK constraint on partitioned parent (DR-007)
- [ ] `ix_ai_audit_logs_clinician_timestamp` on `(clinician_id, request_timestamp DESC)` exists and propagates to partitions (AC-4)
- [ ] `EXPLAIN ANALYZE` on `WHERE clinician_id = X AND request_timestamp BETWEEN Y AND Z` shows partition pruning applied (AC-4, Edge Case 2)

---

## Implementation Checklist

- [ ] Create `ai_audit_logs` parent table with composite PK `(ai_request_id, request_timestamp)` and `PARTITION BY RANGE (request_timestamp)`; all AIR-011 columns; table comment noting 7-year retention (AC-1, AC-3, DR-005)
- [ ] Create year partitions `ai_audit_logs_2026` through `ai_audit_logs_2032` as `PARTITION OF ai_audit_logs FOR VALUES FROM (...) TO (...)` (Edge Case 2)
- [ ] `REVOKE UPDATE, DELETE ON ai_audit_logs` and all year partitions `FROM app_user`; verify `app_admin` retains full privileges (AC-3, DR-005)
- [ ] Create `ai_audit_log_outcomes` table (INSERT-only; REVOKE UPDATE/DELETE); no FK to partitioned parent (AC-2, AC-3)
- [ ] Create `ai_audit_outbox` table with `retry_count`, `last_attempt_at`, `payload JSONB` (Edge Case 1)
- [ ] Add `coding_decisions.ai_request_id UUID NULL` column (additive; DR-007)
- [ ] Create all four indexes: `ix_ai_audit_logs_clinician_timestamp`, `ix_ai_audit_logs_timestamp`, `ix_ai_audit_log_outcomes_request_id`, `ix_ai_audit_outbox_retry_due` (AC-4, Edge Case 1)
