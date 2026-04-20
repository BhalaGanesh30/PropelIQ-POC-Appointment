namespace PropelIQ.SharedKernel.Persistence.Exceptions;

/// <summary>
/// Domain exception for foreign key, unique constraint, and not-null violations.
/// Carries structured metadata (<see cref="TableName"/>, <see cref="ConstraintName"/>)
/// so the API layer can build RFC 9457 ProblemDetails without parsing strings (AC-1).
/// </summary>
public sealed class ReferentialIntegrityException : Exception
{
    public string? TableName { get; }
    public string? ConstraintName { get; }

    public ReferentialIntegrityException(
        string message,
        string? tableName,
        string? constraintName,
        Exception? innerException = null)
        : base(message, innerException)
    {
        TableName = tableName;
        ConstraintName = constraintName;
    }
}
