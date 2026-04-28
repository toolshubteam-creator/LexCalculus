using LexCalculus.Core.Entities.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LexCalculus.Infrastructure.Data.Configurations;

public sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> e)
    {
        e.ToTable("Notifications");
        e.HasKey(n => n.Id);

        e.Property(n => n.Title).IsRequired().HasMaxLength(200);
        e.Property(n => n.Body).IsRequired().HasMaxLength(2000);
        e.Property(n => n.Link).HasMaxLength(500);
        e.Property(n => n.IconHint).HasMaxLength(50);
        e.Property(n => n.RelatedEntityType).HasMaxLength(100);

        e.Property(n => n.CreatedAt).HasColumnType("datetime2(3)");
        e.Property(n => n.ReadAt).HasColumnType("datetime2(3)");

        e.HasOne(n => n.User)
            .WithMany()
            .HasForeignKey(n => n.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Index 1: Bell dropdown / kullanıcı feed
        e.HasIndex(n => new { n.UserId, n.IsRead, n.CreatedAt })
            .HasDatabaseName("IX_Notifications_UserFeed")
            .IsDescending(false, false, true);

        // Index 2: Dedup query (DataFreshnessCheckJob)
        e.HasIndex(n => new { n.UserId, n.Type, n.RelatedEntityType, n.RelatedEntityId, n.CreatedAt })
            .HasDatabaseName("IX_Notifications_Dedup")
            .IsDescending(false, false, false, false, true);

        // Index 3: Retention cleanup (Adım 3.8'e hazırlık)
        e.HasIndex(n => n.CreatedAt)
            .HasDatabaseName("IX_Notifications_CreatedAt");

        // Soft delete (global filter zaten ApplySoftDeleteQueryFilter ile uygulanmıyor —
        // Notification IsDeleted'a sahip ama ISoftDelete interface'ine sahip değil.
        // Explicit query filter koyuyoruz.)
        e.HasQueryFilter(n => !n.IsDeleted);
    }
}
