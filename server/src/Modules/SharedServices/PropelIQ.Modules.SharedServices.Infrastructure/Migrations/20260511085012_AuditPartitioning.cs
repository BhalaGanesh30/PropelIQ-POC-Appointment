using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PropelIQ.Modules.SharedServices.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AuditPartitioning : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── EF Core: Create audit_dead_letters ────────────────────────────────
            // Table for AuditRecordWriterWorker failed-write dead-letter store (US_056, AC-2).
            migrationBuilder.CreateTable(
                name: "audit_dead_letters",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    payload = table.Column<string>(type: "jsonb", nullable: false),
                    error_message = table.Column<string>(type: "varchar(2000)", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    retry_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    last_retry_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    resolved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_audit_dead_letters", x => x.id);
                });

            // ── EF Core: Create audit_mutation_attempts ───────────────────────────
            // Application-layer log of rejected audit mutation attempts (US_056, AC-2).
            migrationBuilder.CreateTable(
                name: "audit_mutation_attempts",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    attempted_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    operation = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    target_audit_id = table.Column<Guid>(type: "uuid", nullable: true),
                    error_message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    source_ip = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_audit_mutation_attempts", x => x.id);
                });

            // ── EF Core: Indexes on new tables ────────────────────────────────────
            migrationBuilder.CreateIndex(
                name: "ix_audit_dead_letters_unresolved",
                schema: "app",
                table: "audit_dead_letters",
                column: "created_at",
                filter: "resolved_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_audit_mutation_attempts_occurred_at",
                schema: "app",
                table: "audit_mutation_attempts",
                column: "occurred_at",
                descending: new bool[0]);

            // ─────────────────────────────────────────────────────────────────────
            // STEP 1: Convert app.audit_records to a range-partitioned table
            //         by occurred_at (US_056 task_002, AC-3, DR-005).
            //
            // PostgreSQL does not support in-place conversion of regular tables to
            // partitioned tables; the migration renames the old table, creates a
            // new partitioned parent with composite PK (id, occurred_at), copies
            // all data, and drops the legacy table.
            // ─────────────────────────────────────────────────────────────────────

            // 1a. Drop the immutability trigger applied to the non-partitioned table.
            //     It must be re-applied per child partition after conversion.
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS trg_audit_records_immutable ON app.audit_records;
                """);

            // 1b. Drop all existing indexes so they can be recreated on the new table.
            migrationBuilder.Sql("""
                DROP INDEX IF EXISTS app.ix_audit_records_actor_user_id;
                DROP INDEX IF EXISTS app.ix_audit_records_occurred_at;
                DROP INDEX IF EXISTS app.ix_audit_records_event_type;
                DROP INDEX IF EXISTS app.ix_audit_records_event_type_occurred_at;
                """);

            // 1c–1h: Idempotent table-swap to partitioned parent.
            //        Only executes if audit_records is not yet a partitioned table.
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    -- Skip if already partitioned (relkind='p' means partitioned table)
                    IF (SELECT relkind FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace
                        WHERE n.nspname = 'app' AND c.relname = 'audit_records') <> 'p' THEN

                        -- Rename the old PK constraint so it doesn't conflict with the new table's PK.
                        ALTER TABLE app.audit_records RENAME CONSTRAINT pk_audit_records TO pk_audit_records_legacy;

                        -- 1c. Rename the current regular table to preserve data during conversion.
                        ALTER TABLE app.audit_records RENAME TO audit_records_legacy;

                        -- 1d. Create the new range-partitioned parent table.
                        CREATE TABLE app.audit_records (
                            id                       UUID         NOT NULL DEFAULT gen_random_uuid(),
                            event_type               VARCHAR(50)  NOT NULL,
                            actor_user_id            UUID         NOT NULL,
                            target_entity_id         UUID,
                            target_entity_type       VARCHAR(100) NOT NULL,
                            occurred_at              TIMESTAMPTZ  NOT NULL DEFAULT now(),
                            details                  JSONB        NOT NULL DEFAULT '{}',
                            override_constraint_type VARCHAR(50),
                            override_reason          VARCHAR(500),
                            override_action          VARCHAR(20),
                            CONSTRAINT pk_audit_records PRIMARY KEY (id, occurred_at)
                        ) PARTITION BY RANGE (occurred_at);

                        -- 1e. Create yearly child partitions (2026-2028).
                        CREATE TABLE app.audit_records_y2026
                            PARTITION OF app.audit_records
                            FOR VALUES FROM ('2026-01-01') TO ('2027-01-01');
                        CREATE TABLE app.audit_records_y2027
                            PARTITION OF app.audit_records
                            FOR VALUES FROM ('2027-01-01') TO ('2028-01-01');
                        CREATE TABLE app.audit_records_y2028
                            PARTITION OF app.audit_records
                            FOR VALUES FROM ('2028-01-01') TO ('2029-01-01');

                        -- 1f. Default partition catches records outside defined ranges.
                        CREATE TABLE app.audit_records_default
                            PARTITION OF app.audit_records DEFAULT;

                        -- 1g. Copy data from the legacy table into the new partitioned table.
                        INSERT INTO app.audit_records (
                            id, event_type, actor_user_id, target_entity_id, target_entity_type,
                            occurred_at, details, override_constraint_type, override_reason, override_action
                        )
                        SELECT
                            id, event_type, actor_user_id, target_entity_id, target_entity_type,
                            occurred_at, details, override_constraint_type, override_reason, override_action
                        FROM app.audit_records_legacy;

                        -- 1h. Drop the legacy table now that data is safely copied.
                        DROP TABLE app.audit_records_legacy;
                    END IF;
                END $$;
                """);

            // ─────────────────────────────────────────────────────────────────────
            // STEP 2: Re-apply immutability triggers on each child partition.
            //         PostgreSQL triggers must be defined per-partition for partitioned tables
            //         (triggers on the parent are not inherited by children).
            // ─────────────────────────────────────────────────────────────────────
            migrationBuilder.Sql("""
                CREATE TRIGGER trg_audit_records_immutable
                    BEFORE UPDATE OR DELETE ON app.audit_records_y2026
                    FOR EACH ROW EXECUTE FUNCTION app.fn_prevent_audit_mutation();

                CREATE TRIGGER trg_audit_records_immutable
                    BEFORE UPDATE OR DELETE ON app.audit_records_y2027
                    FOR EACH ROW EXECUTE FUNCTION app.fn_prevent_audit_mutation();

                CREATE TRIGGER trg_audit_records_immutable
                    BEFORE UPDATE OR DELETE ON app.audit_records_y2028
                    FOR EACH ROW EXECUTE FUNCTION app.fn_prevent_audit_mutation();

                CREATE TRIGGER trg_audit_records_immutable
                    BEFORE UPDATE OR DELETE ON app.audit_records_default
                    FOR EACH ROW EXECUTE FUNCTION app.fn_prevent_audit_mutation();
                """);

            // ─────────────────────────────────────────────────────────────────────
            // STEP 3: Re-apply GRANT restrictions on parent + all child partitions.
            //         INSERT + SELECT only for app_user (AC-2, NFR-010).
            // ─────────────────────────────────────────────────────────────────────
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'app_user') THEN
                        REVOKE ALL ON app.audit_records          FROM app_user;
                        GRANT  INSERT, SELECT ON app.audit_records          TO app_user;
                        REVOKE ALL ON app.audit_records_y2026    FROM app_user;
                        GRANT  INSERT, SELECT ON app.audit_records_y2026    TO app_user;
                        REVOKE ALL ON app.audit_records_y2027    FROM app_user;
                        GRANT  INSERT, SELECT ON app.audit_records_y2027    TO app_user;
                        REVOKE ALL ON app.audit_records_y2028    FROM app_user;
                        GRANT  INSERT, SELECT ON app.audit_records_y2028    TO app_user;
                        REVOKE ALL ON app.audit_records_default  FROM app_user;
                        GRANT  INSERT, SELECT ON app.audit_records_default  TO app_user;
                    END IF;
                END $$;
                """);

            // ─────────────────────────────────────────────────────────────────────
            // STEP 4: Recreate indexes on the partitioned parent table.
            //         PostgreSQL automatically propagates parent-table indexes to
            //         all existing child partitions and future partitions.
            // ─────────────────────────────────────────────────────────────────────
            migrationBuilder.Sql("""
                -- Existing indexes recreated after conversion
                CREATE INDEX ix_audit_records_actor_user_id
                    ON app.audit_records (actor_user_id);

                CREATE INDEX ix_audit_records_occurred_at
                    ON app.audit_records (occurred_at);

                CREATE INDEX ix_audit_records_event_type
                    ON app.audit_records (event_type);

                CREATE INDEX ix_audit_records_event_type_occurred_at
                    ON app.audit_records (event_type, occurred_at DESC);

                -- AC-4 composite indexes for the 3-second admin query target
                CREATE INDEX ix_audit_records_actor_occurred
                    ON app.audit_records (actor_user_id, occurred_at DESC);

                CREATE INDEX ix_audit_records_entity_occurred
                    ON app.audit_records (target_entity_id, occurred_at DESC)
                    WHERE target_entity_id IS NOT NULL;
                """);

            // ─────────────────────────────────────────────────────────────────────
            // STEP 5: Create audit_records_archive cold storage table (AC-3, DR-005).
            //         Stores records older than 7 years moved by RetentionPolicyWorker.
            //         Read-only for app_user; immutability trigger applied.
            // ─────────────────────────────────────────────────────────────────────
            migrationBuilder.Sql("""
                CREATE TABLE app.audit_records_archive (
                    id                       UUID         NOT NULL,
                    event_type               VARCHAR(50)  NOT NULL,
                    actor_user_id            UUID         NOT NULL,
                    target_entity_id         UUID,
                    target_entity_type       VARCHAR(100) NOT NULL,
                    occurred_at              TIMESTAMPTZ  NOT NULL,
                    details                  JSONB        NOT NULL DEFAULT '{}',
                    override_constraint_type VARCHAR(50),
                    override_reason          VARCHAR(500),
                    override_action          VARCHAR(20),
                    archived_at              TIMESTAMPTZ  NOT NULL DEFAULT now(),
                    CONSTRAINT pk_audit_records_archive PRIMARY KEY (id)
                );

                CREATE TRIGGER trg_audit_archive_immutable
                    BEFORE UPDATE OR DELETE ON app.audit_records_archive
                    FOR EACH ROW EXECUTE FUNCTION app.fn_prevent_audit_mutation();

                CREATE INDEX ix_audit_records_archive_occurred_at
                    ON app.audit_records_archive (occurred_at DESC);
                """);

            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'app_user') THEN
                        REVOKE ALL ON app.audit_records_archive FROM app_user;
                        GRANT  SELECT ON app.audit_records_archive TO app_user;
                    END IF;
                END $$;
                """);

            // ─────────────────────────────────────────────────────────────────────
            // STEP 6: Create unified compliance view (AC-3, Edge Case 1).
            //         Default query path uses audit_records (fast, partition-pruned).
            //         include_archived=true switches to this view for full history.
            // ─────────────────────────────────────────────────────────────────────
            migrationBuilder.Sql("""
                CREATE OR REPLACE VIEW app.audit_records_full AS
                SELECT id, event_type, actor_user_id, target_entity_id, target_entity_type,
                       occurred_at, details, override_constraint_type, override_reason, override_action,
                       NULL::TIMESTAMPTZ AS archived_at,
                       FALSE            AS is_archived
                FROM   app.audit_records
                UNION ALL
                SELECT id, event_type, actor_user_id, target_entity_id, target_entity_type,
                       occurred_at, details, override_constraint_type, override_reason, override_action,
                       archived_at,
                       TRUE AS is_archived
                FROM   app.audit_records_archive;
                """);

            // ─────────────────────────────────────────────────────────────────────
            // STEP 7: Lock down app_user on new audit tables (complements task_001).
            // ─────────────────────────────────────────────────────────────────────
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'app_user') THEN
                        -- Dead-letter: app writes but should never delete
                        REVOKE DELETE ON app.audit_dead_letters FROM app_user;
                        -- Mutation attempts: append-only log, no deletes
                        REVOKE DELETE ON app.audit_mutation_attempts FROM app_user;
                    END IF;
                END $$;
                """);

            migrationBuilder.Sql("""
                COMMENT ON TABLE app.audit_dead_letters IS
                    'Stores serialized AuditEvent payloads that failed to write to audit_records. '
                    'Retried by DeadLetterRetryWorker every 5 minutes up to 5 attempts. '
                    'Never deleted; resolved_at marks successful replay. Ref: US_056, AC-2, NFR-010.';

                COMMENT ON TABLE app.audit_mutation_attempts IS
                    'Application-layer record of rejected UPDATE/DELETE attempts against audit_records. '
                    'Complements pgaudit server logs. Append-only. Ref: US_056, AC-2, NFR-010.';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // ── Tear down partitioning (US_056 task_002) ─────────────────────────
            migrationBuilder.Sql("DROP VIEW IF EXISTS app.audit_records_full;");
            migrationBuilder.Sql("DROP TABLE IF EXISTS app.audit_records_archive;");

            // Drop the partitioned table and all its child partitions.
            migrationBuilder.Sql("""
                DROP TABLE IF EXISTS app.audit_records CASCADE;
                """);

            // Recreate the original non-partitioned audit_records table (empty — data lost).
            migrationBuilder.Sql("""
                CREATE TABLE app.audit_records (
                    id                       UUID         NOT NULL DEFAULT gen_random_uuid(),
                    event_type               VARCHAR(50)  NOT NULL,
                    actor_user_id            UUID         NOT NULL,
                    target_entity_id         UUID,
                    target_entity_type       VARCHAR(100) NOT NULL,
                    occurred_at              TIMESTAMPTZ  NOT NULL DEFAULT now(),
                    details                  JSONB        NOT NULL DEFAULT '{}',
                    override_constraint_type VARCHAR(50),
                    override_reason          VARCHAR(500),
                    override_action          VARCHAR(20),
                    CONSTRAINT pk_audit_records PRIMARY KEY (id)
                );

                CREATE TRIGGER trg_audit_records_immutable
                    BEFORE UPDATE OR DELETE ON app.audit_records
                    FOR EACH ROW EXECUTE FUNCTION app.fn_prevent_audit_mutation();

                CREATE INDEX ix_audit_records_actor_user_id ON app.audit_records (actor_user_id);
                CREATE INDEX ix_audit_records_occurred_at   ON app.audit_records (occurred_at);
                CREATE INDEX ix_audit_records_event_type    ON app.audit_records (event_type);
                CREATE INDEX ix_audit_records_event_type_occurred_at
                    ON app.audit_records (event_type, occurred_at DESC);
                """);

            // ── EF Core: Drop new tables ─────────────────────────────────────────
            migrationBuilder.DropTable(
                name: "audit_dead_letters",
                schema: "app");

            migrationBuilder.DropTable(
                name: "audit_mutation_attempts",
                schema: "app");
        }
    }
}

