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

public class ArsaPayiCalculatorTests : SqlServerTestBase
{
    private static readonly DateTime Effective = new(2026, 1, 1);

    private static IDistributedCache CreateCache() =>
        new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));

    private (ArsaPayiCalculator calc, LexCalculus.Infrastructure.Data.ApplicationDbContext ctx) Build()
    {
        var ctx = _db.Create();
        ctx.Set<FormulaParameter>().AddRange(
            new FormulaParameter { ToolSlug = "arsa-payi", Key = "katsayi.mesken", Value = 1.0m, EffectiveDate = Effective },
            new FormulaParameter { ToolSlug = "arsa-payi", Key = "katsayi.dukkan", Value = 1.3m, EffectiveDate = Effective },
            new FormulaParameter { ToolSlug = "arsa-payi", Key = "katsayi.bodrum", Value = 0.6m, EffectiveDate = Effective },
            new FormulaParameter { ToolSlug = "arsa-payi", Key = "katsayi.cati-kati", Value = 0.8m, EffectiveDate = Effective },
            new FormulaParameter { ToolSlug = "arsa-payi", Key = "katsayi.kat.zemin", Value = 1.0m, EffectiveDate = Effective },
            new FormulaParameter { ToolSlug = "arsa-payi", Key = "katsayi.kat.ust-artis-orani", Value = 0.05m, EffectiveDate = Effective }
        );
        ctx.SaveChanges();
        var paramService = new FormulaParameterService(ctx, CreateCache(), new NullActivityLogService(), NullLogger<FormulaParameterService>.Instance);
        var calc = new ArsaPayiCalculator(paramService);
        return (calc, ctx);
    }

    private static BagimsizBolumGirdi Bolum(string tanim, decimal yuzolcumu, KullanimTuru tur = KullanimTuru.Mesken, int kat = 0)
        => new() { Tanim = tanim, Yuzolcumu = yuzolcumu, KullanimTuru = tur, KatNumarasi = kat };

    [Fact]
    public async Task Calculate_StandardApartmentBuilding_HigherFloorsGetLargerShare()
    {
        var (calc, ctx) = Build();
        await using var _ = ctx;

        // 5 eşit yüzölçümlü mesken, zemin → 4. kat. Kat etkisi üst katları büyütür.
        var input = new ArsaPayiInput
        {
            AsOfDate = Effective,
            BagimsizBolumler = new List<BagimsizBolumGirdi>
            {
                Bolum("Zemin", 100m, kat: 0),
                Bolum("1. Kat", 100m, kat: 1),
                Bolum("2. Kat", 100m, kat: 2),
                Bolum("3. Kat", 100m, kat: 3),
                Bolum("4. Kat", 100m, kat: 4)
            }
        };

        var r = await calc.CalculateAsync(input);

        r.IsValid.Should().BeTrue();
        r.Paylar.Should().HaveCount(5);
        // Ağırlıklar: 100, 105, 110, 115, 120 → toplam 550
        r.ToplamAgirliklikDeger.Should().BeApproximately(550m, 0.01m);
        var zemin = r.Paylar.First(p => p.Tanim == "Zemin");
        var dorduncu = r.Paylar.First(p => p.Tanim == "4. Kat");
        dorduncu.Pay1000.Should().BeGreaterThan(zemin.Pay1000);
        zemin.Pay1000.Should().BeApproximately(181.82m, 0.05m);   // 100/550*1000
        dorduncu.Pay1000.Should().BeApproximately(218.18m, 0.05m); // 120/550*1000
    }

    [Fact]
    public async Task Calculate_MixedUsage_DukkanGetsHigherShareThanMesken()
    {
        var (calc, ctx) = Build();
        await using var _ = ctx;

        // Aynı yüzölçümü + aynı kat; sadece kullanım türü farklı.
        var input = new ArsaPayiInput
        {
            AsOfDate = Effective,
            BagimsizBolumler = new List<BagimsizBolumGirdi>
            {
                Bolum("Daire", 100m, KullanimTuru.Mesken, kat: 0),
                Bolum("Dükkan", 100m, KullanimTuru.Dukkan, kat: 0)
            }
        };

        var r = await calc.CalculateAsync(input);

        r.IsValid.Should().BeTrue();
        var mesken = r.Paylar.First(p => p.Tanim == "Daire");
        var dukkan = r.Paylar.First(p => p.Tanim == "Dükkan");
        dukkan.Pay1000.Should().BeGreaterThan(mesken.Pay1000);
        // 100 vs 130 → toplam 230
        mesken.Pay1000.Should().BeApproximately(434.78m, 0.05m);
        dukkan.Pay1000.Should().BeApproximately(565.22m, 0.05m);
    }

    [Fact]
    public async Task Calculate_TotalShares_SumTo1000()
    {
        var (calc, ctx) = Build();
        await using var _ = ctx;

        var input = new ArsaPayiInput
        {
            AsOfDate = Effective,
            BagimsizBolumler = new List<BagimsizBolumGirdi>
            {
                Bolum("A", 85m, KullanimTuru.Mesken, kat: 0),
                Bolum("B", 120m, KullanimTuru.Dukkan, kat: 0),
                Bolum("C", 60m, KullanimTuru.Bodrum, kat: -1),
                Bolum("D", 95m, KullanimTuru.CatiKati, kat: 5)
            }
        };

        var r = await calc.CalculateAsync(input);

        r.IsValid.Should().BeTrue();
        r.ToplamPay.Should().BeApproximately(1000m, 0.5m);
    }

    [Fact]
    public async Task Calculate_EmptyList_ReturnsValidationError()
    {
        var (calc, ctx) = Build();
        await using var _ = ctx;

        var input = new ArsaPayiInput { AsOfDate = Effective, BagimsizBolumler = new List<BagimsizBolumGirdi>() };

        var r = await calc.CalculateAsync(input);

        r.IsValid.Should().BeFalse();
        r.ValidationErrors.Should().ContainKey(nameof(ArsaPayiInput.BagimsizBolumler));
    }

    [Fact]
    public async Task Calculate_NonPositiveArea_ReturnsValidationError()
    {
        var (calc, ctx) = Build();
        await using var _ = ctx;

        var input = new ArsaPayiInput
        {
            AsOfDate = Effective,
            BagimsizBolumler = new List<BagimsizBolumGirdi>
            {
                Bolum("İyi", 100m),
                Bolum("Negatif", -5m)
            }
        };

        var r = await calc.CalculateAsync(input);

        r.IsValid.Should().BeFalse();
        r.ValidationErrors.Keys.Should().Contain(k => k.Contains("[1]") && k.Contains(nameof(BagimsizBolumGirdi.Yuzolcumu)));
    }

    [Fact]
    public async Task Calculate_Bodrum_GetsLowerShareThanMesken()
    {
        var (calc, ctx) = Build();
        await using var _ = ctx;

        // Aynı yüzölçümü + aynı kat (zemin); sadece kullanım türü farklı.
        var input = new ArsaPayiInput
        {
            AsOfDate = Effective,
            BagimsizBolumler = new List<BagimsizBolumGirdi>
            {
                Bolum("Daire", 100m, KullanimTuru.Mesken, kat: 0),
                Bolum("Bodrum", 100m, KullanimTuru.Bodrum, kat: 0)
            }
        };

        var r = await calc.CalculateAsync(input);

        r.IsValid.Should().BeTrue();
        var mesken = r.Paylar.First(p => p.Tanim == "Daire");
        var bodrum = r.Paylar.First(p => p.Tanim == "Bodrum");
        bodrum.Pay1000.Should().BeLessThan(mesken.Pay1000);
    }
}
