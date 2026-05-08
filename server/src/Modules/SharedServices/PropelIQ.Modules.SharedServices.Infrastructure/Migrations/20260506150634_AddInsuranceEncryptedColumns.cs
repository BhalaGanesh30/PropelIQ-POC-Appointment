using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PropelIQ.Modules.SharedServices.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddInsuranceEncryptedColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "encrypted_group_number",
                schema: "app",
                table: "insurance_profiles",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "encrypted_policy_number",
                schema: "app",
                table: "insurance_profiles",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "encrypted_provider_name",
                schema: "app",
                table: "insurance_profiles",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "group_number_hmac",
                schema: "app",
                table: "insurance_profiles",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "key_version",
                schema: "app",
                table: "insurance_profiles",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "policy_number_hmac",
                schema: "app",
                table: "insurance_profiles",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "provider_name_hmac",
                schema: "app",
                table: "insurance_profiles",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_insurance_profiles_key_version",
                schema: "app",
                table: "insurance_profiles",
                column: "key_version");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_insurance_profiles_key_version",
                schema: "app",
                table: "insurance_profiles");

            migrationBuilder.DropColumn(
                name: "encrypted_group_number",
                schema: "app",
                table: "insurance_profiles");

            migrationBuilder.DropColumn(
                name: "encrypted_policy_number",
                schema: "app",
                table: "insurance_profiles");

            migrationBuilder.DropColumn(
                name: "encrypted_provider_name",
                schema: "app",
                table: "insurance_profiles");

            migrationBuilder.DropColumn(
                name: "group_number_hmac",
                schema: "app",
                table: "insurance_profiles");

            migrationBuilder.DropColumn(
                name: "key_version",
                schema: "app",
                table: "insurance_profiles");

            migrationBuilder.DropColumn(
                name: "policy_number_hmac",
                schema: "app",
                table: "insurance_profiles");

            migrationBuilder.DropColumn(
                name: "provider_name_hmac",
                schema: "app",
                table: "insurance_profiles");
        }
    }
}
