using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PropelIQ.Modules.SharedServices.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddInsuranceCardImageKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "card_image_back_key",
                schema: "app",
                table: "insurance_profiles",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "card_image_front_key",
                schema: "app",
                table: "insurance_profiles",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "card_image_back_key",
                schema: "app",
                table: "insurance_profiles");

            migrationBuilder.DropColumn(
                name: "card_image_front_key",
                schema: "app",
                table: "insurance_profiles");
        }
    }
}
