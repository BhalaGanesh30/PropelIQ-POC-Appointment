namespace PropelIQ.SharedKernel.Persistence.Exceptions;

/// <summary>
/// Thrown when a bulk import batch fails due to referential integrity violations.
/// Contains per-row error details so callers can produce an error report
/// identifying the offending rows (edge case: mid-batch FK violation).
/// </summary>
public sealed class BulkImportException : Exception
{
    public IReadOnlyList<BulkImportError> Errors { get; }

    public BulkImportException(
        string message,
        IReadOnlyList<BulkImportError> errors)
        : base(message)
    {
        Errors = errors;
    }
}

public sealed record BulkImportError(
    int RowIndex,
    string EntityType,
    string ErrorMessage,
    string? ConstraintName);
