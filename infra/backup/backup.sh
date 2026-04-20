#!/usr/bin/env bash
set -euo pipefail

# ── Configuration (from environment) ──────────────────────────────────────────
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

# ── Step 1: Pre-flight storage check (edge case: storage full) ────────────────
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
            echo "${TIMESTAMP}|FAILED|CAPACITY_ALERT" >> "${BACKUP_DIR}/backup.log"
            exit 1
        fi
    fi
}

# ── Step 2: Run pg_basebackup ─────────────────────────────────────────────────
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

# ── Step 3: Purge backups older than retention window (AC-4) ──────────────────
purge_old_backups() {
    log_info "Purging backups older than ${RETENTION_DAYS} days"
    local count=0
    while IFS= read -r -d '' dir; do
        log_info "Removing expired backup: ${dir}"
        rm -rf "${dir}"
        count=$((count + 1))
    done < <(find "${BACKUP_DIR}" -maxdepth 1 -name "base_*" -type d -mtime "+${RETENTION_DAYS}" -print0)
    log_info "Purged ${count} expired backup(s)"
}

# ── Main ──────────────────────────────────────────────────────────────────────
main() {
    log_info "=== Automated backup job started ==="
    mkdir -p "${BACKUP_DIR}"
    check_storage
    run_backup
    purge_old_backups
    log_info "=== Automated backup job completed ==="
}

main "$@"
