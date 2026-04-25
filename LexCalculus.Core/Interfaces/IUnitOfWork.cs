namespace LexCalculus.Core.Interfaces;

/// <summary>
/// Coordinates persistence across multiple repositories within a single transaction.
/// Implements <see cref="IAsyncDisposable"/> to ensure the underlying database transaction
/// is disposed even if the caller forgets to commit or rollback.
/// </summary>
public interface IUnitOfWork : IAsyncDisposable
{
    /// <summary>
    /// Persists all pending changes tracked by the DbContext.
    /// Returns the number of state entries written to the database.
    /// </summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Begins an explicit database transaction.
    /// Use this when multiple SaveChanges calls must be atomic.
    /// </summary>
    Task BeginTransactionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Commits the current transaction. Throws if no transaction is active.
    /// </summary>
    Task CommitTransactionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Rolls back the current transaction. Safe to call if no transaction is active.
    /// </summary>
    Task RollbackTransactionAsync(CancellationToken cancellationToken = default);
}
