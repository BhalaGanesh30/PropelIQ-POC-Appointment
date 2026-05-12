using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PropelIQ.Modules.SharedServices.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddConfigurationVersionSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── configuration_versions ────────────────────────────────────────────
            // Append-only versioned configuration log (US_059, AC-1, AC-3, AC-4).
            // Every configuration change creates a new row — no UPDATEs are issued.
            // version_number is monotonically increasing per category.
            // values_json and previous_values_json are JSONB for efficient snapshot storage.
            // restored_from_version_id is a nullable self-reference for rollback traceability (AC-4).
            migrationBuilder.CreateTable(
                name: "configuration_versions",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    category = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    version_number = table.Column<int>(type: "integer", nullable: false),
                    values_json = table.Column<string>(type: "jsonb", nullable: false),
                    previous_values_json = table.Column<string>(type: "jsonb", nullable: true),
                    changed_by_admin_id = table.Column<Guid>(type: "uuid", nullable: false),
                    changed_by_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    changed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    restored_from_version_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_configuration_versions", x => x.id);
                });

            // ── Indexes ────────────────────────────────────────────────────────────

            // Non-unique — supports history listing queries by category.
            migrationBuilder.CreateIndex(
                name: "ix_configuration_versions_category",
                schema: "app",
                table: "configuration_versions",
                column: "category");

            // Unique composite — enforces one version number per category;
            // supports "latest version per category" cache-population query (AC-1).
            migrationBuilder.CreateIndex(
                name: "ix_configuration_versions_category_version",
                schema: "app",
                table: "configuration_versions",
                columns: new[] { "category", "version_number" },
                unique: true);

            // ── Seed data: version 1 defaults for all four FR-AD-001 categories ───
            // Uses a well-known system admin UUID (00000000-0000-0000-0000-000000000001).
            // These are the baseline values against which all future changes are diffed.
            // Keys must match the field names checked by the category validators (AC-2).
            migrationBuilder.Sql(@"
                DO $$
                DECLARE
                    sys_admin_id  UUID    := '00000000-0000-0000-0000-000000000001';
                    sys_name      TEXT    := 'System';
                    sys_time      TIMESTAMPTZ := NOW();
                    sys_offset    TIMESTAMPTZ := NOW();
                BEGIN
                    -- SlotTemplates v1: default slot duration and buffer time.
                    INSERT INTO app.configuration_versions
                        (id, category, version_number, values_json, changed_by_admin_id,
                         changed_by_name, changed_at_utc, created_at, updated_at)
                    VALUES (
                        gen_random_uuid(),
                        'SlotTemplates',
                        1,
                        '{""durationMinutes"": 30, ""bufferMinutes"": 5}'::jsonb,
                        sys_admin_id, sys_name, sys_time, sys_offset, sys_offset
                    );

                    -- ReminderRules v1: default cadence and max reminder count.
                    INSERT INTO app.configuration_versions
                        (id, category, version_number, values_json, changed_by_admin_id,
                         changed_by_name, changed_at_utc, created_at, updated_at)
                    VALUES (
                        gen_random_uuid(),
                        'ReminderRules',
                        1,
                        '{""cadenceHours"": 24, ""maxReminders"": 3}'::jsonb,
                        sys_admin_id, sys_name, sys_time, sys_offset, sys_offset
                    );

                    -- SessionPolicy v1: default timeout and warning lead time.
                    INSERT INTO app.configuration_versions
                        (id, category, version_number, values_json, changed_by_admin_id,
                         changed_by_name, changed_at_utc, created_at, updated_at)
                    VALUES (
                        gen_random_uuid(),
                        'SessionPolicy',
                        1,
                        '{""timeoutMinutes"": 15, ""warningLeadMinutes"": 2, ""maxConcurrentSessions"": 1}'::jsonb,
                        sys_admin_id, sys_name, sys_time, sys_offset, sys_offset
                    );

                    -- CommunicationTemplates v1: default sender and footer text.
                    INSERT INTO app.configuration_versions
                        (id, category, version_number, values_json, changed_by_admin_id,
                         changed_by_name, changed_at_utc, created_at, updated_at)
                    VALUES (
                        gen_random_uuid(),
                        'CommunicationTemplates',
                        1,
                        '{""senderEmail"": ""noreply@propeliq.com"", ""footerText"": ""PropelIQ Healthcare Platform""}'::jsonb,
                        sys_admin_id, sys_name, sys_time, sys_offset, sys_offset
                    );
                END $$;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "configuration_versions",
                schema: "app");
        }
    }
}
