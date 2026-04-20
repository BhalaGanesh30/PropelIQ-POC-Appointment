#!/usr/bin/env bash
set -euo pipefail

CRON_SCHEDULE="${BACKUP_SCHEDULE_CRON:-0 */6 * * *}"

# Write environment variables needed by the backup script into /etc/environment
# so cron jobs inherit them (cron runs with a minimal environment).
printenv | grep -E '^(PGHOST|PGPORT|PGUSER|PGPASSWORD|BACKUP_|WAL_)' > /etc/environment

# Write the cron schedule; redirect stdout/stderr to PID 1 for Docker log aggregation.
echo "${CRON_SCHEDULE} . /etc/environment; /usr/local/bin/backup.sh >> /proc/1/fd/1 2>> /proc/1/fd/2" > /etc/crontabs/root

# Weekly restore verification (default: Sunday 03:00 UTC).
VERIFY_SCHEDULE="${BACKUP_VERIFY_CRON:-0 3 * * 0}"
echo "${VERIFY_SCHEDULE} . /etc/environment; /usr/local/bin/verify-backup.sh >> /proc/1/fd/1 2>> /proc/1/fd/2" >> /etc/crontabs/root

echo "[entrypoint] Backup sidecar started. Schedule: ${CRON_SCHEDULE}"
echo "[entrypoint] Verification schedule: ${VERIFY_SCHEDULE}"
echo "[entrypoint] Running initial backup..."
/usr/local/bin/backup.sh

# Start cron in foreground (keeps container alive).
crond -f -l 2
