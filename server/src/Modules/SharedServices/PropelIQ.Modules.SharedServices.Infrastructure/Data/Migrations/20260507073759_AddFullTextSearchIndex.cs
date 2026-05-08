using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PropelIQ.Modules.SharedServices.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFullTextSearchIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // GIN index on to_tsvector enables fast full-text search over extracted OCR text.
            // Used by SearchExtractedTextAsync: to_tsvector(...) @@ plainto_tsquery(...).
            // Requires pg_trgm extension (already present via 01-create-extensions.sql).
            migrationBuilder.Sql("""
                CREATE INDEX IF NOT EXISTS ix_clinical_documents_extracted_text_fts
                    ON app.clinical_documents
                    USING GIN (to_tsvector('english', coalesce(extracted_text, '')));
                """);

            // pg_trgm GIN index enables ILIKE trigram fallback for fuzzy search.
            migrationBuilder.Sql("""
                CREATE INDEX IF NOT EXISTS ix_clinical_documents_extracted_text_trgm
                    ON app.clinical_documents
                    USING GIN (extracted_text gin_trgm_ops)
                    WHERE extracted_text IS NOT NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS app.ix_clinical_documents_extracted_text_fts;");
            migrationBuilder.Sql("DROP INDEX IF EXISTS app.ix_clinical_documents_extracted_text_trgm;");
        }
    }
}

