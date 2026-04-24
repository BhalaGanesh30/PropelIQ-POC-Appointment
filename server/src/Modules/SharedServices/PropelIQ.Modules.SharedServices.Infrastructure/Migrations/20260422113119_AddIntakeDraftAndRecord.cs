using System;
using System.Collections.Generic;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PropelIQ.Modules.SharedServices.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddIntakeDraftAndRecord : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "intake_drafts",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    patient_id = table.Column<Guid>(type: "uuid", nullable: false),
                    slot_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    form_data = table.Column<JsonDocument>(type: "jsonb", nullable: false),
                    ai_populated_fields = table.Column<List<string>>(type: "jsonb", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_intake_drafts", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "intake_records",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    patient_id = table.Column<Guid>(type: "uuid", nullable: false),
                    appointment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    form_data = table.Column<JsonDocument>(type: "jsonb", nullable: false),
                    ai_populated_fields = table.Column<List<string>>(type: "jsonb", nullable: false),
                    submitted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_intake_records", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_intake_drafts_expires_at",
                schema: "app",
                table: "intake_drafts",
                column: "expires_at");

            migrationBuilder.CreateIndex(
                name: "ix_intake_drafts_patient_slot_status",
                schema: "app",
                table: "intake_drafts",
                columns: new[] { "patient_id", "slot_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_intake_records_appointment_id",
                schema: "app",
                table: "intake_records",
                column: "appointment_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_intake_records_patient_id",
                schema: "app",
                table: "intake_records",
                column: "patient_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "intake_drafts",
                schema: "app");

            migrationBuilder.DropTable(
                name: "intake_records",
                schema: "app");
        }
    }
}
