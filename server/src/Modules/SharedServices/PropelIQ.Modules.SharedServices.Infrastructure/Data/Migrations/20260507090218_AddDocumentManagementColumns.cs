using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PropelIQ.Modules.SharedServices.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentManagementColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_clinical_documents_patient_id",
                schema: "app",
                table: "clinical_documents");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:Enum:app.document_category_type", "lab_report,referral,prescription,imaging,insurance,other")
                .Annotation("Npgsql:PostgresExtension:pg_trgm", ",,")
                .Annotation("Npgsql:PostgresExtension:uuid-ossp", ",,")
                .OldAnnotation("Npgsql:PostgresExtension:pg_trgm", ",,")
                .OldAnnotation("Npgsql:PostgresExtension:uuid-ossp", ",,");

            // Nullify any legacy category values that are not valid enum labels
            // before casting the column type. Valid labels: lab_report, referral,
            // prescription, imaging, insurance, other.
            migrationBuilder.Sql(
                """
                UPDATE app.clinical_documents
                SET category = NULL
                WHERE category NOT IN ('lab_report','referral','prescription','imaging','insurance','other');
                """);

            // Raw SQL with USING clause — PostgreSQL cannot automatically cast
            // varchar → enum without an explicit cast expression.
            migrationBuilder.Sql(
                """
                ALTER TABLE app.clinical_documents
                    ALTER COLUMN category DROP NOT NULL,
                    ALTER COLUMN category TYPE document_category_type
                        USING category::document_category_type;
                """);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "deleted_at",
                schema: "app",
                table: "clinical_documents",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "display_name",
                schema: "app",
                table: "clinical_documents",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_deleted",
                schema: "app",
                table: "clinical_documents",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "ix_clinical_documents_is_deleted",
                schema: "app",
                table: "clinical_documents",
                column: "is_deleted",
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "ix_clinical_documents_patient_active",
                schema: "app",
                table: "clinical_documents",
                columns: new[] { "patient_id", "is_deleted" },
                filter: "is_deleted = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_clinical_documents_is_deleted",
                schema: "app",
                table: "clinical_documents");

            migrationBuilder.DropIndex(
                name: "ix_clinical_documents_patient_active",
                schema: "app",
                table: "clinical_documents");

            migrationBuilder.DropColumn(
                name: "deleted_at",
                schema: "app",
                table: "clinical_documents");

            migrationBuilder.DropColumn(
                name: "display_name",
                schema: "app",
                table: "clinical_documents");

            migrationBuilder.DropColumn(
                name: "is_deleted",
                schema: "app",
                table: "clinical_documents");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:pg_trgm", ",,")
                .Annotation("Npgsql:PostgresExtension:uuid-ossp", ",,")
                .OldAnnotation("Npgsql:Enum:app.document_category_type", "lab_report,referral,prescription,imaging,insurance,other")
                .OldAnnotation("Npgsql:PostgresExtension:pg_trgm", ",,")
                .OldAnnotation("Npgsql:PostgresExtension:uuid-ossp", ",,");

            migrationBuilder.AlterColumn<string>(
                name: "category",
                schema: "app",
                table: "clinical_documents",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "document_category_type",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_clinical_documents_patient_id",
                schema: "app",
                table: "clinical_documents",
                column: "patient_id");
        }
    }
}
