#!/usr/bin/env bash
set -euo pipefail

# ── Configuration ─────────────────────────────────────────────────────────────
BACKUP_DIR="${BACKUP_DIR:-/backups}"
VERIFY_PORT=5434
RESTORE_DIR="/tmp/verify_restore"
QUERIES_FILE="/usr/local/bin/verify-queries.sql"
LOG_PREFIX="[verify][$(date +%Y%m%d_%H%M%S)]"

log_info()  { echo "${LOG_PREFIX} INFO: $*"; }
log_error() { echo "${LOG_PREFIX} ERROR: $*" >&2; }

# ── Cleanup on exit (ensures temp instance is always stopped) ─────────────────
cleanup() {
    log_info "Cleaning up verification instance"
    pg_ctl -D "${RESTORE_DIR}" -m fast stop 2>/dev/null || true
    rm -rf "${RESTORE_DIR}"
}
trap cleanup EXIT

# ── Find most recent backup ──────────────────────────────────────────────────
latest_backup=$(ls -1d "${BACKUP_DIR}"/base_* 2>/dev/null | sort -r | head -1)
if [ -z "${latest_backup}" ]; then
    log_error "No backups found to verify"
    echo "$(date +%Y%m%d_%H%M%S)|VERIFY_FAILED|no_backups" >> "${BACKUP_DIR}/backup.log"
    exit 1
fi

log_info "=== Backup verification started ==="
log_info "Verifying backup: ${latest_backup}"

# ── Restore to temporary directory ────────────────────────────────────────────
rm -rf "${RESTORE_DIR}"
mkdir -p "${RESTORE_DIR}"
tar xzf "${latest_backup}/base.tar.gz" -C "${RESTORE_DIR}"

if [ -f "${latest_backup}/pg_wal.tar.gz" ]; then
    mkdir -p "${RESTORE_DIR}/pg_wal"
    tar xzf "${latest_backup}/pg_wal.tar.gz" -C "${RESTORE_DIR}/pg_wal"
fi

# Remove recovery.signal if present (start as primary, not in recovery).
rm -f "${RESTORE_DIR}/recovery.signal"

chown -R postgres:postgres "${RESTORE_DIR}"
chmod 700 "${RESTORE_DIR}"

# ── Start temporary instance ──────────────────────────────────────────────────
pg_ctl -D "${RESTORE_DIR}" -o "-p ${VERIFY_PORT}" -w start
log_info "Verification instance started on port ${VERIFY_PORT}"

# ── Run validation queries ────────────────────────────────────────────────────
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
    # Fallback: basic connectivity and table check.
    table_count=$(psql -p "${VERIFY_PORT}" -U postgres -d propeliq -tAc \
        "SELECT count(*) FROM pg_catalog.pg_tables WHERE schemaname = 'app';" 2>&1 || echo "0")
    if [ "${table_count}" -gt 0 ]; then
        log_info "Basic validation passed: ${table_count} tables found"
    else
        log_error "Basic validation failed: no application tables found"
        verify_result="FAIL"
    fi
fi

# ── Record result ─────────────────────────────────────────────────────────────
echo "$(date +%Y%m%d_%H%M%S)|VERIFY_${verify_result}|${latest_backup}" >> "${BACKUP_DIR}/backup.log"
log_info "=== Backup verification ${verify_result} ==="

if [ "${verify_result}" = "FAIL" ]; then
    exit 1
fi
