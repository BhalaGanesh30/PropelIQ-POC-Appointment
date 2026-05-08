using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PropelIQ.Modules.SharedServices.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddConflictDetectionTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "conflict_rules",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    rule_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    drug_a_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    drug_b_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    severity = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    source = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false, defaultValue: "system"),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    last_updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_conflict_rules", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "conflict_alerts",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    patient_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fact_id_a = table.Column<Guid>(type: "uuid", nullable: false),
                    fact_id_b = table.Column<Guid>(type: "uuid", nullable: true),
                    rule_id = table.Column<Guid>(type: "uuid", nullable: false),
                    conflict_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    severity = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    drug_a = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    drug_b = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    acknowledged = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    acknowledged_by = table.Column<Guid>(type: "uuid", nullable: true),
                    acknowledged_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_conflict_alerts", x => x.id);
                    table.ForeignKey(
                        name: "fk_conflict_alerts_clinical_facts_fact_id_a",
                        column: x => x.fact_id_a,
                        principalSchema: "app",
                        principalTable: "clinical_facts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_conflict_alerts_clinical_facts_fact_id_b",
                        column: x => x.fact_id_b,
                        principalSchema: "app",
                        principalTable: "clinical_facts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_conflict_alerts_conflict_rules_rule_id",
                        column: x => x.rule_id,
                        principalSchema: "app",
                        principalTable: "conflict_rules",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_conflict_alerts_patients_patient_id",
                        column: x => x.patient_id,
                        principalSchema: "app",
                        principalTable: "patients",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_conflict_alerts_users_acknowledged_by",
                        column: x => x.acknowledged_by,
                        principalSchema: "app",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "ix_conflict_alerts_acknowledged_by",
                schema: "app",
                table: "conflict_alerts",
                column: "acknowledged_by");

            migrationBuilder.CreateIndex(
                name: "ix_conflict_alerts_fact_id_a",
                schema: "app",
                table: "conflict_alerts",
                column: "fact_id_a");

            migrationBuilder.CreateIndex(
                name: "ix_conflict_alerts_fact_id_b",
                schema: "app",
                table: "conflict_alerts",
                column: "fact_id_b");

            migrationBuilder.CreateIndex(
                name: "ix_conflict_alerts_patient_id",
                schema: "app",
                table: "conflict_alerts",
                column: "patient_id");

            migrationBuilder.CreateIndex(
                name: "ix_conflict_alerts_rule_id",
                schema: "app",
                table: "conflict_alerts",
                column: "rule_id");

            migrationBuilder.CreateIndex(
                name: "ix_conflict_alerts_unacknowledged",
                schema: "app",
                table: "conflict_alerts",
                columns: new[] { "patient_id", "severity" },
                filter: "acknowledged = false");

            migrationBuilder.CreateIndex(
                name: "uq_conflict_alerts_pair",
                schema: "app",
                table: "conflict_alerts",
                columns: new[] { "patient_id", "fact_id_a", "fact_id_b" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_conflict_rules_type_drugs",
                schema: "app",
                table: "conflict_rules",
                columns: new[] { "rule_type", "drug_a_name", "drug_b_name" },
                filter: "is_active = true");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "conflict_alerts",
                schema: "app");

            migrationBuilder.DropTable(
                name: "conflict_rules",
                schema: "app");
        }
    }
}
