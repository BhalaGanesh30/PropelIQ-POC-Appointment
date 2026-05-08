using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PropelIQ.Modules.SharedServices.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddWalkInSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Ensure pg_trgm is available for the GIN trigram indexes below.
            // This is idempotent — no-op when already enabled (DR-007 zero-downtime).
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS pg_trgm;");

            migrationBuilder.CreateTable(
                name: "walk_ins",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    patient_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    phone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    visit_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    patient_id = table.Column<Guid>(type: "uuid", nullable: true),
                    appointment_id = table.Column<Guid>(type: "uuid", nullable: true),
                    is_converted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_walk_ins", x => x.id);
                    table.ForeignKey(
                        name: "fk_walk_ins_appointments_appointment_id",
                        column: x => x.appointment_id,
                        principalSchema: "app",
                        principalTable: "appointments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_walk_ins_patients_patient_id",
                        column: x => x.patient_id,
                        principalSchema: "app",
                        principalTable: "patients",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_walk_ins_users_created_by_user_id",
                        column: x => x.created_by_user_id,
                        principalSchema: "app",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_patients_first_name_trgm",
                schema: "app",
                table: "patients",
                column: "first_name")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "ix_patients_last_name_trgm",
                schema: "app",
                table: "patients",
                column: "last_name")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "ix_walk_ins_appointment_id",
                schema: "app",
                table: "walk_ins",
                column: "appointment_id");

            migrationBuilder.CreateIndex(
                name: "ix_walk_ins_created_at",
                schema: "app",
                table: "walk_ins",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_walk_ins_created_by_user_id",
                schema: "app",
                table: "walk_ins",
                column: "created_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_walk_ins_patient_id",
                schema: "app",
                table: "walk_ins",
                column: "patient_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "walk_ins",
                schema: "app");

            migrationBuilder.DropIndex(
                name: "ix_patients_first_name_trgm",
                schema: "app",
                table: "patients");

            migrationBuilder.DropIndex(
                name: "ix_patients_last_name_trgm",
                schema: "app",
                table: "patients");
        }
    }
}
