using FluentAssertions;
using LexCalculus.Core.Calculators.Faiz;
using LexCalculus.Core.Entities.Calculators;
using LexCalculus.Core.Enums;
using LexCalculus.Infrastructure.Services;
using LexCalculus.Tests.TestHelpers;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace LexCalculus.Tests.Calculators;

public class KiraArtisiCalculatorTests
{
    private static (KiraArtisiCalculator calc, LexCalculus.Infrastructure.Data.ApplicationDbContext ctx) Build()
    {
        var ctx = TestDbContextFactory.Create();
        ctx.Set<FormulaParameter>().AddRange(
            new FormulaParameter { ToolSlug = "tufe-12-ay-ort", Key = "2024-02", Value = 55.91m, EffectiveDate = new DateTime(2024, 2, 1) },
            new FormulaParameter { ToolSlug = "tufe-12-ay-ort", Key = "2024-08", Value = 64.91m, EffectiveDate = new DateTime(2024, 8, 1) },
            new FormulaParameter { ToolSlug = "tufe-12-ay-ort", Key = "2025-04", Value = 48.73m, EffectiveDate = new DateTime(2025, 4, 1) },
            new FormulaParameter { ToolSlug = "tufe-12-ay-ort", Key = "2026-03", Value = 32.82m, EffectiveDate = new DateTime(2026, 3, 1) }
        );
        ctx.SaveChanges();

        var svc = new TUFEService(ctx, NullLogger<TUFEService>.Instance);
        return (new KiraArtisiCalculator(svc), ctx);
    }

    [Fact]
    public async Task Konut_TUFE_Sozlesme_Yok_Tufe_Uygulanir()
    {
        var (calc, ctx) = Build();
        await using var _ = ctx;

        var input = new KiraArtisiInput
        {
            MevcutKira = 10000m,
            YenilenmeTarihi = new DateTime(2025, 5, 1),
            MulkTipi = MulkTipi.Konut
        };

        var r = await calc.CalculateAsync(input);

        r.IsValid.Should().BeTrue();
        r.UygulanacakOran.Should().Be(48.73m);
        r.YeniKira.Should().Be(14873m);
        r.YuzdeYirmiBesSiniriUygulandi.Should().BeFalse();
    }

    [Fact]
    public async Task Sozlesme_Orani_TUFE_Den_Dusuk_Sozlesme_Uygulanir()
    {
        var (calc, ctx) = Build();
        await using var _ = ctx;

        var input = new KiraArtisiInput
        {
            MevcutKira = 10000m,
            YenilenmeTarihi = new DateTime(2025, 5, 1),
            MulkTipi = MulkTipi.Konut,
            SozlesmeOrani = 30m
        };

        var r = await calc.CalculateAsync(input);

        r.IsValid.Should().BeTrue();
        r.UygulanacakOran.Should().Be(30m);
        r.SozlesmeOraniUygulandi.Should().BeTrue();
        r.YeniKira.Should().Be(13000m);
    }

    [Fact]
    public async Task Sozlesme_Orani_TUFE_Den_Yuksek_TUFE_Uygulanir_Uyari()
    {
        var (calc, ctx) = Build();
        await using var _ = ctx;

        var input = new KiraArtisiInput
        {
            MevcutKira = 10000m,
            YenilenmeTarihi = new DateTime(2025, 5, 1),
            MulkTipi = MulkTipi.Konut,
            SozlesmeOrani = 60m
        };

        var r = await calc.CalculateAsync(input);

        r.IsValid.Should().BeTrue();
        r.UygulanacakOran.Should().Be(48.73m);
        r.SozlesmeOraniUygulandi.Should().BeFalse();
        r.YeniKira.Should().Be(14873m);
        r.Warnings.Should().Contain(w => w.Contains("TBK m.344/1"));
    }

    [Fact]
    public async Task Konut_25_Donem_Sinir_Uygulanir()
    {
        var (calc, ctx) = Build();
        await using var _ = ctx;

        var input = new KiraArtisiInput
        {
            MevcutKira = 10000m,
            YenilenmeTarihi = new DateTime(2024, 3, 1),
            MulkTipi = MulkTipi.Konut
        };

        var r = await calc.CalculateAsync(input);

        r.IsValid.Should().BeTrue();
        r.UygulanacakOran.Should().Be(25m);
        r.YuzdeYirmiBesSiniriUygulandi.Should().BeTrue();
        r.YeniKira.Should().Be(12500m);
        r.Warnings.Should().Contain(w => w.Contains("7409"));
    }

    [Fact]
    public async Task Catli_Isyeri_25_Donem_Sinir_Uygulanmaz()
    {
        var (calc, ctx) = Build();
        await using var _ = ctx;

        var input = new KiraArtisiInput
        {
            MevcutKira = 10000m,
            YenilenmeTarihi = new DateTime(2024, 3, 1),
            MulkTipi = MulkTipi.CatliIsyeri
        };

        var r = await calc.CalculateAsync(input);

        r.IsValid.Should().BeTrue();
        r.UygulanacakOran.Should().Be(55.91m);
        r.YuzdeYirmiBesSiniriUygulandi.Should().BeFalse();
        r.YeniKira.Should().Be(15591m);
        r.Warnings.Should().Contain(w => w.Contains("SADECE konut"));
    }

    [Fact]
    public async Task Konut_25_Donem_Sozlesme_Dusuk_Sozlesme_Uygulanir()
    {
        var (calc, ctx) = Build();
        await using var _ = ctx;

        var input = new KiraArtisiInput
        {
            MevcutKira = 12000m,
            YenilenmeTarihi = new DateTime(2024, 3, 1),
            MulkTipi = MulkTipi.Konut,
            SozlesmeOrani = 20m
        };

        var r = await calc.CalculateAsync(input);

        r.IsValid.Should().BeTrue();
        r.UygulanacakOran.Should().Be(20m);
        r.SozlesmeOraniUygulandi.Should().BeTrue();
        r.YuzdeYirmiBesSiniriUygulandi.Should().BeFalse();
        r.YeniKira.Should().Be(14400m);
    }

    [Fact]
    public async Task TUFE_Override_Uygulanir()
    {
        var (calc, ctx) = Build();
        await using var _ = ctx;

        var input = new KiraArtisiInput
        {
            MevcutKira = 10000m,
            YenilenmeTarihi = new DateTime(2026, 6, 1),
            MulkTipi = MulkTipi.Konut,
            TUFEOverride = 30m
        };

        var r = await calc.CalculateAsync(input);

        r.IsValid.Should().BeTrue();
        r.UygulanacakOran.Should().Be(30m);
        r.TUFEKaynagi.Should().Contain("Manuel");
        r.YeniKira.Should().Be(13000m);
        r.Warnings.Should().Contain(w => w.Contains("manuel"));
    }

    [Fact]
    public async Task Eksik_TUFE_Verisi_Validation_Hatasi()
    {
        var (calc, ctx) = Build();
        await using var _ = ctx;

        var input = new KiraArtisiInput
        {
            MevcutKira = 10000m,
            YenilenmeTarihi = new DateTime(2026, 5, 1),
            MulkTipi = MulkTipi.Konut
        };

        var r = await calc.CalculateAsync(input);

        r.IsValid.Should().BeFalse();
        r.ValidationErrors.Should().ContainKey(nameof(input.YenilenmeTarihi));
    }

    [Fact]
    public async Task Donem_Disi_Konut_25_Sinir_Uygulanmaz()
    {
        var (calc, ctx) = Build();
        await using var _ = ctx;

        var input = new KiraArtisiInput
        {
            MevcutKira = 10000m,
            YenilenmeTarihi = new DateTime(2024, 9, 1),
            MulkTipi = MulkTipi.Konut
        };

        var r = await calc.CalculateAsync(input);

        r.IsValid.Should().BeTrue();
        r.UygulanacakOran.Should().Be(64.91m);
        r.YuzdeYirmiBesSiniriUygulandi.Should().BeFalse();
        r.YeniKira.Should().Be(16491m);
    }

    [Fact]
    public async Task Mevcut_Kira_Negatif_Validation_Hatasi()
    {
        var (calc, ctx) = Build();
        await using var _ = ctx;

        var input = new KiraArtisiInput
        {
            MevcutKira = -100m,
            YenilenmeTarihi = new DateTime(2025, 5, 1),
            MulkTipi = MulkTipi.Konut
        };

        var r = await calc.CalculateAsync(input);

        r.IsValid.Should().BeFalse();
        r.ValidationErrors.Should().ContainKey(nameof(input.MevcutKira));
    }
}
