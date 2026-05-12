using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Pgvector;

#nullable disable

namespace PropelIQ.Modules.SharedServices.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddClinicalFactsSchemaExtension : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:Enum:app.document_category_type", "lab_report,referral,prescription,imaging,insurance,other")
                .Annotation("Npgsql:PostgresExtension:pg_trgm", ",,")
                .Annotation("Npgsql:PostgresExtension:uuid-ossp", ",,")
                .OldAnnotation("Npgsql:Enum:app.document_category_type", "lab_report,referral,prescription,imaging,insurance,other")
                .OldAnnotation("Npgsql:PostgresExtension:pg_trgm", ",,")
                .OldAnnotation("Npgsql:PostgresExtension:uuid-ossp", ",,");

            // pgvector: skip gracefully when extension is not installed (local dev without Docker).
            migrationBuilder.Sql(
                """
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM pg_available_extensions WHERE name = 'vector') THEN
                        CREATE EXTENSION IF NOT EXISTS vector;
                    ELSE
                        RAISE NOTICE 'pgvector not available — skipping extension creation';
                    END IF;
                END $$;
                """);

            // Only add the embedding column if the vector type is available.
            migrationBuilder.Sql(
                """
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM pg_extension WHERE extname = 'vector') THEN
                        ALTER TABLE app.clinical_facts ADD COLUMN IF NOT EXISTS embedding vector(1536);
                    ELSE
                        RAISE NOTICE 'pgvector not installed — skipping embedding column';
                    END IF;
                END $$;
                """);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "fact_date",
                schema: "app",
                table: "clinical_facts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "patient_id",
                schema: "app",
                table: "clinical_facts",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<bool>(
                name: "verified",
                schema: "app",
                table: "clinical_facts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "verified_by",
                schema: "app",
                table: "clinical_facts",
                type: "uuid",
                nullable: true);

            // HNSW index requires pgvector — skip gracefully on local dev without Docker.
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM pg_extension WHERE extname = 'vector') THEN
                        DROP INDEX IF EXISTS app.ix_clinical_facts_embedding;
                        EXECUTE 'CREATE INDEX ix_clinical_facts_embedding
                            ON app.clinical_facts
                            USING hnsw (embedding vector_cosine_ops)
                            WITH (m = 16, ef_construction = 64)';
                    ELSE
                        RAISE NOTICE 'pgvector not installed — skipping hnsw index';
                    END IF;
                END $$;
                """);

            migrationBuilder.CreateIndex(
                name: "ix_clinical_facts_patient_id",
                schema: "app",
                table: "clinical_facts",
                column: "patient_id");

            migrationBuilder.CreateIndex(
                name: "ix_clinical_facts_verified_by",
                schema: "app",
                table: "clinical_facts",
                column: "verified_by");

            migrationBuilder.AddCheckConstraint(
                name: "chk_clinical_facts_confidence",
                schema: "app",
                table: "clinical_facts",
                sql: "confidence_score >= 0.0 AND confidence_score <= 1.0");

            migrationBuilder.AddForeignKey(
                name: "fk_clinical_facts_patients_patient_id",
                schema: "app",
                table: "clinical_facts",
                column: "patient_id",
                principalSchema: "app",
                principalTable: "patients",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_clinical_facts_users_verified_by",
                schema: "app",
                table: "clinical_facts",
                column: "verified_by",
                principalSchema: "app",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_clinical_facts_patients_patient_id",
                schema: "app",
                table: "clinical_facts");

            migrationBuilder.DropForeignKey(
                name: "fk_clinical_facts_users_verified_by",
                schema: "app",
                table: "clinical_facts");

            migrationBuilder.DropIndex(
                name: "ix_clinical_facts_embedding",
                schema: "app",
                table: "clinical_facts");

            migrationBuilder.DropIndex(
                name: "ix_clinical_facts_patient_id",
                schema: "app",
                table: "clinical_facts");

            migrationBuilder.DropIndex(
                name: "ix_clinical_facts_verified_by",
                schema: "app",
                table: "clinical_facts");

            migrationBuilder.DropCheckConstraint(
                name: "chk_clinical_facts_confidence",
                schema: "app",
                table: "clinical_facts");

            migrationBuilder.DropColumn(
                name: "embedding",
                schema: "app",
                table: "clinical_facts");

            migrationBuilder.DropColumn(
                name: "fact_date",
                schema: "app",
                table: "clinical_facts");

            migrationBuilder.DropColumn(
                name: "patient_id",
                schema: "app",
                table: "clinical_facts");

            migrationBuilder.DropColumn(
                name: "verified",
                schema: "app",
                table: "clinical_facts");

            migrationBuilder.DropColumn(
                name: "verified_by",
                schema: "app",
                table: "clinical_facts");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:Enum:app.document_category_type", "lab_report,referral,prescription,imaging,insurance,other")
                .Annotation("Npgsql:PostgresExtension:pg_trgm", ",,")
                .Annotation("Npgsql:PostgresExtension:uuid-ossp", ",,")
                .OldAnnotation("Npgsql:Enum:app.document_category_type", "lab_report,referral,prescription,imaging,insurance,other")
                .OldAnnotation("Npgsql:PostgresExtension:pg_trgm", ",,")
                .OldAnnotation("Npgsql:PostgresExtension:uuid-ossp", ",,")
                .OldAnnotation("Npgsql:PostgresExtension:vector", ",,");
        }
    }
}
