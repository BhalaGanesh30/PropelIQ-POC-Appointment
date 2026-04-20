namespace PropelIQ.SharedKernel;

/// <summary>
/// Generic repository interface for data access abstraction.
/// Domain projects depend on this interface; Infrastructure projects implement it.
/// This enforces the Dependency Inversion Principle per TR-001.
/// </summary>
/// <typeparam name="T">Domain entity inheriting <see cref="BaseEntity"/>.</typeparam>
public interface IRepository<T> where T : BaseEntity
{
    Task<T?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<T>> GetAllAsync(CancellationToken cancellationToken = default);
    Task AddAsync(T entity, CancellationToken cancellationToken = default);
    Task UpdateAsync(T entity, CancellationToken cancellationToken = default);
    Task DeleteAsync(T entity, CancellationToken cancellationToken = default);
}
