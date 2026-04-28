using LexCalculus.Core.Entities.Calculators;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LexCalculus.Infrastructure.Data.Configurations;

public sealed class FormulaParameterConfiguration : IEntityTypeConfiguration<FormulaParameter>
{
    public void Configure(EntityTypeBuilder<FormulaParameter> builder)
    {
        builder.ToTable("FormulaParameters");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.ToolSlug)
            .IsRequired()
            .HasMaxLength(80);

        builder.Property(p => p.Key)
            .IsRequired()
            .HasMaxLength(80);

        builder.Property(p => p.Value)
            .HasPrecision(18, 6);

        builder.Property(p => p.EffectiveDate)
            .HasColumnType("datetime2(0)");

        builder.Property(p => p.Source).HasMaxLength(200);
        builder.Property(p => p.Note).HasMaxLength(1000);
        builder.Property(p => p.ExpectedUpdateFrequency).HasMaxLength(50);
        builder.Property(p => p.Notes).HasMaxLength(500);
        builder.Property(p => p.LastUpdatedDate).HasColumnType("datetime2(0)");

        builder.Property(p => p.CreatedAt).HasColumnType("datetime2(3)");
        builder.Property(p => p.UpdatedAt).HasColumnType("datetime2(3)");

        builder.HasIndex(p => new { p.ToolSlug, p.Key, p.EffectiveDate }, "IX_FormulaParameters_Lookup")
            .IsDescending(false, false, true);

        builder.HasIndex(p => new { p.ToolSlug, p.Key, p.EffectiveDate }, "UX_FormulaParameters_Version")
            .IsUnique();

        // FK'ler NoAction — SQL Server multi-path cascade kısıtı (1785).
        // Kullanıcıyı silmek isteyen admin önce parametreleri reassign etmeli.
        // Audit'in kaybolmaması için bu davranış istenir.
        builder.HasOne(p => p.CreatedByUser)
            .WithMany()
            .HasForeignKey(p => p.CreatedByUserId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(p => p.LastModifiedByUser)
            .WithMany()
            .HasForeignKey(p => p.LastModifiedByUserId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
