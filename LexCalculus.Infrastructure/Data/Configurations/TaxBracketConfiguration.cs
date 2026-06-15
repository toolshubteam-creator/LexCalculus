using LexCalculus.Core.Entities.Calculators;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LexCalculus.Infrastructure.Data.Configurations;

public sealed class TaxBracketConfiguration : IEntityTypeConfiguration<TaxBracket>
{
    public void Configure(EntityTypeBuilder<TaxBracket> builder)
    {
        builder.ToTable("TaxBrackets");

        builder.HasKey(b => b.Id);

        builder.Property(b => b.ToolSlug)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(b => b.Sira)
            .IsRequired();

        builder.Property(b => b.MinAmount)
            .HasPrecision(18, 2);

        builder.Property(b => b.MaxAmount)
            .HasPrecision(18, 2);

        builder.Property(b => b.Rate)
            .HasPrecision(10, 4);

        builder.Property(b => b.EffectiveDate)
            .HasColumnType("datetime2(0)");

        builder.Property(b => b.Source).HasMaxLength(200);
        builder.Property(b => b.Note).HasMaxLength(1000);

        builder.Property(b => b.CreatedAt).HasColumnType("datetime2(3)");
        builder.Property(b => b.UpdatedAt).HasColumnType("datetime2(3)");

        // Lookup index: bracket set sorgusu (ToolSlug + EffectiveDate desc, Sira asc).
        builder.HasIndex(b => new { b.ToolSlug, b.EffectiveDate, b.Sira }, "IX_TaxBrackets_Lookup")
            .IsDescending(false, true, false);

        // Unique constraint: aynı (ToolSlug, EffectiveDate, Sira) yalnız bir kez.
        builder.HasIndex(b => new { b.ToolSlug, b.EffectiveDate, b.Sira }, "UX_TaxBrackets_Version")
            .IsUnique();
    }
}
