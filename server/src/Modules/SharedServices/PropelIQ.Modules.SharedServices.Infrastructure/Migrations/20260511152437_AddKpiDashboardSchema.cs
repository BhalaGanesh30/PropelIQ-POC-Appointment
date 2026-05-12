using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PropelIQ.Modules.SharedServices.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddKpiDashboardSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "kpi_daily_metrics",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    no_show_rate = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    utilization_rate = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    average_wait_minutes = table.Column<decimal>(type: "numeric(8,2)", nullable: false),
                    booking_count = table.Column<int>(type: "integer", nullable: false),
                    available_slots = table.Column<int>(type: "integer", nullable: false),
                    booked_slots = table.Column<int>(type: "integer", nullable: false),
                    computed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_kpi_daily_metrics", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "kpi_distribution_logs",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    period_from = table.Column<DateOnly>(type: "date", nullable: false),
                    period_to = table.Column<DateOnly>(type: "date", nullable: false),
                    recipient_emails = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    sent_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    error_detail = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_kpi_distribution_logs", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_kpi_daily_metrics_date",
                schema: "app",
                table: "kpi_daily_metrics",
                column: "date",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_kpi_distribution_logs_period_status",
                schema: "app",
                table: "kpi_distribution_logs",
                columns: new[] { "period_from", "status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "kpi_daily_metrics",
                schema: "app");

            migrationBuilder.DropTable(
                name: "kpi_distribution_logs",
                schema: "app");
        }
    }
}
