using LexCalculus.Core.Entities.Calculators;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LexCalculus.Infrastructure.Data.Configurations;

public sealed class LifeTableRowConfiguration : IEntityTypeConfiguration<LifeTableRow>
{
    public void Configure(EntityTypeBuilder<LifeTableRow> builder)
    {
        builder.ToTable("LifeTableRows");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Yas).IsRequired();
        builder.Property(r => r.Cinsiyet).IsRequired();
        builder.Property(r => r.BekledigiYasam).HasPrecision(8, 6);
        builder.Property(r => r.OlumOlasiligi).HasPrecision(10, 8);

        builder.Property(r => r.CreatedAt).HasColumnType("datetime2(3)");
        builder.Property(r => r.UpdatedAt).HasColumnType("datetime2(3)");

        builder.HasIndex(r => new { r.LifeTableId, r.Yas, r.Cinsiyet })
            .IsUnique()
            .HasDatabaseName("UX_LifeTableRows_Lookup");
    }
}
