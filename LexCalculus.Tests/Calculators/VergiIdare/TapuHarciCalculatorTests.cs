using FluentAssertions;
using LexCalculus.Core.Calculators.VergiIdare;
using LexCalculus.Core.Entities.Calculators;
using LexCalculus.Infrastructure.Calculators;
using LexCalculus.Infrastructure.Services;
using LexCalculus.Tests.TestHelpers;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace LexCalculus.Tests.Calculators.VergiIdare;

public class TapuHarciCalculatorTests : SqlServerTestBase
{
    private static readonly DateTime Effective = new(2026, 1, 1);

    private static IDistributedCache CreateCache() =>
        new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));

    private (TapuHarciCalculator calc, LexCalculus.Infrastructure.Data.ApplicationDbContext ctx) Build()
    {
        var ctx = _db.Create();
        ctx.Set<FormulaParameter>().Add(new FormulaParameter
        {
            ToolSlug = "tapu-harci",
            Key = "oran",
            Value = 0.02m,
            EffectiveDate = Effective
        });
        ctx.SaveChanges();

        var paramSvc = new FormulaParameterService(ctx, CreateCache(),
            new NullActivityLogService(), NullLogger<FormulaParameterService>.Instance);
        return (new TapuHarciCalculator(paramSvc), ctx);
    }

    [Fact]
    public async Task Standard_1M_HerIkisi_Total4Percent()
    {
        // 1.000.000 × %2 = 20.000 (alıcı) + 20.000 (satıcı) = 40.000.
        var (calc, ctx) = Build();
        await using var _ = ctx;

        var r = await calc.CalculateAsync(new TapuHarciInput
        {
            SatisDegeri = 1_000_000m,
            KisiBasi = TapuHarcKisi.HerIkisi,
            AsOfDate = Effective
        });

        r.IsValid.Should().BeTrue();
        r.HarcOrani.Should().Be(0.02m);
        r.AliciHarc.Should().Be(20_000m);
        r.SaticiHarc.Should().Be(20_000m);
        r.ToplamHarc.Should().Be(40_000m);
    }

    [Fact]
    public async Task SadeceAlici_KisiBasiOption()
    {
        var (calc, ctx) = Build();
        await using var _ = ctx;

        var r = await calc.CalculateAsync(new TapuHarciInput
        {
            SatisDegeri = 500_000m,
            KisiBasi = TapuHarcKisi.Alici,
            AsOfDate = Effective
        });

        r.ToplamHarc.Should().Be(10_000m); // sadece alıcı
        r.AliciHarc.Should().Be(10_000m);
    }

    [Fact]
    public async Task SadeceSatici_KisiBasiOption()
    {
        var (calc, ctx) = Build();
        await using var _ = ctx;

        var r = await calc.CalculateAsync(new TapuHarciInput
        {
            SatisDegeri = 500_000m,
            KisiBasi = TapuHarcKisi.Satici,
            AsOfDate = Effective
        });

        r.ToplamHarc.Should().Be(10_000m); // sadece satıcı
        r.SaticiHarc.Should().Be(10_000m);
    }

    [Fact]
    public async Task HerIkisi_DefaultOption_TotalIsBothSides()
    {
        var (calc, ctx) = Build();
        await using var _ = ctx;

        // Default KisiBasi = HerIkisi.
        var r = await calc.CalculateAsync(new TapuHarciInput
        {
            SatisDegeri = 2_500_000m,
            AsOfDate = Effective
        });

        // 2.500.000 × %2 = 50.000 alıcı + 50.000 satıcı = 100.000.
        r.ToplamHarc.Should().Be(100_000m);
    }

    [Fact]
    public async Task ValidationErrors_NegativeOrZero()
    {
        var (calc, ctx) = Build();
        await using var _ = ctx;

        var r1 = await calc.CalculateAsync(new TapuHarciInput
        {
            SatisDegeri = 0m,
            AsOfDate = Effective
        });
        r1.IsValid.Should().BeFalse();
        r1.ValidationErrors.Should().ContainKey(nameof(TapuHarciInput.SatisDegeri));

        var r2 = await calc.CalculateAsync(new TapuHarciInput
        {
            SatisDegeri = -100m,
            AsOfDate = Effective
        });
        r2.IsValid.Should().BeFalse();
    }
}
