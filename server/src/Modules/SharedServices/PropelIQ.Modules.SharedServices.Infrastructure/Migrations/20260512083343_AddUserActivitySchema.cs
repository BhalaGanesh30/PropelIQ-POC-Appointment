using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PropelIQ.Modules.SharedServices.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUserActivitySchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "user_type",
                schema: "app",
                table: "users",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "Staff");

            migrationBuilder.CreateTable(
                name: "user_activity_logs",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    description = table.Column<string>(type: "text", nullable: false, defaultValue: ""),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    performed_by_user_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_activity_logs", x => x.id);
                    table.ForeignKey(
                        name: "fk_user_activity_logs_users_performed_by_user_id",
                        column: x => x.performed_by_user_id,
                        principalSchema: "app",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_user_activity_logs_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "app",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_user_activity_logs_performed_by_user_id",
                schema: "app",
                table: "user_activity_logs",
                column: "performed_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_user_activity_logs_user_occurred",
                schema: "app",
                table: "user_activity_logs",
                columns: new[] { "user_id", "occurred_at" },
                descending: new[] { false, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "user_activity_logs",
                schema: "app");

            migrationBuilder.DropColumn(
                name: "user_type",
                schema: "app",
                table: "users");
        }
    }
}
