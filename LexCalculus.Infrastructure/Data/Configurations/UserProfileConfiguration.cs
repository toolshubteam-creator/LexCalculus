using LexCalculus.Core.Entities.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LexCalculus.Infrastructure.Data.Configurations;

public sealed class UserProfileConfiguration : IEntityTypeConfiguration<UserProfile>
{
    public void Configure(EntityTypeBuilder<UserProfile> builder)
    {
        builder.ToTable("UserProfiles");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.DisplayName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(p => p.BaroNo).HasMaxLength(50);
        builder.Property(p => p.Bio).HasMaxLength(2000);
        builder.Property(p => p.AvatarUrl).HasMaxLength(500);
        builder.Property(p => p.City).HasMaxLength(80);

        builder.Property(p => p.CreatedAt).HasColumnType("datetime2(3)");
        builder.Property(p => p.UpdatedAt).HasColumnType("datetime2(3)");

        // One-to-one with ApplicationUser via UserId FK
        builder.HasOne(p => p.User)
            .WithOne(u => u.Profile)
            .HasForeignKey<UserProfile>(p => p.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Unique: one profile per user
        builder.HasIndex(p => p.UserId).IsUnique();

        // Filtered unique index on BaroNo (only when not null)
        // Translates to: CREATE UNIQUE INDEX ... WHERE BaroNo IS NOT NULL
        builder.HasIndex(p => p.BaroNo)
            .IsUnique()
            .HasFilter("[BaroNo] IS NOT NULL");

        // Faz 4.1 P1/3 — public profile alanları
        builder.Property(p => p.PublicSlug).HasMaxLength(100);

        // Filtered unique: NULL slug birden fazla profilde olabilir, dolu olanlar unique.
        builder.HasIndex(p => p.PublicSlug)
            .IsUnique()
            .HasFilter("[PublicSlug] IS NOT NULL");

        // ShowTenant default 0 — migration'da DB defaultValueSql olarak da yaz.
        builder.Property(p => p.ShowTenant).HasDefaultValueSql("0");
    }
}
