using LexCalculus.Core.Entities;
using LexCalculus.Core.Entities.Calculators;
using LexCalculus.Core.Entities.Identity;
using LexCalculus.Core.Entities.Notifications;
using LexCalculus.Core.Services;
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
    private readonly ITenantContext _tenantContext;

    public ApplicationDbContext(
        DbContextOptions<ApplicationDbContext> options,
        ITenantContext tenantContext)
        : base(options)
    {
        _tenantContext = tenantContext;
    }

    public DbSet<UserProfile> UserProfiles => Set<UserProfile>();
    public DbSet<FormulaParameter> FormulaParameters => Set<FormulaParameter>();
    public DbSet<LifeTable> LifeTables => Set<LifeTable>();
    public DbSet<LifeTableRow> LifeTableRows => Set<LifeTableRow>();
    public DbSet<CalculationHistory> CalculationHistories => Set<CalculationHistory>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<TenantRequest> TenantRequests => Set<TenantRequest>();
    public DbSet<TenantInvitation> TenantInvitations => Set<TenantInvitation>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Apply all IEntityTypeConfiguration<T> in this assembly
        builder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);

        // Apply soft-delete global query filter to all ISoftDelete entities
        // Must run AFTER all configurations are applied
        builder.ApplySoftDeleteQueryFilter();

        // Faz 3.7 — Tenant entity + tenant-aware query filter
        // NOT: CalculationHistory için tenant filter, soft-delete filter'ından
        // SONRA çağrılır → EF Core 8+ AND ile birleştirir, ama defansif olarak
        // soft-delete koşulu da içine yazıldı (override-safe).
        builder.Entity<Tenant>(e =>
        {
            e.HasIndex(t => t.Slug).IsUnique();
            e.HasIndex(t => t.IsDeleted);

            e.HasOne(t => t.Owner)
             .WithMany()
             .HasForeignKey(t => t.OwnerUserId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasQueryFilter(t => !t.IsDeleted);
        });

        builder.Entity<ApplicationUser>(e =>
        {
            e.HasOne(u => u.Tenant)
             .WithMany(t => t.Members)
             .HasForeignKey(u => u.TenantId)
             .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<TenantInvitation>(e =>
        {
            e.HasIndex(i => i.Token).IsUnique();
            e.HasIndex(i => new { i.TenantId, i.Email, i.Status });
            e.HasIndex(i => i.Status);

            e.Property(i => i.Email).HasMaxLength(256).IsRequired();
            e.Property(i => i.Token).HasMaxLength(64).IsRequired();

            e.HasOne(i => i.Tenant)
             .WithMany()
             .HasForeignKey(i => i.TenantId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(i => i.InvitedBy)
             .WithMany()
             .HasForeignKey(i => i.InvitedByUserId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(i => i.AcceptedBy)
             .WithMany()
             .HasForeignKey(i => i.AcceptedByUserId)
             .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<TenantRequest>(e =>
        {
            e.HasIndex(r => r.Status);
            e.HasIndex(r => r.RequestedByUserId);
            e.HasIndex(r => r.CreatedAt);

            e.HasOne(r => r.RequestedBy)
             .WithMany()
             .HasForeignKey(r => r.RequestedByUserId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(r => r.ProcessedBy)
             .WithMany()
             .HasForeignKey(r => r.ProcessedByUserId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(r => r.CreatedTenant)
             .WithMany()
             .HasForeignKey(r => r.CreatedTenantId)
             .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<CalculationHistory>(e =>
        {
            e.HasOne(h => h.Tenant)
             .WithMany()
             .HasForeignKey(h => h.TenantId)
             .OnDelete(DeleteBehavior.SetNull);

            // KRİTİK — KVKK/veri izolasyonu.
            // Soft-delete koşulu (!IsDeleted) bu filter içine elle yazıldı,
            // ApplySoftDeleteQueryFilter() çağrısının üzerine yazılmaması için
            // (EF Core 8+ AND birleştirir; defansif olarak yine de elle ekli).
            e.HasQueryFilter(h =>
                !h.IsDeleted &&
                (_tenantContext.CurrentTenantId == null
                    ? h.TenantId == null && h.UserId == _tenantContext.CurrentUserId
                    : h.TenantId == _tenantContext.CurrentTenantId
                      || (h.TenantId == null && h.UserId == _tenantContext.CurrentUserId)));
        });
    }
}
