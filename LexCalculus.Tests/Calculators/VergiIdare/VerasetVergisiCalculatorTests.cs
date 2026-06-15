using FluentAssertions;
using LexCalculus.Core.Calculators.VergiIdare;
using LexCalculus.Core.Entities.Calculators;
using LexCalculus.Infrastructure.Services;
using LexCalculus.Tests.TestHelpers;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace LexCalculus.Tests.Calculators.VergiIdare;

public class VerasetVergisiCalculatorTests : SqlServerTestBase
{
    private static readonly DateTime Effective = new(2026, 1, 1);

    private static IDistributedCache CreateCache() =>
        new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));

    private (VerasetVergisiCalculator calc, LexCalculus.Infrastructure.Data.ApplicationDbContext ctx) Build()
    {
        var ctx = _db.Create();
        // 2026 tarifesi — veraset + ivazsız iki dilim seti.
        ctx.Set<TaxBracket>().AddRange(
            new TaxBracket { ToolSlug = "veraset-vergisi/veraset", Sira = 1, MinAmount =          0m, MaxAmount =  3_000_000m, Rate = 0.01m, EffectiveDate = Effective },
            new TaxBracket { ToolSlug = "veraset-vergisi/veraset", Sira = 2, MinAmount =  3_000_000m, MaxAmount = 10_000_000m, Rate = 0.03m, EffectiveDate = Effective },
            new TaxBracket { ToolSlug = "veraset-vergisi/veraset", Sira = 3, MinAmount = 10_000_000m, MaxAmount = 25_000_000m, Rate = 0.05m, EffectiveDate = Effective },
            new TaxBracket { ToolSlug = "veraset-vergisi/veraset", Sira = 4, MinAmount = 25_000_000m, MaxAmount = 55_000_000m, Rate = 0.07m, EffectiveDate = Effective },
            new TaxBracket { ToolSlug = "veraset-vergisi/veraset", Sira = 5, MinAmount = 55_000_000m, MaxAmount = null,        Rate = 0.10m, EffectiveDate = Effective },

            new TaxBracket { ToolSlug = "veraset-vergisi/ivazsiz", Sira = 1, MinAmount =          0m, MaxAmount =  3_000_000m, Rate = 0.10m, EffectiveDate = Effective },
            new TaxBracket { ToolSlug = "veraset-vergisi/ivazsiz", Sira = 2, MinAmount =  3_000_000m, MaxAmount = 10_000_000m, Rate = 0.15m, EffectiveDate = Effective },
            new TaxBracket { ToolSlug = "veraset-vergisi/ivazsiz", Sira = 3, MinAmount = 10_000_000m, MaxAmount = 25_000_000m, Rate = 0.20m, EffectiveDate = Effective },
            new TaxBracket { ToolSlug = "veraset-vergisi/ivazsiz", Sira = 4, MinAmount = 25_000_000m, MaxAmount = 55_000_000m, Rate = 0.25m, EffectiveDate = Effective },
            new TaxBracket { ToolSlug = "veraset-vergisi/ivazsiz", Sira = 5, MinAmount = 55_000_000m, MaxAmount = null,        Rate = 0.30m, EffectiveDate = Effective }
        );
        ctx.SaveChanges();

        var bracketSvc = new TaxBracketService(ctx, CreateCache(), NullLogger<TaxBracketService>.Instance);
        return (new VerasetVergisiCalculator(bracketSvc), ctx);
    }

    [Fact]
    public async Task Veraset_5M_3Mirasci_NoTax_BelowIstisna()
    {
        // 5M veraset, eş + 2 çocuk (3 mirasçı). İstisna 3 × 2.907.136 = 8.721.408 > 5M → 0 vergi.
        var (calc, ctx) = Build();
        await using var _ = ctx;

        var r = await calc.CalculateAsync(new VerasetVergisiInput
        {
            IntikalTuru = IntikalTuru.VerasetFurugVeEs,
            BrutDeger = 5_000_000m,
            MirascıSayisi = 3,
            AsOfDate = Effective
        });

        r.IsValid.Should().BeTrue();
        r.ToplamIstisna.Should().Be(3 * 2_907_136m);
        r.VergilendirilebilirTutar.Should().Be(0m);
        r.ToplamVergi.Should().Be(0m);
        r.DilimDetaylar.Should().BeEmpty();
    }

    [Fact]
    public async Task Reference_Veraset_50M_2Mirasci_BracketCalculation()
    {
        // Eş + 1 çocuk (2 mirasçı). İstisna 2 × 2.907.136 = 5.814.272.
        // Vergilendirilebilir: 50M - 5.814.272 = 44.185.728.
        //   Dilim 1: 3M × %1 = 30.000
        //   Dilim 2: 7M × %3 = 210.000
        //   Dilim 3: 15M × %5 = 750.000
        //   Dilim 4: 19.185.728 × %7 = 1.343.000,96
        //   Toplam: 2.333.000,96
        var (calc, ctx) = Build();
        await using var _ = ctx;

        var r = await calc.CalculateAsync(new VerasetVergisiInput
        {
            IntikalTuru = IntikalTuru.VerasetFurugVeEs,
            BrutDeger = 50_000_000m,
            MirascıSayisi = 2,
            AsOfDate = Effective
        });

        r.VergilendirilebilirTutar.Should().Be(44_185_728m);
        r.ToplamVergi.Should().Be(2_333_000.96m);
        r.DilimDetaylar.Should().HaveCount(4);
    }

    [Fact]
    public async Task Reference_Ivazsiz_5M_BracketCalculation()
    {
        // İvazsız 5M, sabit istisna 66.935.
        // Vergilendirilebilir: 5M - 66.935 = 4.933.065.
        //   Dilim 1: 3M × %10 = 300.000
        //   Dilim 2: 1.933.065 × %15 = 289.959,75
        //   Toplam: 589.959,75
        var (calc, ctx) = Build();
        await using var _ = ctx;

        var r = await calc.CalculateAsync(new VerasetVergisiInput
        {
            IntikalTuru = IntikalTuru.Ivazsiz,
            BrutDeger = 5_000_000m,
            MirascıSayisi = 1,
            AsOfDate = Effective
        });

        r.VergilendirilebilirTutar.Should().Be(4_933_065m);
        r.ToplamVergi.Should().Be(589_959.75m);
        r.DilimDetaylar.Should().HaveCount(2);
    }

    [Fact]
    public async Task VerasetSadeceEs_FurugYok_HigherIstisna()
    {
        // Yalnız eş istisna 5.817.845. 6M brüt → 6.000.000 - 5.817.845 = 182.155 vergilendirilebilir.
        // Dilim 1: 182.155 × %1 = 1.821,55.
        var (calc, ctx) = Build();
        await using var _ = ctx;

        var r = await calc.CalculateAsync(new VerasetVergisiInput
        {
            IntikalTuru = IntikalTuru.VerasetSadeceEs,
            BrutDeger = 6_000_000m,
            MirascıSayisi = 1,
            AsOfDate = Effective
        });

        r.KisiBasinaIstisna.Should().Be(5_817_845m);
        r.VergilendirilebilirTutar.Should().Be(182_155m);
        r.ToplamVergi.Should().Be(1_821.55m);
    }

    [Fact]
    public async Task MirascıSayisi_Multiplier_ScalesIstisna()
    {
        // 30M brüt, 5 mirasçı: istisna 5 × 2.907.136 = 14.535.680 → vergilendirilebilir 15.464.320.
        //   Dilim 1: 3M × %1 = 30.000
        //   Dilim 2: 7M × %3 = 210.000
        //   Dilim 3: 5.464.320 × %5 = 273.216
        //   Toplam: 513.216
        var (calc, ctx) = Build();
        await using var _ = ctx;

        var r = await calc.CalculateAsync(new VerasetVergisiInput
        {
            IntikalTuru = IntikalTuru.VerasetFurugVeEs,
            BrutDeger = 30_000_000m,
            MirascıSayisi = 5,
            AsOfDate = Effective
        });

        r.ToplamIstisna.Should().Be(14_535_680m);
        r.VergilendirilebilirTutar.Should().Be(15_464_320m);
        r.ToplamVergi.Should().Be(513_216m);
    }

    [Fact]
    public async Task ValidationErrors_NegativeValues()
    {
        var (calc, ctx) = Build();
        await using var _ = ctx;

        var r = await calc.CalculateAsync(new VerasetVergisiInput
        {
            IntikalTuru = IntikalTuru.VerasetFurugVeEs,
            BrutDeger = -1000m,
            MirascıSayisi = 1,
            AsOfDate = Effective
        });

        r.IsValid.Should().BeFalse();
        r.ValidationErrors.Should().ContainKey(nameof(VerasetVergisiInput.BrutDeger));
    }
}
