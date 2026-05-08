using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PropelIQ.Modules.SharedServices.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCreatedByStaffId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "created_by_staff_id",
                schema: "app",
                table: "appointments",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_appointments_created_by_staff_id",
                schema: "app",
                table: "appointments",
                column: "created_by_staff_id",
                filter: "created_by_staff_id IS NOT NULL");

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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_appointments_users_created_by_staff_id",
                schema: "app",
                table: "appointments");

            migrationBuilder.DropIndex(
                name: "ix_appointments_created_by_staff_id",
                schema: "app",
                table: "appointments");

            migrationBuilder.DropColumn(
                name: "created_by_staff_id",
                schema: "app",
                table: "appointments");
        }
    }
}
