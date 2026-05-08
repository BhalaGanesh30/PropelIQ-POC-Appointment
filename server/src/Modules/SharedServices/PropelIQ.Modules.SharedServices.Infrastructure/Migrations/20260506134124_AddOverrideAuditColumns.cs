using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PropelIQ.Modules.SharedServices.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOverrideAuditColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "override_action",
                schema: "app",
                table: "audit_records",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "override_constraint_type",
                schema: "app",
                table: "audit_records",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "override_reason",
                schema: "app",
                table: "audit_records",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_audit_records_event_type",
                schema: "app",
                table: "audit_records",
                column: "event_type");

            migrationBuilder.CreateIndex(
                name: "ix_audit_records_event_type_occurred_at",
                schema: "app",
                table: "audit_records",
                columns: new[] { "event_type", "occurred_at" },
                descending: new[] { false, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_audit_records_event_type",
                schema: "app",
                table: "audit_records");

            migrationBuilder.DropIndex(
                name: "ix_audit_records_event_type_occurred_at",
                schema: "app",
                table: "audit_records");

            migrationBuilder.DropColumn(
                name: "override_action",
                schema: "app",
                table: "audit_records");

            migrationBuilder.DropColumn(
                name: "override_constraint_type",
                schema: "app",
                table: "audit_records");

            migrationBuilder.DropColumn(
                name: "override_reason",
                schema: "app",
                table: "audit_records");
        }
    }
}
