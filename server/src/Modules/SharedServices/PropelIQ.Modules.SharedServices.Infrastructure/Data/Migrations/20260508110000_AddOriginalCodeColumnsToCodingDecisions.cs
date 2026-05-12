using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PropelIQ.Modules.SharedServices.Infrastructure.Data.Migrations
{
    /// <summary>
    /// Additive migration adding original-code snapshot columns to <c>coding_decisions</c>
    /// for AIR-007 agreement rate tracking (US_051/task_003).
    ///
    /// Changes applied:
    ///   - Adds <c>original_icd10_code</c> VARCHAR(20) NULL — AI-suggested ICD-10 code snapshot
    ///     captured before a Modify action overwrites the finalized code.
    ///   - Adds <c>original_cpt_code</c> VARCHAR(20) NULL — AI-suggested CPT code snapshot,
    ///     same semantics as <c>original_icd10_code</c> for CPT decisions.
    ///
    /// Both columns are NULL for Accepted and Rejected rows; populated only when
    /// <c>reviewer_action = 'Modified'</c> to enable direct AI-vs-final comparison
    /// without parsing the audit log JSONB (AIR-007).
    ///
    /// No destructive changes — all existing columns and constraints are preserved.
    /// </summary>
    public partial class AddOriginalCodeColumnsToCodingDecisions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "original_icd10_code",
                schema: "app",
                table: "coding_decisions",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "original_cpt_code",
                schema: "app",
                table: "coding_decisions",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "original_icd10_code",
                schema: "app",
                table: "coding_decisions");

            migrationBuilder.DropColumn(
                name: "original_cpt_code",
                schema: "app",
                table: "coding_decisions");
        }
    }
}
