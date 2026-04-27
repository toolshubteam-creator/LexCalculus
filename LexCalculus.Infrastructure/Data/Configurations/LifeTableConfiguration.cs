using LexCalculus.Core.Entities.Calculators;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LexCalculus.Infrastructure.Data.Configurations;

public sealed class LifeTableConfiguration : IEntityTypeConfiguration<LifeTable>
{
    public void Configure(EntityTypeBuilder<LifeTable> builder)
    {
        builder.ToTable("LifeTables");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Code).IsRequired().HasMaxLength(50);
        builder.Property(t => t.Name).IsRequired().HasMaxLength(150);
        builder.Property(t => t.Source).HasMaxLength(200);
        builder.Property(t => t.Note).HasMaxLength(1000);
        builder.Property(t => t.EffectiveDate).HasColumnType("datetime2(0)");

        builder.Property(t => t.CreatedAt).HasColumnType("datetime2(3)");
        builder.Property(t => t.UpdatedAt).HasColumnType("datetime2(3)");

        builder.HasIndex(t => t.Code).IsUnique().HasDatabaseName("UX_LifeTables_Code");

        builder.HasMany(t => t.Rows)
            .WithOne(r => r.LifeTable)
            .HasForeignKey(r => r.LifeTableId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
