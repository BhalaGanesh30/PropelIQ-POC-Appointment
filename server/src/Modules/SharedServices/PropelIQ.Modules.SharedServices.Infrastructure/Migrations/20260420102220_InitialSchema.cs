using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PropelIQ.Modules.SharedServices.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Rename/rekey embedding_samples only if the table was created in InitialCreate.
            // When pgvector is absent the table is skipped, so we guard every operation.
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1 FROM pg_catalog.pg_class c
                        JOIN pg_catalog.pg_namespace n ON n.oid = c.relnamespace
                        WHERE n.nspname = 'app' AND c.relname = 'embedding_samples'
                    ) THEN
                        ALTER TABLE app.embedding_samples DROP CONSTRAINT IF EXISTS "PK_embedding_samples";

                        ALTER TABLE app.embedding_samples RENAME COLUMN "Embedding"  TO embedding;
                        ALTER TABLE app.embedding_samples RENAME COLUMN "Id"         TO id;
                        ALTER TABLE app.embedding_samples RENAME COLUMN "CreatedAt"  TO created_at;
                        ALTER TABLE app.embedding_samples RENAME COLUMN "ContentRef" TO content_ref;

                        ALTER INDEX IF EXISTS app."IX_embedding_samples_Embedding"
                            RENAME TO ix_embedding_samples_embedding;

                        ALTER TABLE app.embedding_samples
                            ADD CONSTRAINT pk_embedding_samples PRIMARY KEY (id);
                    END IF;
                END $$;
                """);

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:pg_trgm", ",,")
                .Annotation("Npgsql:PostgresExtension:uuid-ossp", ",,")
                .OldAnnotation("Npgsql:PostgresExtension:pg_trgm", ",,")
                .OldAnnotation("Npgsql:PostgresExtension:uuid-ossp", ",,");

            migrationBuilder.CreateTable(
                name: "audit_records",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    event_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    actor_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    target_entity_id = table.Column<Guid>(type: "uuid", nullable: true),
                    target_entity_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    details = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_audit_records", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "users",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    password_hash = table.Column<string>(type: "text", nullable: false),
                    role = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    first_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    last_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    last_login_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_users", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "patients",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    first_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    last_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    date_of_birth = table.Column<DateOnly>(type: "date", nullable: false),
                    mrn = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    contact_preferences = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_patients", x => x.id);
                    table.ForeignKey(
                        name: "fk_patients_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "app",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "appointments",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    patient_id = table.Column<Guid>(type: "uuid", nullable: false),
                    staff_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    scheduled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    duration_minutes = table.Column<int>(type: "integer", nullable: false),
                    appointment_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    queue_state = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_appointments", x => x.id);
                    table.ForeignKey(
                        name: "fk_appointments_patients_patient_id",
                        column: x => x.patient_id,
                        principalSchema: "app",
                        principalTable: "patients",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_appointments_users_staff_user_id",
                        column: x => x.staff_user_id,
                        principalSchema: "app",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "clinical_documents",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    patient_id = table.Column<Guid>(type: "uuid", nullable: false),
                    file_name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    category = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    extraction_status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    storage_path = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_clinical_documents", x => x.id);
                    table.ForeignKey(
                        name: "fk_clinical_documents_patients_patient_id",
                        column: x => x.patient_id,
                        principalSchema: "app",
                        principalTable: "patients",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "insurance_profiles",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    patient_id = table.Column<Guid>(type: "uuid", nullable: false),
                    payer_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    member_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    is_primary = table.Column<bool>(type: "boolean", nullable: false),
                    verification_status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_insurance_profiles", x => x.id);
                    table.ForeignKey(
                        name: "fk_insurance_profiles_patients_patient_id",
                        column: x => x.patient_id,
                        principalSchema: "app",
                        principalTable: "patients",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "reminder_events",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    appointment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    channel = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    send_status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    confirmation_response = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    retry_count = table.Column<int>(type: "integer", nullable: false),
                    sent_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_reminder_events", x => x.id);
                    table.ForeignKey(
                        name: "fk_reminder_events_appointments_appointment_id",
                        column: x => x.appointment_id,
                        principalSchema: "app",
                        principalTable: "appointments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "waitlist_entries",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    patient_id = table.Column<Guid>(type: "uuid", nullable: false),
                    appointment_id = table.Column<Guid>(type: "uuid", nullable: true),
                    priority = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    offered_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_waitlist_entries", x => x.id);
                    table.ForeignKey(
                        name: "fk_waitlist_entries_appointments_appointment_id",
                        column: x => x.appointment_id,
                        principalSchema: "app",
                        principalTable: "appointments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_waitlist_entries_patients_patient_id",
                        column: x => x.patient_id,
                        principalSchema: "app",
                        principalTable: "patients",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "clinical_facts",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fact_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    value = table.Column<string>(type: "text", nullable: false),
                    confidence_score = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: false),
                    verification_state = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    last_reviewed_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    last_reviewed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_clinical_facts", x => x.id);
                    table.ForeignKey(
                        name: "fk_clinical_facts_clinical_documents_document_id",
                        column: x => x.document_id,
                        principalSchema: "app",
                        principalTable: "clinical_documents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "coding_decisions",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    patient_id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    suggested_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    rationale = table.Column<string>(type: "text", nullable: true),
                    confidence_score = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: false),
                    reviewer_action = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    finalized_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    reviewed_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_coding_decisions", x => x.id);
                    table.ForeignKey(
                        name: "fk_coding_decisions_clinical_documents_document_id",
                        column: x => x.document_id,
                        principalSchema: "app",
                        principalTable: "clinical_documents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_coding_decisions_patients_patient_id",
                        column: x => x.patient_id,
                        principalSchema: "app",
                        principalTable: "patients",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_appointments_patient_id",
                schema: "app",
                table: "appointments",
                column: "patient_id");

            migrationBuilder.CreateIndex(
                name: "ix_appointments_staff_user_id",
                schema: "app",
                table: "appointments",
                column: "staff_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_audit_records_actor_user_id",
                schema: "app",
                table: "audit_records",
                column: "actor_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_audit_records_occurred_at",
                schema: "app",
                table: "audit_records",
                column: "occurred_at");

            migrationBuilder.CreateIndex(
                name: "ix_clinical_documents_patient_id",
                schema: "app",
                table: "clinical_documents",
                column: "patient_id");

            migrationBuilder.CreateIndex(
                name: "ix_clinical_facts_document_id",
                schema: "app",
                table: "clinical_facts",
                column: "document_id");

            migrationBuilder.CreateIndex(
                name: "ix_coding_decisions_document_id",
                schema: "app",
                table: "coding_decisions",
                column: "document_id");

            migrationBuilder.CreateIndex(
                name: "ix_coding_decisions_patient_id",
                schema: "app",
                table: "coding_decisions",
                column: "patient_id");

            migrationBuilder.CreateIndex(
                name: "ix_insurance_profiles_patient_id",
                schema: "app",
                table: "insurance_profiles",
                column: "patient_id");

            migrationBuilder.CreateIndex(
                name: "ix_patients_mrn",
                schema: "app",
                table: "patients",
                column: "mrn",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_patients_user_id",
                schema: "app",
                table: "patients",
                column: "user_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_reminder_events_appointment_id",
                schema: "app",
                table: "reminder_events",
                column: "appointment_id");

            migrationBuilder.CreateIndex(
                name: "ix_users_email",
                schema: "app",
                table: "users",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_waitlist_entries_appointment_id",
                schema: "app",
                table: "waitlist_entries",
                column: "appointment_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_waitlist_entries_patient_id",
                schema: "app",
                table: "waitlist_entries",
                column: "patient_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "audit_records",
                schema: "app");

            migrationBuilder.DropTable(
                name: "clinical_facts",
                schema: "app");

            migrationBuilder.DropTable(
                name: "coding_decisions",
                schema: "app");

            migrationBuilder.DropTable(
                name: "insurance_profiles",
                schema: "app");

            migrationBuilder.DropTable(
                name: "reminder_events",
                schema: "app");

            migrationBuilder.DropTable(
                name: "waitlist_entries",
                schema: "app");

            migrationBuilder.DropTable(
                name: "clinical_documents",
                schema: "app");

            migrationBuilder.DropTable(
                name: "appointments",
                schema: "app");

            migrationBuilder.DropTable(
                name: "patients",
                schema: "app");

            migrationBuilder.DropTable(
                name: "users",
                schema: "app");

            migrationBuilder.DropPrimaryKey(
                name: "pk_embedding_samples",
                schema: "app",
                table: "embedding_samples");

            migrationBuilder.RenameColumn(
                name: "embedding",
                schema: "app",
                table: "embedding_samples",
                newName: "Embedding");

            migrationBuilder.RenameColumn(
                name: "id",
                schema: "app",
                table: "embedding_samples",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "created_at",
                schema: "app",
                table: "embedding_samples",
                newName: "CreatedAt");

            migrationBuilder.RenameColumn(
                name: "content_ref",
                schema: "app",
                table: "embedding_samples",
                newName: "ContentRef");

            migrationBuilder.RenameIndex(
                name: "ix_embedding_samples_embedding",
                schema: "app",
                table: "embedding_samples",
                newName: "IX_embedding_samples_Embedding");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:pg_trgm", ",,")
                .Annotation("Npgsql:PostgresExtension:uuid-ossp", ",,")
                .Annotation("Npgsql:PostgresExtension:vector", ",,")
                .OldAnnotation("Npgsql:PostgresExtension:hstore", ",,")
                .OldAnnotation("Npgsql:PostgresExtension:pg_trgm", ",,")
                .OldAnnotation("Npgsql:PostgresExtension:uuid-ossp", ",,")
                .OldAnnotation("Npgsql:PostgresExtension:vector", ",,");

            migrationBuilder.AddPrimaryKey(
                name: "PK_embedding_samples",
                schema: "app",
                table: "embedding_samples",
                column: "Id");
        }
    }
}
