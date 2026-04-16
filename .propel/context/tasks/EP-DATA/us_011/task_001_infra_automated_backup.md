# Task - TASK_001

## Requirement Reference

- User Story: us_011
- Story Location: .propel/context/tasks/EP-DATA/us_011/us_011.md
- Acceptance Criteria:
  - AC-1: Given the backup schedule is configured, When 6 hours elapse from the last backup, Then a new backup is triggered automatically and a completion record is logged.
  - AC-3: Given the backup process is running, When a backup job fails, Then an alert is triggered and the failure is recorded in the operations log with the error reason.
  - AC-4: Given backup retention policy is configured, When a backup is older than the configured retention window, Then it is purged automatically to manage storage consumption.
- Edge Case:
  - What happens if the backup storage target is full? Backup fails with a capacity alert; oldest backups outside retention window are purged before retrying.

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
| Library | pg_basebackup | 15.x (bundled) |
| Library | Docker Compose | latest stable |
| Library | Bash / cron | POSIX |
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

Configure PostgreSQL WAL (Write-Ahead Log) continuous archiving and an automated `pg_basebackup` schedule running every 6 hours via a dedicated Docker Compose backup sidecar container. WAL archiving provides the foundation for point-in-time recovery (PITR) by continuously copying completed WAL segments to a shared archive volume. The backup script logs completion records, handles failures with descriptive error logging and alerting (via stdout for Docker log aggregation), and enforces a configurable retention policy that purges backups older than the retention window. When backup storage is full (edge case), the script purges expired backups first and retries before failing with a capacity alert. All backups are stored on a named Docker volume to persist across container restarts.

## Dependent Tasks

- US_003 task_001 (requires PostgreSQL container running and accessible)
- US_005 tasks (requires base docker-compose.yml)

## Impacted Components

- New: `infra/backup/backup.sh` (automated backup script with logging, retention, and error handling)
- New: `infra/backup/Dockerfile` (backup sidecar container with pg_basebackup and cron)
- Modify: `docker-compose.yml` (add backup sidecar service, WAL archive volume, PostgreSQL WAL config)
- Modify: PostgreSQL service in `docker-compose.yml` (enable WAL archiving via command args)
- Modify: `.env.example` (add BACKUP_RETENTION_DAYS, BACKUP_SCHEDULE_CRON variables)

## Implementation Plan

1. **Enable PostgreSQL WAL archiving** by adding configuration parameters to the PostgreSQL service in `docker-compose.yml`. WAL archiving copies completed 16MB WAL segments to a shared archive directory, enabling PITR:

```yaml
postgres:
  image: pgvector/pgvector:pg15
  command: >
    postgres
    -c wal_level=replica
    -c archive_mode=on
    -c archive_command='test ! -f /var/lib/postgresql/wal_archive/%f && cp %p /var/lib/postgresql/wal_archive/%f'
    -c archive_timeout=300
    -c max_wal_senders=3
    -c shared_preload_libraries=pgaudit
  volumes:
    - pgdata:/var/lib/postgresql/data
    - wal-archive:/var/lib/postgresql/wal_archive
    - backups:/var/lib/postgresql/backups
```

Key parameters:
- `wal_level=replica`: Required for WAL archiving and PITR
- `archive_mode=on`: Enables WAL segment archiving
- `archive_command`: Copies completed WAL files to archive directory (idempotent — skips if file exists)
- `archive_timeout=300`: Forces WAL switch every 5 minutes even during low activity, bounding data loss to ≤5 minutes (well within 1-hour RPO per NFR-006)
- `max_wal_senders=3`: Allows concurrent `pg_basebackup` connections

2. **Create `infra/backup/backup.sh`** — the core backup script that `pg_basebackup` calls, with logging, retention purge, and error handling:

```bash
#!/usr/bin/env bash
set -euo pipefail

# Configuration (from environment)
BACKUP_DIR="${BACKUP_DIR:-/backups}"
WAL_ARCHIVE_DIR="${WAL_ARCHIVE_DIR:-/wal_archive}"
RETENTION_DAYS="${BACKUP_RETENTION_DAYS:-7}"
PG_HOST="${PGHOST:-postgres}"
PG_PORT="${PGPORT:-5432}"
PG_USER="${PGUSER:-replication_user}"
TIMESTAMP=$(date +%Y%m%d_%H%M%S)
BACKUP_PATH="${BACKUP_DIR}/base_${TIMESTAMP}"
LOG_PREFIX="[backup][${TIMESTAMP}]"

log_info()  { echo "${LOG_PREFIX} INFO: $*"; }
log_error() { echo "${LOG_PREFIX} ERROR: $*" >&2; }
log_alert() { echo "${LOG_PREFIX} ALERT: $*" >&2; }

# Step 1: Pre-flight storage check (edge case: storage full)
check_storage() {
    local available_kb
    available_kb=$(df "${BACKUP_DIR}" | tail -1 | awk '{print $4}')
    local min_required_kb=$((1024 * 1024))  # 1 GB minimum

    if [ "${available_kb}" -lt "${min_required_kb}" ]; then
        log_alert "Storage below 1GB threshold (${available_kb}KB available). Purging expired backups first."
        purge_old_backups
        # Re-check after purge
        available_kb=$(df "${BACKUP_DIR}" | tail -1 | awk '{print $4}')
        if [ "${available_kb}" -lt "${min_required_kb}" ]; then
            log_alert "CAPACITY ALERT: Storage still insufficient after purge (${available_kb}KB). Backup aborted."
            exit 1
        fi
    fi
}

# Step 2: Run pg_basebackup
run_backup() {
    log_info "Starting base backup to ${BACKUP_PATH}"
    if pg_basebackup \
        -h "${PG_HOST}" \
        -p "${PG_PORT}" \
        -U "${PG_USER}" \
        -D "${BACKUP_PATH}" \
        -Ft -z \
        -Xs \
        --checkpoint=fast \
        --progress 2>&1; then
        log_info "Backup completed successfully: ${BACKUP_PATH}"
        # Write completion record (AC-1)
        echo "${TIMESTAMP}|SUCCESS|${BACKUP_PATH}" >> "${BACKUP_DIR}/backup.log"
    else
        local exit_code=$?
        log_error "Backup FAILED with exit code ${exit_code}"
        # Record failure (AC-3)
        echo "${TIMESTAMP}|FAILED|exit_code=${exit_code}" >> "${BACKUP_DIR}/backup.log"
        log_alert "BACKUP FAILURE: pg_basebackup exited with code ${exit_code}. Check PostgreSQL logs."
        exit "${exit_code}"
    fi
}

# Step 3: Purge backups older than retention window (AC-4)
purge_old_backups() {
    log_info "Purging backups older than ${RETENTION_DAYS} days"
    local count=0
    while IFS= read -r -d '' dir; do
        log_info "Removing expired backup: ${dir}"
        rm -rf "${dir}"
        count=$((count + 1))
    done < <(find "${BACKUP_DIR}" -maxdepth 1 -name "base_*" -type d -mtime "+${RETENTION_DAYS}" -print0)

    # Also purge old WAL files that are no longer needed
    if [ -d "${WAL_ARCHIVE_DIR}" ]; then
        local oldest_backup
        oldest_backup=$(ls -1d "${BACKUP_DIR}"/base_* 2>/dev/null | head -1 || echo "")
        if [ -n "${oldest_backup}" ]; then
            log_info "Retaining WAL files needed for oldest backup."
        fi
    fi

    log_info "Purged ${count} expired backup(s)"
}

# Main execution
main() {
    log_info "=== Automated backup job started ==="
    check_storage
    run_backup
    purge_old_backups
    log_info "=== Automated backup job completed ==="
}

main "$@"
```

Key flags for `pg_basebackup`:
- `-Ft -z`: tar format with gzip compression (space efficient)
- `-Xs`: Stream WAL during backup (ensures WAL files needed for PITR are included)
- `--checkpoint=fast`: Starts backup immediately without waiting for normal checkpoint
- `--progress`: Outputs progress for logging

3. **Create `infra/backup/Dockerfile`** for the backup sidecar container running cron:

```dockerfile
FROM postgres:15-alpine

RUN apk add --no-cache bash dcron

COPY backup.sh /usr/local/bin/backup.sh
RUN chmod +x /usr/local/bin/backup.sh

# Create cron schedule from environment variable
# Default: every 6 hours (0 */6 * * *)
COPY entrypoint.sh /usr/local/bin/entrypoint.sh
RUN chmod +x /usr/local/bin/entrypoint.sh

ENTRYPOINT ["/usr/local/bin/entrypoint.sh"]
```

4. **Create `infra/backup/entrypoint.sh`** that writes the cron schedule from environment and starts the cron daemon:

```bash
#!/usr/bin/env bash
set -euo pipefail

CRON_SCHEDULE="${BACKUP_SCHEDULE_CRON:-0 */6 * * *}"

# Write crontab with environment passthrough
printenv | grep -E '^(PGHOST|PGPORT|PGUSER|PGPASSWORD|BACKUP_|WAL_)' > /etc/environment
echo "${CRON_SCHEDULE} /usr/local/bin/backup.sh >> /proc/1/fd/1 2>> /proc/1/fd/2" > /etc/crontabs/root

echo "[entrypoint] Backup sidecar started. Schedule: ${CRON_SCHEDULE}"
echo "[entrypoint] Running initial backup..."
/usr/local/bin/backup.sh

# Start cron in foreground
crond -f -l 2
```

5. **Add backup sidecar service** to `docker-compose.yml`:

```yaml
backup:
  build:
    context: ./infra/backup
    dockerfile: Dockerfile
  container_name: propeliq-backup
  environment:
    - PGHOST=postgres
    - PGPORT=5432
    - PGUSER=replication_user
    - PGPASSWORD=${POSTGRES_REPLICATION_PASSWORD:-repl_pass}
    - BACKUP_DIR=/backups
    - WAL_ARCHIVE_DIR=/wal_archive
    - BACKUP_RETENTION_DAYS=${BACKUP_RETENTION_DAYS:-7}
    - BACKUP_SCHEDULE_CRON=${BACKUP_SCHEDULE_CRON:-0 */6 * * *}
  volumes:
    - backups:/backups
    - wal-archive:/wal_archive
  depends_on:
    postgres:
      condition: service_healthy
  restart: unless-stopped
```

6. **Create the replication user** in the PostgreSQL init script for `pg_basebackup` authentication (least-privilege — `REPLICATION` role only):

```sql
-- In infra/postgres/init.sql (append)
CREATE ROLE replication_user WITH REPLICATION LOGIN PASSWORD 'repl_pass';
```

Also add to `pg_hba.conf` (via Docker command or init script):
```
host replication replication_user 0.0.0.0/0 scram-sha-256
```

7. **Add named volumes** to `docker-compose.yml`:

```yaml
volumes:
  pgdata:
  wal-archive:
  backups:
```

8. **Update `.env.example`** with configurable backup parameters:

```bash
# Backup Configuration
BACKUP_RETENTION_DAYS=7
BACKUP_SCHEDULE_CRON=0 */6 * * *
POSTGRES_REPLICATION_PASSWORD=repl_pass
```

## Current Project State

```text
propelIQ/
├── docker-compose.yml       (from US_005)
├── .env.example
├── infra/
│   ├── postgres/
│   │   └── init.sql         (from US_003)
│   ├── otel-collector/      (from US_007)
│   ├── prometheus/           (from US_007)
│   ├── grafana/              (from US_007)
│   └── loki/                 (from US_007)
└── server/
```

> Placeholder: Update on execution based on US_003 and US_005 task completion state.

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | infra/backup/backup.sh | Automated pg_basebackup script with logging, retention purge, storage check |
| CREATE | infra/backup/entrypoint.sh | Cron setup and initial backup on container start |
| CREATE | infra/backup/Dockerfile | Backup sidecar container with postgres client tools and cron |
| MODIFY | docker-compose.yml | Add backup sidecar service, WAL archive config on postgres, named volumes |
| MODIFY | infra/postgres/init.sql | Add replication_user role for pg_basebackup authentication |
| MODIFY | .env.example | Add BACKUP_RETENTION_DAYS, BACKUP_SCHEDULE_CRON, POSTGRES_REPLICATION_PASSWORD |

## External References

- PostgreSQL continuous archiving and PITR: https://www.postgresql.org/docs/15/continuous-archiving.html
- pg_basebackup reference: https://www.postgresql.org/docs/15/app-pgbasebackup.html
- PostgreSQL WAL configuration: https://www.postgresql.org/docs/15/runtime-config-wal.html
- PostgreSQL archive_command: https://www.postgresql.org/docs/15/runtime-config-wal.html#GUC-ARCHIVE-COMMAND
- PostgreSQL replication roles: https://www.postgresql.org/docs/15/role-attributes.html
- Docker Compose volumes: https://docs.docker.com/compose/compose-file/07-volumes/
- Alpine cron (dcron): https://wiki.alpinelinux.org/wiki/Setting_Up_cron

## Build Commands

```bash
# Build backup sidecar image
docker compose build backup

# Start PostgreSQL with WAL archiving + backup sidecar
docker compose up -d postgres backup

# View backup logs
docker compose logs backup --tail 50

# Manually trigger backup (for testing)
docker exec propeliq-backup /usr/local/bin/backup.sh

# List backups
docker exec propeliq-backup ls -la /backups/

# Check WAL archive
docker exec propeliq-backup ls -la /wal_archive/

# View backup completion log
docker exec propeliq-backup cat /backups/backup.log
```

## Implementation Validation Strategy

- [ ] Backup sidecar container starts and runs initial backup on startup
- [ ] Cron job triggers `pg_basebackup` every 6 hours (verify via backup.log timestamps) (AC-1)
- [ ] Completion record written to `backup.log` with timestamp and status on success (AC-1)
- [ ] Failed backup records error reason in `backup.log` and outputs ALERT to stderr (AC-3)
- [ ] Backups older than `BACKUP_RETENTION_DAYS` are purged automatically after each backup run (AC-4)
- [ ] WAL archive directory receives continuous WAL segments from PostgreSQL
- [ ] Storage-full scenario triggers purge-then-retry behavior before failing (edge case)
- [ ] `replication_user` has only REPLICATION privilege (least-privilege)

## Implementation Checklist

- [ ] Enable WAL archiving on PostgreSQL service: `wal_level=replica`, `archive_mode=on`, `archive_command`, `archive_timeout=300`
- [ ] Create `infra/backup/backup.sh` with `pg_basebackup`, completion logging, error handling with ALERT output, and retention purge
- [ ] Create `infra/backup/entrypoint.sh` with dynamic cron schedule from `BACKUP_SCHEDULE_CRON` and initial backup on start
- [ ] Create `infra/backup/Dockerfile` using `postgres:15-alpine` with cron and backup script
- [ ] Add backup sidecar service to `docker-compose.yml` with `wal-archive` and `backups` named volumes
- [ ] Create `replication_user` role with REPLICATION privilege in `infra/postgres/init.sql`
- [ ] Implement pre-backup storage check that purges expired backups before retrying on low space
- [ ] Add `BACKUP_RETENTION_DAYS`, `BACKUP_SCHEDULE_CRON`, `POSTGRES_REPLICATION_PASSWORD` to `.env.example`
