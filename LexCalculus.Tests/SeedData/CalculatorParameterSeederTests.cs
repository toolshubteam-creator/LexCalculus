using FluentAssertions;
using LexCalculus.Core.Entities.Calculators;
using LexCalculus.Infrastructure.Data.SeedData;
using LexCalculus.Tests.TestHelpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace LexCalculus.Tests.SeedData;

public class CalculatorParameterSeederTests
{
    [Fact]
    public async Task SeedAsync_RunsTwice_NoDuplicateOrConstraintError()
    {
        await using var ctx = TestDbContextFactory.Create();

        await CalculatorParameterSeeder.SeedAsync(ctx, NullLogger.Instance);
        var afterFirst = await ctx.Set<FormulaParameter>().IgnoreQueryFilters().CountAsync();

        // İkinci çağrı (uygulama restart simülasyonu) — exception fırlatmamalı
        await CalculatorParameterSeeder.SeedAsync(ctx, NullLogger.Instance);
        var afterSecond = await ctx.Set<FormulaParameter>().IgnoreQueryFilters().CountAsync();

        afterSecond.Should().Be(afterFirst, "ikinci seed run mevcut satırları skip etmeli");
    }

    [Fact]
    public async Task SeedAsync_CanonicalRowSoftDeleted_RestoredOnNextRun()
    {
        await using var ctx = TestDbContextFactory.Create();

        // İlk seed — kanonik satırlar yüklü
        await CalculatorParameterSeeder.SeedAsync(ctx, NullLogger.Instance);

        // Admin yanlışlıkla bir kanonik satırı soft-delete etti (ör. kıdem tavanı 2024-01-01)
        var canonical = await ctx.Set<FormulaParameter>()
            .FirstAsync(p => p.ToolSlug == "kidem-tazminati" && p.Key == "tavan"
                          && p.EffectiveDate == new DateTime(2024, 1, 1));
        canonical.IsDeleted = true;
        await ctx.SaveChangesAsync();

        // Application restart simülasyonu — seeder yeniden çalışır
        // Önce: bu satır boot fail'e neden oluyordu (filter onu görmüyor → INSERT → unique constraint)
        // Şimdi: IgnoreQueryFilters ile bulup IsDeleted=false yapmalı
        var act = async () => await CalculatorParameterSeeder.SeedAsync(ctx, NullLogger.Instance);
        await act.Should().NotThrowAsync();

        // Restore doğrulama: aynı satır artık IsDeleted=false
        var restored = await ctx.Set<FormulaParameter>()
            .IgnoreQueryFilters()
            .FirstAsync(p => p.Id == canonical.Id);
        restored.IsDeleted.Should().BeFalse();

        // Duplicate yok: aynı (slug, key, date) bir kez var
        var sameKeyCount = await ctx.Set<FormulaParameter>()
            .IgnoreQueryFilters()
            .CountAsync(p => p.ToolSlug == "kidem-tazminati" && p.Key == "tavan"
                          && p.EffectiveDate == new DateTime(2024, 1, 1));
        sameKeyCount.Should().Be(1);
    }
}
