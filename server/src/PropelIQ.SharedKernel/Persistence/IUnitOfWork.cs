namespace PropelIQ.SharedKernel.Persistence;

/// <summary>
/// Transactional boundary contract for domain operations.
/// Application services depend on this abstraction rather than directly on
/// <c>DbContext.SaveChangesAsync</c>, ensuring multi-step operations commit
/// atomically or roll back completely (DR-002, AC-2).
/// </summary>
public interface IUnitOfWork : IDisposable, IAsyncDisposable
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    Task BeginTransactionAsync(CancellationToken cancellationToken = default);
    Task CommitTransactionAsync(CancellationToken cancellationToken = default);
    Task RollbackTransactionAsync(CancellationToken cancellationToken = default);
}
