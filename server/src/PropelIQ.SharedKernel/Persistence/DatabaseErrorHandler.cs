using Microsoft.EntityFrameworkCore;
using Npgsql;
using PropelIQ.SharedKernel.Persistence.Exceptions;

namespace PropelIQ.SharedKernel.Persistence;

/// <summary>
/// Translates PostgreSQL-specific <see cref="DbUpdateException"/> into domain
/// exceptions by inspecting the inner <see cref="PostgresException"/> SQLSTATE code.
/// Extracts constraint name, table, and column for structured error reporting (AC-1).
/// </summary>
public static class DatabaseErrorHandler
{
    public static Exception Translate(DbUpdateException ex)
    {
        if (ex.InnerException is PostgresException pgEx)
        {
            return pgEx.SqlState switch
            {
                PostgresErrorCodes.ForeignKeyViolation => new ReferentialIntegrityException(
                    $"Foreign key violation on table '{pgEx.TableName}': " +
                    $"constraint '{pgEx.ConstraintName}' " +
                    $"referencing column '{pgEx.ColumnName}'. " +
                    $"The referenced record does not exist.",
                    pgEx.TableName,
                    pgEx.ConstraintName,
                    pgEx),

                PostgresErrorCodes.UniqueViolation => new ReferentialIntegrityException(
                    $"Unique constraint violation on table '{pgEx.TableName}': " +
                    $"constraint '{pgEx.ConstraintName}'. " +
                    $"A record with the same value already exists.",
                    pgEx.TableName,
                    pgEx.ConstraintName,
                    pgEx),

                PostgresErrorCodes.NotNullViolation => new ReferentialIntegrityException(
                    $"Not null violation on table '{pgEx.TableName}': " +
                    $"column '{pgEx.ColumnName}' cannot be null.",
                    pgEx.TableName,
                    pgEx.ColumnName,
                    pgEx),

                _ => ex
            };
        }

        return ex;
    }
}
