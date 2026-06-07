using FluentAssertions;
using LexCalculus.Core.Calculators.Gayrimenkul;
using LexCalculus.Core.Entities.Calculators;
using LexCalculus.Infrastructure.Calculators;
using LexCalculus.Tests.TestHelpers;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace LexCalculus.Tests.Calculators;

public class KamulastirmaBedeliCalculatorTests : SqlServerTestBase
{
    private static IDistributedCache CreateCache() =>
        new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));

    private (KamulastirmaBedeliCalculator calc, LexCalculus.Infrastructure.Data.ApplicationDbContext ctx) Build()
    {
        var ctx = _db.Create();
        ctx.Set<FormulaParameter>().AddRange(
            new FormulaParameter { ToolSlug = "kamulastirma-bedeli", Key = "objektif-artis.max-orani", Value = 1.0m, EffectiveDate = new DateTime(2005, 1, 1) },
            new FormulaParameter { ToolSlug = "kamulastirma-bedeli", Key = "kapitalizasyon-orani.default", Value = 0.06m, EffectiveDate = new DateTime(2020, 1, 1) }
        );
        ctx.SaveChanges();
        var paramService = new FormulaParameterService(ctx, CreateCache(), new NullActivityLogService(), NullLogger<FormulaParameterService>.Instance);
        var calc = new KamulastirmaBedeliCalculator(paramService);
        return (calc, ctx);
    }

    [Fact]
    public async Task Calculate_EmsalMethod_AppliesObjectiveIncrease()
    {
        var (calc, ctx) = Build();
        await using var _ = ctx;

        var input = new KamulastirmaBedeliInput
        {
            TasinmazTuru = TasinmazTuru.Arsa,
            Yuzolcumu = 1000m,
            Yontem = KamulastirmaYontemi.EmsalKarsilastirma,
            EmsalBirimFiyat = 5000m,
            ObjektifArtisOrani = 30m
        };

        var r = await calc.CalculateAsync(input);

        r.IsValid.Should().BeTrue();
        r.ObjektifArtisCapllendi.Should().BeFalse();
        // 5000 × 1000 × 1.30 = 6.500.000
        r.ArsaBedeli.Should().Be(6_500_000m);
        r.ToplamBedel.Should().Be(6_500_000m);
        r.KullanilanYontem.Should().Be("Emsal Karşılaştırma");
    }

    [Fact]
    public async Task Calculate_GelirMethod_UsesDefaultCapitalizationRate()
    {
        var (calc, ctx) = Build();
        await using var _ = ctx;

        var input = new KamulastirmaBedeliInput
        {
            TasinmazTuru = TasinmazTuru.Tarim,
            Yuzolcumu = 5000m,
            Yontem = KamulastirmaYontemi.GelirKapitalizasyonu,
            YillikNetGelir = 60000m
            // KapitalizasyonOrani null → varsayılan %6
        };

        var r = await calc.CalculateAsync(input);

        r.IsValid.Should().BeTrue();
        // 60.000 / 0.06 = 1.000.000
        r.ArsaBedeli.Should().Be(1_000_000m);
        r.KullanilanYontem.Should().Be("Gelir Kapitalizasyonu");
    }

    [Fact]
    public async Task Calculate_GelirMethod_ExplicitRate_OverridesDefault()
    {
        var (calc, ctx) = Build();
        await using var _ = ctx;

        var input = new KamulastirmaBedeliInput
        {
            Yuzolcumu = 5000m,
            Yontem = KamulastirmaYontemi.GelirKapitalizasyonu,
            YillikNetGelir = 100000m,
            KapitalizasyonOrani = 8m
        };

        var r = await calc.CalculateAsync(input);

        r.IsValid.Should().BeTrue();
        // 100.000 / 0.08 = 1.250.000
        r.ArsaBedeli.Should().Be(1_250_000m);
    }

    [Fact]
    public async Task Reference_Yargitay5HD_2005_675_ObjectiveIncrease_CappedAt100Percent()
    {
        // Yargıtay 5. HD E. 2004/12813 K. 2005/675: objektif değer artırıcı unsur
        // oranı %100'ü (çarpan 2.0) aşamaz. %150 girilse de %100 uygulanır.
        var (calc, ctx) = Build();
        await using var _ = ctx;

        var input = new KamulastirmaBedeliInput
        {
            Yuzolcumu = 1000m,
            Yontem = KamulastirmaYontemi.EmsalKarsilastirma,
            EmsalBirimFiyat = 5000m,
            ObjektifArtisOrani = 150m // tavanı aşıyor
        };

        var r = await calc.CalculateAsync(input);

        r.IsValid.Should().BeTrue();
        r.ObjektifArtisCapllendi.Should().BeTrue();
        r.ObjektifArtisUygulanan.Should().Be(1.0m); // %100
        // çarpan 1 + 1.0 = 2.0 → 5000 × 1000 × 2.0 = 10.000.000 (150% değil)
        r.ArsaBedeli.Should().Be(10_000_000m);
        r.Warnings.Should().Contain(w => w.Contains("2005/675") && w.Contains("tavan"));
    }

    [Fact]
    public async Task Calculate_WithBuilding_AddsBuildingValue()
    {
        var (calc, ctx) = Build();
        await using var _ = ctx;

        var input = new KamulastirmaBedeliInput
        {
            Yuzolcumu = 1000m,
            Yontem = KamulastirmaYontemi.EmsalKarsilastirma,
            EmsalBirimFiyat = 5000m,
            ObjektifArtisOrani = 0m,
            YapiVar = true,
            YapiAlani = 150m,
            YapiBirimMaliyet = 8000m
        };

        var r = await calc.CalculateAsync(input);

        r.IsValid.Should().BeTrue();
        r.ArsaBedeli.Should().Be(5_000_000m);   // 5000 × 1000 × 1.0
        r.BinaBedeli.Should().Be(1_200_000m);   // 150 × 8000
        r.ToplamBedel.Should().Be(6_200_000m);
    }

    [Fact]
    public async Task Calculate_EmsalMethod_MissingUnitPrice_ReturnsValidationError()
    {
        var (calc, ctx) = Build();
        await using var _ = ctx;

        var input = new KamulastirmaBedeliInput
        {
            Yuzolcumu = 1000m,
            Yontem = KamulastirmaYontemi.EmsalKarsilastirma,
            EmsalBirimFiyat = null
        };

        var r = await calc.CalculateAsync(input);

        r.IsValid.Should().BeFalse();
        r.ValidationErrors.Should().ContainKey(nameof(KamulastirmaBedeliInput.EmsalBirimFiyat));
    }

    [Fact]
    public async Task Calculate_NonPositiveArea_ReturnsValidationError()
    {
        var (calc, ctx) = Build();
        await using var _ = ctx;

        var input = new KamulastirmaBedeliInput
        {
            Yuzolcumu = 0m,
            Yontem = KamulastirmaYontemi.EmsalKarsilastirma,
            EmsalBirimFiyat = 5000m
        };

        var r = await calc.CalculateAsync(input);

        r.IsValid.Should().BeFalse();
        r.ValidationErrors.Should().ContainKey(nameof(KamulastirmaBedeliInput.Yuzolcumu));
    }
}
