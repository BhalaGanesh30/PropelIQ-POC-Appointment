using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PropelIQ.Modules.SharedServices.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMergeFieldRegistry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── merge_field_registry ─────────────────────────────────────────────
            // Canonical store of allowed merge-field tokens (US_062, edge case 2).
            // String primary key on field_name keeps rows self-documenting.

            migrationBuilder.CreateTable(
                name: "merge_field_registry",
                schema: "app",
                columns: table => new
                {
                    field_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    display_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    sample_value = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    category = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "General"),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_merge_field_registry", x => x.field_name);
                });

            migrationBuilder.CreateIndex(
                name: "ix_merge_field_registry_is_active",
                schema: "app",
                table: "merge_field_registry",
                column: "is_active");

            // ── Seed: initial merge fields (AC-2, edge case 2) ──────────────────
            // These are the canonical substitution tokens supported by the
            // MergeFieldRegistry singleton.  Keeping them in the DB ensures orphan
            // detection survives process restarts and future registry changes.

            migrationBuilder.Sql(@"
INSERT INTO app.merge_field_registry (field_name, display_name, sample_value, category, is_active)
VALUES
    ('patient_name',      'Patient Name',         'Jane Smith',                         'Patient',      true),
    ('appointment_date',  'Appointment Date',      '2026-05-15',                         'Appointment',  true),
    ('appointment_time',  'Appointment Time',      '10:30 AM',                           'Appointment',  true),
    ('clinic_name',       'Clinic Name',           'PropelIQ Health Center',             'Organization', true),
    ('provider_name',     'Provider Name',         'Dr. Sarah Johnson',                  'Provider',     true),
    ('appointment_type',  'Appointment Type',      'Follow-up Visit',                    'Appointment',  true),
    ('cancellation_link', 'Cancellation Link',     'https://propeliq.example.com/cancel/sample',    'Action', true),
    ('reschedule_link',   'Reschedule Link',       'https://propeliq.example.com/reschedule/sample','Action', true)
ON CONFLICT (field_name) DO NOTHING;
");

            // ── Partial unique index: single active version per template (AC-1) ─
            // EF Core does not emit partial indexes natively; raw SQL is required.
            // This guarantees only one version row has is_active = true for any
            // given template_id at any moment.

            migrationBuilder.Sql(@"
CREATE UNIQUE INDEX ix_template_versions_active
    ON app.template_versions (template_id)
    WHERE is_active = true;
");

            // ── CHECK constraint: restrict type column to known values ───────────
            // Guards against arbitrary strings being inserted outside the ORM path.

            migrationBuilder.Sql(@"
ALTER TABLE app.notification_templates
    ADD CONSTRAINT ck_notification_templates_type
    CHECK (type IN ('HTML', 'SMS'));
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Remove the CHECK constraint before dropping related objects.
            migrationBuilder.Sql(@"
ALTER TABLE app.notification_templates
    DROP CONSTRAINT IF EXISTS ck_notification_templates_type;
");

            // Remove the partial unique index.
            migrationBuilder.Sql(@"
DROP INDEX IF EXISTS app.ix_template_versions_active;
");

            migrationBuilder.DropTable(
                name: "merge_field_registry",
                schema: "app");
        }
    }
}

