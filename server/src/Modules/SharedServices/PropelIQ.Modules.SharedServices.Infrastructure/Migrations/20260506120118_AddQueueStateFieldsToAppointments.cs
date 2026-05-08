using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PropelIQ.Modules.SharedServices.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddQueueStateFieldsToAppointments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "arrived_at",
                schema: "app",
                table: "appointments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "visit_ended_at",
                schema: "app",
                table: "appointments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "visit_started_at",
                schema: "app",
                table: "appointments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_appointments_queue_date",
                schema: "app",
                table: "appointments",
                columns: new[] { "scheduled_at", "queue_state" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_appointments_queue_date",
                schema: "app",
                table: "appointments");

            migrationBuilder.DropColumn(
                name: "arrived_at",
                schema: "app",
                table: "appointments");

            migrationBuilder.DropColumn(
                name: "visit_ended_at",
                schema: "app",
                table: "appointments");

            migrationBuilder.DropColumn(
                name: "visit_started_at",
                schema: "app",
                table: "appointments");
        }
    }
}
