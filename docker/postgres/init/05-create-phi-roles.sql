-- PropelIQ PostgreSQL Role Initialization (PHI Access Policies)
-- Executed after 02-create-schemas.sql on first container start.
-- Creates three application roles per DR-008 / TR-006 least-privilege model.

DO $$
BEGIN
  -- app_api: API service role — permitted PHI columns for direct patient care.
  IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'app_api') THEN
    CREATE ROLE app_api WITH LOGIN PASSWORD 'api_pass' NOINHERIT;
    COMMENT ON ROLE app_api IS 'API service role — permitted PHI columns for direct patient care';
    RAISE NOTICE 'Created role: app_api';
  ELSE
    RAISE NOTICE 'Role app_api already exists — skipping.';
  END IF;

  -- app_analytics: Reporting role — de-identified access only, no direct PHI.
  IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'app_analytics') THEN
    CREATE ROLE app_analytics WITH LOGIN PASSWORD 'analytics_pass' NOINHERIT;
    COMMENT ON ROLE app_analytics IS 'Analytics role — no direct PHI access, de-identified views only';
    RAISE NOTICE 'Created role: app_analytics';
  ELSE
    RAISE NOTICE 'Role app_analytics already exists — skipping.';
  END IF;

  -- app_admin: Administrative role — full PHI access, audited via pgaudit.
  IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'app_admin') THEN
    CREATE ROLE app_admin WITH LOGIN PASSWORD 'admin_pass' NOINHERIT;
    COMMENT ON ROLE app_admin IS 'Admin role — full PHI access with pgaudit logging';
    RAISE NOTICE 'Created role: app_admin';
  ELSE
    RAISE NOTICE 'Role app_admin already exists — skipping.';
  END IF;

  -- Grant schema usage to all roles.
  GRANT USAGE ON SCHEMA app TO app_api, app_analytics, app_admin;
  GRANT USAGE ON SCHEMA audit TO app_admin;
END $$;
