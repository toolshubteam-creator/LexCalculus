using FluentAssertions;
using LexCalculus.Core.Calculators.Faiz;
using LexCalculus.Core.Entities.Calculators;
using LexCalculus.Infrastructure.Calculators;
using LexCalculus.Tests.TestHelpers;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace LexCalculus.Tests.Calculators;

public class YasalFaizCalculatorTests
{
    private static (YasalFaizCalculator calc, LexCalculus.Infrastructure.Data.ApplicationDbContext ctx) Build()
    {
        var ctx = TestDbContextFactory.Create();
        ctx.Set<FormulaParameter>().AddRange(
            new FormulaParameter { ToolSlug = "yasal-faiz", Key = "yillik-oran", Value = 0.09m, EffectiveDate = new DateTime(2006, 5, 1) },
            new FormulaParameter { ToolSlug = "yasal-faiz", Key = "yillik-oran", Value = 0.15m, EffectiveDate = new DateTime(2018, 4, 4) },
            new FormulaParameter { ToolSlug = "yasal-faiz", Key = "yillik-oran", Value = 0.09m, EffectiveDate = new DateTime(2020, 12, 31) }
        );
        ctx.SaveChanges();

        var rateService = new InterestRateService(ctx, NullLogger<InterestRateService>.Instance);
        return (new YasalFaizCalculator(rateService), ctx);
    }

    [Fact]
    public async Task Tek_Donem_Basit_Faiz()
    {
        var (calc, ctx) = Build();
        await using var _ = ctx;

        var input = new YasalFaizInput
        {
            AnaPara = 10000m,
            BaslangicTarihi = new DateTime(2010, 1, 1),
            HesapTarihi = new DateTime(2011, 1, 1)
        };

        var r = await calc.CalculateAsync(input);

        r.IsValid.Should().BeTrue();
        r.DonemDetaylar.Should().HaveCount(1);
        r.DonemDetaylar[0].YillikOran.Should().Be(0.09m);
        r.ToplamFaiz.Should().BeApproximately(903m, 5m);
        r.ToplamTutar.Should().BeApproximately(10903m, 5m);
    }

    [Fact]
    public async Task Iki_Donem_Faiz_Toplami()
    {
        var (calc, ctx) = Build();
        await using var _ = ctx;

        var input = new YasalFaizInput
        {
            AnaPara = 10000m,
            BaslangicTarihi = new DateTime(2017, 1, 1),
            HesapTarihi = new DateTime(2019, 1, 1)
        };

        var r = await calc.CalculateAsync(input);

        r.IsValid.Should().BeTrue();
        r.DonemDetaylar.Should().HaveCount(2);
        r.ToplamFaiz.Should().BeGreaterThan(0m);
    }

    [Fact]
    public async Task Hesap_Baslangic_Onunde_Validation_Hatasi()
    {
        var (calc, ctx) = Build();
        await using var _ = ctx;

        var input = new YasalFaizInput
        {
            AnaPara = 10000m,
            BaslangicTarihi = new DateTime(2020, 1, 1),
            HesapTarihi = new DateTime(2019, 1, 1)
        };

        var r = await calc.CalculateAsync(input);

        r.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Sifir_Ana_Para_Validation_Hatasi()
    {
        var (calc, ctx) = Build();
        await using var _ = ctx;

        var input = new YasalFaizInput
        {
            AnaPara = 0m,
            BaslangicTarihi = new DateTime(2020, 1, 1),
            HesapTarihi = new DateTime(2021, 1, 1)
        };

        var r = await calc.CalculateAsync(input);

        r.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task GunYili_360_Bazi_Daha_Yuksek_Faiz_Verir()
    {
        var (calc, ctx) = Build();
        await using var _ = ctx;

        var input365 = new YasalFaizInput
        {
            AnaPara = 10000m,
            BaslangicTarihi = new DateTime(2010, 1, 1),
            HesapTarihi = new DateTime(2011, 1, 1),
            GunYili = LexCalculus.Core.Enums.GunYiliBazi.UcYuzAltmisBes
        };
        var input360 = new YasalFaizInput
        {
            AnaPara = 10000m,
            BaslangicTarihi = new DateTime(2010, 1, 1),
            HesapTarihi = new DateTime(2011, 1, 1),
            GunYili = LexCalculus.Core.Enums.GunYiliBazi.UcYuzAltmis
        };

        var r365 = await calc.CalculateAsync(input365);
        var r360 = await calc.CalculateAsync(input360);

        r365.IsValid.Should().BeTrue();
        r360.IsValid.Should().BeTrue();
        r360.ToplamFaiz.Should().BeGreaterThan(r365.ToplamFaiz);
    }

    [Fact]
    public void GunYili_Default_365_Olmali()
    {
        var input = new YasalFaizInput();
        input.GunYili.Should().Be(LexCalculus.Core.Enums.GunYiliBazi.UcYuzAltmisBes);
    }
}
