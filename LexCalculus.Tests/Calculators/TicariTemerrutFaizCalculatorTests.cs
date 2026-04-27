using FluentAssertions;
using LexCalculus.Core.Calculators.Faiz;
using LexCalculus.Core.Entities.Calculators;
using LexCalculus.Infrastructure.Calculators;
using LexCalculus.Tests.TestHelpers;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace LexCalculus.Tests.Calculators;

public class TicariTemerrutFaizCalculatorTests
{
    private static (TicariTemerrutFaizCalculator calc, LexCalculus.Infrastructure.Data.ApplicationDbContext ctx) Build()
    {
        var ctx = TestDbContextFactory.Create();
        // Yasal faiz: 2 oran
        ctx.Set<FormulaParameter>().AddRange(
            new FormulaParameter { ToolSlug = "yasal-faiz", Key = "yillik-oran", Value = 0.09m, EffectiveDate = new DateTime(2006, 1, 1) },
            new FormulaParameter { ToolSlug = "yasal-faiz", Key = "yillik-oran", Value = 0.24m, EffectiveDate = new DateTime(2024, 6, 1) },
            // TCMB avans: gerçek tarihçe (test için yeterli alt küme)
            new FormulaParameter { ToolSlug = "tcmb-avans", Key = "yillik-oran", Value = 0.1075m, EffectiveDate = new DateTime(2022, 12, 31) },
            new FormulaParameter { ToolSlug = "tcmb-avans", Key = "yillik-oran", Value = 0.1675m, EffectiveDate = new DateTime(2023, 6, 24) },
            new FormulaParameter { ToolSlug = "tcmb-avans", Key = "yillik-oran", Value = 0.4425m, EffectiveDate = new DateTime(2023, 12, 23) },
            new FormulaParameter { ToolSlug = "tcmb-avans", Key = "yillik-oran", Value = 0.5175m, EffectiveDate = new DateTime(2024, 4, 1) }
        );
        ctx.SaveChanges();

        var rateService = new InterestRateService(ctx, NullLogger<InterestRateService>.Instance);
        var ticariService = new Three095CommercialRateService(ctx, NullLogger<Three095CommercialRateService>.Instance);
        return (new TicariTemerrutFaizCalculator(rateService, ticariService), ctx);
    }

    [Fact]
    public async Task Paralel_Hesap_Iki_Sonuc_Uretir()
    {
        var (calc, ctx) = Build();
        await using var _ = ctx;

        var input = new TicariTemerrutFaizInput
        {
            AnaPara = 100000m,
            BaslangicTarihi = new DateTime(2023, 1, 1),
            HesapTarihi = new DateTime(2023, 12, 31)
        };

        var r = await calc.CalculateAsync(input);

        r.IsValid.Should().BeTrue();
        r.YasalFaizTutari.Should().BeGreaterThan(0m);
        r.TicariFaizTutari.Should().BeGreaterThan(0m);
        r.YasalDonemDetaylar.Should().NotBeEmpty();
        r.TicariDonemDetaylar.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Yuksek_Olan_Onerilir_Ve_Isaretlenir()
    {
        var (calc, ctx) = Build();
        await using var _ = ctx;

        // 2023 boyunca yasal %9, ticari ~%10.75 → ticari daha yüksek
        var input = new TicariTemerrutFaizInput
        {
            AnaPara = 100000m,
            BaslangicTarihi = new DateTime(2023, 1, 1),
            HesapTarihi = new DateTime(2023, 12, 31)
        };

        var r = await calc.CalculateAsync(input);

        r.IsValid.Should().BeTrue();
        r.TicariFaizTutari.Should().BeGreaterThan(r.YasalFaizTutari);
        r.OnerilenSecim.Should().Be("Ticari Temerrüt Faizi");
        r.OnerilenTutar.Should().Be(Math.Round(r.AnaPara + r.TicariFaizTutari, 2));

        // Highlight kontrolü: ticari satırı IsHighlighted olmalı
        var ticariRow = r.Rows.FirstOrDefault(row => row.Key.Contains("Ticari"));
        ticariRow.Should().NotBeNull();
        ticariRow!.IsHighlighted.Should().BeTrue();

        var yasalRow = r.Rows.FirstOrDefault(row => row.Key.Contains("Yasal"));
        yasalRow.Should().NotBeNull();
        yasalRow!.IsHighlighted.Should().BeFalse();
    }

    [Fact]
    public async Task AYM_Yururluk_Sonrasi_Uyari_Verilir()
    {
        var (calc, ctx) = Build();
        await using var _ = ctx;

        var input = new TicariTemerrutFaizInput
        {
            AnaPara = 100000m,
            BaslangicTarihi = new DateTime(2025, 1, 1),
            HesapTarihi = new DateTime(2026, 12, 1)
        };

        var r = await calc.CalculateAsync(input);

        r.IsValid.Should().BeTrue();
        r.Warnings.Should().Contain(w => w.Contains("AYM"));
        r.Warnings.Should().Contain(w => w.Contains("01.08.2026"));
    }

    [Fact]
    public async Task Sifir_Ana_Para_Validation_Hatasi()
    {
        var (calc, ctx) = Build();
        await using var _ = ctx;

        var input = new TicariTemerrutFaizInput
        {
            AnaPara = 0m,
            BaslangicTarihi = new DateTime(2023, 1, 1),
            HesapTarihi = new DateTime(2023, 12, 31)
        };

        var r = await calc.CalculateAsync(input);

        r.IsValid.Should().BeFalse();
        r.ValidationErrors.Should().ContainKey(nameof(input.AnaPara));
    }

    [Fact]
    public async Task Hesap_Baslangic_Onunde_Validation_Hatasi()
    {
        var (calc, ctx) = Build();
        await using var _ = ctx;

        var input = new TicariTemerrutFaizInput
        {
            AnaPara = 100000m,
            BaslangicTarihi = new DateTime(2024, 1, 1),
            HesapTarihi = new DateTime(2023, 1, 1)
        };

        var r = await calc.CalculateAsync(input);

        r.IsValid.Should().BeFalse();
    }
}
