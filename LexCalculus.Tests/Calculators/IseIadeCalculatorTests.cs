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

public class IseIadeCalculatorTests : SqlServerTestBase
{
    private static IDistributedCache CreateCache() =>
        new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));

    private (IseIadeCalculator calc, LexCalculus.Infrastructure.Data.ApplicationDbContext ctx) Build()
    {
        var ctx = _db.Create();
        ctx.Set<FormulaParameter>().AddRange(
            new FormulaParameter { ToolSlug = "*", Key = "damga-vergisi-orani", Value = 0.00759m, EffectiveDate = new DateTime(2020, 1, 1) },
            new FormulaParameter { ToolSlug = "ihbar-tazminati", Key = "gelir-vergisi-orani-basit", Value = 0.15m, EffectiveDate = new DateTime(2020, 1, 1) }
        );
        ctx.SaveChanges();
        var paramSvc = new FormulaParameterService(ctx, CreateCache(), new NullActivityLogService(), NullLogger<FormulaParameterService>.Instance);
        return (new IseIadeCalculator(paramSvc), ctx);
    }

    [Fact]
    public async Task Reference_Case_4_Ay_Iade_2_Ay_Bosta_30000_Brut()
    {
        var (calc, ctx) = Build();
        await using var _ = ctx;

        var input = new IseIadeInput
        {
            GirisTarihi = new DateTime(2023, 1, 1),
            FesihTarihi = new DateTime(2025, 1, 1),
            KararTarihi = new DateTime(2025, 3, 1),
            BrutAylikUcret = 30000m,
            IadeAyiSayisi = 4,
            IsyeriCalisanSayisi = 50
        };

        var r = await calc.CalculateAsync(input);

        r.IsValid.Should().BeTrue();
        r.IsGuvencesindeMi.Should().BeTrue();
        r.KidemAyi.Should().Be(24);
        r.BostaGecenAy.Should().Be(2);
        r.BostaSinirliAy.Should().Be(2);

        r.IadeTazminati.Should().Be(120000m);
        r.BostaGecenSureUcreti.Should().Be(60000m);
        r.BrutToplam.Should().Be(180000m);

        r.DamgaVergisi.Should().BeApproximately(1366.20m, 0.5m);
        r.GelirVergisi.Should().BeApproximately(27000m, 0.5m);
        r.NetTutar.Should().BeApproximately(151633.80m, 1m);
    }

    [Fact]
    public async Task Bosta_Gecen_Sure_4_Aydan_Fazla_Ise_Sinir_Uygulanir()
    {
        var (calc, ctx) = Build();
        await using var _ = ctx;

        var input = new IseIadeInput
        {
            GirisTarihi = new DateTime(2023, 1, 1),
            FesihTarihi = new DateTime(2025, 1, 1),
            KararTarihi = new DateTime(2025, 9, 1),
            BrutAylikUcret = 30000m,
            IadeAyiSayisi = 4,
            IsyeriCalisanSayisi = 50
        };

        var r = await calc.CalculateAsync(input);

        r.IsValid.Should().BeTrue();
        r.BostaGecenAy.Should().Be(8);
        r.BostaSinirliAy.Should().Be(4);
        r.BostaGecenSureUcreti.Should().Be(120000m);
    }

    [Fact]
    public async Task Az_Calisan_Isyeri_Is_Guvencesi_Disinda_Uyari()
    {
        var (calc, ctx) = Build();
        await using var _ = ctx;

        var input = new IseIadeInput
        {
            GirisTarihi = new DateTime(2023, 1, 1),
            FesihTarihi = new DateTime(2025, 1, 1),
            KararTarihi = new DateTime(2025, 3, 1),
            BrutAylikUcret = 30000m,
            IadeAyiSayisi = 4,
            IsyeriCalisanSayisi = 10
        };

        var r = await calc.CalculateAsync(input);

        r.IsValid.Should().BeTrue();
        r.IsGuvencesindeMi.Should().BeFalse();
        r.Warnings.Should().Contain(w => w.Contains("30"));
    }

    [Fact]
    public async Task Az_Kidem_Is_Guvencesi_Disinda_Uyari()
    {
        var (calc, ctx) = Build();
        await using var _ = ctx;

        var input = new IseIadeInput
        {
            GirisTarihi = new DateTime(2024, 10, 1),
            FesihTarihi = new DateTime(2025, 1, 1),
            KararTarihi = new DateTime(2025, 3, 1),
            BrutAylikUcret = 30000m,
            IadeAyiSayisi = 4,
            IsyeriCalisanSayisi = 50
        };

        var r = await calc.CalculateAsync(input);

        r.IsValid.Should().BeTrue();
        r.IsGuvencesindeMi.Should().BeFalse();
        r.Warnings.Should().Contain(w => w.Contains("6"));
    }

    [Fact]
    public async Task Iade_Ay_Sayisi_4_Den_Az_Validation_Hatasi()
    {
        var (calc, ctx) = Build();
        await using var _ = ctx;

        var input = new IseIadeInput
        {
            GirisTarihi = new DateTime(2023, 1, 1),
            FesihTarihi = new DateTime(2025, 1, 1),
            KararTarihi = new DateTime(2025, 3, 1),
            BrutAylikUcret = 30000m,
            IadeAyiSayisi = 3,
            IsyeriCalisanSayisi = 50
        };

        var r = await calc.CalculateAsync(input);

        r.IsValid.Should().BeFalse();
        r.ValidationErrors.Should().ContainKey(nameof(IseIadeInput.IadeAyiSayisi));
    }

    [Fact]
    public async Task Karar_Tarihi_Fesih_Den_Once_Validation_Hatasi()
    {
        var (calc, ctx) = Build();
        await using var _ = ctx;

        var input = new IseIadeInput
        {
            GirisTarihi = new DateTime(2023, 1, 1),
            FesihTarihi = new DateTime(2025, 6, 1),
            KararTarihi = new DateTime(2025, 1, 1),
            BrutAylikUcret = 30000m,
            IadeAyiSayisi = 4,
            IsyeriCalisanSayisi = 50
        };

        var r = await calc.CalculateAsync(input);

        r.IsValid.Should().BeFalse();
        r.ValidationErrors.Should().ContainKey(nameof(IseIadeInput.KararTarihi));
    }
}
