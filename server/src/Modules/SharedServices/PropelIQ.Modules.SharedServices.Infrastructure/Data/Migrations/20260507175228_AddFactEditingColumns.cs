using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PropelIQ.Modules.SharedServices.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFactEditingColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "fact_id",
                schema: "app",
                table: "coding_decisions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "row_version",
                schema: "app",
                table: "clinical_facts",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "verified_at",
                schema: "app",
                table: "clinical_facts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_coding_decisions_fact_id",
                schema: "app",
                table: "coding_decisions",
                column: "fact_id");

            migrationBuilder.CreateIndex(
                name: "ix_clinical_facts_row_version",
                schema: "app",
                table: "clinical_facts",
                columns: new[] { "id", "row_version" });

            migrationBuilder.AddForeignKey(
                name: "fk_coding_decisions_clinical_facts_fact_id",
                schema: "app",
                table: "coding_decisions",
                column: "fact_id",
                principalSchema: "app",
                principalTable: "clinical_facts",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_coding_decisions_clinical_facts_fact_id",
                schema: "app",
                table: "coding_decisions");

            migrationBuilder.DropIndex(
                name: "ix_coding_decisions_fact_id",
                schema: "app",
                table: "coding_decisions");

            migrationBuilder.DropIndex(
                name: "ix_clinical_facts_row_version",
                schema: "app",
                table: "clinical_facts");

            migrationBuilder.DropColumn(
                name: "fact_id",
                schema: "app",
                table: "coding_decisions");

            migrationBuilder.DropColumn(
                name: "row_version",
                schema: "app",
                table: "clinical_facts");

            migrationBuilder.DropColumn(
                name: "verified_at",
                schema: "app",
                table: "clinical_facts");
        }
    }
}
