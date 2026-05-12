using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PropelIQ.Modules.SharedServices.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCptCodesReferenceTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "cpt_codes",
                schema: "app",
                columns: table => new
                {
                    cpt_code = table.Column<string>(
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
                    table.PrimaryKey("pk_cpt_codes", x => x.cpt_code);
                });

            // B-tree index on last_updated_at DESC for MAX() freshness scan.
            migrationBuilder.CreateIndex(
                name: "ix_cpt_codes_last_updated",
                schema: "app",
                table: "cpt_codes",
                column: "last_updated_at");

            // Partial index on active (non-deprecated) codes — standard access pattern.
            // EF Core CreateIndex does not support WHERE clauses; use raw SQL.
            migrationBuilder.Sql(
                "CREATE INDEX IF NOT EXISTS ix_cpt_codes_active " +
                "ON app.cpt_codes (cpt_code) " +
                "WHERE is_deprecated = false;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "cpt_codes",
                schema: "app");
        }
    }
}
