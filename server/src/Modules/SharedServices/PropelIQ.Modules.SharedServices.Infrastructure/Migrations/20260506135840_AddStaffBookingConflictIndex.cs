using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PropelIQ.Modules.SharedServices.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStaffBookingConflictIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_appointments_users_created_by_staff_id",
                schema: "app",
                table: "appointments");

            migrationBuilder.CreateIndex(
                name: "ix_appointments_patient_datetime",
                schema: "app",
                table: "appointments",
                columns: new[] { "patient_id", "scheduled_at" });

            migrationBuilder.AddForeignKey(
                name: "fk_appointments_users_created_by_staff_id",
                schema: "app",
                table: "appointments",
                column: "created_by_staff_id",
                principalSchema: "app",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_appointments_users_created_by_staff_id",
                schema: "app",
                table: "appointments");

            migrationBuilder.DropIndex(
                name: "ix_appointments_patient_datetime",
                schema: "app",
                table: "appointments");

            migrationBuilder.AddForeignKey(
                name: "fk_appointments_users_created_by_staff_id",
                schema: "app",
                table: "appointments",
                column: "created_by_staff_id",
                principalSchema: "app",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
