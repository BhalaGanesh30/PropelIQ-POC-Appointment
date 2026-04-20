# Disaster Recovery Runbook — Point-in-Time Recovery

## Overview

This runbook describes how to restore the PropelIQ PostgreSQL database to a
specific point in time using continuous WAL archiving and `pg_basebackup`.
All operations execute inside the backup sidecar container (`propeliq-backup`).

## RTO and RPO Targets

| Metric | Target | Actual Bound |
|--------|--------|--------------|
| RPO (Recovery Point Objective) | 1 hour (NFR-006) | ~5 minutes (`archive_timeout=300`) |
| RTO (Recovery Time Objective) | 4 hours (NFR-006) | Enforced by `restore.sh` timeout guard |

## Prerequisites

- Docker Compose stack is running (`docker compose ps` shows healthy postgres and backup).
- Backup and WAL archive volumes are intact (`backups`, `wal-archive`).
- Target timestamp is within the retention window (`BACKUP_RETENTION_DAYS`, default 7).
- Target timestamp is in **UTC** and uses `YYYY-MM-DD HH:MM:SS` format.

## Recovery Procedure

### Step 1 — Identify Target Timestamp

Determine the exact UTC timestamp to restore to. Review application logs or
incident timeline to select the moment just before data loss or corruption.

```bash
# Check available backups and their timestamps
docker exec propeliq-backup ls -lt /backups/base_*

# Check backup completion log
docker exec propeliq-backup cat /backups/backup.log
```

### Step 2 — Execute Point-in-Time Recovery

Run the restore script with the target timestamp. The script automatically
selects the correct base backup, replays WAL segments, and validates the result.

```bash
docker exec propeliq-backup /usr/local/bin/restore.sh "2026-04-20 14:30:00"
```

The restored database starts on port **5433** (configurable via second argument).

### Step 3 — Validate Restored Data

Connect to the restored instance and verify critical data:

```bash
# Connect to the restored instance
docker exec -it propeliq-backup psql -p 5433 -U postgres -d propeliq

# Run verification queries
docker exec propeliq-backup psql -p 5433 -U postgres -d propeliq \
    -f /usr/local/bin/verify-queries.sql
```

### Step 4 — Promote or Migrate

Choose one approach based on the recovery scenario:

**Option A — Replace primary (full outage recovery):**

```bash
# Stop the current primary
docker compose stop postgres

# Copy restored data to the primary volume (destructive — backs up old data first)
docker exec propeliq-backup sh -c \
    "cp -a /tmp/restore_5433/* /var/lib/postgresql/data/"

# Restart postgres with restored data
docker compose start postgres
```

**Option B — Selective data migration (partial corruption):**

```bash
# Dump specific tables from the restored instance
docker exec propeliq-backup pg_dump -p 5433 -U postgres -d propeliq \
    --schema=app -t app.appointments > /tmp/appointments_restore.sql

# Apply to production primary
docker exec propeliq-backup psql -p 5432 -h postgres -U propeliq_user \
    -d propeliq < /tmp/appointments_restore.sql
```

### Step 5 — Clean Up

Stop the temporary restore instance after migration is complete:

```bash
docker exec propeliq-backup pg_ctl -D /tmp/restore_5433 stop
```

## Weekly Verification

Automated verification runs every **Sunday at 03:00 UTC**. The job restores the
most recent backup to a temporary instance, runs validation queries, and logs
the result.

```bash
# Check latest verification result
docker exec propeliq-backup grep VERIFY /backups/backup.log | tail -5

# Manually trigger verification
docker exec propeliq-backup /usr/local/bin/verify-backup.sh
```

## Troubleshooting

| Symptom | Cause | Resolution |
|---------|-------|------------|
| `No base backup found before target timestamp` | Target precedes oldest backup | Choose a timestamp within the retention window |
| `Recovery exceeded 4-hour RTO target` | Large WAL replay or slow I/O | Check disk performance; consider more frequent base backups |
| `Validation failed: no application tables` | Backup may be corrupted | Try the next-most-recent backup |
| `restore_command failed` | Missing WAL segments in archive | Verify `wal-archive` volume is intact |

## Contacts

| Role | Responsibility |
|------|---------------|
| On-call DBA | Execute recovery procedure, validate data |
| Engineering Lead | Approve production cutover decision |
| Incident Commander | Coordinate communication, track RTO |
