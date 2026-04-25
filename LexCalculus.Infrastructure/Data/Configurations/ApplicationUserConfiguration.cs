using LexCalculus.Core.Entities.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LexCalculus.Infrastructure.Data.Configurations;

public sealed class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        builder.Property(u => u.FullName).HasMaxLength(200);

        builder.Property(u => u.CreatedAt).HasColumnType("datetime2(3)");
        builder.Property(u => u.LastLoginAt).HasColumnType("datetime2(3)");
    }
}
