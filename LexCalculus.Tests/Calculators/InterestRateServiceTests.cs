using FluentAssertions;
using LexCalculus.Core.Entities.Calculators;
using LexCalculus.Infrastructure.Calculators;
using LexCalculus.Tests.TestHelpers;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace LexCalculus.Tests.Calculators;

public class InterestRateServiceTests
{
    private static (InterestRateService svc, LexCalculus.Infrastructure.Data.ApplicationDbContext ctx) Build()
    {
        var ctx = TestDbContextFactory.Create();
        ctx.Set<FormulaParameter>().AddRange(
            new FormulaParameter { ToolSlug = "yasal-faiz", Key = "yillik-oran", Value = 0.09m, EffectiveDate = new DateTime(2006, 5, 1) },
            new FormulaParameter { ToolSlug = "yasal-faiz", Key = "yillik-oran", Value = 0.15m, EffectiveDate = new DateTime(2018, 4, 4) },
            new FormulaParameter { ToolSlug = "yasal-faiz", Key = "yillik-oran", Value = 0.09m, EffectiveDate = new DateTime(2020, 12, 31) }
        );
        ctx.SaveChanges();
        return (new InterestRateService(ctx, NullLogger<InterestRateService>.Instance), ctx);
    }

    [Fact]
    public async Task Tek_Donem_Bir_Period_Doner()
    {
        var (svc, ctx) = Build();
        await using var _ = ctx;

        var periods = await svc.GetRatePeriodsAsync("yasal-faiz", "yillik-oran",
            new DateTime(2010, 1, 1), new DateTime(2015, 1, 1));

        periods.Should().HaveCount(1);
        periods[0].AnnualRate.Should().Be(0.09m);
    }

    [Fact]
    public async Task Iki_Donem_Spans_Rate_Change()
    {
        var (svc, ctx) = Build();
        await using var _ = ctx;

        var periods = await svc.GetRatePeriodsAsync("yasal-faiz", "yillik-oran",
            new DateTime(2017, 1, 1), new DateTime(2019, 1, 1));

        periods.Should().HaveCount(2);
        periods[0].AnnualRate.Should().Be(0.09m);
        periods[0].End.Should().Be(new DateTime(2018, 4, 3));
        periods[1].AnnualRate.Should().Be(0.15m);
        periods[1].Start.Should().Be(new DateTime(2018, 4, 4));
    }

    [Fact]
    public async Task Uc_Donem_Span_Eden_Aralik()
    {
        var (svc, ctx) = Build();
        await using var _ = ctx;

        var periods = await svc.GetRatePeriodsAsync("yasal-faiz", "yillik-oran",
            new DateTime(2017, 1, 1), new DateTime(2021, 1, 1));

        periods.Should().HaveCount(3);
        periods[0].AnnualRate.Should().Be(0.09m);
        periods[1].AnnualRate.Should().Be(0.15m);
        periods[2].AnnualRate.Should().Be(0.09m);
    }

    [Fact]
    public async Task GetRate_Belirli_Tarih_Dogru_Oran()
    {
        var (svc, ctx) = Build();
        await using var _ = ctx;

        var rate = await svc.GetRateAsync("yasal-faiz", "yillik-oran", new DateTime(2019, 6, 1));

        rate.Should().Be(0.15m);
    }

    [Fact]
    public async Task Bilinmeyen_Slug_Bos_Donus()
    {
        var (svc, ctx) = Build();
        await using var _ = ctx;

        var periods = await svc.GetRatePeriodsAsync("bilinmiyor", "yillik-oran",
            new DateTime(2020, 1, 1), new DateTime(2021, 1, 1));

        periods.Should().BeEmpty();
    }
}
