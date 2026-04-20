namespace PropelIQ.SharedKernel;

/// <summary>
/// Base entity for all domain entities. Provides auditable identity fields.
/// All domain entities inherit from this class to satisfy DR-002 (audit trail)
/// and TR-001 (layered architecture — entity base lives in SharedKernel, referenced
/// only by Domain layer projects).
/// </summary>
public abstract class BaseEntity
{
    public Guid Id { get; protected set; } = Guid.NewGuid();
    public DateTimeOffset CreatedAt { get; protected set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; protected set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Updates the UpdatedAt timestamp. Call from domain methods that mutate state.
    /// </summary>
    protected void MarkUpdated() => UpdatedAt = DateTimeOffset.UtcNow;
}
