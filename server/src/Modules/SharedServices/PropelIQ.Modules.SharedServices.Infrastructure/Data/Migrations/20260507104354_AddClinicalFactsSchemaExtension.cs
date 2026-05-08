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
                .Annotation("Npgsql:PostgresExtension:vector", ",,")
                .OldAnnotation("Npgsql:Enum:app.document_category_type", "lab_report,referral,prescription,imaging,insurance,other")
                .OldAnnotation("Npgsql:PostgresExtension:pg_trgm", ",,")
                .OldAnnotation("Npgsql:PostgresExtension:uuid-ossp", ",,");

            migrationBuilder.AddColumn<Vector>(
                name: "embedding",
                schema: "app",
                table: "clinical_facts",
                type: "vector(1536)",
                nullable: true);

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

            migrationBuilder.CreateIndex(
                name: "ix_clinical_facts_embedding",
                schema: "app",
                table: "clinical_facts",
                column: "embedding")
                .Annotation("Npgsql:IndexMethod", "hnsw")
                .Annotation("Npgsql:IndexOperators", new[] { "vector_cosine_ops" });
            // Re-create with HNSW parameters (m=16, ef_construction=64) via raw SQL
            // so similarity search is tuned for the 1536-dim workload (AIR-010).
            migrationBuilder.Sql("""
                DROP INDEX IF EXISTS app.ix_clinical_facts_embedding;
                CREATE INDEX ix_clinical_facts_embedding
                    ON app.clinical_facts
                    USING hnsw (embedding vector_cosine_ops)
                    WITH (m = 16, ef_construction = 64);
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
