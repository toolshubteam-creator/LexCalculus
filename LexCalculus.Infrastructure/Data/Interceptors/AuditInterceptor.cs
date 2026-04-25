using LexCalculus.Core.Entities.Common;
using LexCalculus.Core.Entities.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace LexCalculus.Infrastructure.Data.Interceptors;

/// <summary>
/// EF Core SaveChanges interceptor that automatically sets CreatedAt/UpdatedAt
/// on entities deriving from BaseEntity. Eliminates manual timestamp management
/// in repositories or business logic.
/// </summary>
public sealed class AuditInterceptor : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        UpdateAuditFields(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        UpdateAuditFields(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private static void UpdateAuditFields(DbContext? context)
    {
        if (context is null) return;

        var now = DateTime.UtcNow;

        // Audit handling for two entity families:
        // 1. BaseEntity descendants — full CreatedAt + UpdatedAt management
        // 2. ApplicationUser — only CreatedAt on insert (Modified is too noisy for Identity tables)

        foreach (EntityEntry<BaseEntity> entry in context.ChangeTracker.Entries<BaseEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = now;
                    // UpdatedAt explicitly null on creation
                    entry.Entity.UpdatedAt = null;
                    break;

                case EntityState.Modified:
                    entry.Entity.UpdatedAt = now;
                    // Prevent CreatedAt from being modified
                    entry.Property(nameof(BaseEntity.CreatedAt)).IsModified = false;
                    break;
            }
        }

        // ApplicationUser audit (separate from BaseEntity loop because IdentityUser is the base)
        foreach (var entry in context.ChangeTracker.Entries<ApplicationUser>())
        {
            if (entry.State == EntityState.Added)
            {
                // Only set CreatedAt if it's still default (caller may have set it explicitly)
                if (entry.Entity.CreatedAt == default)
                {
                    entry.Entity.CreatedAt = now;
                }
            }
            // NOT: ApplicationUser için Modified state'inde otomatik UpdatedAt güncelleme yapmıyoruz
            // çünkü Identity'nin SecurityStamp, ConcurrencyStamp gibi her güncelleme için tetiklenen
            // alanları var; bunlar gerçek bir kullanıcı eylemi olmadan da Modified state oluşturabilir.
            // LastLoginAt'i ayrı bir mekanizmayla (sign-in event handler) güncelleyeceğiz.
        }
    }
}
