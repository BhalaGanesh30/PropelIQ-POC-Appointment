using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PropelIQ.Modules.SharedServices.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAiAuditSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "document_id",
                schema: "app",
                table: "coding_decisions",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "ai_request_id",
                schema: "app",
                table: "coding_decisions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "original_cpt_code",
                schema: "app",
                table: "coding_decisions",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "original_icd10code",
                schema: "app",
                table: "coding_decisions",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "reviewer_note",
                schema: "app",
                table: "coding_decisions",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ai_audit_log_outcomes",
                schema: "app",
                columns: table => new
                {
                    outcome_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    ai_request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reviewer_action = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    reviewer_note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    decided_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ai_audit_log_outcomes", x => x.outcome_id);
                });

            // ── ai_audit_log_outcomes: append-only enforcement (AC-3, DR-005) ───────────────
            // app_user is the application DB role used by the EF Core connection string.
            // app_admin (the migrations role) retains full privileges (CREATE/INSERT/UPDATE/DELETE).
            migrationBuilder.Sql(@"
                REVOKE UPDATE, DELETE ON app.ai_audit_log_outcomes FROM app_user;
                COMMENT ON TABLE app.ai_audit_log_outcomes IS
                    'AIR-011: Append-only reviewer decision outcomes linked to ai_audit_logs by ai_request_id.
                     No FK to the partitioned parent — PostgreSQL 15 does not support FKs to
                     partitioned tables with composite PKs; referential integrity enforced at application layer.
                     No UPDATE or DELETE permitted for app_user (AC-3, DR-005).';
            ");

            migrationBuilder.CreateTable(
                name: "ai_audit_logs",
                schema: "app",
                columns: table => new
                {
                    ai_request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    request_timestamp = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    clinician_id = table.Column<Guid>(type: "uuid", nullable: false),
                    prompt_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    context_refs = table.Column<string>(type: "jsonb", nullable: false, defaultValue: "[]"),
                    model_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    response_payload = table.Column<string>(type: "jsonb", nullable: false, defaultValue: "{}"),
                    confidence_scores = table.Column<string>(type: "jsonb", nullable: false, defaultValue: "{}"),
                    latency_ms = table.Column<int>(type: "integer", nullable: false),
                    fallback_reason = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ai_audit_logs", x => new { x.ai_request_id, x.request_timestamp });
                });

            // ── ai_audit_logs: convert to PostgreSQL range-partitioned table (Edge Case 2) ──
            //
            // EF Core cannot generate native partitioned table DDL, so we:
            //   1. Drop the regular table created by EF Core above.
            //   2. Recreate it as PARTITION BY RANGE (request_timestamp).
            //   3. Create year child partitions 2026–2032 (7-year initial set).
            //   4. REVOKE UPDATE, DELETE from app_user on the parent and all partitions (AC-3, DR-005).
            //
            // The CreateIndex calls further below still reference app.ai_audit_logs and will
            // propagate to all child partitions automatically (PostgreSQL 15 behaviour).
            migrationBuilder.Sql(@"
                -- 1. Drop the non-partitioned table created by EF Core migration above.
                DROP TABLE app.ai_audit_logs;

                -- 2. Recreate as a range-partitioned parent table (no rows stored directly in parent).
                CREATE TABLE app.ai_audit_logs (
                    ai_request_id     uuid         NOT NULL,
                    request_timestamp timestamptz  NOT NULL DEFAULT NOW(),
                    clinician_id      uuid         NOT NULL,
                    prompt_hash       varchar(64)  NOT NULL,
                    context_refs      jsonb        NOT NULL DEFAULT '[]',
                    model_name        varchar(100) NOT NULL,
                    response_payload  jsonb        NOT NULL DEFAULT '{}',
                    confidence_scores jsonb        NOT NULL DEFAULT '{}',
                    latency_ms        integer      NOT NULL,
                    fallback_reason   varchar(255) NULL,
                    created_at        timestamptz  NOT NULL DEFAULT NOW(),
                    PRIMARY KEY (ai_request_id, request_timestamp)
                ) PARTITION BY RANGE (request_timestamp);

                COMMENT ON TABLE app.ai_audit_logs IS
                    'AIR-011: Append-only AI request audit log. 7-year retention per DR-005.
                     Range-partitioned by request_timestamp (yearly partitions 2026-2032).
                     No UPDATE or DELETE permitted for app_user role (AC-3, DR-005, NFR-010).
                     Cold storage migration for partitions > 3 years old is a DBA runbook concern
                     (Edge Case 2) — partitions remain queryable via partition pruning.';

                -- 3. Create year child partitions 2026–2032 (initial 7-year set, Edge Case 2).
                -- Each partition stores rows WHERE request_timestamp >= lower AND < upper.
                CREATE TABLE app.ai_audit_logs_2026 PARTITION OF app.ai_audit_logs
                    FOR VALUES FROM ('2026-01-01') TO ('2027-01-01');

                CREATE TABLE app.ai_audit_logs_2027 PARTITION OF app.ai_audit_logs
                    FOR VALUES FROM ('2027-01-01') TO ('2028-01-01');

                CREATE TABLE app.ai_audit_logs_2028 PARTITION OF app.ai_audit_logs
                    FOR VALUES FROM ('2028-01-01') TO ('2029-01-01');

                CREATE TABLE app.ai_audit_logs_2029 PARTITION OF app.ai_audit_logs
                    FOR VALUES FROM ('2029-01-01') TO ('2030-01-01');

                CREATE TABLE app.ai_audit_logs_2030 PARTITION OF app.ai_audit_logs
                    FOR VALUES FROM ('2030-01-01') TO ('2031-01-01');

                CREATE TABLE app.ai_audit_logs_2031 PARTITION OF app.ai_audit_logs
                    FOR VALUES FROM ('2031-01-01') TO ('2032-01-01');

                CREATE TABLE app.ai_audit_logs_2032 PARTITION OF app.ai_audit_logs
                    FOR VALUES FROM ('2032-01-01') TO ('2033-01-01');

                -- 4. REVOKE UPDATE, DELETE from app_user on the parent and all year partitions.
                -- app_user is the application DB role used by the EF Core connection string.
                -- app_admin (migrations role) retains full CREATE/INSERT/UPDATE/DELETE privileges.
                -- REVOKE on the parent cascades to all future partitions created by DBA scripts.
                -- Per-partition REVOKE here ensures inheritance for the initial partition set.
                REVOKE UPDATE, DELETE ON app.ai_audit_logs          FROM app_user;
                REVOKE UPDATE, DELETE ON app.ai_audit_logs_2026     FROM app_user;
                REVOKE UPDATE, DELETE ON app.ai_audit_logs_2027     FROM app_user;
                REVOKE UPDATE, DELETE ON app.ai_audit_logs_2028     FROM app_user;
                REVOKE UPDATE, DELETE ON app.ai_audit_logs_2029     FROM app_user;
                REVOKE UPDATE, DELETE ON app.ai_audit_logs_2030     FROM app_user;
                REVOKE UPDATE, DELETE ON app.ai_audit_logs_2031     FROM app_user;
                REVOKE UPDATE, DELETE ON app.ai_audit_logs_2032     FROM app_user;
            ");

            migrationBuilder.CreateTable(
                name: "ai_audit_outbox",
                schema: "app",
                columns: table => new
                {
                    outbox_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    ai_request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    payload = table.Column<string>(type: "jsonb", nullable: false),
                    retry_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    last_attempt_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ai_audit_outbox", x => x.outbox_id);
                });

            migrationBuilder.CreateTable(
                name: "icd_codes",
                schema: "app",
                columns: table => new
                {
                    code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    category = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    is_deprecated = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    effective_date = table.Column<DateOnly>(type: "date", nullable: true),
                    deprecation_date = table.Column<DateOnly>(type: "date", nullable: true),
                    last_updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_icd_codes", x => x.code);
                });

            migrationBuilder.CreateTable(
                name: "user_code_favorites",
                schema: "app",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code_type = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_code_favorites", x => new { x.user_id, x.code_type, x.code });
                    table.ForeignKey(
                        name: "fk_user_code_favorites_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "app",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_ai_audit_log_outcomes_request_id",
                schema: "app",
                table: "ai_audit_log_outcomes",
                column: "ai_request_id");

            migrationBuilder.CreateIndex(
                name: "ix_ai_audit_logs_clinician_timestamp",
                schema: "app",
                table: "ai_audit_logs",
                columns: new[] { "clinician_id", "request_timestamp" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_ai_audit_logs_timestamp",
                schema: "app",
                table: "ai_audit_logs",
                column: "request_timestamp",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "ix_ai_audit_outbox_retry_due",
                schema: "app",
                table: "ai_audit_outbox",
                columns: new[] { "retry_count", "last_attempt_at" },
                filter: "retry_count < 3");

            migrationBuilder.CreateIndex(
                name: "ix_icd_codes_active",
                schema: "app",
                table: "icd_codes",
                column: "code",
                filter: "is_deprecated = false");

            migrationBuilder.CreateIndex(
                name: "ix_icd_codes_last_updated",
                schema: "app",
                table: "icd_codes",
                column: "last_updated_at");

            migrationBuilder.CreateIndex(
                name: "ix_user_code_favorites_user_id",
                schema: "app",
                table: "user_code_favorites",
                column: "user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ai_audit_log_outcomes",
                schema: "app");

            // ai_audit_logs is a partitioned parent — use CASCADE to drop the parent and
            // all child partitions (ai_audit_logs_2026 … ai_audit_logs_2032) atomically.
            // EF Core's generated DropTable would fail with a dependency error without CASCADE.
            migrationBuilder.Sql("DROP TABLE IF EXISTS app.ai_audit_logs CASCADE;");

            migrationBuilder.DropTable(
                name: "ai_audit_outbox",
                schema: "app");

            migrationBuilder.DropTable(
                name: "icd_codes",
                schema: "app");

            migrationBuilder.DropTable(
                name: "user_code_favorites",
                schema: "app");

            migrationBuilder.DropColumn(
                name: "ai_request_id",
                schema: "app",
                table: "coding_decisions");

            migrationBuilder.DropColumn(
                name: "original_cpt_code",
                schema: "app",
                table: "coding_decisions");

            migrationBuilder.DropColumn(
                name: "original_icd10code",
                schema: "app",
                table: "coding_decisions");

            migrationBuilder.DropColumn(
                name: "reviewer_note",
                schema: "app",
                table: "coding_decisions");

            migrationBuilder.AlterColumn<Guid>(
                name: "document_id",
                schema: "app",
                table: "coding_decisions",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
