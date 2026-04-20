#!/usr/bin/env bash
set -euo pipefail

# ── Arguments ─────────────────────────────────────────────────────────────────
TARGET_TIMESTAMP="${1:?Usage: restore.sh <target_timestamp> [restore_port]}"
RESTORE_PORT="${2:-5433}"

# ── Configuration ─────────────────────────────────────────────────────────────
BACKUP_DIR="${BACKUP_DIR:-/backups}"
WAL_ARCHIVE_DIR="${WAL_ARCHIVE_DIR:-/wal_archive}"
RESTORE_DIR="/tmp/restore_${RESTORE_PORT}"
LOG_PREFIX="[restore][$(date +%Y%m%d_%H%M%S)]"
RTO_TIMEOUT=14400  # 4 hours in seconds (NFR-006)

log_info()  { echo "${LOG_PREFIX} INFO: $*"; }
log_error() { echo "${LOG_PREFIX} ERROR: $*" >&2; }

# ── Step 1: Find the most recent base backup before target timestamp ──────────
find_backup() {
    local target_epoch
    target_epoch=$(date -d "${TARGET_TIMESTAMP}" +%s 2>/dev/null) || {
        log_error "Invalid timestamp format: ${TARGET_TIMESTAMP}. Use 'YYYY-MM-DD HH:MM:SS'."
        exit 1
    }

    local selected_backup=""
    for backup_dir in $(ls -1d "${BACKUP_DIR}"/base_* 2>/dev/null | sort -r); do
        local backup_name
        backup_name=$(basename "${backup_dir}")
        # Parse base_YYYYMMDD_HHMMSS → epoch
        local backup_date_str
        backup_date_str=$(echo "${backup_name}" | sed 's/base_//' | sed 's/\([0-9]\{8\}\)_\([0-9]\{2\}\)\([0-9]\{2\}\)\([0-9]\{2\}\)/\1 \2:\3:\4/')
        local backup_epoch
        backup_epoch=$(date -d "${backup_date_str}" +%s 2>/dev/null || echo "0")

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

# ── Step 2: Restore base backup to temporary directory ────────────────────────
restore_base() {
    local backup_path="$1"
    log_info "Restoring base backup from: ${backup_path}"

    rm -rf "${RESTORE_DIR}"
    mkdir -p "${RESTORE_DIR}"

    tar xzf "${backup_path}/base.tar.gz" -C "${RESTORE_DIR}"

    if [ -f "${backup_path}/pg_wal.tar.gz" ]; then
        mkdir -p "${RESTORE_DIR}/pg_wal"
        tar xzf "${backup_path}/pg_wal.tar.gz" -C "${RESTORE_DIR}/pg_wal"
    fi

    log_info "Base backup restored to: ${RESTORE_DIR}"
}

# ── Step 3: Configure recovery for PITR ───────────────────────────────────────
configure_recovery() {
    log_info "Configuring recovery to target: ${TARGET_TIMESTAMP}"

    # PostgreSQL 12+ uses postgresql.auto.conf + recovery.signal
    cat > "${RESTORE_DIR}/postgresql.auto.conf" <<EOF
restore_command = 'cp ${WAL_ARCHIVE_DIR}/%f %p'
recovery_target_time = '${TARGET_TIMESTAMP}'
recovery_target_action = 'promote'
EOF

    touch "${RESTORE_DIR}/recovery.signal"
    log_info "Recovery configuration written"
}

# ── Step 4: Start temporary PostgreSQL for WAL replay ─────────────────────────
start_recovery() {
    log_info "Starting temporary PostgreSQL on port ${RESTORE_PORT} for WAL replay"

    chown -R postgres:postgres "${RESTORE_DIR}"
    chmod 700 "${RESTORE_DIR}"

    pg_ctl -D "${RESTORE_DIR}" -o "-p ${RESTORE_PORT}" -w start

    log_info "Recovery instance started. WAL replay in progress..."

    # Wait for recovery to complete (recovery.signal is removed on promote)
    local waited=0
    while [ -f "${RESTORE_DIR}/recovery.signal" ] && [ "${waited}" -lt "${RTO_TIMEOUT}" ]; do
        sleep 10
        waited=$((waited + 10))
        if [ $((waited % 300)) -eq 0 ]; then
            log_info "Recovery in progress... (${waited}s elapsed)"
        fi
    done

    if [ "${waited}" -ge "${RTO_TIMEOUT}" ]; then
        log_error "Recovery exceeded 4-hour RTO target. Aborting."
        pg_ctl -D "${RESTORE_DIR}" -m fast stop 2>/dev/null || true
        exit 1
    fi

    log_info "Recovery completed in ${waited} seconds"
}

# ── Step 5: Validate restored data ───────────────────────────────────────────
validate_restore() {
    log_info "Validating restored database"

    local result
    result=$(psql -p "${RESTORE_PORT}" -U postgres -d propeliq -tAc \
        "SELECT count(*) FROM pg_catalog.pg_tables WHERE schemaname = 'app';" 2>&1)

    if [ "$?" -eq 0 ] && [ "${result}" -gt 0 ]; then
        log_info "Validation passed: ${result} application tables found"
    else
        log_error "Validation failed: Could not query restored database"
        return 1
    fi
}

# ── Main ──────────────────────────────────────────────────────────────────────
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
