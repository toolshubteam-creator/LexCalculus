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

public class EcrimisilCalculatorTests : SqlServerTestBase
{
    private static IDistributedCache CreateCache() =>
        new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));

    // 2020-2024 ÜFE seed; 2025+ kasıtlı eksik (eksik-parametre uyarı yolu test edilir).
    private (EcrimisilCalculator calc, LexCalculus.Infrastructure.Data.ApplicationDbContext ctx) Build()
    {
        var ctx = _db.Create();
        ctx.Set<FormulaParameter>().AddRange(
            new FormulaParameter { ToolSlug = "*", Key = "ufe.yillik", Value = 25.7m, EffectiveDate = new DateTime(2020, 1, 1) },
            new FormulaParameter { ToolSlug = "*", Key = "ufe.yillik", Value = 79.9m, EffectiveDate = new DateTime(2021, 1, 1) },
            new FormulaParameter { ToolSlug = "*", Key = "ufe.yillik", Value = 97.7m, EffectiveDate = new DateTime(2022, 1, 1) },
            new FormulaParameter { ToolSlug = "*", Key = "ufe.yillik", Value = 64.8m, EffectiveDate = new DateTime(2023, 1, 1) },
            new FormulaParameter { ToolSlug = "*", Key = "ufe.yillik", Value = 51.3m, EffectiveDate = new DateTime(2024, 1, 1) }
        );
        ctx.SaveChanges();
        var paramService = new FormulaParameterService(ctx, CreateCache(), new NullActivityLogService(), NullLogger<FormulaParameterService>.Instance);
        var calc = new EcrimisilCalculator(paramService);
        return (calc, ctx);
    }

    [Fact]
    public async Task Calculate_SingleYear_NoEscalation()
    {
        var (calc, ctx) = Build();
        await using var _ = ctx;

        var input = new EcrimisilInput
        {
            IsgalBaslangic = new DateTime(2022, 1, 1),
            IsgalBitis = new DateTime(2022, 7, 1), // 6 ay (Oca-Haz 2022)
            IlkDonemRayicKira = 1000m,
            DonemTuru = EcrimisilDonemTuru.Aylik
        };

        var r = await calc.CalculateAsync(input);

        r.IsValid.Should().BeTrue();
        r.DonemListesi.Should().HaveCount(1);
        r.DonemListesi[0].AySayisi.Should().Be(6);
        r.DonemListesi[0].GuncelAylikKira.Should().Be(1000m); // başlangıç yılı, artış yok
        r.ToplamEcrimisil.Should().Be(6000m);
    }

    [Fact]
    public async Task Reference_Yargitay1HD_2014_4059_FullUfeEscalationApplied()
    {
        // Yargıtay 1. HD E. 2013/16267 K. 2014/4059: sonraki dönemler için ÜFE
        // artış oranının tamamı yansıtılır; ecrimisil bu miktardan az olamaz.
        var (calc, ctx) = Build();
        await using var _ = ctx;

        var input = new EcrimisilInput
        {
            IsgalBaslangic = new DateTime(2020, 1, 1),
            IsgalBitis = new DateTime(2024, 1, 1), // 2020-2023 tam, 48 ay
            IlkDonemRayicKira = 1000m,
            DonemTuru = EcrimisilDonemTuru.Aylik
        };

        var r = await calc.CalculateAsync(input);

        r.IsValid.Should().BeTrue();
        r.DonemListesi.Should().HaveCount(4);
        r.DonemListesi.Select(d => d.Yil).Should().ContainInOrder(2020, 2021, 2022, 2023);

        // 2023 güncel aylık kira ≈ 1000 × 1.799 × 1.977 × 1.648 ≈ 5861 (±%2)
        var d2023 = r.DonemListesi.First(d => d.Yil == 2023);
        d2023.GuncelAylikKira.Should().BeApproximately(5861m, 120m);

        // ÜFE'nin tamamı yansıdığı için toplam, sabit kira (1000 × 48 = 48.000) toplamından büyüktür.
        r.ToplamEcrimisil.Should().BeGreaterThan(48000m);
    }

    [Fact]
    public async Task Calculate_YillikDonemTuru_DividesByTwelve()
    {
        var (calc, ctx) = Build();
        await using var _ = ctx;

        var input = new EcrimisilInput
        {
            IsgalBaslangic = new DateTime(2022, 1, 1),
            IsgalBitis = new DateTime(2022, 7, 1), // 6 ay
            IlkDonemRayicKira = 12000m,            // yıllık → aylık 1000
            DonemTuru = EcrimisilDonemTuru.Yillik
        };

        var r = await calc.CalculateAsync(input);

        r.IsValid.Should().BeTrue();
        r.DonemListesi[0].GuncelAylikKira.Should().Be(1000m);
        r.ToplamEcrimisil.Should().Be(6000m);
    }

    [Fact]
    public async Task Calculate_EndBeforeStart_ReturnsValidationError()
    {
        var (calc, ctx) = Build();
        await using var _ = ctx;

        var input = new EcrimisilInput
        {
            IsgalBaslangic = new DateTime(2023, 1, 1),
            IsgalBitis = new DateTime(2022, 1, 1),
            IlkDonemRayicKira = 1000m
        };

        var r = await calc.CalculateAsync(input);

        r.IsValid.Should().BeFalse();
        r.ValidationErrors.Should().ContainKey(nameof(EcrimisilInput.IsgalBitis));
    }

    [Fact]
    public async Task Calculate_NonPositiveRent_ReturnsValidationError()
    {
        var (calc, ctx) = Build();
        await using var _ = ctx;

        var input = new EcrimisilInput
        {
            IsgalBaslangic = new DateTime(2022, 1, 1),
            IsgalBitis = new DateTime(2023, 1, 1),
            IlkDonemRayicKira = 0m
        };

        var r = await calc.CalculateAsync(input);

        r.IsValid.Should().BeFalse();
        r.ValidationErrors.Should().ContainKey(nameof(EcrimisilInput.IlkDonemRayicKira));
    }

    [Fact]
    public async Task Calculate_MissingUfeForYear_WarnsAndDoesNotEscalate()
    {
        var (calc, ctx) = Build();
        await using var _ = ctx;

        // 2025 ÜFE seed'de yok → o yıl için artış uygulanmaz + uyarı.
        var input = new EcrimisilInput
        {
            IsgalBaslangic = new DateTime(2024, 1, 1),
            IsgalBitis = new DateTime(2026, 1, 1), // 2024 + 2025
            IlkDonemRayicKira = 1000m,
            DonemTuru = EcrimisilDonemTuru.Aylik
        };

        var r = await calc.CalculateAsync(input);

        r.IsValid.Should().BeTrue();
        r.Warnings.Should().Contain(w => w.Contains("2025") && w.Contains("ÜFE"));
        var d2024 = r.DonemListesi.First(d => d.Yil == 2024);
        var d2025 = r.DonemListesi.First(d => d.Yil == 2025);
        d2025.GuncelAylikKira.Should().Be(d2024.GuncelAylikKira); // artış yok
    }

    [Fact]
    public async Task Calculate_MultiYear_ProducesAscendingYearlyPeriods()
    {
        var (calc, ctx) = Build();
        await using var _ = ctx;

        var input = new EcrimisilInput
        {
            IsgalBaslangic = new DateTime(2021, 1, 1),
            IsgalBitis = new DateTime(2024, 1, 1), // 2021, 2022, 2023
            IlkDonemRayicKira = 2000m,
            DonemTuru = EcrimisilDonemTuru.Aylik
        };

        var r = await calc.CalculateAsync(input);

        r.IsValid.Should().BeTrue();
        r.DonemListesi.Should().HaveCount(3);
        r.DonemListesi.Select(d => d.Yil).Should().ContainInOrder(2021, 2022, 2023);
        // Her yıl bir önceki yıldan büyük güncel kira (ÜFE > 0)
        r.DonemListesi[1].GuncelAylikKira.Should().BeGreaterThan(r.DonemListesi[0].GuncelAylikKira);
        r.DonemListesi[2].GuncelAylikKira.Should().BeGreaterThan(r.DonemListesi[1].GuncelAylikKira);
    }
}
