using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PropelIQ.Modules.SharedServices.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBookingFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "staff_user_id",
                schema: "app",
                table: "appointments",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<bool>(
                name: "artifacts_generated",
                schema: "app",
                table: "appointments",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "booked_at",
                schema: "app",
                table: "appointments",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()");

            migrationBuilder.AddColumn<string>(
                name: "confirmation_code",
                schema: "app",
                table: "appointments",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "intake_record_id",
                schema: "app",
                table: "appointments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "location",
                schema: "app",
                table: "appointments",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "provider_name",
                schema: "app",
                table: "appointments",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "slot_id",
                schema: "app",
                table: "appointments",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_appointments_confirmation_code",
                schema: "app",
                table: "appointments",
                column: "confirmation_code",
                unique: true,
                filter: "confirmation_code IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_appointments_slot_id",
                schema: "app",
                table: "appointments",
                column: "slot_id",
                unique: true,
                filter: "slot_id IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_appointments_confirmation_code",
                schema: "app",
                table: "appointments");

            migrationBuilder.DropIndex(
                name: "ix_appointments_slot_id",
                schema: "app",
                table: "appointments");

            migrationBuilder.DropColumn(
                name: "artifacts_generated",
                schema: "app",
                table: "appointments");

            migrationBuilder.DropColumn(
                name: "booked_at",
                schema: "app",
                table: "appointments");

            migrationBuilder.DropColumn(
                name: "confirmation_code",
                schema: "app",
                table: "appointments");

            migrationBuilder.DropColumn(
                name: "intake_record_id",
                schema: "app",
                table: "appointments");

            migrationBuilder.DropColumn(
                name: "location",
                schema: "app",
                table: "appointments");

            migrationBuilder.DropColumn(
                name: "provider_name",
                schema: "app",
                table: "appointments");

            migrationBuilder.DropColumn(
                name: "slot_id",
                schema: "app",
                table: "appointments");

            migrationBuilder.AlterColumn<Guid>(
                name: "staff_user_id",
                schema: "app",
                table: "appointments",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
