using LexCalculus.Core.Entities.Common;

namespace LexCalculus.Core.Interfaces;

/// <summary>
/// Generic repository contract for basic CRUD operations on domain entities.
/// Implementations must NOT call SaveChanges — the caller commits via
/// <see cref="IUnitOfWork.SaveChangesAsync"/> to allow multi-repository transactions.
/// </summary>
/// <typeparam name="T">Entity type that inherits from <see cref="BaseEntity"/>.</typeparam>
public interface IRepository<T> where T : BaseEntity
{
    /// <summary>
    /// Returns the entity with the given <paramref name="id"/>, or null if not found.
    /// Soft-deleted entities are excluded by the global query filter.
    /// </summary>
    Task<T?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns all non-deleted entities of type <typeparamref name="T"/>.
    /// </summary>
    Task<IReadOnlyList<T>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds the entity to the change tracker. Does NOT call SaveChanges.
    /// Returns the tracked entity instance (with any store-generated defaults applied after save).
    /// </summary>
    Task<T> AddAsync(T entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks the entity as modified in the change tracker. Synchronous because
    /// EF Core's Update method only flags the entry — no I/O occurs.
    /// </summary>
    void Update(T entity);

    /// <summary>
    /// Performs a soft delete by setting <see cref="BaseEntity.IsDeleted"/> to true.
    /// Does NOT call SaveChanges.
    /// </summary>
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Exposes an <see cref="IQueryable{T}"/> for advanced queries (Include, Where, OrderBy chains).
    /// The global soft-delete filter is applied automatically.
    /// </summary>
    IQueryable<T> Query();
}
