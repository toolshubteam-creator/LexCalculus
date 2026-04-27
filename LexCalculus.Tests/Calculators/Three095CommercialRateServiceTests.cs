using FluentAssertions;
using LexCalculus.Core.Entities.Calculators;
using LexCalculus.Infrastructure.Calculators;
using LexCalculus.Tests.TestHelpers;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace LexCalculus.Tests.Calculators;

public class Three095CommercialRateServiceTests
{
    private static (Three095CommercialRateService svc, LexCalculus.Infrastructure.Data.ApplicationDbContext ctx) Build(
        params (DateTime date, decimal rate)[] rates)
    {
        var ctx = TestDbContextFactory.Create();
        foreach (var (date, rate) in rates)
        {
            ctx.Set<FormulaParameter>().Add(new FormulaParameter
            {
                ToolSlug = "tcmb-avans",
                Key = "yillik-oran",
                Value = rate,
                EffectiveDate = date
            });
        }
        ctx.SaveChanges();
        return (new Three095CommercialRateService(ctx, NullLogger<Three095CommercialRateService>.Instance), ctx);
    }

    [Fact]
    public async Task Sadece_Ilk_Yari_Tek_Donem()
    {
        // 31 Aralık 2022'deki oran %10.75
        // Sorgu: 1 Ocak - 30 Haziran 2023 → tek dönem, ilk yarı oranı
        var (svc, ctx) = Build(
            (new DateTime(2022, 12, 31), 0.1075m)
        );
        await using var _ = ctx;

        var periods = await svc.GetCommercialPeriodsAsync(
            new DateTime(2023, 1, 1), new DateTime(2023, 6, 30));

        periods.Should().HaveCount(1);
        periods[0].AnnualRate.Should().Be(0.1075m);
        periods[0].Start.Should().Be(new DateTime(2023, 1, 1));
        periods[0].End.Should().Be(new DateTime(2023, 6, 30));
    }

    [Fact]
    public async Task Bes_Puan_Altinda_Degisim_Birinci_Yari_Devam_Eder()
    {
        // 31 Aralık 2020 → %16.75
        // 30 Haziran 2021 → %15.75 (fark = 1 puan, 5 puanın altında)
        // Beklenen: yıl boyunca %16.75 (tek dönem birleşik)
        var (svc, ctx) = Build(
            (new DateTime(2020, 12, 19), 0.1675m),
            (new DateTime(2021, 6, 1), 0.1575m)
        );
        await using var _ = ctx;

        var periods = await svc.GetCommercialPeriodsAsync(
            new DateTime(2021, 1, 1), new DateTime(2021, 12, 31));

        periods.Should().HaveCount(1);
        periods[0].AnnualRate.Should().Be(0.1675m);
    }

    [Fact]
    public async Task Bes_Puan_Esit_Veya_Ustu_Degisim_Ikinci_Yari_Yeni_Oran()
    {
        // 31 Aralık 2023 → %44.25
        // 30 Haziran 2024 → %51.75 (fark = 7.5 puan, 5 puan üstü)
        // Beklenen: ilk yarı %44.25, ikinci yarı %51.75
        var (svc, ctx) = Build(
            (new DateTime(2023, 12, 23), 0.4425m),
            (new DateTime(2024, 4, 1), 0.5175m)
        );
        await using var _ = ctx;

        var periods = await svc.GetCommercialPeriodsAsync(
            new DateTime(2024, 1, 1), new DateTime(2024, 12, 31));

        periods.Should().HaveCount(2);
        periods[0].AnnualRate.Should().Be(0.4425m);
        periods[0].End.Should().Be(new DateTime(2024, 6, 30));
        periods[1].AnnualRate.Should().Be(0.5175m);
        periods[1].Start.Should().Be(new DateTime(2024, 7, 1));
    }

    [Fact]
    public async Task Yil_Ici_Diger_Degisimler_Yok_Sayilir()
    {
        // 31 Aralık 2022 → %10.75
        // 24 Haziran 2023 → %16.75 (30 Haziran'dan ÖNCE, ama hâlâ Jun 30 sayımına dahil)
        // 1 Eylül 2023 → %26.75 (yıl içi değişim, 3095 m.2'ye göre yok hükmünde)
        // Beklenen: Sadece 31 Aralık 2022 (%10.75) ve 30 Haziran 2023 (%16.75) snapshot'ı kullanılır
        // Fark = 6 puan (5+ ÜSTÜ) → ikinci yarı %16.75; 1 Eylül'deki %26.75 göz ardı edilir
        var (svc, ctx) = Build(
            (new DateTime(2022, 12, 31), 0.1075m),
            (new DateTime(2023, 6, 24), 0.1675m),
            (new DateTime(2023, 9, 1), 0.2675m)
        );
        await using var _ = ctx;

        var periods = await svc.GetCommercialPeriodsAsync(
            new DateTime(2023, 1, 1), new DateTime(2023, 12, 31));

        periods.Should().HaveCount(2);
        periods[0].AnnualRate.Should().Be(0.1075m);
        periods[1].AnnualRate.Should().Be(0.1675m);
        periods.Any(p => p.AnnualRate == 0.2675m).Should().BeFalse();
    }

    [Fact]
    public async Task Coklu_Yil_Donem_Algilar()
    {
        // 31 Aralık 2022 → %10.75
        // 30 Haziran 2023 snapshot → %16.75 (fark 6 puan, 5+) → 2023 H2 = %16.75
        // 31 Aralık 2023 → %44.25 → 2024 H1 = %44.25
        // 30 Haziran 2024 snapshot → %51.75 (fark 7.5 puan, 5+) → 2024 H2 = %51.75
        var (svc, ctx) = Build(
            (new DateTime(2022, 12, 31), 0.1075m),
            (new DateTime(2023, 6, 24), 0.1675m),
            (new DateTime(2023, 12, 23), 0.4425m),
            (new DateTime(2024, 4, 1), 0.5175m)
        );
        await using var _ = ctx;

        var periods = await svc.GetCommercialPeriodsAsync(
            new DateTime(2023, 1, 1), new DateTime(2024, 12, 31));

        periods.Should().HaveCount(4);
        periods[0].AnnualRate.Should().Be(0.1075m);
        periods[1].AnnualRate.Should().Be(0.1675m);
        periods[2].AnnualRate.Should().Be(0.4425m);
        periods[3].AnnualRate.Should().Be(0.5175m);
    }

    [Fact]
    public async Task Bos_Tablo_Bos_Donus()
    {
        var (svc, ctx) = Build();
        await using var _ = ctx;

        var periods = await svc.GetCommercialPeriodsAsync(
            new DateTime(2023, 1, 1), new DateTime(2023, 12, 31));

        periods.Should().BeEmpty();
    }
}
