using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PropelIQ.Modules.SharedServices.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddComplianceReportSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── compliance_distribution_lists ────────────────────────────────────
            // Stores active email recipients for compliance report distribution (US_058, AC-3).
            migrationBuilder.CreateTable(
                name: "compliance_distribution_lists",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_compliance_distribution_lists", x => x.id);
                });

            // ── compliance_distribution_log ──────────────────────────────────────
            // Append-only delivery-attempt log (US_058, AC-3, edge case 2).
            migrationBuilder.CreateTable(
                name: "compliance_distribution_log",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    report_id = table.Column<Guid>(type: "uuid", nullable: false),
                    recipient_id = table.Column<Guid>(type: "uuid", nullable: false),
                    recipient_email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    attempted_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    attempt_number = table.Column<int>(type: "integer", nullable: false),
                    error_detail = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_compliance_distribution_log", x => x.id);
                });

            // ── compliance_report_jobs ────────────────────────────────────────────
            // Async job tracking for large-range report generation (US_058, edge case 1).
            migrationBuilder.CreateTable(
                name: "compliance_report_jobs",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    report_id = table.Column<Guid>(type: "uuid", nullable: false),
                    requested_by = table.Column<Guid>(type: "uuid", nullable: false),
                    request_json = table.Column<string>(type: "jsonb", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Queued"),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    completed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    error_message = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_compliance_report_jobs", x => x.id);
                });

            // ── compliance_report_schedules ───────────────────────────────────────
            // Admin-configured schedules driving the schedule worker (US_058, AC-1).
            migrationBuilder.CreateTable(
                name: "compliance_report_schedules",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    report_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    recurrence = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Monthly"),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    last_run_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    next_run_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_compliance_report_schedules", x => x.id);
                });

            // ── compliance_reports ────────────────────────────────────────────────
            // Report metadata and rendered PDF content (US_058, AC-2).
            migrationBuilder.CreateTable(
                name: "compliance_reports",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    report_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    period_start_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    period_end_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    generated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    total_audit_events = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    unique_actors = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    anomaly_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    failed_access_attempts = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    pdf_content = table.Column<byte[]>(type: "bytea", nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Generating"),
                    is_async = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    job_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_compliance_reports", x => x.id);
                });

            // ── Indexes ────────────────────────────────────────────────────────────

            // Partial — distributor queries only active recipients (AC-3).
            migrationBuilder.CreateIndex(
                name: "ix_compliance_distribution_lists_active_email",
                schema: "app",
                table: "compliance_distribution_lists",
                column: "email",
                filter: "is_active = TRUE");

            // Composite — all delivery attempts per report, newest first (edge case 2 review).
            migrationBuilder.CreateIndex(
                name: "ix_compliance_distribution_log_report_attempted",
                schema: "app",
                table: "compliance_distribution_log",
                columns: new[] { "report_id", "attempted_at_utc" },
                descending: new[] { false, true });

            // Partial — job-worker recovery on restart; only in-flight jobs scanned.
            migrationBuilder.CreateIndex(
                name: "ix_compliance_report_jobs_inflight",
                schema: "app",
                table: "compliance_report_jobs",
                column: "status",
                filter: "status IN ('Queued', 'Generating')");

            // Non-unique — FK lookup from job → report.
            migrationBuilder.CreateIndex(
                name: "ix_compliance_report_jobs_report_id",
                schema: "app",
                table: "compliance_report_jobs",
                column: "report_id");

            // Partial — schedule-worker polls only active schedules (AC-1).
            migrationBuilder.CreateIndex(
                name: "ix_compliance_report_schedules_next_run",
                schema: "app",
                table: "compliance_report_schedules",
                column: "next_run_at",
                filter: "is_active = TRUE");

            // Chronological listing — admin dashboard (AC-2).
            migrationBuilder.CreateIndex(
                name: "ix_compliance_reports_generated_at",
                schema: "app",
                table: "compliance_reports",
                column: "generated_at_utc",
                descending: new[] { true });

            // Composite — report-list endpoint filtering by status + date (AC-2).
            migrationBuilder.CreateIndex(
                name: "ix_compliance_reports_status_generated",
                schema: "app",
                table: "compliance_reports",
                columns: new[] { "status", "generated_at_utc" },
                descending: new[] { false, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "compliance_distribution_lists", schema: "app");
            migrationBuilder.DropTable(name: "compliance_distribution_log",   schema: "app");
            migrationBuilder.DropTable(name: "compliance_report_jobs",        schema: "app");
            migrationBuilder.DropTable(name: "compliance_report_schedules",   schema: "app");
            migrationBuilder.DropTable(name: "compliance_reports",            schema: "app");
        }
    }
}
