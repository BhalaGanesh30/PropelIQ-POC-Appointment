using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PropelIQ.Modules.SharedServices.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddClinicalDocumentsSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "extraction_status",
                schema: "app",
                table: "clinical_documents",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "Queued",
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AddColumn<string>(
                name: "content_type",
                schema: "app",
                table: "clinical_documents",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "extracted_text",
                schema: "app",
                table: "clinical_documents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "file_size_bytes",
                schema: "app",
                table: "clinical_documents",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "r2_object_key",
                schema: "app",
                table: "clinical_documents",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "scan_result",
                schema: "app",
                table: "clinical_documents",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "PendingScan");

            migrationBuilder.CreateIndex(
                name: "ix_clinical_documents_extraction_status",
                schema: "app",
                table: "clinical_documents",
                column: "extraction_status");

            migrationBuilder.CreateIndex(
                name: "ix_clinical_documents_scan_result",
                schema: "app",
                table: "clinical_documents",
                column: "scan_result");

            migrationBuilder.AddCheckConstraint(
                name: "chk_clinical_documents_file_size",
                schema: "app",
                table: "clinical_documents",
                sql: "file_size_bytes <= 10485760");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_clinical_documents_extraction_status",
                schema: "app",
                table: "clinical_documents");

            migrationBuilder.DropIndex(
                name: "ix_clinical_documents_scan_result",
                schema: "app",
                table: "clinical_documents");

            migrationBuilder.DropCheckConstraint(
                name: "chk_clinical_documents_file_size",
                schema: "app",
                table: "clinical_documents");

            migrationBuilder.DropColumn(
                name: "content_type",
                schema: "app",
                table: "clinical_documents");

            migrationBuilder.DropColumn(
                name: "extracted_text",
                schema: "app",
                table: "clinical_documents");

            migrationBuilder.DropColumn(
                name: "file_size_bytes",
                schema: "app",
                table: "clinical_documents");

            migrationBuilder.DropColumn(
                name: "r2_object_key",
                schema: "app",
                table: "clinical_documents");

            migrationBuilder.DropColumn(
                name: "scan_result",
                schema: "app",
                table: "clinical_documents");

            migrationBuilder.AlterColumn<string>(
                name: "extraction_status",
                schema: "app",
                table: "clinical_documents",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldDefaultValue: "Queued");
        }
    }
}
