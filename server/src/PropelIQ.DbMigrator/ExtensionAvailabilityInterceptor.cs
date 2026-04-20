using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace PropelIQ.DbMigrator;

/// <summary>
/// Intercepts <c>CREATE EXTENSION IF NOT EXISTS &lt;ext&gt;</c> commands during
/// migration execution and replaces them with a PL/pgSQL DO block that logs a
/// NOTICE instead of raising an error when the extension isn't available.
///
/// This allows EF Core migrations to run on plain PostgreSQL installations that
/// lack optional extensions (pgvector, pgaudit) without failing the pipeline.
/// In Docker / production, the extensions are pre-installed and the DO block
/// exits the normal path with no observable difference.
/// </summary>
internal sealed class ExtensionAvailabilityInterceptor : DbCommandInterceptor
{
    // Extensions that are optional for local development.
    private static readonly HashSet<string> OptionalExtensions =
        new(StringComparer.OrdinalIgnoreCase) { "vector", "pgaudit" };

    public override InterceptionResult<int> NonQueryExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<int> result)
    {
        RewriteIfExtensionCommand(command);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        RewriteIfExtensionCommand(command);
        return ValueTask.FromResult(result);
    }

    private static void RewriteIfExtensionCommand(DbCommand command)
    {
        var sql = command.CommandText.Trim();

        // Match: CREATE EXTENSION IF NOT EXISTS <name>  (with or without trailing semicolon/quotes)
        if (!sql.StartsWith("CREATE EXTENSION IF NOT EXISTS ", StringComparison.OrdinalIgnoreCase))
            return;

        // Extract the extension name token (strip quotes and semicolons)
        var token = sql["CREATE EXTENSION IF NOT EXISTS ".Length..]
            .TrimEnd(';', ' ')
            .Trim('"');

        if (!OptionalExtensions.Contains(token))
            return;

        // Rewrite to a DO block that catches feature_not_supported (0A000)
        // so the migration continues when the extension isn't installed.
        command.CommandText = $"""
            DO $$
            BEGIN
                CREATE EXTENSION IF NOT EXISTS "{token}";
            EXCEPTION WHEN feature_not_supported OR undefined_file THEN
                RAISE NOTICE 'Optional extension "{token}" is not available on this PostgreSQL instance. Skipping.';
            END $$;
            """;
    }
}
