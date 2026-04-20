#!/bin/bash
# PropelIQ — Append replication entry to pg_hba.conf
# Runs after SQL init scripts (04-* prefix, .sh extension).
# Grants replication_user SCRAM-SHA-256 replication access from Docker network.

set -euo pipefail

HBA_FILE="${PGDATA}/pg_hba.conf"

if ! grep -q 'replication_user' "${HBA_FILE}" 2>/dev/null; then
    echo "host replication replication_user 0.0.0.0/0 scram-sha-256" >> "${HBA_FILE}"
    echo "[init] Added replication_user entry to pg_hba.conf"
else
    echo "[init] replication_user already in pg_hba.conf — skipping"
fi
