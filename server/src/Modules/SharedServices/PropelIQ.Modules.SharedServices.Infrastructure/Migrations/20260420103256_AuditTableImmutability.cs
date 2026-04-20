using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PropelIQ.Modules.SharedServices.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AuditTableImmutability : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Trigger function: reject UPDATE and DELETE on audit_records (DR-005, AC-3)
            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION app.fn_prevent_audit_mutation()
                RETURNS TRIGGER AS $$
                BEGIN
                    RAISE EXCEPTION
                        'Audit records are immutable. % operations are prohibited on app.audit_records. Ref: DR-005, NFR-010.',
                        TG_OP;
                END;
                $$ LANGUAGE plpgsql;
                """);

            // 2. Trigger: fires BEFORE any UPDATE or DELETE attempt
            migrationBuilder.Sql("""
                CREATE TRIGGER trg_audit_records_immutable
                    BEFORE UPDATE OR DELETE ON app.audit_records
                    FOR EACH ROW
                    EXECUTE FUNCTION app.fn_prevent_audit_mutation();
                """);

            // 3. Restrict privileges: app_user gets INSERT + SELECT only (AC-3 two-layer defense)
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'app_user') THEN
                        REVOKE ALL ON app.audit_records FROM app_user;
                        GRANT INSERT, SELECT ON app.audit_records TO app_user;
                    END IF;
                END $$;
                """);

            // 4. Server-generated timestamp default for occurred_at (AC-4)
            migrationBuilder.Sql("""
                ALTER TABLE app.audit_records
                    ALTER COLUMN occurred_at SET DEFAULT now();
                """);

            // 5. Post-apply validation: confirm trigger exists
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1 FROM pg_trigger
                        WHERE tgname = 'trg_audit_records_immutable'
                    ) THEN
                        RAISE EXCEPTION 'Migration verification failed: trg_audit_records_immutable trigger not found.';
                    END IF;
                END $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Remove in reverse order
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS trg_audit_records_immutable ON app.audit_records;
                """);

            migrationBuilder.Sql("""
                DROP FUNCTION IF EXISTS app.fn_prevent_audit_mutation();
                """);

            migrationBuilder.Sql("""
                ALTER TABLE app.audit_records
                    ALTER COLUMN occurred_at DROP DEFAULT;
                """);

            // Restore default privileges for app_user if the role exists
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'app_user') THEN
                        REVOKE ALL ON app.audit_records FROM app_user;
                        GRANT ALL ON app.audit_records TO app_user;
                    END IF;
                END $$;
                """);
        }
    }
}
