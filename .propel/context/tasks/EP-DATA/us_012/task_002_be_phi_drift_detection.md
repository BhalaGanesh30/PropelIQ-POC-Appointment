# Task - TASK_002

## Requirement Reference

- User Story: us_012
- Story Location: .propel/context/tasks/EP-DATA/us_012/us_012.md
- Acceptance Criteria:
  - AC-3: Given a migration is applied that adds a new PHI column, When the access policy is not updated, Then a validation test fails indicating the new column must be explicitly added to the policy definition.
- Edge Case:
  - What happens if the application role is accidentally granted elevated privileges? (A policy drift check runs as part of CI/CD and fails the pipeline if permissions exceed the defined baseline.)

## Design References (Frontend Tasks Only)

| Reference Type | Value |
|----------------|-------|
| **UI Impact** | No |
| **Figma URL** | N/A |
| **Wireframe Status** | N/A |
| **Wireframe Type** | N/A |
| **Wireframe Path/URL** | N/A |
| **Screen Spec** | N/A |
| **UXR Requirements** | N/A |
| **Design Tokens** | N/A |

## Applicable Technology Stack

| Layer | Technology | Version |
|-------|------------|---------|
| Frontend | N/A | N/A |
| Backend | ASP.NET Core Web API | 8.x |
| Database | PostgreSQL with pgvector | 15.x |
| Library | xUnit | latest stable |
| Library | Npgsql | latest stable |
| Library | GitHub Actions | latest stable |
| AI/ML | N/A | N/A |
| Vector Store | N/A | N/A |
| AI Gateway | N/A | N/A |
| Mobile | N/A | N/A |

**Note**: All code, and libraries, MUST be compatible with versions above.

## AI References (AI Tasks Only)

| Reference Type | Value |
|----------------|-------|
| **AI Impact** | No |
| **AIR Requirements** | N/A |
| **AI Pattern** | N/A |
| **Prompt Template Path** | N/A |
| **Guardrails Config** | N/A |
| **Model Provider** | N/A |

## Mobile References (Mobile Tasks Only)

| Reference Type | Value |
|----------------|-------|
| **Mobile Impact** | No |
| **Platform Target** | N/A |
| **Min OS Version** | N/A |
| **Mobile Framework** | N/A |

## Task Overview

Implement a PHI policy drift detection system that validates column-level permission assignments and RLS policies against a canonical baseline file. A database integration test (`PhiPolicyDriftTests`) compares the live database permission state against `phi_policy_baseline.sql`, failing when any new column is not explicitly assigned to a policy, or when any role has privileges exceeding the defined baseline. This test runs as part of the CI/CD pipeline to catch both accidental privilege escalation (edge case) and unclassified PHI columns from new migrations (AC-3). A helper SQL script queries `information_schema.column_privileges` and `pg_catalog.pg_policies` to extract the current permission state for comparison.

## Dependent Tasks

- task_001_db_phi_access_policies (requires roles, RLS policies, and baseline file to exist)
- US_009 task_001 (requires entity models and schema)
- US_006 tasks (requires CI pipeline)

## Impacted Components

- New: `server/tests/PropelIQ.Infrastructure.Tests/PhiPolicyDriftTests.cs` (integration test class)
- New: `infra/postgres/check_phi_drift.sql` (SQL script that extracts current permission state)
- Modify: `.github/workflows/ci.yml` (add PHI policy drift check step to backend job)

## Implementation Plan

1. **Create `infra/postgres/check_phi_drift.sql`** — a SQL script that extracts the current column-level permission state from PostgreSQL system catalogs. This provides the "actual" state for comparison against the baseline:

```sql
-- Extract current column-level privileges for PHI-bearing tables
-- Output: table_name | column_name | grantee | privilege_type
SELECT
    table_name,
    column_name,
    grantee,
    privilege_type
FROM information_schema.column_privileges
WHERE table_schema = 'app'
    AND grantee IN ('app_api', 'app_analytics', 'app_admin')
ORDER BY table_name, column_name, grantee;
```

```sql
-- Extract current RLS policies
-- Output: tablename | policyname | roles | cmd | qual
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
```

```sql
-- Detect unclassified columns (AC-3): columns in app schema tables
-- that have NO explicit column privilege entry for any app role
SELECT
    c.table_name,
    c.column_name
FROM information_schema.columns c
WHERE c.table_schema = 'app'
    AND c.table_name IN ('patients', 'clinical_documents', 'clinical_facts', 'insurance_profiles')
    AND NOT EXISTS (
        SELECT 1
        FROM information_schema.column_privileges cp
        WHERE cp.table_schema = c.table_schema
            AND cp.table_name = c.table_name
            AND cp.column_name = c.column_name
            AND cp.grantee IN ('app_api', 'app_analytics', 'app_admin')
    )
ORDER BY c.table_name, c.column_name;
```

2. **Create `PhiPolicyDriftTests.cs`** — an xUnit integration test class that connects to the test database and validates permission state against the baseline:

```csharp
using Npgsql;
using Xunit;

namespace PropelIQ.Infrastructure.Tests;

/// <summary>
/// Integration tests that validate PHI access policies match the canonical baseline.
/// Fails CI pipeline if permissions drift from the approved configuration.
/// </summary>
[Collection("Database")]
public class PhiPolicyDriftTests : IAsyncLifetime
{
    private NpgsqlConnection _connection = null!;
    private readonly string _connectionString;

    // Canonical baseline: columns that each role IS permitted to access
    // Any column NOT listed here for a role must be DENIED
    private static readonly Dictionary<string, HashSet<string>> AppApiPermittedColumns = new()
    {
        ["patients"] = new()
        {
            "id", "first_name", "last_name", "date_of_birth",
            "email", "phone", "mrn", "contact_preferences",
            "created_at", "updated_at", "tenant_id"
        },
        ["clinical_documents"] = new()
        {
            "id", "patient_id", "document_type",
            "created_at", "updated_at", "tenant_id"
        }
    };

    private static readonly Dictionary<string, HashSet<string>> AppAnalyticsPermittedColumns = new()
    {
        ["patients"] = new() { "id", "created_at", "updated_at", "tenant_id" },
        ["clinical_documents"] = new()
        {
            "id", "patient_id", "document_type",
            "created_at", "updated_at", "tenant_id"
        },
        ["clinical_facts"] = new()
        {
            "id", "document_id", "field_name", "confidence_score",
            "verification_state", "created_at", "tenant_id"
        }
    };

    public PhiPolicyDriftTests()
    {
        _connectionString = Environment.GetEnvironmentVariable("TEST_DB_CONNECTION")
            ?? "Host=localhost;Port=5432;Database=propeliq;Username=postgres;Password=postgres";
    }

    public async Task InitializeAsync()
    {
        _connection = new NpgsqlConnection(_connectionString);
        await _connection.OpenAsync();
    }

    public async Task DisposeAsync()
    {
        await _connection.DisposeAsync();
    }

    [Fact]
    public async Task AppApi_Should_Not_Have_Excess_Column_Privileges()
    {
        // Arrange: query actual column privileges for app_api
        var actualPrivileges = await GetColumnPrivileges("app_api");

        // Assert: no column is granted beyond the baseline
        foreach (var (table, columns) in actualPrivileges)
        {
            if (AppApiPermittedColumns.TryGetValue(table, out var permitted))
            {
                var excess = columns.Except(permitted).ToList();
                Assert.True(excess.Count == 0,
                    $"DRIFT DETECTED: app_api has excess privileges on {table}: [{string.Join(", ", excess)}]");
            }
            else
            {
                // Table not in baseline — all column privileges are excess
                Assert.Fail(
                    $"DRIFT DETECTED: app_api has privileges on unbaselined table: {table}");
            }
        }
    }

    [Fact]
    public async Task AppAnalytics_Should_Not_Access_Phi_Columns()
    {
        var actualPrivileges = await GetColumnPrivileges("app_analytics");

        foreach (var (table, columns) in actualPrivileges)
        {
            if (AppAnalyticsPermittedColumns.TryGetValue(table, out var permitted))
            {
                var excess = columns.Except(permitted).ToList();
                Assert.True(excess.Count == 0,
                    $"DRIFT DETECTED: app_analytics has PHI access on {table}: [{string.Join(", ", excess)}]");
            }
        }
    }

    [Fact]
    public async Task NewColumns_Must_Be_Explicitly_Classified()
    {
        // AC-3: any column without an explicit policy entry must fail
        const string sql = @"
            SELECT c.table_name, c.column_name
            FROM information_schema.columns c
            WHERE c.table_schema = 'app'
                AND c.table_name IN ('patients', 'clinical_documents',
                                     'clinical_facts', 'insurance_profiles')
                AND NOT EXISTS (
                    SELECT 1
                    FROM information_schema.column_privileges cp
                    WHERE cp.table_schema = c.table_schema
                        AND cp.table_name = c.table_name
                        AND cp.column_name = c.column_name
                        AND cp.grantee IN ('app_api', 'app_analytics', 'app_admin')
                )
            ORDER BY c.table_name, c.column_name;";

        await using var cmd = new NpgsqlCommand(sql, _connection);
        await using var reader = await cmd.ExecuteReaderAsync();

        var unclassifiedColumns = new List<string>();
        while (await reader.ReadAsync())
        {
            unclassifiedColumns.Add(
                $"{reader.GetString(0)}.{reader.GetString(1)}");
        }

        Assert.True(unclassifiedColumns.Count == 0,
            $"UNCLASSIFIED PHI COLUMNS: The following columns have no policy assignment " +
            $"and must be added to phi_policy_baseline.sql: [{string.Join(", ", unclassifiedColumns)}]");
    }

    [Fact]
    public async Task RlsPolicies_Must_Exist_On_TenantTables()
    {
        const string sql = @"
            SELECT tablename
            FROM pg_catalog.pg_tables
            WHERE schemaname = 'app'
                AND tablename IN ('patients', 'appointments', 'clinical_documents',
                                  'clinical_facts', 'insurance_profiles',
                                  'waitlist_entries', 'reminder_events', 'coding_decisions')
                AND tablename NOT IN (
                    SELECT DISTINCT tablename FROM pg_catalog.pg_policies
                    WHERE schemaname = 'app'
                );";

        await using var cmd = new NpgsqlCommand(sql, _connection);
        await using var reader = await cmd.ExecuteReaderAsync();

        var missingRls = new List<string>();
        while (await reader.ReadAsync())
        {
            missingRls.Add(reader.GetString(0));
        }

        Assert.True(missingRls.Count == 0,
            $"MISSING RLS: The following tables lack row-level security policies: [{string.Join(", ", missingRls)}]");
    }

    private async Task<Dictionary<string, HashSet<string>>> GetColumnPrivileges(string role)
    {
        const string sql = @"
            SELECT table_name, column_name
            FROM information_schema.column_privileges
            WHERE table_schema = 'app'
                AND grantee = @role
                AND privilege_type = 'SELECT'
            ORDER BY table_name, column_name;";

        await using var cmd = new NpgsqlCommand(sql, _connection);
        cmd.Parameters.AddWithValue("role", role);
        await using var reader = await cmd.ExecuteReaderAsync();

        var result = new Dictionary<string, HashSet<string>>();
        while (await reader.ReadAsync())
        {
            var table = reader.GetString(0);
            var column = reader.GetString(1);
            if (!result.ContainsKey(table))
                result[table] = new HashSet<string>();
            result[table].Add(column);
        }

        return result;
    }
}
```

3. **Add PHI policy drift check to CI pipeline** (`.github/workflows/ci.yml`). The drift check runs after migrations are applied to the test database:

```yaml
# Add to backend job steps (after database migration step):
- name: PHI Policy Drift Check
  run: |
    dotnet test server/tests/PropelIQ.Infrastructure.Tests \
      --filter "FullyQualifiedName~PhiPolicyDriftTests" \
      --logger "console;verbosity=detailed"
  env:
    TEST_DB_CONNECTION: "Host=localhost;Port=5432;Database=propeliq_test;Username=postgres;Password=postgres"
```

This step fails the pipeline if:
- Any role has column privileges exceeding the baseline (edge case: accidental elevation)
- Any new column is not explicitly classified in the policy (AC-3)
- Any tenant-bearing table is missing RLS policies

4. **Document the PHI column classification workflow** for developers. When adding a new migration that introduces a PHI column, the developer must:

```text
PHI Column Addition Workflow:
1. Add the column via EF Core migration
2. Classify the column as PHI or non-PHI
3. Add explicit GRANT or DENY entry to phi_policy_baseline.sql
4. Add corresponding GRANT SQL to the migration for each role
5. Run PhiPolicyDriftTests locally to verify
6. CI pipeline will enforce via the drift check step
```

5. **Parameterize the baseline for maintainability**. The `phi_policy_baseline.sql` file (created in task_001) serves as the single source of truth. The test class references the same column sets, keeping the C# dictionaries in sync with the SQL baseline. A comment block at the top of both files references the same version identifier:

```csharp
// Baseline version: must match infra/postgres/phi_policy_baseline.sql header
// When updating baseline, update BOTH this file and the SQL baseline
private const string BaselineVersion = "2025-01-01-initial";
```

## Current Project State

```text
propelIQ/
├── docker-compose.yml
├── .env.example
├── .github/
│   └── workflows/
│       └── ci.yml               (from US_006)
├── infra/
│   ├── postgres/
│   │   ├── init.sql             (from US_003, modified by task_001)
│   │   └── phi_policy_baseline.sql  (from task_001)
│   └── backup/                   (from US_011)
└── server/
    ├── src/
    │   ├── PropelIQ.Api/
    │   ├── PropelIQ.Domain/
    │   ├── PropelIQ.Infrastructure/
    │   └── PropelIQ.Application/
    └── tests/
        └── PropelIQ.Infrastructure.Tests/
```

> Placeholder: Update on execution based on task_001 and US_006 completion state.

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | server/tests/PropelIQ.Infrastructure.Tests/PhiPolicyDriftTests.cs | Integration tests validating column privileges, RLS policies, and unclassified column detection |
| CREATE | infra/postgres/check_phi_drift.sql | SQL queries for extracting current permission state from system catalogs |
| MODIFY | .github/workflows/ci.yml | Add PHI Policy Drift Check step to backend job after migration |

## External References

- PostgreSQL information_schema.column_privileges: https://www.postgresql.org/docs/15/infoschema-column-privileges.html
- PostgreSQL pg_policies catalog: https://www.postgresql.org/docs/15/view-pg-policies.html
- PostgreSQL information_schema.columns: https://www.postgresql.org/docs/15/infoschema-columns.html
- xUnit collection fixtures for database tests: https://xunit.net/docs/shared-context#collection-fixture
- GitHub Actions job steps: https://docs.github.com/en/actions/using-workflows/workflow-syntax-for-github-actions#jobsjob_idsteps

## Build Commands

```bash
# Run PHI drift tests locally
cd server
dotnet test tests/PropelIQ.Infrastructure.Tests \
  --filter "FullyQualifiedName~PhiPolicyDriftTests" \
  --logger "console;verbosity=detailed"

# Run drift SQL manually to inspect current state
docker exec propeliq-postgres psql -U postgres -d propeliq \
  -f /docker-entrypoint-initdb.d/check_phi_drift.sql

# Verify no unclassified columns
docker exec propeliq-postgres psql -U postgres -d propeliq -c "
  SELECT c.table_name, c.column_name
  FROM information_schema.columns c
  WHERE c.table_schema = 'app'
    AND c.table_name IN ('patients','clinical_documents','clinical_facts','insurance_profiles')
    AND NOT EXISTS (
      SELECT 1 FROM information_schema.column_privileges cp
      WHERE cp.table_schema = c.table_schema
        AND cp.table_name = c.table_name
        AND cp.column_name = c.column_name
        AND cp.grantee IN ('app_api','app_analytics','app_admin')
    );"
```

## Implementation Validation Strategy

- [ ] `NewColumns_Must_Be_Explicitly_Classified` test fails when a column lacks policy assignment (AC-3)
- [ ] `AppApi_Should_Not_Have_Excess_Column_Privileges` test fails when `app_api` gains unpermitted access (edge case)
- [ ] `AppAnalytics_Should_Not_Access_Phi_Columns` test fails when analytics role gains PHI column access
- [ ] `RlsPolicies_Must_Exist_On_TenantTables` test fails when a tenant table lacks RLS policies
- [ ] CI pipeline runs `PhiPolicyDriftTests` after migration step and fails on drift detection
- [ ] Adding a new migration with an unclassified column causes the test to fail with descriptive message
- [ ] All tests pass when permissions match the canonical baseline

## Implementation Checklist

- [x] Create `infra/postgres/check_phi_drift.sql` with queries for column privileges, RLS policies, and unclassified column detection
- [x] Create `PhiPolicyDriftTests.cs` with baseline dictionaries for `app_api` and `app_analytics` permitted columns
- [x] Implement `NewColumns_Must_Be_Explicitly_Classified` test that detects unassigned columns in PHI tables (AC-3)
- [x] Implement `AppApi_Should_Not_Have_Excess_Column_Privileges` test for privilege escalation detection (edge case)
- [x] Implement `RlsPolicies_Must_Exist_On_TenantTables` test for RLS coverage validation
- [x] Add PHI Policy Drift Check step to `.github/workflows/ci.yml` in the backend job
