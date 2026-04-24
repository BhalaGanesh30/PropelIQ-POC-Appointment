using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PropelIQ.Modules.SharedServices.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAppointmentAuditEntry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "appointment_audit_entries",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    appointment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    performed_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    action = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    is_override = table.Column<bool>(type: "boolean", nullable: false),
                    performed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    previous_status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    previous_slot_id = table.Column<Guid>(type: "uuid", nullable: true),
                    new_slot_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_appointment_audit_entries", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_appointment_audit_appointment_id",
                schema: "app",
                table: "appointment_audit_entries",
                column: "appointment_id");

            migrationBuilder.CreateIndex(
                name: "ix_appointment_audit_performed_at",
                schema: "app",
                table: "appointment_audit_entries",
                column: "performed_at");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "appointment_audit_entries",
                schema: "app");
        }
    }
}
