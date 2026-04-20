-- PropelIQ PostgreSQL Schema Initialization
-- Executed after 01-create-extensions.sql on first container start.
-- Establishes the two application schemas used throughout the data model:
--
--   app    — primary application tables (appointments, clinicians, patients, etc.)
--   audit  — append-only audit trail tables (DR-002, NFR-007, AC-3 compliance)
--
-- All application tables created by EF Core migrations use the app schema.
-- All audit tables are INSERT-only with REVOKE UPDATE/DELETE per DR-005.

-- ── Application schema ───────────────────────────────────────────────────────
-- Owns all domain entity tables produced by EF Core migrations.
CREATE SCHEMA IF NOT EXISTS app;

-- ── Audit schema ─────────────────────────────────────────────────────────────
-- Append-only audit trail. EF Core migrations MUST NOT add UPDATE/DELETE
-- permissions on tables in this schema (DR-005 immutability requirement).
CREATE SCHEMA IF NOT EXISTS audit;

-- ── Compliance schema ─────────────────────────────────────────────────────────
-- AI audit logs, outbox, and compliance artefacts (US_055 EP-009).
CREATE SCHEMA IF NOT EXISTS compliance;

-- Set default search path for the application user so unqualified table names
-- resolve to the app schema first.
-- The application user name matches POSTGRES_USER from the .env file.
DO $$
BEGIN
  EXECUTE format(
    'ALTER ROLE %I SET search_path = app, public',
    current_user
  );
END $$;

RAISE NOTICE 'Schemas app, audit, compliance created and search_path configured.';
