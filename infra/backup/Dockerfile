# Backup sidecar — runs pg_basebackup on a cron schedule.
# Uses postgres:15-alpine for bundled pg_basebackup client tools.
FROM postgres:15-alpine

RUN apk add --no-cache bash dcron

COPY backup.sh /usr/local/bin/backup.sh
COPY restore.sh /usr/local/bin/restore.sh
COPY verify-backup.sh /usr/local/bin/verify-backup.sh
COPY verify-queries.sql /usr/local/bin/verify-queries.sql
COPY entrypoint.sh /usr/local/bin/entrypoint.sh
RUN chmod +x /usr/local/bin/backup.sh /usr/local/bin/restore.sh \
    /usr/local/bin/verify-backup.sh /usr/local/bin/entrypoint.sh

ENTRYPOINT ["/usr/local/bin/entrypoint.sh"]
