-- =============================================================================
-- PropelIQ — DBeaver Database Scaffold
-- =============================================================================
-- Run this script against a blank PostgreSQL 15 database to recreate the full
-- PropelIQ schema without EF Core.
--
-- Prerequisites:
--   • PostgreSQL 15+ with pgvector image  (pgvector/pgvector:pg15)
--   • Connect as superuser (postgres) to run extension and role DDL
--
-- DBeaver connection:
--   Host:     localhost   Port: 5432
--   Database: propeliq
--   User:     postgres    Password: (from .env POSTGRES_PASSWORD)
--
-- Sections:
--   1. Extensions
--   2. Schemas
--   3. Application Roles (PHI access control)
--   4. Tables & Constraints (creation order respects FK deps)
--   5. Indexes
--   6. Audit immutability trigger
--   7. tenant_id columns & indexes
--   8. Column-level GRANTs (PHI classification)
--   9. Row-Level Security (RLS) policies
--  10. De-identified analytics view
--  11. pgaudit role configuration
-- =============================================================================

-- ── 1. Extensions ─────────────────────────────────────────────────────────────
-- pgvector is optional — only available when running on pgvector/pgvector:pg15 image.
-- Skip silently if not installed (embedding_samples table is omitted from this scaffold).
DO $$ BEGIN
    CREATE EXTENSION IF NOT EXISTS vector;
EXCEPTION WHEN OTHERS THEN
    RAISE NOTICE 'pgvector not available on this server — vector search features disabled.';
END $$;

CREATE EXTENSION IF NOT EXISTS "uuid-ossp";
CREATE EXTENSION IF NOT EXISTS pg_trgm;

-- pgaudit is optional — available when shared_preload_libraries includes pgaudit.
DO $$ BEGIN
    CREATE EXTENSION IF NOT EXISTS pgaudit;
EXCEPTION WHEN OTHERS THEN
    RAISE NOTICE 'pgaudit not available — audit logging will use application-level logging only.';
END $$;

-- ── 2. Schemas ────────────────────────────────────────────────────────────────
CREATE SCHEMA IF NOT EXISTS app;
CREATE SCHEMA IF NOT EXISTS audit;
CREATE SCHEMA IF NOT EXISTS compliance;

-- ── 3. Application Roles ──────────────────────────────────────────────────────
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'app_api') THEN
        CREATE ROLE app_api WITH LOGIN PASSWORD 'api_pass' NOINHERIT;
        COMMENT ON ROLE app_api IS 'API service role — permitted PHI columns for direct patient care';
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'app_analytics') THEN
        CREATE ROLE app_analytics WITH LOGIN PASSWORD 'analytics_pass' NOINHERIT;
        COMMENT ON ROLE app_analytics IS 'Analytics role — no direct PHI access, de-identified views only';
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'app_admin') THEN
        CREATE ROLE app_admin WITH LOGIN PASSWORD 'admin_pass' NOINHERIT;
        COMMENT ON ROLE app_admin IS 'Admin role — full PHI access with pgaudit logging';
    END IF;
END $$;

GRANT USAGE ON SCHEMA app TO app_api, app_analytics, app_admin;
GRANT USAGE ON SCHEMA audit TO app_admin;

-- ── 4. Tables ─────────────────────────────────────────────────────────────────
-- Order: independent → dependents (users → patients → appointments → …)

CREATE TABLE IF NOT EXISTS app.users (
    id              uuid                     NOT NULL DEFAULT gen_random_uuid(),
    email           character varying(254)   NOT NULL,
    password_hash   text                     NOT NULL,
    role            character varying(50)    NOT NULL,
    first_name      character varying(100),
    last_name       character varying(100),
    is_active       boolean                  NOT NULL,
    last_login_at   timestamp with time zone,
    created_at      timestamp with time zone NOT NULL DEFAULT now(),
    updated_at      timestamp with time zone NOT NULL,
    CONSTRAINT pk_users PRIMARY KEY (id)
);

CREATE TABLE IF NOT EXISTS app.patients (
    id                  uuid                     NOT NULL DEFAULT gen_random_uuid(),
    user_id             uuid                     NOT NULL,
    first_name          character varying(100)   NOT NULL,
    last_name           character varying(100)   NOT NULL,
    date_of_birth       date                     NOT NULL,
    mrn                 character varying(50)    NOT NULL,
    contact_preferences jsonb                    NOT NULL,
    created_at          timestamp with time zone NOT NULL DEFAULT now(),
    updated_at          timestamp with time zone NOT NULL,
    CONSTRAINT pk_patients PRIMARY KEY (id),
    CONSTRAINT fk_patients_users_user_id
        FOREIGN KEY (user_id) REFERENCES app.users (id) ON DELETE RESTRICT
);

CREATE TABLE IF NOT EXISTS app.appointments (
    id               uuid                     NOT NULL DEFAULT gen_random_uuid(),
    patient_id       uuid                     NOT NULL,
    staff_user_id    uuid                     NOT NULL,
    scheduled_at     timestamp with time zone NOT NULL,
    duration_minutes integer                  NOT NULL,
    appointment_type character varying(100)   NOT NULL,
    status           character varying(50)    NOT NULL,
    queue_state      character varying(50)    NOT NULL,
    created_at       timestamp with time zone NOT NULL DEFAULT now(),
    updated_at       timestamp with time zone NOT NULL,
    CONSTRAINT pk_appointments PRIMARY KEY (id),
    CONSTRAINT fk_appointments_patients_patient_id
        FOREIGN KEY (patient_id) REFERENCES app.patients (id) ON DELETE RESTRICT,
    CONSTRAINT fk_appointments_users_staff_user_id
        FOREIGN KEY (staff_user_id) REFERENCES app.users (id) ON DELETE RESTRICT
);

CREATE TABLE IF NOT EXISTS app.clinical_documents (
    id               uuid                     NOT NULL DEFAULT gen_random_uuid(),
    patient_id       uuid                     NOT NULL,
    file_name        character varying(500)   NOT NULL,
    category         character varying(50)    NOT NULL,
    extraction_status character varying(50)   NOT NULL,
    storage_path     character varying(1000),
    created_at       timestamp with time zone NOT NULL DEFAULT now(),
    updated_at       timestamp with time zone NOT NULL,
    CONSTRAINT pk_clinical_documents PRIMARY KEY (id),
    CONSTRAINT fk_clinical_documents_patients_patient_id
        FOREIGN KEY (patient_id) REFERENCES app.patients (id) ON DELETE RESTRICT
);

CREATE TABLE IF NOT EXISTS app.insurance_profiles (
    id                  uuid                     NOT NULL DEFAULT gen_random_uuid(),
    patient_id          uuid                     NOT NULL,
    payer_name          character varying(200)   NOT NULL,
    member_id           character varying(100)   NOT NULL,
    is_primary          boolean                  NOT NULL,
    verification_status character varying(50)    NOT NULL,
    created_at          timestamp with time zone NOT NULL DEFAULT now(),
    updated_at          timestamp with time zone NOT NULL,
    CONSTRAINT pk_insurance_profiles PRIMARY KEY (id),
    CONSTRAINT fk_insurance_profiles_patients_patient_id
        FOREIGN KEY (patient_id) REFERENCES app.patients (id) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS app.reminder_events (
    id                    uuid                     NOT NULL DEFAULT gen_random_uuid(),
    appointment_id        uuid                     NOT NULL,
    channel               character varying(50)    NOT NULL,
    send_status           character varying(50)    NOT NULL,
    confirmation_response character varying(500),
    retry_count           integer                  NOT NULL,
    sent_at               timestamp with time zone,
    created_at            timestamp with time zone NOT NULL DEFAULT now(),
    updated_at            timestamp with time zone NOT NULL,
    CONSTRAINT pk_reminder_events PRIMARY KEY (id),
    CONSTRAINT fk_reminder_events_appointments_appointment_id
        FOREIGN KEY (appointment_id) REFERENCES app.appointments (id) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS app.waitlist_entries (
    id             uuid                     NOT NULL DEFAULT gen_random_uuid(),
    patient_id     uuid                     NOT NULL,
    appointment_id uuid,
    priority       integer                  NOT NULL,
    status         character varying(50)    NOT NULL,
    offered_at     timestamp with time zone,
    created_at     timestamp with time zone NOT NULL DEFAULT now(),
    updated_at     timestamp with time zone NOT NULL,
    CONSTRAINT pk_waitlist_entries PRIMARY KEY (id),
    CONSTRAINT fk_waitlist_entries_patients_patient_id
        FOREIGN KEY (patient_id) REFERENCES app.patients (id) ON DELETE RESTRICT,
    CONSTRAINT fk_waitlist_entries_appointments_appointment_id
        FOREIGN KEY (appointment_id) REFERENCES app.appointments (id) ON DELETE SET NULL
);

CREATE TABLE IF NOT EXISTS app.clinical_facts (
    id                      uuid                     NOT NULL DEFAULT gen_random_uuid(),
    document_id             uuid                     NOT NULL,
    fact_type               character varying(100)   NOT NULL,
    value                   text                     NOT NULL,
    confidence_score        numeric(5,4)             NOT NULL,
    verification_state      character varying(50)    NOT NULL,
    last_reviewed_by_user_id uuid,
    last_reviewed_at        timestamp with time zone,
    created_at              timestamp with time zone NOT NULL DEFAULT now(),
    updated_at              timestamp with time zone NOT NULL,
    CONSTRAINT pk_clinical_facts PRIMARY KEY (id),
    CONSTRAINT fk_clinical_facts_clinical_documents_document_id
        FOREIGN KEY (document_id) REFERENCES app.clinical_documents (id) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS app.coding_decisions (
    id                  uuid                     NOT NULL DEFAULT gen_random_uuid(),
    patient_id          uuid                     NOT NULL,
    document_id         uuid                     NOT NULL,
    code_type           character varying(20)    NOT NULL,
    suggested_code      character varying(20)    NOT NULL,
    rationale           text,
    confidence_score    numeric(5,4)             NOT NULL,
    reviewer_action     character varying(50)    NOT NULL,
    finalized_code      character varying(20),
    reviewed_by_user_id uuid,
    created_at          timestamp with time zone NOT NULL DEFAULT now(),
    updated_at          timestamp with time zone NOT NULL,
    CONSTRAINT pk_coding_decisions PRIMARY KEY (id),
    CONSTRAINT fk_coding_decisions_patients_patient_id
        FOREIGN KEY (patient_id) REFERENCES app.patients (id) ON DELETE RESTRICT,
    CONSTRAINT fk_coding_decisions_clinical_documents_document_id
        FOREIGN KEY (document_id) REFERENCES app.clinical_documents (id) ON DELETE RESTRICT
);

CREATE TABLE IF NOT EXISTS app.audit_records (
    id                 uuid                     NOT NULL DEFAULT gen_random_uuid(),
    event_type         character varying(50)    NOT NULL,
    actor_user_id      uuid                     NOT NULL,
    target_entity_id   uuid,
    target_entity_type character varying(100)   NOT NULL,
    occurred_at        timestamp with time zone NOT NULL DEFAULT now(),
    details            jsonb                    NOT NULL,
    CONSTRAINT pk_audit_records PRIMARY KEY (id)
);

-- ── 5. Indexes ────────────────────────────────────────────────────────────────
CREATE UNIQUE INDEX IF NOT EXISTS ix_users_email
    ON app.users (email);

CREATE UNIQUE INDEX IF NOT EXISTS ix_patients_mrn
    ON app.patients (mrn);

CREATE UNIQUE INDEX IF NOT EXISTS ix_patients_user_id
    ON app.patients (user_id);

CREATE INDEX IF NOT EXISTS ix_appointments_patient_id
    ON app.appointments (patient_id);

CREATE INDEX IF NOT EXISTS ix_appointments_staff_user_id
    ON app.appointments (staff_user_id);

CREATE INDEX IF NOT EXISTS ix_clinical_documents_patient_id
    ON app.clinical_documents (patient_id);

CREATE INDEX IF NOT EXISTS ix_insurance_profiles_patient_id
    ON app.insurance_profiles (patient_id);

CREATE INDEX IF NOT EXISTS ix_reminder_events_appointment_id
    ON app.reminder_events (appointment_id);

CREATE UNIQUE INDEX IF NOT EXISTS ix_waitlist_entries_appointment_id
    ON app.waitlist_entries (appointment_id);

CREATE INDEX IF NOT EXISTS ix_waitlist_entries_patient_id
    ON app.waitlist_entries (patient_id);

CREATE INDEX IF NOT EXISTS ix_clinical_facts_document_id
    ON app.clinical_facts (document_id);

CREATE INDEX IF NOT EXISTS ix_coding_decisions_patient_id
    ON app.coding_decisions (patient_id);

CREATE INDEX IF NOT EXISTS ix_coding_decisions_document_id
    ON app.coding_decisions (document_id);

CREATE INDEX IF NOT EXISTS ix_audit_records_actor_user_id
    ON app.audit_records (actor_user_id);

CREATE INDEX IF NOT EXISTS ix_audit_records_occurred_at
    ON app.audit_records (occurred_at);

-- ── 6. Audit immutability trigger ─────────────────────────────────────────────
CREATE OR REPLACE FUNCTION app.fn_prevent_audit_mutation()
RETURNS TRIGGER AS $$
BEGIN
    RAISE EXCEPTION
        'Audit records are immutable. % operations are prohibited on app.audit_records. Ref: DR-005, NFR-010.',
        TG_OP;
END;
$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS trg_audit_records_immutable ON app.audit_records;
CREATE TRIGGER trg_audit_records_immutable
    BEFORE UPDATE OR DELETE ON app.audit_records
    FOR EACH ROW
    EXECUTE FUNCTION app.fn_prevent_audit_mutation();

-- ── 7. tenant_id columns & indexes ───────────────────────────────────────────
ALTER TABLE app.patients          ADD COLUMN IF NOT EXISTS tenant_id uuid;
ALTER TABLE app.users             ADD COLUMN IF NOT EXISTS tenant_id uuid;
ALTER TABLE app.appointments      ADD COLUMN IF NOT EXISTS tenant_id uuid;
ALTER TABLE app.clinical_documents ADD COLUMN IF NOT EXISTS tenant_id uuid;
ALTER TABLE app.clinical_facts    ADD COLUMN IF NOT EXISTS tenant_id uuid;
ALTER TABLE app.insurance_profiles ADD COLUMN IF NOT EXISTS tenant_id uuid;
ALTER TABLE app.waitlist_entries   ADD COLUMN IF NOT EXISTS tenant_id uuid;
ALTER TABLE app.reminder_events   ADD COLUMN IF NOT EXISTS tenant_id uuid;
ALTER TABLE app.coding_decisions  ADD COLUMN IF NOT EXISTS tenant_id uuid;

CREATE INDEX IF NOT EXISTS ix_patients_tenant_id            ON app.patients (tenant_id);
CREATE INDEX IF NOT EXISTS ix_users_tenant_id               ON app.users (tenant_id);
CREATE INDEX IF NOT EXISTS ix_appointments_tenant_id        ON app.appointments (tenant_id);
CREATE INDEX IF NOT EXISTS ix_clinical_documents_tenant_id  ON app.clinical_documents (tenant_id);
CREATE INDEX IF NOT EXISTS ix_clinical_facts_tenant_id      ON app.clinical_facts (tenant_id);
CREATE INDEX IF NOT EXISTS ix_insurance_profiles_tenant_id  ON app.insurance_profiles (tenant_id);
CREATE INDEX IF NOT EXISTS ix_waitlist_entries_tenant_id    ON app.waitlist_entries (tenant_id);
CREATE INDEX IF NOT EXISTS ix_reminder_events_tenant_id     ON app.reminder_events (tenant_id);
CREATE INDEX IF NOT EXISTS ix_coding_decisions_tenant_id    ON app.coding_decisions (tenant_id);

-- ── 8. Column-level GRANTs ────────────────────────────────────────────────────
DO $$ BEGIN
IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'app_api') THEN
    GRANT SELECT (id, user_id, first_name, last_name, date_of_birth,
                  mrn, contact_preferences, tenant_id, created_at, updated_at)
        ON app.patients TO app_api;
    GRANT INSERT, UPDATE ON app.patients TO app_api;

    GRANT SELECT (id, email, role, first_name, last_name, is_active,
                  last_login_at, tenant_id, created_at, updated_at)
        ON app.users TO app_api;

    GRANT SELECT, INSERT, UPDATE ON app.insurance_profiles TO app_api;

    GRANT SELECT (id, patient_id, file_name, category, extraction_status,
                  tenant_id, created_at, updated_at)
        ON app.clinical_documents TO app_api;
    GRANT INSERT ON app.clinical_documents TO app_api;

    GRANT SELECT, INSERT, UPDATE ON app.clinical_facts TO app_api;
    GRANT SELECT, INSERT, UPDATE ON app.coding_decisions TO app_api;
    GRANT SELECT, INSERT, UPDATE ON app.appointments TO app_api;
    GRANT SELECT, INSERT, UPDATE ON app.waitlist_entries TO app_api;
    GRANT SELECT, INSERT, UPDATE ON app.reminder_events TO app_api;
    GRANT INSERT, SELECT ON app.audit_records TO app_api;
END IF;
END $$;

DO $$ BEGIN
IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'app_analytics') THEN
    GRANT SELECT (id, tenant_id, created_at, updated_at)
        ON app.patients TO app_analytics;
    GRANT SELECT (id, role, is_active, tenant_id, created_at, updated_at)
        ON app.users TO app_analytics;
    GRANT SELECT (id, patient_id, category, extraction_status,
                  tenant_id, created_at, updated_at)
        ON app.clinical_documents TO app_analytics;
    GRANT SELECT (id, document_id, fact_type, confidence_score,
                  verification_state, tenant_id, created_at, updated_at)
        ON app.clinical_facts TO app_analytics;
    GRANT SELECT (id, patient_id, document_id, code_type, confidence_score,
                  reviewer_action, tenant_id, created_at, updated_at)
        ON app.coding_decisions TO app_analytics;
    GRANT SELECT (id, patient_id, staff_user_id, scheduled_at, duration_minutes,
                  appointment_type, status, queue_state, tenant_id, created_at, updated_at)
        ON app.appointments TO app_analytics;
    GRANT SELECT (id, patient_id, is_primary, verification_status,
                  tenant_id, created_at, updated_at)
        ON app.insurance_profiles TO app_analytics;
END IF;
END $$;

DO $$ BEGIN
IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'app_admin') THEN
    GRANT ALL ON ALL TABLES IN SCHEMA app TO app_admin;
    GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA app TO app_admin;
END IF;
END $$;

-- ── 9. Row-Level Security (RLS) ───────────────────────────────────────────────
ALTER TABLE app.patients           ENABLE ROW LEVEL SECURITY;
ALTER TABLE app.users              ENABLE ROW LEVEL SECURITY;
ALTER TABLE app.appointments       ENABLE ROW LEVEL SECURITY;
ALTER TABLE app.clinical_documents ENABLE ROW LEVEL SECURITY;
ALTER TABLE app.clinical_facts     ENABLE ROW LEVEL SECURITY;
ALTER TABLE app.insurance_profiles ENABLE ROW LEVEL SECURITY;
ALTER TABLE app.waitlist_entries    ENABLE ROW LEVEL SECURITY;
ALTER TABLE app.reminder_events    ENABLE ROW LEVEL SECURITY;
ALTER TABLE app.coding_decisions   ENABLE ROW LEVEL SECURITY;

-- Tenant isolation: rows filtered by JWT-supplied session variable
CREATE POLICY tenant_isolation_patients ON app.patients
    AS PERMISSIVE FOR ALL TO app_api, app_analytics
    USING (tenant_id = current_setting('app.current_tenant_id', true)::uuid);

CREATE POLICY tenant_isolation_users ON app.users
    AS PERMISSIVE FOR ALL TO app_api, app_analytics
    USING (tenant_id = current_setting('app.current_tenant_id', true)::uuid);

CREATE POLICY tenant_isolation_appointments ON app.appointments
    AS PERMISSIVE FOR ALL TO app_api, app_analytics
    USING (tenant_id = current_setting('app.current_tenant_id', true)::uuid);

CREATE POLICY tenant_isolation_clinical_documents ON app.clinical_documents
    AS PERMISSIVE FOR ALL TO app_api, app_analytics
    USING (tenant_id = current_setting('app.current_tenant_id', true)::uuid);

CREATE POLICY tenant_isolation_clinical_facts ON app.clinical_facts
    AS PERMISSIVE FOR ALL TO app_api, app_analytics
    USING (tenant_id = current_setting('app.current_tenant_id', true)::uuid);

CREATE POLICY tenant_isolation_insurance ON app.insurance_profiles
    AS PERMISSIVE FOR ALL TO app_api, app_analytics
    USING (tenant_id = current_setting('app.current_tenant_id', true)::uuid);

CREATE POLICY tenant_isolation_waitlist ON app.waitlist_entries
    AS PERMISSIVE FOR ALL TO app_api, app_analytics
    USING (tenant_id = current_setting('app.current_tenant_id', true)::uuid);

CREATE POLICY tenant_isolation_reminders ON app.reminder_events
    AS PERMISSIVE FOR ALL TO app_api, app_analytics
    USING (tenant_id = current_setting('app.current_tenant_id', true)::uuid);

CREATE POLICY tenant_isolation_coding ON app.coding_decisions
    AS PERMISSIVE FOR ALL TO app_api, app_analytics
    USING (tenant_id = current_setting('app.current_tenant_id', true)::uuid);

-- Admin bypass: app_admin sees all rows (pgaudit ensures every access is logged)
CREATE POLICY admin_bypass_patients     ON app.patients           AS PERMISSIVE FOR ALL TO app_admin USING (true);
CREATE POLICY admin_bypass_users        ON app.users              AS PERMISSIVE FOR ALL TO app_admin USING (true);
CREATE POLICY admin_bypass_appointments ON app.appointments        AS PERMISSIVE FOR ALL TO app_admin USING (true);
CREATE POLICY admin_bypass_clinical_docs ON app.clinical_documents AS PERMISSIVE FOR ALL TO app_admin USING (true);
CREATE POLICY admin_bypass_clinical_facts ON app.clinical_facts    AS PERMISSIVE FOR ALL TO app_admin USING (true);
CREATE POLICY admin_bypass_insurance    ON app.insurance_profiles  AS PERMISSIVE FOR ALL TO app_admin USING (true);
CREATE POLICY admin_bypass_waitlist     ON app.waitlist_entries     AS PERMISSIVE FOR ALL TO app_admin USING (true);
CREATE POLICY admin_bypass_reminders    ON app.reminder_events     AS PERMISSIVE FOR ALL TO app_admin USING (true);
CREATE POLICY admin_bypass_coding       ON app.coding_decisions    AS PERMISSIVE FOR ALL TO app_admin USING (true);

-- Force RLS even for table owners (defense in depth)
ALTER TABLE app.patients           FORCE ROW LEVEL SECURITY;
ALTER TABLE app.users              FORCE ROW LEVEL SECURITY;
ALTER TABLE app.appointments       FORCE ROW LEVEL SECURITY;
ALTER TABLE app.clinical_documents FORCE ROW LEVEL SECURITY;
ALTER TABLE app.clinical_facts     FORCE ROW LEVEL SECURITY;
ALTER TABLE app.insurance_profiles FORCE ROW LEVEL SECURITY;
ALTER TABLE app.waitlist_entries    FORCE ROW LEVEL SECURITY;
ALTER TABLE app.reminder_events    FORCE ROW LEVEL SECURITY;
ALTER TABLE app.coding_decisions   FORCE ROW LEVEL SECURITY;

-- ── 10. De-identified analytics view ─────────────────────────────────────────
CREATE OR REPLACE VIEW app.vw_patients_deidentified AS
SELECT
    p.id,
    encode(sha256(convert_to(p.first_name || p.last_name, 'UTF8')), 'hex') AS name_hash,
    date_part('year', age(p.date_of_birth::timestamp))::int                 AS age_years,
    p.tenant_id,
    p.created_at
FROM app.patients p;

DO $$ BEGIN
    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'app_analytics') THEN
        GRANT SELECT ON app.vw_patients_deidentified TO app_analytics;
    END IF;
    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'app_admin') THEN
        GRANT SELECT ON app.vw_patients_deidentified TO app_admin;
    END IF;
END $$;

-- ── 11. pgaudit role-level configuration ─────────────────────────────────────
DO $$ BEGIN
    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'app_api') THEN
        ALTER ROLE app_api SET pgaudit.log = 'write, ddl';
    END IF;
    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'app_analytics') THEN
        ALTER ROLE app_analytics SET pgaudit.log = 'read, ddl';
    END IF;
    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'app_admin') THEN
        ALTER ROLE app_admin SET pgaudit.log = 'all';
    END IF;
END $$;

-- =============================================================================
-- Done. Verify with:
--   \dn+          -- schemas
--   \dt app.*     -- tables
--   \du           -- roles
--   SELECT schemaname, tablename, policyname FROM pg_policies WHERE schemaname = 'app';
-- =============================================================================
