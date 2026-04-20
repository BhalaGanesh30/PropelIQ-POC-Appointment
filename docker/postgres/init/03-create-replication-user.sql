-- PropelIQ PostgreSQL Replication User Initialization
-- Executed after 02-create-schemas.sql on first container start.
-- Creates a least-privilege replication role used exclusively by the backup
-- sidecar's pg_basebackup (US_011, AC-1). The role has REPLICATION privilege
-- only — no superuser, no createdb, no createrole.

DO $$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'replication_user') THEN
    CREATE ROLE replication_user WITH REPLICATION LOGIN PASSWORD 'repl_pass';
    RAISE NOTICE 'Created replication_user role for pg_basebackup.';
  ELSE
    RAISE NOTICE 'replication_user role already exists — skipping.';
  END IF;
END $$;
