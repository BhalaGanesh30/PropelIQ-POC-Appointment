# Task - TASK_002

## Requirement Reference

- User Story: us_011
- Story Location: .propel/context/tasks/EP-DATA/us_011/us_011.md
- Acceptance Criteria:
  - AC-2: Given a backup has completed, When a point-in-time restore is initiated for a timestamp within the last 6-hour window, Then the database is restored to the requested state within the 4-hour RTO target.
- Edge Cases:
  - How does the system validate backup integrity? A restore verification test runs weekly against the most recent backup to confirm recoverability.

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
| Library | pg_basebackup / pg_restore | 15.x (bundled) |
| Library | Docker Compose | latest stable |
| Library | Bash | POSIX |
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

Create the point-in-time recovery (PITR) script and a weekly restore verification job that validates backup recoverability. The PITR script accepts a target timestamp, identifies the correct base backup, restores it to a temporary PostgreSQL instance, replays WAL segments up to the target time, and verifies data integrity — all within the 4-hour RTO window defined by NFR-006. The weekly verification job automatically tests the most recent backup by performing a full restore to a temporary container, running validation queries, and reporting pass/fail status. A runbook document captures the operational procedure for disaster recovery scenarios.

## Dependent Tasks

- task_001_infra_automated_backup (requires WAL archiving and base backups to exist)
- US_003 task_001 (requires PostgreSQL container)
- US_005 tasks (requires base docker-compose.yml)

## Impacted Components

- New: `infra/backup/restore.sh` (point-in-time recovery script)
- New: `infra/backup/verify-backup.sh` (weekly restore verification job)
- New: `infra/backup/verify-queries.sql` (validation queries for restore verification)
- New: `docs/DISASTER_RECOVERY_RUNBOOK.md` (operational runbook for PITR procedures)
- Modify: `infra/backup/entrypoint.sh` (add weekly verification cron job)
- Modify: `infra/backup/Dockerfile` (include restore and verify scripts)

## Implementation Plan

1. **Create `infra/backup/restore.sh`** — the point-in-time recovery script that restores a PostgreSQL database to a specific timestamp:

```bash
#!/usr/bin/env bash
set -euo pipefail

# Required arguments
TARGET_TIMESTAMP="${1:?Usage: restore.sh <target_timestamp> [restore_port]}"
RESTORE_PORT="${2:-5433}"

# Configuration
BACKUP_DIR="${BACKUP_DIR:-/backups}"
WAL_ARCHIVE_DIR="${WAL_ARCHIVE_DIR:-/wal_archive}"
RESTORE_DIR="/tmp/restore_${RESTORE_PORT}"
LOG_PREFIX="[restore][$(date +%Y%m%d_%H%M%S)]"

log_info()  { echo "${LOG_PREFIX} INFO: $*"; }
log_error() { echo "${LOG_PREFIX} ERROR: $*" >&2; }

# Step 1: Identify the most recent base backup before target timestamp
find_backup() {
    local target_epoch
    target_epoch=$(date -d "${TARGET_TIMESTAMP}" +%s 2>/dev/null || date -j -f "%Y-%m-%d %H:%M:%S" "${TARGET_TIMESTAMP}" +%s)

    local selected_backup=""
    for backup_dir in $(ls -1d "${BACKUP_DIR}"/base_* 2>/dev/null | sort -r); do
        local backup_ts
        backup_ts=$(basename "${backup_dir}" | sed 's/base_//' | sed 's/_/ /')
        local backup_epoch
        backup_epoch=$(date -d "${backup_ts}" +%s 2>/dev/null || echo "0")

        if [ "${backup_epoch}" -le "${target_epoch}" ]; then
            selected_backup="${backup_dir}"
            break
        fi
    done

    if [ -z "${selected_backup}" ]; then
        log_error "No base backup found before target timestamp: ${TARGET_TIMESTAMP}"
        exit 1
    fi

    echo "${selected_backup}"
}

# Step 2: Restore base backup to temporary directory
restore_base() {
    local backup_path="$1"
    log_info "Restoring base backup from: ${backup_path}"

    rm -rf "${RESTORE_DIR}"
    mkdir -p "${RESTORE_DIR}"

    # Extract tar backup
    tar xzf "${backup_path}/base.tar.gz" -C "${RESTORE_DIR}"

    # Copy pg_wal from backup if present
    if [ -f "${backup_path}/pg_wal.tar.gz" ]; then
        mkdir -p "${RESTORE_DIR}/pg_wal"
        tar xzf "${backup_path}/pg_wal.tar.gz" -C "${RESTORE_DIR}/pg_wal"
    fi

    log_info "Base backup restored to: ${RESTORE_DIR}"
}

# Step 3: Configure recovery settings for PITR
configure_recovery() {
    log_info "Configuring recovery to target: ${TARGET_TIMESTAMP}"

    # Write recovery configuration (PostgreSQL 12+ uses postgresql.auto.conf + recovery.signal)
    cat > "${RESTORE_DIR}/postgresql.auto.conf" <<EOF
restore_command = 'cp ${WAL_ARCHIVE_DIR}/%f %p'
recovery_target_time = '${TARGET_TIMESTAMP}'
recovery_target_action = 'promote'
EOF

    # Create recovery signal file
    touch "${RESTORE_DIR}/recovery.signal"

    log_info "Recovery configuration written"
}

# Step 4: Start temporary PostgreSQL instance for recovery
start_recovery() {
    log_info "Starting temporary PostgreSQL on port ${RESTORE_PORT} for WAL replay"

    # Ensure correct ownership
    chown -R postgres:postgres "${RESTORE_DIR}"
    chmod 700 "${RESTORE_DIR}"

    # Start PostgreSQL in recovery mode
    pg_ctl -D "${RESTORE_DIR}" -o "-p ${RESTORE_PORT}" -w start

    log_info "Recovery instance started. WAL replay in progress..."

    # Wait for recovery to complete (check for recovery.signal removal)
    local max_wait=14400  # 4 hours (RTO per NFR-006)
    local waited=0
    while [ -f "${RESTORE_DIR}/recovery.signal" ] && [ "${waited}" -lt "${max_wait}" ]; do
        sleep 10
        waited=$((waited + 10))
        if [ $((waited % 300)) -eq 0 ]; then
            log_info "Recovery in progress... (${waited}s elapsed)"
        fi
    done

    if [ "${waited}" -ge "${max_wait}" ]; then
        log_error "Recovery exceeded 4-hour RTO target. Aborting."
        pg_ctl -D "${RESTORE_DIR}" -m fast stop 2>/dev/null || true
        exit 1
    fi

    log_info "Recovery completed in ${waited} seconds"
}

# Step 5: Validate restored data
validate_restore() {
    log_info "Validating restored database"

    local result
    result=$(psql -p "${RESTORE_PORT}" -U postgres -d propeliq -tAc "SELECT count(*) FROM pg_catalog.pg_tables WHERE schemaname = 'app';" 2>&1)

    if [ $? -eq 0 ] && [ "${result}" -gt 0 ]; then
        log_info "Validation passed: ${result} application tables found"
    else
        log_error "Validation failed: Could not query restored database"
        return 1
    fi
}

# Main execution
main() {
    log_info "=== Point-in-time recovery started ==="
    log_info "Target timestamp: ${TARGET_TIMESTAMP}"

    local backup_path
    backup_path=$(find_backup)
    log_info "Selected base backup: ${backup_path}"

    restore_base "${backup_path}"
    configure_recovery
    start_recovery
    validate_restore

    log_info "=== Point-in-time recovery completed ==="
    log_info "Restored database available on port ${RESTORE_PORT}"
    log_info "To stop: pg_ctl -D ${RESTORE_DIR} stop"
}

main "$@"
```

Key design decisions:
- Uses `recovery.signal` + `postgresql.auto.conf` (PostgreSQL 12+ PITR approach, replacing deprecated `recovery.conf`)
- `recovery_target_time`: Restores to exact user-specified timestamp
- `recovery_target_action = 'promote'`: Automatically promotes to read-write after replay
- `restore_command`: Reads WAL segments from the shared archive volume
- RTO guard: Aborts after 4 hours of WAL replay (per NFR-006)
- Runs on a configurable port (default 5433) to avoid conflicting with the production instance

2. **Create `infra/backup/verify-backup.sh`** — the weekly restore verification job that tests recoverability of the most recent backup:

```bash
#!/usr/bin/env bash
set -euo pipefail

BACKUP_DIR="${BACKUP_DIR:-/backups}"
VERIFY_PORT=5434
RESTORE_DIR="/tmp/verify_restore"
QUERIES_FILE="/usr/local/bin/verify-queries.sql"
LOG_PREFIX="[verify][$(date +%Y%m%d_%H%M%S)]"

log_info()  { echo "${LOG_PREFIX} INFO: $*"; }
log_error() { echo "${LOG_PREFIX} ERROR: $*" >&2; }

cleanup() {
    log_info "Cleaning up verification instance"
    pg_ctl -D "${RESTORE_DIR}" -m fast stop 2>/dev/null || true
    rm -rf "${RESTORE_DIR}"
}
trap cleanup EXIT

# Find most recent backup
latest_backup=$(ls -1d "${BACKUP_DIR}"/base_* 2>/dev/null | sort -r | head -1)
if [ -z "${latest_backup}" ]; then
    log_error "No backups found to verify"
    echo "$(date +%Y%m%d_%H%M%S)|VERIFY_FAILED|no_backups" >> "${BACKUP_DIR}/backup.log"
    exit 1
fi

log_info "=== Backup verification started ==="
log_info "Verifying backup: ${latest_backup}"

# Restore to temporary directory
rm -rf "${RESTORE_DIR}"
mkdir -p "${RESTORE_DIR}"
tar xzf "${latest_backup}/base.tar.gz" -C "${RESTORE_DIR}"

if [ -f "${latest_backup}/pg_wal.tar.gz" ]; then
    mkdir -p "${RESTORE_DIR}/pg_wal"
    tar xzf "${latest_backup}/pg_wal.tar.gz" -C "${RESTORE_DIR}/pg_wal"
fi

# Remove recovery.signal if present (start as primary)
rm -f "${RESTORE_DIR}/recovery.signal"

chown -R postgres:postgres "${RESTORE_DIR}"
chmod 700 "${RESTORE_DIR}"

# Start temporary instance
pg_ctl -D "${RESTORE_DIR}" -o "-p ${VERIFY_PORT}" -w start
log_info "Verification instance started on port ${VERIFY_PORT}"

# Run validation queries
verify_result="PASS"
if [ -f "${QUERIES_FILE}" ]; then
    if psql -p "${VERIFY_PORT}" -U postgres -d propeliq -f "${QUERIES_FILE}" > /tmp/verify_output.txt 2>&1; then
        log_info "Validation queries passed"
    else
        log_error "Validation queries failed"
        cat /tmp/verify_output.txt >&2
        verify_result="FAIL"
    fi
else
    # Fallback: basic connectivity and table check
    table_count=$(psql -p "${VERIFY_PORT}" -U postgres -d propeliq -tAc \
        "SELECT count(*) FROM pg_catalog.pg_tables WHERE schemaname = 'app';" 2>&1 || echo "0")
    if [ "${table_count}" -gt 0 ]; then
        log_info "Basic validation passed: ${table_count} tables found"
    else
        log_error "Basic validation failed: no application tables found"
        verify_result="FAIL"
    fi
fi

# Record result
echo "$(date +%Y%m%d_%H%M%S)|VERIFY_${verify_result}|${latest_backup}" >> "${BACKUP_DIR}/backup.log"
log_info "=== Backup verification ${verify_result} ==="

if [ "${verify_result}" = "FAIL" ]; then
    exit 1
fi
```

3. **Create `infra/backup/verify-queries.sql`** — SQL validation queries run during restore verification:

```sql
-- Verify database is accessible and consistent
-- Run against restored database during weekly verification

-- 1. Check that application schema exists
SELECT EXISTS (
    SELECT 1 FROM information_schema.schemata WHERE schema_name = 'app'
) AS schema_exists;

-- 2. Verify core tables exist
DO $$
DECLARE
    expected_tables TEXT[] := ARRAY['users', 'patients', 'appointments', 'audit_records'];
    tbl TEXT;
BEGIN
    FOREACH tbl IN ARRAY expected_tables LOOP
        IF NOT EXISTS (
            SELECT 1 FROM pg_catalog.pg_tables
            WHERE schemaname = 'app' AND tablename = tbl
        ) THEN
            RAISE EXCEPTION 'Missing expected table: app.%', tbl;
        END IF;
    END LOOP;
    RAISE NOTICE 'All expected tables present';
END $$;

-- 3. Verify referential integrity (no orphaned foreign keys)
DO $$
DECLARE
    violation_count INTEGER;
BEGIN
    SELECT count(*) INTO violation_count
    FROM pg_catalog.pg_constraint c
    JOIN pg_catalog.pg_namespace n ON n.oid = c.connamespace
    WHERE n.nspname = 'app' AND c.contype = 'f' AND NOT c.convalidated;

    IF violation_count > 0 THEN
        RAISE EXCEPTION 'Found % unvalidated foreign key constraints', violation_count;
    END IF;
    RAISE NOTICE 'Referential integrity verified';
END $$;

-- 4. Verify row counts are non-zero for seed data tables
DO $$
DECLARE
    user_count INTEGER;
BEGIN
    SELECT count(*) INTO user_count FROM app.users;
    IF user_count = 0 THEN
        RAISE EXCEPTION 'Users table is empty — seed data may be missing';
    END IF;
    RAISE NOTICE 'Seed data verification passed: % users', user_count;
END $$;
```

4. **Update `infra/backup/entrypoint.sh`** to add the weekly verification cron job alongside the 6-hour backup job:

```bash
# Add after the backup cron line in entrypoint.sh:
VERIFY_SCHEDULE="${BACKUP_VERIFY_CRON:-0 3 * * 0}"  # Weekly Sunday 3 AM
echo "${VERIFY_SCHEDULE} /usr/local/bin/verify-backup.sh >> /proc/1/fd/1 2>> /proc/1/fd/2" >> /etc/crontabs/root
```

5. **Update `infra/backup/Dockerfile`** to include restore and verification scripts:

```dockerfile
# Add to existing Dockerfile
COPY restore.sh /usr/local/bin/restore.sh
COPY verify-backup.sh /usr/local/bin/verify-backup.sh
COPY verify-queries.sql /usr/local/bin/verify-queries.sql
RUN chmod +x /usr/local/bin/restore.sh /usr/local/bin/verify-backup.sh
```

6. **Create `docs/DISASTER_RECOVERY_RUNBOOK.md`** documenting the operational PITR procedure:

```markdown
# Disaster Recovery Runbook — Point-in-Time Recovery

## Overview
This runbook describes how to restore the PropelIQ PostgreSQL database to a
specific point in time using continuous WAL archiving and pg_basebackup.

## Prerequisites
- Access to the backup sidecar container (`propeliq-backup`)
- Backups and WAL archive volumes are intact
- Target timestamp is within the RPO window (< 1 hour data loss per NFR-006)

## Recovery Procedure

### Step 1: Identify target timestamp
Determine the exact timestamp to restore to (UTC).

### Step 2: Execute PITR
docker exec propeliq-backup /usr/local/bin/restore.sh "2025-01-15 14:30:00"

### Step 3: Validate restored data
Connect to the restored instance on port 5433 and verify data.

### Step 4: Promote restored instance
Stop the original PostgreSQL and point the application to the restored instance,
or use pg_dump/pg_restore to migrate data back to the primary.

## RTO/RPO Targets
- RPO: 1 hour (NFR-006) — bounded by archive_timeout=300s (~5 min actual)
- RTO: 4 hours (NFR-006) — script enforces timeout

## Verification
Weekly automated verification runs every Sunday at 03:00 UTC.
Check results: docker exec propeliq-backup cat /backups/backup.log
```

## Current Project State

```text
propelIQ/
├── docker-compose.yml                          (from US_005)
├── .env.example
├── infra/
│   ├── postgres/
│   │   └── init.sql                            (from US_003)
│   ├── backup/
│   │   ├── backup.sh                           (from task_001)
│   │   ├── entrypoint.sh                       (from task_001)
│   │   └── Dockerfile                          (from task_001)
│   ├── otel-collector/                          (from US_007)
│   └── ...
└── server/
```

> Placeholder: Update on execution based on task_001 completion state.

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | infra/backup/restore.sh | Point-in-time recovery script with timestamp targeting, RTO guard, validation |
| CREATE | infra/backup/verify-backup.sh | Weekly restore verification job testing latest backup recoverability |
| CREATE | infra/backup/verify-queries.sql | SQL validation queries for restore integrity checks |
| CREATE | docs/DISASTER_RECOVERY_RUNBOOK.md | Operational runbook for PITR procedures, RTO/RPO targets |
| MODIFY | infra/backup/entrypoint.sh | Add weekly verification cron job schedule |
| MODIFY | infra/backup/Dockerfile | Include restore.sh, verify-backup.sh, verify-queries.sql |

## External References

- PostgreSQL PITR with recovery.signal: https://www.postgresql.org/docs/15/recovery-config.html
- PostgreSQL recovery_target_time: https://www.postgresql.org/docs/15/runtime-config-wal.html#GUC-RECOVERY-TARGET-TIME
- PostgreSQL pg_ctl reference: https://www.postgresql.org/docs/15/app-pg-ctl.html
- PostgreSQL restore_command: https://www.postgresql.org/docs/15/runtime-config-wal.html#GUC-RESTORE-COMMAND
- PostgreSQL WAL replay monitoring: https://www.postgresql.org/docs/15/monitoring-stats.html#MONITORING-PG-STAT-WAL-RECEIVER

## Build Commands

```bash
# Rebuild backup sidecar (after adding new scripts)
docker compose build backup

# Manually trigger point-in-time recovery
docker exec propeliq-backup /usr/local/bin/restore.sh "2025-01-15 14:30:00"

# Connect to restored instance
docker exec -it propeliq-backup psql -p 5433 -U postgres -d propeliq

# Manually trigger backup verification
docker exec propeliq-backup /usr/local/bin/verify-backup.sh

# Check verification results in log
docker exec propeliq-backup cat /backups/backup.log

# Stop restored instance
docker exec propeliq-backup pg_ctl -D /tmp/restore_5433 stop
```

## Implementation Validation Strategy

- [ ] `restore.sh` accepts a target timestamp and restores the database to that point in time (AC-2)
- [ ] Recovery completes within the 4-hour RTO target; script aborts if exceeded (AC-2, NFR-006)
- [ ] Restored database passes table existence and row count validation checks
- [ ] `verify-backup.sh` runs weekly, restores the latest backup to a temporary instance, and reports pass/fail
- [ ] Verification results are logged to `backup.log` with VERIFY_PASS or VERIFY_FAIL status (edge case)
- [ ] `verify-queries.sql` validates schema presence, core tables, referential integrity, and seed data
- [ ] DISASTER_RECOVERY_RUNBOOK.md documents complete PITR procedure with RTO/RPO targets
- [ ] Temporary restore instances are cleaned up automatically via trap on exit

## Implementation Checklist

- [x] Create `infra/backup/restore.sh` with base backup selection, WAL replay via `recovery.signal` + `recovery_target_time`, and 4-hour RTO timeout guard
- [x] Create `infra/backup/verify-backup.sh` that restores the latest backup to a temporary instance, runs validation queries, and logs pass/fail to `backup.log`
- [x] Create `infra/backup/verify-queries.sql` with schema existence, core table presence, FK integrity, and seed data row count checks
- [x] Create `docs/DISASTER_RECOVERY_RUNBOOK.md` with step-by-step PITR procedure, RTO/RPO targets, and verification instructions
- [x] Update `infra/backup/entrypoint.sh` to add weekly verification cron job (default: Sunday 03:00 UTC)
- [x] Update `infra/backup/Dockerfile` to copy `restore.sh`, `verify-backup.sh`, and `verify-queries.sql` into the sidecar image
