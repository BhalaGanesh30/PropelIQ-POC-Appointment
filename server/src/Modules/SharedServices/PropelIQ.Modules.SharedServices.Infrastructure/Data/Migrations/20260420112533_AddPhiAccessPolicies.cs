using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PropelIQ.Modules.SharedServices.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPhiAccessPolicies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── 1. Add tenant_id column to all tenant-bearing tables ─────────────
            migrationBuilder.Sql("""
                ALTER TABLE app.patients          ADD COLUMN IF NOT EXISTS tenant_id uuid;
                ALTER TABLE app.users             ADD COLUMN IF NOT EXISTS tenant_id uuid;
                ALTER TABLE app.appointments      ADD COLUMN IF NOT EXISTS tenant_id uuid;
                ALTER TABLE app.clinical_documents ADD COLUMN IF NOT EXISTS tenant_id uuid;
                ALTER TABLE app.clinical_facts    ADD COLUMN IF NOT EXISTS tenant_id uuid;
                ALTER TABLE app.insurance_profiles ADD COLUMN IF NOT EXISTS tenant_id uuid;
                ALTER TABLE app.waitlist_entries   ADD COLUMN IF NOT EXISTS tenant_id uuid;
                ALTER TABLE app.reminder_events   ADD COLUMN IF NOT EXISTS tenant_id uuid;
                ALTER TABLE app.coding_decisions  ADD COLUMN IF NOT EXISTS tenant_id uuid;

                CREATE INDEX IF NOT EXISTS ix_patients_tenant_id           ON app.patients (tenant_id);
                CREATE INDEX IF NOT EXISTS ix_users_tenant_id              ON app.users (tenant_id);
                CREATE INDEX IF NOT EXISTS ix_appointments_tenant_id       ON app.appointments (tenant_id);
                CREATE INDEX IF NOT EXISTS ix_clinical_documents_tenant_id ON app.clinical_documents (tenant_id);
                CREATE INDEX IF NOT EXISTS ix_clinical_facts_tenant_id     ON app.clinical_facts (tenant_id);
                CREATE INDEX IF NOT EXISTS ix_insurance_profiles_tenant_id ON app.insurance_profiles (tenant_id);
                CREATE INDEX IF NOT EXISTS ix_waitlist_entries_tenant_id   ON app.waitlist_entries (tenant_id);
                CREATE INDEX IF NOT EXISTS ix_reminder_events_tenant_id    ON app.reminder_events (tenant_id);
                CREATE INDEX IF NOT EXISTS ix_coding_decisions_tenant_id   ON app.coding_decisions (tenant_id);
                """);

            // ── 2. Column-level GRANT restrictions (AC-1) ────────────────────────
            // app_api: care-relevant columns including PHI for direct patient care.
            migrationBuilder.Sql("""
                DO $$ BEGIN
                IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'app_api') THEN
                    -- patients: full PHI access for care workflows
                    GRANT SELECT (id, user_id, first_name, last_name, date_of_birth,
                                  mrn, contact_preferences, tenant_id, created_at, updated_at)
                        ON app.patients TO app_api;
                    GRANT INSERT, UPDATE ON app.patients TO app_api;

                    -- users: profile access (no password_hash)
                    GRANT SELECT (id, email, role, first_name, last_name, is_active,
                                  last_login_at, tenant_id, created_at, updated_at)
                        ON app.users TO app_api;

                    -- insurance_profiles: full access for billing workflows
                    GRANT SELECT, INSERT, UPDATE ON app.insurance_profiles TO app_api;

                    -- clinical_documents: metadata only, exclude storage_path
                    GRANT SELECT (id, patient_id, file_name, category, extraction_status,
                                  tenant_id, created_at, updated_at)
                        ON app.clinical_documents TO app_api;
                    GRANT INSERT ON app.clinical_documents TO app_api;

                    -- clinical_facts: full read for care, value is PHI but needed
                    GRANT SELECT ON app.clinical_facts TO app_api;
                    GRANT INSERT, UPDATE ON app.clinical_facts TO app_api;

                    -- coding_decisions: full access for coding workflows
                    GRANT SELECT, INSERT, UPDATE ON app.coding_decisions TO app_api;

                    -- scheduling tables: full operational access
                    GRANT SELECT, INSERT, UPDATE ON app.appointments TO app_api;
                    GRANT SELECT, INSERT, UPDATE ON app.waitlist_entries TO app_api;
                    GRANT SELECT, INSERT, UPDATE ON app.reminder_events TO app_api;

                    -- audit: insert-only (append-only per DR-005)
                    GRANT INSERT, SELECT ON app.audit_records TO app_api;
                END IF;
                END $$;
                """);

            // app_analytics: non-PHI columns only — must use de-identified view.
            migrationBuilder.Sql("""
                DO $$ BEGIN
                IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'app_analytics') THEN
                    -- patients: id and metadata only, no PHI
                    GRANT SELECT (id, tenant_id, created_at, updated_at)
                        ON app.patients TO app_analytics;

                    -- users: id and role only
                    GRANT SELECT (id, role, is_active, tenant_id, created_at, updated_at)
                        ON app.users TO app_analytics;

                    -- clinical_documents: metadata only
                    GRANT SELECT (id, patient_id, category, extraction_status,
                                  tenant_id, created_at, updated_at)
                        ON app.clinical_documents TO app_analytics;

                    -- clinical_facts: non-PHI columns only (no value)
                    GRANT SELECT (id, document_id, fact_type, confidence_score,
                                  verification_state, tenant_id, created_at, updated_at)
                        ON app.clinical_facts TO app_analytics;

                    -- coding_decisions: aggregate-safe columns only
                    GRANT SELECT (id, patient_id, document_id, code_type, confidence_score,
                                  reviewer_action, tenant_id, created_at, updated_at)
                        ON app.coding_decisions TO app_analytics;

                    -- scheduling: operational metrics
                    GRANT SELECT (id, patient_id, staff_user_id, scheduled_at, duration_minutes,
                                  appointment_type, status, queue_state, tenant_id, created_at, updated_at)
                        ON app.appointments TO app_analytics;

                    -- insurance_profiles: non-PHI only
                    GRANT SELECT (id, patient_id, is_primary, verification_status,
                                  tenant_id, created_at, updated_at)
                        ON app.insurance_profiles TO app_analytics;
                END IF;
                END $$;
                """);

            // app_admin: full access (audited via pgaudit).
            migrationBuilder.Sql("""
                DO $$ BEGIN
                IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'app_admin') THEN
                    GRANT ALL ON ALL TABLES IN SCHEMA app TO app_admin;
                    GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA app TO app_admin;
                END IF;
                END $$;
                """);

            // ── 3. Enable RLS and create tenant isolation policies ───────────────
            migrationBuilder.Sql("""
                ALTER TABLE app.patients           ENABLE ROW LEVEL SECURITY;
                ALTER TABLE app.users              ENABLE ROW LEVEL SECURITY;
                ALTER TABLE app.appointments       ENABLE ROW LEVEL SECURITY;
                ALTER TABLE app.clinical_documents ENABLE ROW LEVEL SECURITY;
                ALTER TABLE app.clinical_facts     ENABLE ROW LEVEL SECURITY;
                ALTER TABLE app.insurance_profiles ENABLE ROW LEVEL SECURITY;
                ALTER TABLE app.waitlist_entries    ENABLE ROW LEVEL SECURITY;
                ALTER TABLE app.reminder_events    ENABLE ROW LEVEL SECURITY;
                ALTER TABLE app.coding_decisions   ENABLE ROW LEVEL SECURITY;
                """);

            // Tenant isolation policies — filter by session variable set by API middleware.
            migrationBuilder.Sql("""
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
                """);

            // Admin bypass policies — app_admin sees all rows.
            migrationBuilder.Sql("""
                CREATE POLICY admin_bypass_patients    ON app.patients           AS PERMISSIVE FOR ALL TO app_admin USING (true);
                CREATE POLICY admin_bypass_users       ON app.users              AS PERMISSIVE FOR ALL TO app_admin USING (true);
                CREATE POLICY admin_bypass_appointments ON app.appointments      AS PERMISSIVE FOR ALL TO app_admin USING (true);
                CREATE POLICY admin_bypass_clinical_docs ON app.clinical_documents AS PERMISSIVE FOR ALL TO app_admin USING (true);
                CREATE POLICY admin_bypass_clinical_facts ON app.clinical_facts  AS PERMISSIVE FOR ALL TO app_admin USING (true);
                CREATE POLICY admin_bypass_insurance   ON app.insurance_profiles AS PERMISSIVE FOR ALL TO app_admin USING (true);
                CREATE POLICY admin_bypass_waitlist    ON app.waitlist_entries    AS PERMISSIVE FOR ALL TO app_admin USING (true);
                CREATE POLICY admin_bypass_reminders   ON app.reminder_events    AS PERMISSIVE FOR ALL TO app_admin USING (true);
                CREATE POLICY admin_bypass_coding      ON app.coding_decisions   AS PERMISSIVE FOR ALL TO app_admin USING (true);
                """);

            // Force RLS even for table owners (defense in depth).
            migrationBuilder.Sql("""
                ALTER TABLE app.patients           FORCE ROW LEVEL SECURITY;
                ALTER TABLE app.users              FORCE ROW LEVEL SECURITY;
                ALTER TABLE app.appointments       FORCE ROW LEVEL SECURITY;
                ALTER TABLE app.clinical_documents FORCE ROW LEVEL SECURITY;
                ALTER TABLE app.clinical_facts     FORCE ROW LEVEL SECURITY;
                ALTER TABLE app.insurance_profiles FORCE ROW LEVEL SECURITY;
                ALTER TABLE app.waitlist_entries    FORCE ROW LEVEL SECURITY;
                ALTER TABLE app.reminder_events    FORCE ROW LEVEL SECURITY;
                ALTER TABLE app.coding_decisions   FORCE ROW LEVEL SECURITY;
                """);

            // ── 4. De-identified analytics view (AC-2) ───────────────────────────
            migrationBuilder.Sql("""
                CREATE OR REPLACE VIEW app.vw_patients_deidentified AS
                SELECT
                    p.id,
                    encode(sha256(convert_to(p.first_name || p.last_name, 'UTF8')), 'hex')
                        AS name_hash,
                    date_part('year', age(p.date_of_birth::timestamp))::int AS age_years,
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
                """);

            // ── 5. pgaudit role-level logging (AC-4) ─────────────────────────────
            migrationBuilder.Sql("""
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
                """);

            // ── 6. Post-apply validation ─────────────────────────────────────────
            migrationBuilder.Sql("""
                DO $$
                DECLARE
                    rls_count INT;
                BEGIN
                    SELECT count(*) INTO rls_count
                    FROM pg_catalog.pg_class c
                    JOIN pg_catalog.pg_namespace n ON n.oid = c.relnamespace
                    WHERE n.nspname = 'app' AND c.relrowsecurity = true;

                    IF rls_count < 9 THEN
                        RAISE EXCEPTION 'Migration verification failed: expected >= 9 RLS-enabled tables, found %', rls_count;
                    END IF;
                END $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // ── Drop policies ────────────────────────────────────────────────────
            migrationBuilder.Sql("""
                DROP POLICY IF EXISTS tenant_isolation_patients ON app.patients;
                DROP POLICY IF EXISTS tenant_isolation_users ON app.users;
                DROP POLICY IF EXISTS tenant_isolation_appointments ON app.appointments;
                DROP POLICY IF EXISTS tenant_isolation_clinical_documents ON app.clinical_documents;
                DROP POLICY IF EXISTS tenant_isolation_clinical_facts ON app.clinical_facts;
                DROP POLICY IF EXISTS tenant_isolation_insurance ON app.insurance_profiles;
                DROP POLICY IF EXISTS tenant_isolation_waitlist ON app.waitlist_entries;
                DROP POLICY IF EXISTS tenant_isolation_reminders ON app.reminder_events;
                DROP POLICY IF EXISTS tenant_isolation_coding ON app.coding_decisions;

                DROP POLICY IF EXISTS admin_bypass_patients ON app.patients;
                DROP POLICY IF EXISTS admin_bypass_users ON app.users;
                DROP POLICY IF EXISTS admin_bypass_appointments ON app.appointments;
                DROP POLICY IF EXISTS admin_bypass_clinical_docs ON app.clinical_documents;
                DROP POLICY IF EXISTS admin_bypass_clinical_facts ON app.clinical_facts;
                DROP POLICY IF EXISTS admin_bypass_insurance ON app.insurance_profiles;
                DROP POLICY IF EXISTS admin_bypass_waitlist ON app.waitlist_entries;
                DROP POLICY IF EXISTS admin_bypass_reminders ON app.reminder_events;
                DROP POLICY IF EXISTS admin_bypass_coding ON app.coding_decisions;
                """);

            // ── Disable RLS ──────────────────────────────────────────────────────
            migrationBuilder.Sql("""
                ALTER TABLE app.patients           NO FORCE ROW LEVEL SECURITY;
                ALTER TABLE app.users              NO FORCE ROW LEVEL SECURITY;
                ALTER TABLE app.appointments       NO FORCE ROW LEVEL SECURITY;
                ALTER TABLE app.clinical_documents NO FORCE ROW LEVEL SECURITY;
                ALTER TABLE app.clinical_facts     NO FORCE ROW LEVEL SECURITY;
                ALTER TABLE app.insurance_profiles NO FORCE ROW LEVEL SECURITY;
                ALTER TABLE app.waitlist_entries    NO FORCE ROW LEVEL SECURITY;
                ALTER TABLE app.reminder_events    NO FORCE ROW LEVEL SECURITY;
                ALTER TABLE app.coding_decisions   NO FORCE ROW LEVEL SECURITY;

                ALTER TABLE app.patients           DISABLE ROW LEVEL SECURITY;
                ALTER TABLE app.users              DISABLE ROW LEVEL SECURITY;
                ALTER TABLE app.appointments       DISABLE ROW LEVEL SECURITY;
                ALTER TABLE app.clinical_documents DISABLE ROW LEVEL SECURITY;
                ALTER TABLE app.clinical_facts     DISABLE ROW LEVEL SECURITY;
                ALTER TABLE app.insurance_profiles DISABLE ROW LEVEL SECURITY;
                ALTER TABLE app.waitlist_entries    DISABLE ROW LEVEL SECURITY;
                ALTER TABLE app.reminder_events    DISABLE ROW LEVEL SECURITY;
                ALTER TABLE app.coding_decisions   DISABLE ROW LEVEL SECURITY;
                """);

            // ── Drop de-identified view ──────────────────────────────────────────
            migrationBuilder.Sql("""
                DROP VIEW IF EXISTS app.vw_patients_deidentified;
                """);

            // ── Revoke grants ────────────────────────────────────────────────────
            migrationBuilder.Sql("""
                DO $$ BEGIN
                IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'app_api') THEN
                    REVOKE ALL ON ALL TABLES IN SCHEMA app FROM app_api;
                END IF;
                IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'app_analytics') THEN
                    REVOKE ALL ON ALL TABLES IN SCHEMA app FROM app_analytics;
                END IF;
                IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'app_admin') THEN
                    REVOKE ALL ON ALL TABLES IN SCHEMA app FROM app_admin;
                END IF;
                END $$;
                """);

            // ── Drop tenant_id columns and indexes ───────────────────────────────
            migrationBuilder.Sql("""
                DROP INDEX IF EXISTS app.ix_patients_tenant_id;
                DROP INDEX IF EXISTS app.ix_users_tenant_id;
                DROP INDEX IF EXISTS app.ix_appointments_tenant_id;
                DROP INDEX IF EXISTS app.ix_clinical_documents_tenant_id;
                DROP INDEX IF EXISTS app.ix_clinical_facts_tenant_id;
                DROP INDEX IF EXISTS app.ix_insurance_profiles_tenant_id;
                DROP INDEX IF EXISTS app.ix_waitlist_entries_tenant_id;
                DROP INDEX IF EXISTS app.ix_reminder_events_tenant_id;
                DROP INDEX IF EXISTS app.ix_coding_decisions_tenant_id;

                ALTER TABLE app.patients           DROP COLUMN IF EXISTS tenant_id;
                ALTER TABLE app.users              DROP COLUMN IF EXISTS tenant_id;
                ALTER TABLE app.appointments       DROP COLUMN IF EXISTS tenant_id;
                ALTER TABLE app.clinical_documents DROP COLUMN IF EXISTS tenant_id;
                ALTER TABLE app.clinical_facts     DROP COLUMN IF EXISTS tenant_id;
                ALTER TABLE app.insurance_profiles DROP COLUMN IF EXISTS tenant_id;
                ALTER TABLE app.waitlist_entries    DROP COLUMN IF EXISTS tenant_id;
                ALTER TABLE app.reminder_events    DROP COLUMN IF EXISTS tenant_id;
                ALTER TABLE app.coding_decisions   DROP COLUMN IF EXISTS tenant_id;
                """);
        }
    }
}
