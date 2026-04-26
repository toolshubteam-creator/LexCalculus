using FluentAssertions;
using LexCalculus.Core.Calculators.IsHukuku;
using LexCalculus.Core.Entities.Calculators;
using LexCalculus.Infrastructure.Calculators;
using LexCalculus.Tests.TestHelpers;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace LexCalculus.Tests.Calculators;

public class KidemTazminatiCalculatorTests
{
    private static IDistributedCache CreateCache() =>
        new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));

    private static (KidemTazminatiCalculator calc, LexCalculus.Infrastructure.Data.ApplicationDbContext ctx) Build()
    {
        var ctx = TestDbContextFactory.Create();
        ctx.Set<FormulaParameter>().AddRange(
            new FormulaParameter { ToolSlug = "kidem-tazminati", Key = "tavan", Value = 41828.42m, EffectiveDate = new DateTime(2025, 7, 1) },
            new FormulaParameter { ToolSlug = "kidem-tazminati", Key = "tavan", Value = 53919.68m, EffectiveDate = new DateTime(2026, 1, 1) },
            new FormulaParameter { ToolSlug = "*", Key = "damga-vergisi-orani", Value = 0.00759m, EffectiveDate = new DateTime(2020, 1, 1) }
        );
        ctx.SaveChanges();
        var paramService = new FormulaParameterService(ctx, CreateCache(), NullLogger<FormulaParameterService>.Instance);
        var calc = new KidemTazminatiCalculator(paramService);
        return (calc, ctx);
    }

    [Fact]
    public async Task Reference_Case_5_29_Years_30000_BrutBase_2025_Q4_Returns_Expected_Net()
    {
        var (calc, ctx) = Build();
        await using var _ = ctx;

        var input = new KidemTazminatiInput
        {
            GirisTarihi = new DateTime(2020, 6, 1),
            CikisTarihi = new DateTime(2025, 9, 15),
            BrutAylikUcret = 30000m,
            YanOdemelerAylik = 5000m,
            IhbarDahil = false
        };

        var r = await calc.CalculateAsync(input);

        r.IsValid.Should().BeTrue();
        r.ToplamGun.Should().Be((int)(input.CikisTarihi.Value - input.GirisTarihi.Value).TotalDays);
        r.GiydirilmisUcret.Should().Be(35000m);
        r.TavanAsildi.Should().BeFalse("35000 < 41828.42");
        r.KullanilanTavan.Should().Be(41828.42m);

        r.BrutKidem.Should().BeApproximately(185_260.27m, 1m);
        r.DamgaVergisi.Should().BeApproximately(1_405.62m, 1m);
        r.NetKidem.Should().BeApproximately(r.BrutKidem - r.DamgaVergisi, 0.01m);

        r.IhbarTazminati.Should().BeNull();
    }

    [Fact]
    public async Task Tavan_Asildiginda_Tavan_Degeri_Esas_Alinir_Ve_Uyari_Eklenir()
    {
        var (calc, ctx) = Build();
        await using var _ = ctx;

        var input = new KidemTazminatiInput
        {
            GirisTarihi = new DateTime(2024, 1, 1),
            CikisTarihi = new DateTime(2026, 6, 1),
            BrutAylikUcret = 80000m,
            YanOdemelerAylik = 0m
        };

        var r = await calc.CalculateAsync(input);

        r.IsValid.Should().BeTrue();
        r.TavanAsildi.Should().BeTrue();
        r.KullanilanTavan.Should().Be(53919.68m);
        r.Warnings.Should().NotBeEmpty();
        r.Warnings[0].Should().Contain("tavan");

        var toplamGun = (int)(input.CikisTarihi.Value - input.GirisTarihi.Value).TotalDays;
        var expectedBrut = Math.Round(53919.68m * (decimal)toplamGun / 365m, 2);
        r.BrutKidem.Should().BeApproximately(expectedBrut, 1m);
    }

    [Fact]
    public async Task Bir_Yildan_Az_Calismada_Validation_Hatasi_Doner()
    {
        var (calc, ctx) = Build();
        await using var _ = ctx;

        var input = new KidemTazminatiInput
        {
            GirisTarihi = new DateTime(2025, 1, 1),
            CikisTarihi = new DateTime(2025, 6, 1),
            BrutAylikUcret = 30000m
        };

        var r = await calc.CalculateAsync(input);

        r.IsValid.Should().BeFalse();
        r.ValidationErrors.Should().ContainKey(nameof(KidemTazminatiInput.CikisTarihi));
        r.ValidationErrors[nameof(KidemTazminatiInput.CikisTarihi)].Should().Contain("1 yıl");
    }

    [Fact]
    public async Task Cikis_Tarihi_Giristen_Once_Validation_Hatasi()
    {
        var (calc, ctx) = Build();
        await using var _ = ctx;

        var input = new KidemTazminatiInput
        {
            GirisTarihi = new DateTime(2026, 1, 1),
            CikisTarihi = new DateTime(2025, 1, 1),
            BrutAylikUcret = 30000m
        };

        var r = await calc.CalculateAsync(input);

        r.IsValid.Should().BeFalse();
        r.ValidationErrors.Should().ContainKey(nameof(KidemTazminatiInput.CikisTarihi));
    }

    [Fact]
    public async Task Ihbar_Dahil_Edildiginde_Hesaplanir_Ve_Hafta_Tablosu_Dogru()
    {
        var (calc, ctx) = Build();
        await using var _ = ctx;

        var input = new KidemTazminatiInput
        {
            GirisTarihi = new DateTime(2020, 1, 1),
            CikisTarihi = new DateTime(2025, 9, 1),
            BrutAylikUcret = 30000m,
            IhbarDahil = true
        };

        var r = await calc.CalculateAsync(input);

        r.IsValid.Should().BeTrue();
        r.IhbarHaftasi.Should().Be(8);
        r.IhbarTazminati.Should().Be(56000m);
    }

    [Fact]
    public async Task Eksik_Tavan_Parametresinde_Anlamli_Exception()
    {
        var ctx = TestDbContextFactory.Create();
        ctx.Set<FormulaParameter>().Add(
            new FormulaParameter { ToolSlug = "*", Key = "damga-vergisi-orani", Value = 0.00759m, EffectiveDate = new DateTime(2020, 1, 1) }
        );
        await ctx.SaveChangesAsync();
        var paramSvc = new FormulaParameterService(ctx, CreateCache(), NullLogger<FormulaParameterService>.Instance);
        var calc = new KidemTazminatiCalculator(paramSvc);

        var input = new KidemTazminatiInput
        {
            GirisTarihi = new DateTime(2020, 1, 1),
            CikisTarihi = new DateTime(2025, 1, 1),
            BrutAylikUcret = 30000m
        };

        var act = async () => await calc.CalculateAsync(input);

        var ex = await act.Should().ThrowAsync<InvalidOperationException>();
        ex.Which.Message.Should().Contain("tavan");

        await ctx.DisposeAsync();
    }
}
