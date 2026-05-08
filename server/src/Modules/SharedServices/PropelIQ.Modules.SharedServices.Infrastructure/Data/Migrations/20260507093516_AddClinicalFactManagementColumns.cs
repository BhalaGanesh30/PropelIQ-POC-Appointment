using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PropelIQ.Modules.SharedServices.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddClinicalFactManagementColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "name",
                schema: "app",
                table: "clinical_facts",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "needs_review",
                schema: "app",
                table: "clinical_facts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "source_text",
                schema: "app",
                table: "clinical_facts",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_clinical_facts_needs_review",
                schema: "app",
                table: "clinical_facts",
                column: "needs_review",
                filter: "needs_review = true");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_clinical_facts_needs_review",
                schema: "app",
                table: "clinical_facts");

            migrationBuilder.DropColumn(
                name: "name",
                schema: "app",
                table: "clinical_facts");

            migrationBuilder.DropColumn(
                name: "needs_review",
                schema: "app",
                table: "clinical_facts");

            migrationBuilder.DropColumn(
                name: "source_text",
                schema: "app",
                table: "clinical_facts");
        }
    }
}
