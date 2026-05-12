using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PropelIQ.Modules.SharedServices.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DisclosureRequestTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── 1. Add patient_id column to partitioned audit_records ────────────
            // Adds nullable patient_id to the parent partitioned table.
            // PostgreSQL 15+ propagates ALTER TABLE to all child partitions automatically.
            // Filtered partial index for efficient DataAccess queries per patient (AC-4).
            migrationBuilder.Sql(@"
                ALTER TABLE app.audit_records
                    ADD COLUMN IF NOT EXISTS patient_id UUID NULL;

                CREATE INDEX IF NOT EXISTS ix_audit_records_patient_id
                    ON app.audit_records (patient_id)
                    WHERE patient_id IS NOT NULL;
            ");

            // ── 2. Create disclosure_reports ─────────────────────────────────────
            // Must be created before disclosure_requests due to the FK from requests → reports.
            migrationBuilder.CreateTable(
                name: "disclosure_reports",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    disclosure_request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    report_json = table.Column<string>(type: "jsonb", nullable: false),
                    access_event_count = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    download_token = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    download_expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_disclosure_reports", x => x.id);
                });

            // ── 3. Create disclosure_requests ────────────────────────────────────
            migrationBuilder.CreateTable(
                name: "disclosure_requests",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    patient_id = table.Column<Guid>(type: "uuid", nullable: false),
                    from_date_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    to_date_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Submitted"),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    compiled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    reviewed_by = table.Column<Guid>(type: "uuid", nullable: true),
                    reviewed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    review_notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    delivered_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    delivery_method = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    report_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_disclosure_requests", x => x.id);
                    table.ForeignKey(
                        name: "fk_disclosure_requests_report",
                        column: x => x.report_id,
                        principalSchema: "app",
                        principalTable: "disclosure_reports",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            // ── 4. Indexes ────────────────────────────────────────────────────────
            migrationBuilder.CreateIndex(
                name: "ix_disclosure_reports_request_id",
                schema: "app",
                table: "disclosure_reports",
                column: "disclosure_request_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_disclosure_requests_patient_created",
                schema: "app",
                table: "disclosure_requests",
                columns: ["patient_id", "created_at"],
                descending: [false, true]);

            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ix_disclosure_requests_active_status
                    ON app.disclosure_requests (status)
                    WHERE status NOT IN ('Delivered', 'Rejected');
            ");

            // ── 5. Access control: restrict DELETE on disclosure tables ───────────
            // Immutability guardrail: app_user role may INSERT and SELECT but not DELETE.
            migrationBuilder.Sql(@"
                REVOKE DELETE ON app.disclosure_requests FROM app_user;
                REVOKE DELETE ON app.disclosure_reports   FROM app_user;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop partial index on audit_records.
            migrationBuilder.Sql(@"
                DROP INDEX IF EXISTS app.ix_audit_records_patient_id;
                ALTER TABLE app.audit_records DROP COLUMN IF EXISTS patient_id;
            ");

            migrationBuilder.DropTable(name: "disclosure_requests", schema: "app");
            migrationBuilder.DropTable(name: "disclosure_reports", schema: "app");
        }
    }
}
