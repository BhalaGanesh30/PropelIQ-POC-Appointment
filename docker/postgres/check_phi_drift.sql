-- PropelIQ PHI Drift Detection Queries
-- Used by PhiPolicyDriftTests and manual inspection to compare live DB state
-- against the canonical baseline (phi_policy_baseline.sql).

-- ╔═══════════════════════════════════════════════════════════════════════════╗
-- ║ 1. Current column-level privileges for PHI-bearing tables               ║
-- ╚═══════════════════════════════════════════════════════════════════════════╝
SELECT
    table_name,
    column_name,
    grantee,
    privilege_type
FROM information_schema.column_privileges
WHERE table_schema = 'app'
    AND grantee IN ('app_api', 'app_analytics', 'app_admin')
ORDER BY table_name, column_name, grantee;

-- ╔═══════════════════════════════════════════════════════════════════════════╗
-- ║ 2. Current RLS policies                                                 ║
-- ╚═══════════════════════════════════════════════════════════════════════════╝
SELECT
    schemaname,
    tablename,
    policyname,
    roles,
    cmd,
    qual
FROM pg_catalog.pg_policies
WHERE schemaname = 'app'
ORDER BY tablename, policyname;

-- ╔═══════════════════════════════════════════════════════════════════════════╗
-- ║ 3. Unclassified columns (AC-3): columns with NO explicit privilege      ║
-- ║    entry for any application role                                       ║
-- ╚═══════════════════════════════════════════════════════════════════════════╝
SELECT
    c.table_name,
    c.column_name
FROM information_schema.columns c
WHERE c.table_schema = 'app'
    AND c.table_name IN (
        'patients', 'users', 'clinical_documents', 'clinical_facts',
        'insurance_profiles', 'coding_decisions', 'appointments',
        'waitlist_entries', 'reminder_events'
    )
    AND NOT EXISTS (
        SELECT 1
        FROM information_schema.column_privileges cp
        WHERE cp.table_schema = c.table_schema
            AND cp.table_name = c.table_name
            AND cp.column_name = c.column_name
            AND cp.grantee IN ('app_api', 'app_analytics', 'app_admin')
    )
ORDER BY c.table_name, c.column_name;
