using System.Linq.Expressions;
using LexCalculus.Core.Entities.Common;
using Microsoft.EntityFrameworkCore;

namespace LexCalculus.Infrastructure.Data.Extensions;

/// <summary>
/// Extensions for ModelBuilder. Currently applies a global query filter
/// to soft-delete entities (any entity implementing ISoftDelete) so that
/// IsDeleted == true rows are invisible to all LINQ queries unless
/// explicitly opted out via .IgnoreQueryFilters().
/// </summary>
public static class ModelBuilderExtensions
{
    public static void ApplySoftDeleteQueryFilter(this ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (!typeof(ISoftDelete).IsAssignableFrom(entityType.ClrType))
                continue;

            // Build expression: e => !e.IsDeleted
            var parameter = Expression.Parameter(entityType.ClrType, "e");
            var propertyAccess = Expression.Property(parameter, nameof(ISoftDelete.IsDeleted));
            var notDeleted = Expression.Not(propertyAccess);
            var lambda = Expression.Lambda(notDeleted, parameter);

            modelBuilder.Entity(entityType.ClrType).HasQueryFilter(lambda);
        }
    }
}
