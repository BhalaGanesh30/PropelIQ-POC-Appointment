using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PropelIQ.Modules.SharedServices.Infrastructure.Identity.Migrations
{
    /// <inheritdoc />
    public partial class AddActiveSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "active_sessions",
                schema: "auth",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    session_token = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    last_activity_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ip_address = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    user_agent = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    termination_reason = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    terminated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_active_sessions", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_active_sessions_last_activity_at",
                schema: "auth",
                table: "active_sessions",
                column: "last_activity_at");

            migrationBuilder.CreateIndex(
                name: "ix_active_sessions_session_token",
                schema: "auth",
                table: "active_sessions",
                column: "session_token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_active_sessions_user_id_is_active",
                schema: "auth",
                table: "active_sessions",
                columns: new[] { "user_id", "is_active" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "active_sessions",
                schema: "auth");
        }
    }
}
