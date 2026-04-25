using LexCalculus.Core.Entities.Common;
using LexCalculus.Core.Interfaces;
using LexCalculus.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LexCalculus.Infrastructure.Repositories;

/// <summary>
/// Generic repository implementation backed by EF Core. Soft-delete is enforced
/// at the global query filter level — calls to GetByIdAsync, GetAllAsync, and
/// Query() never see IsDeleted=true rows unless explicitly opted out via
/// IgnoreQueryFilters().
///
/// IMPORTANT: This repository does NOT call SaveChangesAsync. The caller must
/// commit through IUnitOfWork.SaveChangesAsync to allow batching of multiple
/// operations into a single transaction.
/// </summary>
public class Repository<T> : IRepository<T> where T : BaseEntity
{
    protected readonly ApplicationDbContext _context;
    protected readonly DbSet<T> _dbSet;

    public Repository(ApplicationDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _dbSet = _context.Set<T>();
    }

    public virtual async Task<T?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _dbSet.FindAsync(new object[] { id }, cancellationToken);
    }

    public virtual async Task<IReadOnlyList<T>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _dbSet.ToListAsync(cancellationToken);
    }

    public virtual async Task<T> AddAsync(T entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        await _dbSet.AddAsync(entity, cancellationToken);
        return entity;
    }

    public virtual void Update(T entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        _dbSet.Update(entity);
    }

    public virtual async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        // Soft delete: load entity, set IsDeleted, mark Modified.
        // Use IgnoreQueryFilters so a previously-soft-deleted row can still be re-deleted
        // (no-op effectively, but harmless).
        var entity = await _dbSet
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

        if (entity is null) return;
        if (entity.IsDeleted) return; // already deleted, idempotent

        entity.IsDeleted = true;
        // UpdatedAt will be set by AuditInterceptor automatically
        _dbSet.Update(entity);
    }

    public virtual IQueryable<T> Query()
    {
        return _dbSet.AsQueryable();
    }
}
