using FluentAssertions;
using LexCalculus.Core.Entities.Calculators;
using LexCalculus.Infrastructure.Services;
using LexCalculus.Tests.TestHelpers;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace LexCalculus.Tests.Services;

public class TaxBracketServiceTests : SqlServerTestBase
{
    private static readonly DateTime Effective = new(2026, 1, 1);

    private static IDistributedCache CreateCache() =>
        new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));

    private (TaxBracketService svc, LexCalculus.Infrastructure.Data.ApplicationDbContext ctx) Build(string slug)
    {
        var ctx = _db.Create();
        ctx.Set<TaxBracket>().AddRange(
            // 5 dilim: %1, %3, %5, %7, %10 (veraset tarifesi referansı).
            new TaxBracket { ToolSlug = slug, Sira = 1, MinAmount =          0m, MaxAmount =  3_000_000m, Rate = 0.01m, EffectiveDate = Effective },
            new TaxBracket { ToolSlug = slug, Sira = 2, MinAmount =  3_000_000m, MaxAmount = 10_000_000m, Rate = 0.03m, EffectiveDate = Effective },
            new TaxBracket { ToolSlug = slug, Sira = 3, MinAmount = 10_000_000m, MaxAmount = 25_000_000m, Rate = 0.05m, EffectiveDate = Effective },
            new TaxBracket { ToolSlug = slug, Sira = 4, MinAmount = 25_000_000m, MaxAmount = 55_000_000m, Rate = 0.07m, EffectiveDate = Effective },
            new TaxBracket { ToolSlug = slug, Sira = 5, MinAmount = 55_000_000m, MaxAmount = null,        Rate = 0.10m, EffectiveDate = Effective }
        );
        ctx.SaveChanges();

        var svc = new TaxBracketService(ctx, CreateCache(), NullLogger<TaxBracketService>.Instance);
        return (svc, ctx);
    }

    [Fact]
    public async Task GetBracketsAsync_ReturnsActiveBracketsForDate_SortedBySira()
    {
        var (svc, ctx) = Build("test-tool");
        await using var _ = ctx;

        var brackets = await svc.GetBracketsAsync("test-tool", Effective);

        brackets.Should().HaveCount(5);
        brackets.Select(b => b.Sira).Should().BeInAscendingOrder();
        brackets[0].Rate.Should().Be(0.01m);
        brackets[^1].MaxAmount.Should().BeNull();
    }

    [Fact]
    public async Task HesaplaAsync_SingleBracket_StandardCalculation()
    {
        var (svc, ctx) = Build("test-tool");
        await using var _ = ctx;

        // 2.000.000 TL — yalnız dilim 1'de (0-3M, %1). 2M × %1 = 20.000.
        var r = await svc.HesaplaAsync("test-tool", 2_000_000m, Effective);

        r.ToplamVergi.Should().Be(20_000m);
        r.DilimDetaylar.Should().HaveCount(1);
        r.DilimDetaylar[0].DilimdekiTutar.Should().Be(2_000_000m);
        r.DilimDetaylar[0].Rate.Should().Be(0.01m);
    }

    [Fact]
    public async Task HesaplaAsync_MultiBracket_CumulativeCorrect()
    {
        var (svc, ctx) = Build("test-tool");
        await using var _ = ctx;

        // 12.000.000 TL — dilim 1 (3M×%1=30k) + dilim 2 (7M×%3=210k) + dilim 3 (2M×%5=100k) = 340.000.
        var r = await svc.HesaplaAsync("test-tool", 12_000_000m, Effective);

        r.ToplamVergi.Should().Be(340_000m);
        r.DilimDetaylar.Should().HaveCount(3);
        r.DilimDetaylar[0].DilimVergisi.Should().Be(30_000m);
        r.DilimDetaylar[1].DilimVergisi.Should().Be(210_000m);
        r.DilimDetaylar[2].DilimVergisi.Should().Be(100_000m);
    }

    [Fact]
    public async Task HesaplaAsync_LastBracketUnbounded_Works()
    {
        var (svc, ctx) = Build("test-tool");
        await using var _ = ctx;

        // 100.000.000 TL — son dilim sınırsız (55M+ × %10).
        // Dilim 1: 3M×%1 = 30k; Dilim 2: 7M×%3 = 210k; Dilim 3: 15M×%5 = 750k;
        // Dilim 4: 30M×%7 = 2.100k; Dilim 5: 45M×%10 = 4.500k → toplam 7.590.000.
        var r = await svc.HesaplaAsync("test-tool", 100_000_000m, Effective);

        r.ToplamVergi.Should().Be(7_590_000m);
        r.DilimDetaylar.Should().HaveCount(5);
        r.DilimDetaylar[^1].DilimdekiTutar.Should().Be(45_000_000m);
        r.DilimDetaylar[^1].Rate.Should().Be(0.10m);
    }

    [Fact]
    public async Task HesaplaAsync_ZeroOrNegative_ReturnsZero()
    {
        var (svc, ctx) = Build("test-tool");
        await using var _ = ctx;

        var r1 = await svc.HesaplaAsync("test-tool", 0m, Effective);
        r1.ToplamVergi.Should().Be(0m);
        r1.DilimDetaylar.Should().BeEmpty();

        var r2 = await svc.HesaplaAsync("test-tool", -1000m, Effective);
        r2.ToplamVergi.Should().Be(0m);
    }
}
