using LexCalculus.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LexCalculus.Infrastructure.Persistence.Configurations;

public sealed class CalculationHistoryConfiguration : IEntityTypeConfiguration<CalculationHistory>
{
    public void Configure(EntityTypeBuilder<CalculationHistory> b)
    {
        b.ToTable("CalculationHistories");
        b.HasKey(x => x.Id);

        b.Property(x => x.UserId).IsRequired();
        b.Property(x => x.CategorySlug).IsRequired().HasMaxLength(50);
        b.Property(x => x.ToolSlug).IsRequired().HasMaxLength(100);
        b.Property(x => x.ToolTitle).HasMaxLength(200);
        b.Property(x => x.Unit).HasMaxLength(10);
        b.Property(x => x.UserLabel).HasMaxLength(200);
        b.Property(x => x.CaseReference).HasMaxLength(100);

        b.Property(x => x.InputJson).IsRequired();
        b.Property(x => x.OutputJson).IsRequired();

        b.Property(x => x.TotalAmount).HasColumnType("decimal(18,2)");

        b.Property(x => x.CreatedAt).HasColumnType("datetime2(3)");
        b.Property(x => x.UpdatedAt).HasColumnType("datetime2(3)");

        b.HasIndex(x => x.UserId);
        b.HasIndex(x => new { x.UserId, x.CreatedAt });
        b.HasIndex(x => new { x.UserId, x.ToolSlug });

        // Soft-delete filter applied globally via ApplicationDbContext.ApplySoftDeleteQueryFilter
    }
}
