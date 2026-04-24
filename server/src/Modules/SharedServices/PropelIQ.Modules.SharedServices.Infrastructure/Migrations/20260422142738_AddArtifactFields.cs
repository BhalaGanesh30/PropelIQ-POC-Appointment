using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PropelIQ.Modules.SharedServices.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddArtifactFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "artifacts_generated_at",
                schema: "app",
                table: "appointments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "email_retry_count",
                schema: "app",
                table: "appointments",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "email_sent",
                schema: "app",
                table: "appointments",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ics_storage_path",
                schema: "app",
                table: "appointments",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "pdf_storage_path",
                schema: "app",
                table: "appointments",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "qr_code_storage_path",
                schema: "app",
                table: "appointments",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "artifacts_generated_at",
                schema: "app",
                table: "appointments");

            migrationBuilder.DropColumn(
                name: "email_retry_count",
                schema: "app",
                table: "appointments");

            migrationBuilder.DropColumn(
                name: "email_sent",
                schema: "app",
                table: "appointments");

            migrationBuilder.DropColumn(
                name: "ics_storage_path",
                schema: "app",
                table: "appointments");

            migrationBuilder.DropColumn(
                name: "pdf_storage_path",
                schema: "app",
                table: "appointments");

            migrationBuilder.DropColumn(
                name: "qr_code_storage_path",
                schema: "app",
                table: "appointments");
        }
    }
}
