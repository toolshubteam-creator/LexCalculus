using LexCalculus.Core.Entities;
using LexCalculus.Core.Entities.Calculators;
using LexCalculus.Core.Entities.Content;
using LexCalculus.Core.Entities.Identity;
using LexCalculus.Core.Entities.Messaging;
using LexCalculus.Core.Entities.Moderation;
using LexCalculus.Core.Entities.Notifications;
using LexCalculus.Core.Entities.Social;
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
    public DbSet<ActivityLog> ActivityLogs => Set<ActivityLog>();
    public DbSet<MediaFile> MediaFiles => Set<MediaFile>();
    public DbSet<UserConnection> UserConnections => Set<UserConnection>();
    public DbSet<UserBlock> UserBlocks => Set<UserBlock>();
    public DbSet<PostCategory> PostCategories => Set<PostCategory>();
    public DbSet<PostTag> PostTags => Set<PostTag>();
    public DbSet<UserPost> UserPosts => Set<UserPost>();
    public DbSet<PostTagLink> PostTagLinks => Set<PostTagLink>();
    public DbSet<PostComment> PostComments => Set<PostComment>();
    public DbSet<PostLike> PostLikes => Set<PostLike>();
    public DbSet<ContentReport> ContentReports => Set<ContentReport>();
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<Message> Messages => Set<Message>();

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

        builder.Entity<MediaFile>(e =>
        {
            e.HasIndex(m => m.UserId);
            e.HasIndex(m => m.RelativePath).IsUnique();

            e.HasOne(m => m.User)
             .WithMany()
             .HasForeignKey(m => m.UserId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<UserConnection>(e =>
        {
            e.HasKey(c => c.Id);

            e.HasOne(c => c.Requester)
             .WithMany()
             .HasForeignKey(c => c.RequesterId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(c => c.Target)
             .WithMany()
             .HasForeignKey(c => c.TargetId)
             .OnDelete(DeleteBehavior.Restrict);

            e.Property(c => c.Status).HasConversion<int>();

            e.HasIndex(c => c.RequesterId);
            e.HasIndex(c => c.TargetId);
            e.HasIndex(c => c.Status);

            // Aynı yönde birden fazla aktif Pending olmamalı (filtered unique).
            // Servis seviyesinde de cooldown kontrolü var; DB seviyesinde de
            // yarış koşulu (race) için savunma.
            e.HasIndex(c => new { c.RequesterId, c.TargetId })
             .IsUnique()
             .HasFilter("[Status] = 0");
        });

        builder.Entity<UserBlock>(e =>
        {
            e.HasKey(b => b.Id);

            e.HasOne(b => b.Blocker)
             .WithMany()
             .HasForeignKey(b => b.BlockerId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(b => b.Blocked)
             .WithMany()
             .HasForeignKey(b => b.BlockedId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasIndex(b => b.BlockerId);
            e.HasIndex(b => b.BlockedId);

            // Aynı yönde tek block (BlockerId → BlockedId) — DB seviyesinde garanti.
            e.HasIndex(b => new { b.BlockerId, b.BlockedId }).IsUnique();
        });

        builder.Entity<PostCategory>(e =>
        {
            e.HasKey(c => c.Id);
            e.Property(c => c.Name).HasMaxLength(80).IsRequired();
            e.Property(c => c.Slug).HasMaxLength(100).IsRequired();
            e.Property(c => c.Description).HasMaxLength(500);

            e.HasIndex(c => c.Slug).IsUnique();
            e.HasIndex(c => c.DisplayOrder);
            e.HasIndex(c => c.IsActive);
        });

        builder.Entity<PostTag>(e =>
        {
            e.HasKey(t => t.Id);
            e.Property(t => t.Name).HasMaxLength(30).IsRequired();
            e.Property(t => t.Slug).HasMaxLength(40).IsRequired();

            e.HasIndex(t => t.Slug).IsUnique();
            e.HasIndex(t => t.UsageCount);
        });

        builder.Entity<UserPost>(e =>
        {
            e.HasKey(p => p.Id);

            e.Property(p => p.Title).HasMaxLength(200).IsRequired();
            e.Property(p => p.Slug).HasMaxLength(200).IsRequired();
            e.Property(p => p.Body).IsRequired();
            e.Property(p => p.FeaturedImageUrl).HasMaxLength(500);

            e.HasOne(p => p.User)
             .WithMany()
             .HasForeignKey(p => p.UserId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(p => p.Category)
             .WithMany()
             .HasForeignKey(p => p.CategoryId)
             .OnDelete(DeleteBehavior.Restrict);

            // Slug user namespace altında unique (Yaklaşım 4)
            e.HasIndex(p => new { p.UserId, p.Slug }).IsUnique();

            // Public listing — yayın tarihi DESC sorgusu
            e.HasIndex(p => new { p.IsPublished, p.PublishedAt });
            e.HasIndex(p => p.CategoryId);

            // Faz 5.3 — Hide moderation
            e.HasIndex(p => p.IsModeratorHidden);
        });

        builder.Entity<PostTagLink>(e =>
        {
            e.HasKey(l => l.Id);

            e.HasOne(l => l.Post)
             .WithMany(p => p.TagLinks)
             .HasForeignKey(l => l.PostId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(l => l.Tag)
             .WithMany()
             .HasForeignKey(l => l.TagId)
             .OnDelete(DeleteBehavior.Restrict);

            // Aynı (post, tag) ikilisi sadece bir kez
            e.HasIndex(l => new { l.PostId, l.TagId }).IsUnique();
        });

        builder.Entity<PostComment>(e =>
        {
            e.HasKey(c => c.Id);

            // Sanitize edilmiş HTML için margin (raw 1000 → URL/<br> şişer ~2000)
            e.Property(c => c.Body).HasMaxLength(2000).IsRequired();

            e.HasOne(c => c.Post)
             .WithMany()
             .HasForeignKey(c => c.PostId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(c => c.User)
             .WithMany()
             .HasForeignKey(c => c.UserId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasIndex(c => c.PostId);
            e.HasIndex(c => new { c.PostId, c.CreatedAt });

            // Faz 5.3 — Hide moderation
            e.HasIndex(c => c.IsModeratorHidden);
        });

        builder.Entity<PostLike>(e =>
        {
            e.HasKey(l => l.Id);

            e.HasOne(l => l.Post)
             .WithMany()
             .HasForeignKey(l => l.PostId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(l => l.User)
             .WithMany()
             .HasForeignKey(l => l.UserId)
             .OnDelete(DeleteBehavior.Restrict);

            // Bir kullanıcı bir post'u sadece bir kez beğenir
            e.HasIndex(l => new { l.PostId, l.UserId }).IsUnique();
            e.HasIndex(l => l.PostId);
        });

        builder.Entity<ContentReport>(e =>
        {
            e.HasKey(r => r.Id);

            e.Property(r => r.TargetType).HasConversion<int>();
            e.Property(r => r.Reason).HasConversion<int>();
            e.Property(r => r.Status).HasConversion<int>();

            e.Property(r => r.Note).HasMaxLength(500);
            e.Property(r => r.ReviewNote).HasMaxLength(500);

            e.HasOne(r => r.Reporter)
             .WithMany()
             .HasForeignKey(r => r.ReporterId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(r => r.ReviewedBy)
             .WithMany()
             .HasForeignKey(r => r.ReviewedByUserId)
             .OnDelete(DeleteBehavior.SetNull);

            // Mükerrer engel: bir user aynı içeriği bir kez şikayet eder
            e.HasIndex(r => new { r.ReporterId, r.TargetType, r.TargetId }).IsUnique();

            // Admin paneli queries
            e.HasIndex(r => new { r.Status, r.CreatedAt });
            e.HasIndex(r => new { r.TargetType, r.TargetId });
        });

        // Faz 5.4 — Mesajlaşma altyapısı
        builder.Entity<Conversation>(e =>
        {
            e.HasKey(c => c.Id);

            e.HasOne(c => c.User1)
             .WithMany()
             .HasForeignKey(c => c.User1Id)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(c => c.User2)
             .WithMany()
             .HasForeignKey(c => c.User2Id)
             .OnDelete(DeleteBehavior.Restrict);

            // Tek conversation iki kullanıcı arasında (User1<User2 normalize)
            e.HasIndex(c => new { c.User1Id, c.User2Id }).IsUnique();

            // Listeleme: kullanıcı conversation'ları LastMessageAt DESC
            e.HasIndex(c => c.User1Id);
            e.HasIndex(c => c.User2Id);
            e.HasIndex(c => c.LastMessageAt);
        });

        builder.Entity<Message>(e =>
        {
            e.HasKey(m => m.Id);

            e.Property(m => m.Body).HasMaxLength(2000).IsRequired();

            e.HasOne(m => m.Conversation)
             .WithMany(c => c.Messages)
             .HasForeignKey(m => m.ConversationId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(m => m.Sender)
             .WithMany()
             .HasForeignKey(m => m.SenderId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasIndex(m => new { m.ConversationId, m.CreatedAt });
            e.HasIndex(m => m.SenderId);

            // Faz 5.7 — admin "Gizlenenler" listesi için (Hide pattern, Karar 11).
            e.HasIndex(m => m.IsModeratorHidden);
        });

        builder.Entity<ActivityLog>(e =>
        {
            e.HasIndex(a => a.CreatedAt);
            e.HasIndex(a => a.UserId);
            e.HasIndex(a => a.Action);
            e.HasIndex(a => new { a.EntityType, a.EntityId });
            e.HasIndex(a => a.TenantId);

            e.HasOne(a => a.User)
             .WithMany()
             .HasForeignKey(a => a.UserId)
             .OnDelete(DeleteBehavior.SetNull);

            e.HasOne(a => a.Tenant)
             .WithMany()
             .HasForeignKey(a => a.TenantId)
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
