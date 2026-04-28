using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PropelIQ.Modules.SharedServices.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddWaitlistClaimTokenHash : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_reminder_events_appointment_id",
                schema: "app",
                table: "reminder_events");

            migrationBuilder.RenameColumn(
                name: "priority",
                schema: "app",
                table: "waitlist_entries",
                newName: "preferred_duration_minutes");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "cancelled_at",
                schema: "app",
                table: "waitlist_entries",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "claim_expires_at",
                schema: "app",
                table: "waitlist_entries",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "claim_token_hash",
                schema: "app",
                table: "waitlist_entries",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "claimed_at",
                schema: "app",
                table: "waitlist_entries",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "expired_at",
                schema: "app",
                table: "waitlist_entries",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "offered_slot_id",
                schema: "app",
                table: "waitlist_entries",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "position",
                schema: "app",
                table: "waitlist_entries",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "preferred_appointment_type",
                schema: "app",
                table: "waitlist_entries",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "preferred_date_end",
                schema: "app",
                table: "waitlist_entries",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "preferred_date_start",
                schema: "app",
                table: "waitlist_entries",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<string>(
                name: "idempotency_key",
                schema: "app",
                table: "reminder_events",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "scheduled_at",
                schema: "app",
                table: "reminder_events",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<double>(
                name: "risk_confidence",
                schema: "app",
                table: "appointments",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "risk_features",
                schema: "app",
                table: "appointments",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "risk_level",
                schema: "app",
                table: "appointments",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "risk_scored_at",
                schema: "app",
                table: "appointments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "dead_letter_events",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    source_reminder_id = table.Column<Guid>(type: "uuid", nullable: false),
                    appointment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    channel = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    failure_reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    total_attempts = table.Column<int>(type: "integer", nullable: false),
                    reprocessed = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_dead_letter_events", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_waitlist_entries_claim_expires_at_offered",
                schema: "app",
                table: "waitlist_entries",
                column: "claim_expires_at",
                filter: "\"Status\" = 'Offered' AND \"ClaimExpiresAt\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_waitlist_entries_status_claim_expires_at",
                schema: "app",
                table: "waitlist_entries",
                columns: new[] { "status", "claim_expires_at" });

            migrationBuilder.CreateIndex(
                name: "ix_waitlist_entries_status_position",
                schema: "app",
                table: "waitlist_entries",
                columns: new[] { "status", "position" });

            migrationBuilder.CreateIndex(
                name: "ix_reminder_events_appointment_send_status",
                schema: "app",
                table: "reminder_events",
                columns: new[] { "appointment_id", "send_status" });

            migrationBuilder.CreateIndex(
                name: "ix_reminder_events_idempotency_key",
                schema: "app",
                table: "reminder_events",
                column: "idempotency_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_reminder_events_pending_scheduled_at",
                schema: "app",
                table: "reminder_events",
                columns: new[] { "send_status", "scheduled_at" },
                filter: "\"SendStatus\" = 'Pending'");

            migrationBuilder.CreateIndex(
                name: "ix_appointments_patient_scheduled_status",
                schema: "app",
                table: "appointments",
                columns: new[] { "patient_id", "scheduled_at", "status" },
                descending: new[] { false, true, false });

            migrationBuilder.CreateIndex(
                name: "ix_appointments_risk_scored_at",
                schema: "app",
                table: "appointments",
                column: "risk_scored_at");

            migrationBuilder.CreateIndex(
                name: "ix_dead_letter_events_reprocessed",
                schema: "app",
                table: "dead_letter_events",
                column: "reprocessed",
                filter: "\"Reprocessed\" = false");

            migrationBuilder.CreateIndex(
                name: "ix_dead_letter_events_source_reminder",
                schema: "app",
                table: "dead_letter_events",
                column: "source_reminder_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "dead_letter_events",
                schema: "app");

            migrationBuilder.DropIndex(
                name: "ix_waitlist_entries_claim_expires_at_offered",
                schema: "app",
                table: "waitlist_entries");

            migrationBuilder.DropIndex(
                name: "ix_waitlist_entries_status_claim_expires_at",
                schema: "app",
                table: "waitlist_entries");

            migrationBuilder.DropIndex(
                name: "ix_waitlist_entries_status_position",
                schema: "app",
                table: "waitlist_entries");

            migrationBuilder.DropIndex(
                name: "ix_reminder_events_appointment_send_status",
                schema: "app",
                table: "reminder_events");

            migrationBuilder.DropIndex(
                name: "ix_reminder_events_idempotency_key",
                schema: "app",
                table: "reminder_events");

            migrationBuilder.DropIndex(
                name: "ix_reminder_events_pending_scheduled_at",
                schema: "app",
                table: "reminder_events");

            migrationBuilder.DropIndex(
                name: "ix_appointments_patient_scheduled_status",
                schema: "app",
                table: "appointments");

            migrationBuilder.DropIndex(
                name: "ix_appointments_risk_scored_at",
                schema: "app",
                table: "appointments");

            migrationBuilder.DropColumn(
                name: "cancelled_at",
                schema: "app",
                table: "waitlist_entries");

            migrationBuilder.DropColumn(
                name: "claim_expires_at",
                schema: "app",
                table: "waitlist_entries");

            migrationBuilder.DropColumn(
                name: "claim_token_hash",
                schema: "app",
                table: "waitlist_entries");

            migrationBuilder.DropColumn(
                name: "claimed_at",
                schema: "app",
                table: "waitlist_entries");

            migrationBuilder.DropColumn(
                name: "expired_at",
                schema: "app",
                table: "waitlist_entries");

            migrationBuilder.DropColumn(
                name: "offered_slot_id",
                schema: "app",
                table: "waitlist_entries");

            migrationBuilder.DropColumn(
                name: "position",
                schema: "app",
                table: "waitlist_entries");

            migrationBuilder.DropColumn(
                name: "preferred_appointment_type",
                schema: "app",
                table: "waitlist_entries");

            migrationBuilder.DropColumn(
                name: "preferred_date_end",
                schema: "app",
                table: "waitlist_entries");

            migrationBuilder.DropColumn(
                name: "preferred_date_start",
                schema: "app",
                table: "waitlist_entries");

            migrationBuilder.DropColumn(
                name: "idempotency_key",
                schema: "app",
                table: "reminder_events");

            migrationBuilder.DropColumn(
                name: "scheduled_at",
                schema: "app",
                table: "reminder_events");

            migrationBuilder.DropColumn(
                name: "risk_confidence",
                schema: "app",
                table: "appointments");

            migrationBuilder.DropColumn(
                name: "risk_features",
                schema: "app",
                table: "appointments");

            migrationBuilder.DropColumn(
                name: "risk_level",
                schema: "app",
                table: "appointments");

            migrationBuilder.DropColumn(
                name: "risk_scored_at",
                schema: "app",
                table: "appointments");

            migrationBuilder.RenameColumn(
                name: "preferred_duration_minutes",
                schema: "app",
                table: "waitlist_entries",
                newName: "priority");

            migrationBuilder.CreateIndex(
                name: "ix_reminder_events_appointment_id",
                schema: "app",
                table: "reminder_events",
                column: "appointment_id");
        }
    }
}
