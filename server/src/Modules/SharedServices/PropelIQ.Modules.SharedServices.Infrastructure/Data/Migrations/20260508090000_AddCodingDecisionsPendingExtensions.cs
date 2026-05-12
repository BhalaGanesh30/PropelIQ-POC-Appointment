using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PropelIQ.Modules.SharedServices.Infrastructure.Data.Migrations
{
    /// <summary>
    /// Additive migration extending <c>coding_decisions</c> with US_049/US_050 requirements (EP-008 task_003).
    ///
    /// Changes applied:
    ///   - Adds <c>cpt_code</c> VARCHAR(20) NULL — reserved for CPT code in US_050 workflow.
    ///   - Adds <c>decided_at</c> TIMESTAMPTZ NULL — timestamp when clinician accepts/modifies/rejects.
    ///   - Adds FK <c>reviewed_by_user_id → users ON DELETE SET NULL</c> — enforces referential integrity
    ///     while allowing user deletion without corrupting historical decision records.
    ///   - Adds partial index <c>ix_coding_decisions_pending</c> on <c>patient_id WHERE reviewer_action = 'Pending'</c>
    ///     to accelerate pending-queue queries in the US_050 coding review workflow.
    ///
    /// No destructive changes — all existing columns, indexes, and FK constraints are preserved.
    /// </summary>
    public partial class AddCodingDecisionsPendingExtensions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add cpt_code column (nullable; populated in US_050).
            migrationBuilder.AddColumn<string>(
                name: "cpt_code",
                schema: "app",
                table: "coding_decisions",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            // Add decided_at column (nullable until clinician acts).
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "decided_at",
                schema: "app",
                table: "coding_decisions",
                type: "timestamp with time zone",
                nullable: true);

            // Add FK from reviewed_by_user_id → users ON DELETE SET NULL.
            // The column already exists as a bare UUID; this adds the FK constraint.
            migrationBuilder.AddForeignKey(
                name: "fk_coding_decisions_users_reviewed_by_user_id",
                schema: "app",
                table: "coding_decisions",
                column: "reviewed_by_user_id",
                principalSchema: "app",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            // Partial index on patient_id filtered to pending rows — accelerates US_050 review queue.
            // EF Core migrationBuilder.CreateIndex does not support WHERE clauses; use raw SQL.
            migrationBuilder.Sql("""
                CREATE INDEX IF NOT EXISTS ix_coding_decisions_pending
                ON app.coding_decisions (patient_id)
                WHERE reviewer_action = 'Pending';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP INDEX IF EXISTS app.ix_coding_decisions_pending;
                """);

            migrationBuilder.DropForeignKey(
                name: "fk_coding_decisions_users_reviewed_by_user_id",
                schema: "app",
                table: "coding_decisions");

            migrationBuilder.DropColumn(
                name: "decided_at",
                schema: "app",
                table: "coding_decisions");

            migrationBuilder.DropColumn(
                name: "cpt_code",
                schema: "app",
                table: "coding_decisions");
        }
    }
}
