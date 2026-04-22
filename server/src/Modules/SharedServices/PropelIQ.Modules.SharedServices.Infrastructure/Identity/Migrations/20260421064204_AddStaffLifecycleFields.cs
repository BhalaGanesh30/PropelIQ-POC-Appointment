using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PropelIQ.Modules.SharedServices.Infrastructure.Identity.Migrations
{
    /// <inheritdoc />
    public partial class AddStaffLifecycleFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "account_status",
                schema: "auth",
                table: "AspNetUsers",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "activated_at",
                schema: "auth",
                table: "AspNetUsers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "deactivated_at",
                schema: "auth",
                table: "AspNetUsers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "deactivated_by",
                schema: "auth",
                table: "AspNetUsers",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "invitation_expires_at",
                schema: "auth",
                table: "AspNetUsers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "invited_at",
                schema: "auth",
                table: "AspNetUsers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "invited_by",
                schema: "auth",
                table: "AspNetUsers",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "account_status",
                schema: "auth",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "activated_at",
                schema: "auth",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "deactivated_at",
                schema: "auth",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "deactivated_by",
                schema: "auth",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "invitation_expires_at",
                schema: "auth",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "invited_at",
                schema: "auth",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "invited_by",
                schema: "auth",
                table: "AspNetUsers");
        }
    }
}
