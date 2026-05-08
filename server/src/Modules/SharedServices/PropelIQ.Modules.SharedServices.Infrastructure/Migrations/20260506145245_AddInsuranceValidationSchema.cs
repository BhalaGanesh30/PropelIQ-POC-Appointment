using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace PropelIQ.Modules.SharedServices.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddInsuranceValidationSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "updated_at",
                schema: "app",
                table: "insurance_profiles",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone");

            migrationBuilder.AddColumn<string>(
                name: "card_image_back_path",
                schema: "app",
                table: "insurance_profiles",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "card_image_front_path",
                schema: "app",
                table: "insurance_profiles",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "group_number",
                schema: "app",
                table: "insurance_profiles",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "provider_code",
                schema: "app",
                table: "insurance_profiles",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "insurance_providers",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    provider_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    provider_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    policy_number_pattern = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_insurance_providers", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "insurance_validation_results",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    patient_id = table.Column<Guid>(type: "uuid", nullable: false),
                    policy_number = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    provider_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    tier = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    warnings_json = table.Column<string>(type: "text", nullable: true),
                    retry_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_insurance_validation_results", x => x.id);
                });

            migrationBuilder.InsertData(
                schema: "app",
                table: "insurance_providers",
                columns: new[] { "id", "created_at", "is_active", "policy_number_pattern", "provider_code", "provider_name", "updated_at" },
                values: new object[,]
                {
                    { new Guid("11111111-0000-0000-0000-000000000001"), new DateTimeOffset(new DateTime(2026, 5, 6, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), true, "^[A-Z]{3}[0-9]{9}$", "BCBS", "Blue Cross Blue Shield", new DateTimeOffset(new DateTime(2026, 5, 6, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { new Guid("11111111-0000-0000-0000-000000000002"), new DateTimeOffset(new DateTime(2026, 5, 6, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), true, "^W[0-9]{8,12}$", "AETNA", "Aetna", new DateTimeOffset(new DateTime(2026, 5, 6, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { new Guid("11111111-0000-0000-0000-000000000003"), new DateTimeOffset(new DateTime(2026, 5, 6, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), true, "^[0-9]{9,11}$", "UHC", "UnitedHealthcare", new DateTimeOffset(new DateTime(2026, 5, 6, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { new Guid("11111111-0000-0000-0000-000000000004"), new DateTimeOffset(new DateTime(2026, 5, 6, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), true, "^U[0-9]{8}$", "CIGNA", "Cigna", new DateTimeOffset(new DateTime(2026, 5, 6, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { new Guid("11111111-0000-0000-0000-000000000005"), new DateTimeOffset(new DateTime(2026, 5, 6, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), true, "^H[A-Z0-9]{6,14}$", "HUMANA", "Humana", new DateTimeOffset(new DateTime(2026, 5, 6, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) }
                });

            migrationBuilder.CreateIndex(
                name: "ix_insurance_providers_active_code",
                schema: "app",
                table: "insurance_providers",
                column: "provider_code",
                unique: true,
                filter: "\"is_active\" = true");

            migrationBuilder.CreateIndex(
                name: "ix_insurance_validation_results_patient_id",
                schema: "app",
                table: "insurance_validation_results",
                column: "patient_id");

            migrationBuilder.CreateIndex(
                name: "ix_insurance_validation_results_status_retry",
                schema: "app",
                table: "insurance_validation_results",
                columns: new[] { "status", "retry_count" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "insurance_providers",
                schema: "app");

            migrationBuilder.DropTable(
                name: "insurance_validation_results",
                schema: "app");

            migrationBuilder.DropColumn(
                name: "card_image_back_path",
                schema: "app",
                table: "insurance_profiles");

            migrationBuilder.DropColumn(
                name: "card_image_front_path",
                schema: "app",
                table: "insurance_profiles");

            migrationBuilder.DropColumn(
                name: "group_number",
                schema: "app",
                table: "insurance_profiles");

            migrationBuilder.DropColumn(
                name: "provider_code",
                schema: "app",
                table: "insurance_profiles");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "updated_at",
                schema: "app",
                table: "insurance_profiles",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");
        }
    }
}
