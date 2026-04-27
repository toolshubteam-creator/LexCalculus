using LexCalculus.Core.Entities;
using LexCalculus.Core.Entities.Calculators;
using LexCalculus.Core.Entities.Identity;
using LexCalculus.Infrastructure.Data.Extensions;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace LexCalculus.Infrastructure.Data;

/// <summary>
/// Primary EF Core DbContext for LexCalculus. Inherits IdentityDbContext to
/// integrate ASP.NET Identity tables (AspNetUsers, AspNetRoles, etc.).
/// Applies entity configurations from this assembly and a global soft-delete
/// query filter to all entities implementing ISoftDelete.
/// </summary>
public class ApplicationDbContext : IdentityDbContext<ApplicationUser, ApplicationRole, int>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options) { }

    public DbSet<UserProfile> UserProfiles => Set<UserProfile>();
    public DbSet<FormulaParameter> FormulaParameters => Set<FormulaParameter>();
    public DbSet<LifeTable> LifeTables => Set<LifeTable>();
    public DbSet<LifeTableRow> LifeTableRows => Set<LifeTableRow>();
    public DbSet<CalculationHistory> CalculationHistories => Set<CalculationHistory>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Apply all IEntityTypeConfiguration<T> in this assembly
        builder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);

        // Apply soft-delete global query filter to all ISoftDelete entities
        // Must run AFTER all configurations are applied
        builder.ApplySoftDeleteQueryFilter();
    }
}
