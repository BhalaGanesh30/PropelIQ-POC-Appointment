using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PropelIQ.Modules.SharedServices.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCodeSearchSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── 0. Ensure pg_trgm extension is active ─────────────────────────────
            // Required before GIN trigram indexes can be created (AC-1, NFR-002).
            // Safe to run multiple times — CREATE EXTENSION IF NOT EXISTS is idempotent.
            // Docker init/01-create-extensions.sql also enables this; the migration guard
            // ensures correctness for CI and fresh-environment setups.
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS pg_trgm;");

            // ── 1. Make coding_decisions.document_id nullable ─────────────────────
            // Manual code selections (US_052, AC-2) do not originate from a clinical document.
            // Per DR-007 two-migration strategy: this migration makes the column nullable.
            migrationBuilder.DropForeignKey(
                name: "fk_coding_decisions_clinical_documents_document_id",
                schema: "app",
                table: "coding_decisions");

            migrationBuilder.AlterColumn<Guid>(
                name: "document_id",
                schema: "app",
                table: "coding_decisions",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: false);

            migrationBuilder.AddForeignKey(
                name: "fk_coding_decisions_clinical_documents_document_id",
                schema: "app",
                table: "coding_decisions",
                column: "document_id",
                principalSchema: "app",
                principalTable: "clinical_documents",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            // ── 2. Create app.icd_codes reference table ───────────────────────────
            // Mirrors app.cpt_codes structure (US_050/task_003) for UNION query consistency.
            // Columns: code (PK), description, category, is_deprecated, effective_date,
            //          deprecation_date, last_updated_at.
            migrationBuilder.CreateTable(
                name: "icd_codes",
                schema: "app",
                columns: table => new
                {
                    code = table.Column<string>(
                        type: "character varying(20)",
                        maxLength: 20,
                        nullable: false),
                    description = table.Column<string>(
                        type: "text",
                        nullable: false),
                    category = table.Column<string>(
                        type: "character varying(100)",
                        maxLength: 100,
                        nullable: true),
                    is_deprecated = table.Column<bool>(
                        type: "boolean",
                        nullable: false,
                        defaultValue: false),
                    effective_date = table.Column<DateOnly>(
                        type: "date",
                        nullable: true),
                    deprecation_date = table.Column<DateOnly>(
                        type: "date",
                        nullable: true),
                    last_updated_at = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false,
                        defaultValueSql: "now()"),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_icd_codes", x => x.code);
                });

            // Partial B-tree index: active (non-deprecated) codes (Edge Case 2).
            migrationBuilder.CreateIndex(
                name: "ix_icd_codes_active",
                schema: "app",
                table: "icd_codes",
                column: "code",
                filter: "is_deprecated = false");

            // B-tree index on last_updated_at — accelerates freshness tracking (mirrors cpt_codes).
            migrationBuilder.CreateIndex(
                name: "ix_icd_codes_last_updated",
                schema: "app",
                table: "icd_codes",
                column: "last_updated_at");

            // GIN trigram expression index for pg_trgm similarity search (NFR-002 ≤ 500ms p95, AC-1).
            // Single expression index on (code || ' ' || description) supports:
            //   - similarity(code || ' ' || description, query) scoring used by CodeReferenceRepository
            //   - ILIKE '%pattern%' filtering on both columns via the trigram index
            // EF Core CreateIndex does not support GIN operator classes; raw SQL is required.
            migrationBuilder.Sql(
                "CREATE INDEX IF NOT EXISTS ix_icd_codes_trgm " +
                "ON app.icd_codes USING GIN ((code || ' ' || description) gin_trgm_ops);");

            // ── 3. Backfill GIN trigram expression index on cpt_codes ─────────────
            // US_050/task_003 created cpt_codes without a trigram index.
            // Required for the UNION search in CodeReferenceRepository (type=cpt or type=all).
            migrationBuilder.Sql(
                "CREATE INDEX IF NOT EXISTS ix_cpt_codes_trgm " +
                "ON app.cpt_codes USING GIN ((cpt_code || ' ' || description) gin_trgm_ops);");

            // ── 4. Create app.user_code_favorites table ───────────────────────────
            // Composite PK (user_id, code_type, code) prevents duplicate favorites per user.
            // FK to users ON DELETE CASCADE — user deletion removes their favorites.
            // No FK to icd_codes/cpt_codes — referential integrity enforced at service layer
            // (see ICodeFavoriteRepository.AddAsync code-existence validation, US_052 AC-3).
            migrationBuilder.CreateTable(
                name: "user_code_favorites",
                schema: "app",
                columns: table => new
                {
                    user_id = table.Column<Guid>(
                        type: "uuid",
                        nullable: false),
                    code_type = table.Column<string>(
                        type: "character varying(10)",
                        maxLength: 10,
                        nullable: false),
                    code = table.Column<string>(
                        type: "character varying(20)",
                        maxLength: 20,
                        nullable: false),
                    created_at = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false,
                        defaultValueSql: "now()"),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_code_favorites", x => new { x.user_id, x.code_type, x.code });

                    table.ForeignKey(
                        name: "fk_user_code_favorites_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "app",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            // B-tree index on user_id — accelerates per-user favorites lookup (AC-3, AC-4).
            migrationBuilder.CreateIndex(
                name: "ix_user_code_favorites_user_id",
                schema: "app",
                table: "user_code_favorites",
                column: "user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "user_code_favorites", schema: "app");

            migrationBuilder.Sql("DROP INDEX IF EXISTS app.ix_cpt_codes_trgm;");
            migrationBuilder.Sql("DROP INDEX IF EXISTS app.ix_icd_codes_trgm;");

            migrationBuilder.DropTable(name: "icd_codes", schema: "app");

            // Reverse: restore NOT NULL constraint on document_id.
            migrationBuilder.DropForeignKey(
                name: "fk_coding_decisions_clinical_documents_document_id",
                schema: "app",
                table: "coding_decisions");

            migrationBuilder.AlterColumn<Guid>(
                name: "document_id",
                schema: "app",
                table: "coding_decisions",
                type: "uuid",
                nullable: false,
                defaultValue: Guid.Empty,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "fk_coding_decisions_clinical_documents_document_id",
                schema: "app",
                table: "coding_decisions",
                column: "document_id",
                principalSchema: "app",
                principalTable: "clinical_documents",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
