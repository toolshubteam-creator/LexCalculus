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

public class IhbarTazminatiCalculatorTests : SqlServerTestBase
{
    private static IDistributedCache CreateCache() =>
        new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));

    private (IhbarTazminatiCalculator calc, LexCalculus.Infrastructure.Data.ApplicationDbContext ctx) Build()
    {
        var ctx = _db.Create();
        ctx.Set<FormulaParameter>().AddRange(
            new FormulaParameter { ToolSlug = "*", Key = "damga-vergisi-orani", Value = 0.00759m, EffectiveDate = new DateTime(2020, 1, 1) },
            new FormulaParameter { ToolSlug = "ihbar-tazminati", Key = "gelir-vergisi-orani-basit", Value = 0.15m, EffectiveDate = new DateTime(2020, 1, 1) }
        );
        ctx.SaveChanges();
        var paramSvc = new FormulaParameterService(ctx, CreateCache(), new NullActivityLogService(), NullLogger<FormulaParameterService>.Instance);
        var calc = new IhbarTazminatiCalculator(paramSvc);
        return (calc, ctx);
    }

    [Theory]
    [InlineData(60, 2)]
    [InlineData(180, 2)]
    [InlineData(200, 4)]
    [InlineData(500, 4)]
    [InlineData(600, 6)]
    [InlineData(1000, 6)]
    [InlineData(1100, 8)]
    [InlineData(3650, 8)]
    public async Task Ihbar_Haftasi_Tablosu_Dogru_Calismali(int gunSayisi, int beklenenHafta)
    {
        var (calc, ctx) = Build();
        await using var _ = ctx;

        var input = new IhbarTazminatiInput
        {
            GirisTarihi = new DateTime(2020, 1, 1),
            CikisTarihi = new DateTime(2020, 1, 1).AddDays(gunSayisi),
            BrutAylikUcret = 30000m
        };

        var r = await calc.CalculateAsync(input);

        r.IsValid.Should().BeTrue();
        r.IhbarHaftasi.Should().Be(beklenenHafta);
    }

    [Fact]
    public async Task Reference_Case_8_Hafta_30000_Brut_Hesap()
    {
        var (calc, ctx) = Build();
        await using var _ = ctx;

        var input = new IhbarTazminatiInput
        {
            GirisTarihi = new DateTime(2020, 1, 1),
            CikisTarihi = new DateTime(2025, 1, 1),
            BrutAylikUcret = 30000m
        };

        var r = await calc.CalculateAsync(input);

        r.IsValid.Should().BeTrue();
        r.IhbarHaftasi.Should().Be(8);
        r.IhbarGunu.Should().Be(56);
        r.GunlukUcret.Should().Be(1000m);
        r.BrutIhbar.Should().Be(56000m);

        r.DamgaVergisi.Should().BeApproximately(425.04m, 0.5m);
        r.GelirVergisi.Should().BeApproximately(8400m, 0.5m);
        r.NetIhbar.Should().BeApproximately(47174.96m, 1m);
    }

    [Fact]
    public async Task Bir_Yildan_Az_Calismada_Bile_Ihbar_Hak_Edilebilir()
    {
        var (calc, ctx) = Build();
        await using var _ = ctx;

        var input = new IhbarTazminatiInput
        {
            GirisTarihi = new DateTime(2025, 1, 1),
            CikisTarihi = new DateTime(2025, 4, 1),
            BrutAylikUcret = 30000m
        };

        var r = await calc.CalculateAsync(input);

        r.IsValid.Should().BeTrue();
        r.IhbarHaftasi.Should().Be(2);
        r.IhbarGunu.Should().Be(14);
        r.BrutIhbar.Should().Be(14000m);
    }

    [Fact]
    public async Task Cikis_Giristen_Once_Validation_Hatasi()
    {
        var (calc, ctx) = Build();
        await using var _ = ctx;

        var input = new IhbarTazminatiInput
        {
            GirisTarihi = new DateTime(2026, 1, 1),
            CikisTarihi = new DateTime(2025, 1, 1),
            BrutAylikUcret = 30000m
        };

        var r = await calc.CalculateAsync(input);

        r.IsValid.Should().BeFalse();
        r.ValidationErrors.Should().ContainKey(nameof(IhbarTazminatiInput.CikisTarihi));
    }

    [Fact]
    public async Task Negatif_Brut_Validation_Hatasi()
    {
        var (calc, ctx) = Build();
        await using var _ = ctx;

        var input = new IhbarTazminatiInput
        {
            GirisTarihi = new DateTime(2024, 1, 1),
            CikisTarihi = new DateTime(2025, 1, 1),
            BrutAylikUcret = -100m
        };

        var r = await calc.CalculateAsync(input);

        r.IsValid.Should().BeFalse();
        r.ValidationErrors.Should().ContainKey(nameof(IhbarTazminatiInput.BrutAylikUcret));
    }
}
