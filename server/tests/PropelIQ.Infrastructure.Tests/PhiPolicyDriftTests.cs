using Npgsql;
using Xunit;

namespace PropelIQ.Infrastructure.Tests;

/// <summary>
/// Integration tests that validate PHI access policies match the canonical baseline
/// defined in <c>docker/postgres/phi_policy_baseline.sql</c>.
/// Fails CI pipeline if permissions drift from the approved configuration.
/// </summary>
[Collection("Database")]
public sealed class PhiPolicyDriftTests : IAsyncLifetime
{
    // Baseline version: must match docker/postgres/phi_policy_baseline.sql header.
    // When updating baseline, update BOTH this file and the SQL baseline.
    private const string BaselineVersion = "20260420-initial";

    private NpgsqlConnection _connection = null!;
    private readonly string _connectionString;

    // ── Canonical baselines derived from AddPhiAccessPolicies migration ──────

    // Tables where column-level SELECT GRANTs are explicitly scoped.
    // Maps table → set of columns the role is permitted to SELECT.
    private static readonly Dictionary<string, HashSet<string>> AppApiPermittedColumns = new()
    {
        ["patients"] = new()
        {
            "id", "user_id", "first_name", "last_name", "date_of_birth",
            "mrn", "contact_preferences", "tenant_id", "created_at", "updated_at"
        },
        ["users"] = new()
        {
            "id", "email", "role", "first_name", "last_name", "is_active",
            "last_login_at", "tenant_id", "created_at", "updated_at"
        },
        ["clinical_documents"] = new()
        {
            "id", "patient_id", "file_name", "category", "extraction_status",
            "tenant_id", "created_at", "updated_at"
        },
        // Tables with full SELECT granted (all columns permitted):
        // insurance_profiles, clinical_facts, coding_decisions,
        // appointments, waitlist_entries, reminder_events, audit_records
    };

    private static readonly Dictionary<string, HashSet<string>> AppAnalyticsPermittedColumns = new()
    {
        ["patients"] = new() { "id", "tenant_id", "created_at", "updated_at" },
        ["users"] = new() { "id", "role", "is_active", "tenant_id", "created_at", "updated_at" },
        ["clinical_documents"] = new()
        {
            "id", "patient_id", "category", "extraction_status",
            "tenant_id", "created_at", "updated_at"
        },
        ["clinical_facts"] = new()
        {
            "id", "document_id", "fact_type", "confidence_score",
            "verification_state", "tenant_id", "created_at", "updated_at"
        },
        ["coding_decisions"] = new()
        {
            "id", "patient_id", "document_id", "code_type", "confidence_score",
            "reviewer_action", "tenant_id", "created_at", "updated_at"
        },
        ["appointments"] = new()
        {
            "id", "patient_id", "staff_user_id", "scheduled_at", "duration_minutes",
            "appointment_type", "status", "queue_state", "tenant_id", "created_at", "updated_at"
        },
        ["insurance_profiles"] = new()
        {
            "id", "patient_id", "is_primary", "verification_status",
            "tenant_id", "created_at", "updated_at"
        },
    };

    // All tables that must have RLS policies.
    private static readonly HashSet<string> RlsRequiredTables = new()
    {
        "patients", "users", "appointments", "clinical_documents",
        "clinical_facts", "insurance_profiles", "waitlist_entries",
        "reminder_events", "coding_decisions"
    };

    // All tables with PHI columns that must have every column classified.
    private static readonly HashSet<string> PhiClassifiedTables = new()
    {
        "patients", "users", "clinical_documents", "clinical_facts",
        "insurance_profiles", "coding_decisions", "appointments",
        "waitlist_entries", "reminder_events"
    };

    public PhiPolicyDriftTests()
    {
        _connectionString = Environment.GetEnvironmentVariable("TEST_DB_CONNECTION")
            ?? "Host=localhost;Port=5432;Database=propeliq;Username=postgres;Password=propeliq_dev_pass;Search Path=app";
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

    /// <summary>
    /// Detects privilege escalation: app_api must not have SELECT on columns
    /// beyond the approved baseline for column-restricted tables.
    /// </summary>
    [Fact]
    public async Task AppApi_Should_Not_Have_Excess_Column_Privileges()
    {
        var actualPrivileges = await GetColumnPrivileges("app_api");

        foreach (var (table, columns) in actualPrivileges)
        {
            if (!AppApiPermittedColumns.TryGetValue(table, out var permitted))
                continue; // Table has full SELECT grant; no column-level restriction.

            var excess = columns.Except(permitted).ToList();
            Assert.True(excess.Count == 0,
                $"DRIFT DETECTED: app_api has excess SELECT privileges on {table}: " +
                $"[{string.Join(", ", excess)}]");
        }
    }

    /// <summary>
    /// Detects PHI leakage: app_analytics must not have SELECT on PHI columns.
    /// </summary>
    [Fact]
    public async Task AppAnalytics_Should_Not_Access_Phi_Columns()
    {
        var actualPrivileges = await GetColumnPrivileges("app_analytics");

        foreach (var (table, columns) in actualPrivileges)
        {
            if (!AppAnalyticsPermittedColumns.TryGetValue(table, out var permitted))
            {
                Assert.Fail(
                    $"DRIFT DETECTED: app_analytics has privileges on unbaselined table: {table}");
                continue;
            }

            var excess = columns.Except(permitted).ToList();
            Assert.True(excess.Count == 0,
                $"DRIFT DETECTED: app_analytics has PHI access on {table}: " +
                $"[{string.Join(", ", excess)}]");
        }
    }

    /// <summary>
    /// AC-3: Any column without an explicit privilege entry for at least one
    /// application role must fail, indicating the column must be classified in
    /// phi_policy_baseline.sql.
    /// </summary>
    [Fact]
    public async Task NewColumns_Must_Be_Explicitly_Classified()
    {
        var tableList = string.Join(", ", PhiClassifiedTables.Select(t => $"'{t}'"));

        var sql = $@"
            SELECT c.table_name, c.column_name
            FROM information_schema.columns c
            WHERE c.table_schema = 'app'
                AND c.table_name IN ({tableList})
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

        var unclassified = new List<string>();
        while (await reader.ReadAsync())
        {
            unclassified.Add($"{reader.GetString(0)}.{reader.GetString(1)}");
        }

        Assert.True(unclassified.Count == 0,
            $"UNCLASSIFIED PHI COLUMNS: The following columns have no policy assignment " +
            $"and must be added to phi_policy_baseline.sql: [{string.Join(", ", unclassified)}]");
    }

    /// <summary>
    /// Validates that every tenant-bearing table has at least one RLS policy.
    /// </summary>
    [Fact]
    public async Task RlsPolicies_Must_Exist_On_TenantTables()
    {
        var tableList = string.Join(", ", RlsRequiredTables.Select(t => $"'{t}'"));

        var sql = $@"
            SELECT tablename
            FROM pg_catalog.pg_tables
            WHERE schemaname = 'app'
                AND tablename IN ({tableList})
                AND tablename NOT IN (
                    SELECT DISTINCT tablename FROM pg_catalog.pg_policies
                    WHERE schemaname = 'app'
                )
            ORDER BY tablename;";

        await using var cmd = new NpgsqlCommand(sql, _connection);
        await using var reader = await cmd.ExecuteReaderAsync();

        var missing = new List<string>();
        while (await reader.ReadAsync())
        {
            missing.Add(reader.GetString(0));
        }

        Assert.True(missing.Count == 0,
            $"MISSING RLS: The following tables lack row-level security policies: " +
            $"[{string.Join(", ", missing)}]");
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

            if (!result.TryGetValue(table, out var columns))
            {
                columns = new HashSet<string>();
                result[table] = columns;
            }

            columns.Add(column);
        }

        return result;
    }
}
