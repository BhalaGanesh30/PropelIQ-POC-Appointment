-- PropelIQ Backup Verification Queries
-- Run against a restored database to validate recoverability.
-- Any RAISE EXCEPTION aborts the script with a non-zero exit code.

-- 1. Verify application schema exists.
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.schemata WHERE schema_name = 'app'
    ) THEN
        RAISE EXCEPTION 'Missing expected schema: app';
    END IF;
    RAISE NOTICE 'Schema check passed: app exists';
END $$;

-- 2. Verify core domain tables exist.
DO $$
DECLARE
    expected_tables TEXT[] := ARRAY[
        'users', 'patients', 'appointments',
        'audit_records', 'clinical_documents'
    ];
    tbl TEXT;
BEGIN
    FOREACH tbl IN ARRAY expected_tables LOOP
        IF NOT EXISTS (
            SELECT 1 FROM pg_catalog.pg_tables
            WHERE schemaname = 'app' AND tablename = tbl
        ) THEN
            RAISE EXCEPTION 'Missing expected table: app.%', tbl;
        END IF;
    END LOOP;
    RAISE NOTICE 'All % expected tables present', array_length(expected_tables, 1);
END $$;

-- 3. Verify referential integrity (no unvalidated foreign keys).
DO $$
DECLARE
    violation_count INTEGER;
BEGIN
    SELECT count(*) INTO violation_count
    FROM pg_catalog.pg_constraint c
    JOIN pg_catalog.pg_namespace n ON n.oid = c.connamespace
    WHERE n.nspname = 'app' AND c.contype = 'f' AND NOT c.convalidated;

    IF violation_count > 0 THEN
        RAISE EXCEPTION 'Found % unvalidated foreign key constraints', violation_count;
    END IF;
    RAISE NOTICE 'Referential integrity verified: 0 unvalidated FKs';
END $$;

-- 4. Verify seed data is present (at least one user from AppDbContextSeed).
DO $$
DECLARE
    user_count INTEGER;
BEGIN
    SELECT count(*) INTO user_count FROM app.users;
    IF user_count = 0 THEN
        RAISE EXCEPTION 'Users table is empty — seed data may be missing';
    END IF;
    RAISE NOTICE 'Seed data verification passed: % user(s)', user_count;
END $$;
