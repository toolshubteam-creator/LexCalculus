using FluentAssertions;
using LexCalculus.Core.Calculators.Faiz;
using LexCalculus.Core.Entities.Calculators;
using LexCalculus.Core.Enums;
using LexCalculus.Infrastructure.Calculators;
using LexCalculus.Tests.TestHelpers;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace LexCalculus.Tests.Calculators;

public class MenfiTespitFaizCalculatorTests
{
    private static (MenfiTespitFaizCalculator calc, LexCalculus.Infrastructure.Data.ApplicationDbContext ctx) Build()
    {
        var ctx = TestDbContextFactory.Create();
        ctx.Set<FormulaParameter>().AddRange(
            new FormulaParameter { ToolSlug = "yasal-faiz", Key = "yillik-oran", Value = 0.09m, EffectiveDate = new DateTime(2006, 1, 1) },
            new FormulaParameter { ToolSlug = "yasal-faiz", Key = "yillik-oran", Value = 0.24m, EffectiveDate = new DateTime(2024, 6, 1) }
        );
        ctx.SaveChanges();

        var legalSvc = new InterestRateService(ctx, NullLogger<InterestRateService>.Instance);
        return (new MenfiTespitFaizCalculator(legalSvc), ctx);
    }

    [Fact]
    public async Task Kotuniyetli_Alacakli_Tahsil_Tarihinden_Faiz()
    {
        var (calc, ctx) = Build();
        await using var _ = ctx;

        var input = new MenfiTespitFaizInput
        {
            HaksizTahsilTutari = 50000m,
            TahsilTarihi = new DateTime(2024, 1, 1),
            HesapTarihi = new DateTime(2024, 12, 31),
            AlacakliKotuNiyetli = true,
            GunYili = GunYiliBazi.UcYuzAltmisBes
        };

        var r = await calc.CalculateAsync(input);

        r.IsValid.Should().BeTrue();
        r.FaizBaslangicTarihi.Should().Be(new DateTime(2024, 1, 1));
        r.FaizBaslangicAciklama.Should().Contain("kötüniyetli");
        r.FaizTutari.Should().BeGreaterThan(8000m);
        r.FaizTutari.Should().BeLessThan(9500m);
        r.IadeEdilecekToplamTutar.Should().Be(r.HaksizTahsilTutari + r.FaizTutari);
    }

    [Fact]
    public async Task Iyiniyetli_Alacakli_Talep_Tarihinden_Faiz()
    {
        var (calc, ctx) = Build();
        await using var _ = ctx;

        var input = new MenfiTespitFaizInput
        {
            HaksizTahsilTutari = 50000m,
            TahsilTarihi = new DateTime(2024, 1, 1),
            IadeTalepTarihi = new DateTime(2024, 7, 1),
            HesapTarihi = new DateTime(2024, 12, 31),
            AlacakliKotuNiyetli = false,
            GunYili = GunYiliBazi.UcYuzAltmisBes
        };

        var r = await calc.CalculateAsync(input);

        r.IsValid.Should().BeTrue();
        r.FaizBaslangicTarihi.Should().Be(new DateTime(2024, 7, 1));
        r.FaizBaslangicAciklama.Should().Contain("iyiniyetli");
        r.FaizTutari.Should().BeGreaterThan(5800m);
        r.FaizTutari.Should().BeLessThan(6300m);
    }

    [Fact]
    public async Task Iyiniyetli_Alacakli_Talep_Tarihi_Bos_Validation_Hatasi()
    {
        var (calc, ctx) = Build();
        await using var _ = ctx;

        var input = new MenfiTespitFaizInput
        {
            HaksizTahsilTutari = 50000m,
            TahsilTarihi = new DateTime(2024, 1, 1),
            HesapTarihi = new DateTime(2024, 12, 31),
            AlacakliKotuNiyetli = false,
            IadeTalepTarihi = null
        };

        var r = await calc.CalculateAsync(input);

        r.IsValid.Should().BeFalse();
        r.ValidationErrors.Should().ContainKey(nameof(input.IadeTalepTarihi));
    }

    [Fact]
    public async Task Talep_Tarihi_Tahsilden_Once_Validation_Hatasi()
    {
        var (calc, ctx) = Build();
        await using var _ = ctx;

        var input = new MenfiTespitFaizInput
        {
            HaksizTahsilTutari = 50000m,
            TahsilTarihi = new DateTime(2024, 6, 1),
            IadeTalepTarihi = new DateTime(2024, 1, 1),
            HesapTarihi = new DateTime(2024, 12, 31),
            AlacakliKotuNiyetli = false
        };

        var r = await calc.CalculateAsync(input);

        r.IsValid.Should().BeFalse();
        r.ValidationErrors.Should().ContainKey(nameof(input.IadeTalepTarihi));
    }

    [Fact]
    public async Task Hesap_Tarihi_Tahsilden_Once_Validation_Hatasi()
    {
        var (calc, ctx) = Build();
        await using var _ = ctx;

        var input = new MenfiTespitFaizInput
        {
            HaksizTahsilTutari = 50000m,
            TahsilTarihi = new DateTime(2024, 6, 1),
            HesapTarihi = new DateTime(2024, 1, 1)
        };

        var r = await calc.CalculateAsync(input);

        r.IsValid.Should().BeFalse();
        r.ValidationErrors.Should().ContainKey(nameof(input.HesapTarihi));
    }

    [Fact]
    public async Task Negatif_Tutar_Validation_Hatasi()
    {
        var (calc, ctx) = Build();
        await using var _ = ctx;

        var input = new MenfiTespitFaizInput
        {
            HaksizTahsilTutari = -100m,
            TahsilTarihi = new DateTime(2024, 1, 1),
            HesapTarihi = new DateTime(2024, 12, 31)
        };

        var r = await calc.CalculateAsync(input);

        r.IsValid.Should().BeFalse();
        r.ValidationErrors.Should().ContainKey(nameof(input.HaksizTahsilTutari));
    }

    [Fact]
    public async Task AYM_Yururluk_Sonrasi_Hesap_Uyarisi()
    {
        var (calc, ctx) = Build();
        await using var _ = ctx;

        var input = new MenfiTespitFaizInput
        {
            HaksizTahsilTutari = 50000m,
            TahsilTarihi = new DateTime(2026, 1, 1),
            HesapTarihi = new DateTime(2026, 12, 1),
            AlacakliKotuNiyetli = true
        };

        var r = await calc.CalculateAsync(input);

        r.IsValid.Should().BeTrue();
        r.Warnings.Should().Contain(w => w.Contains("AYM") && w.Contains("K.2025/164"));
    }

    [Fact]
    public async Task Tek_Donem_Hesap_2006_Sonrasi()
    {
        var (calc, ctx) = Build();
        await using var _ = ctx;

        var input = new MenfiTespitFaizInput
        {
            HaksizTahsilTutari = 100000m,
            TahsilTarihi = new DateTime(2010, 1, 1),
            HesapTarihi = new DateTime(2010, 12, 31),
            AlacakliKotuNiyetli = true
        };

        var r = await calc.CalculateAsync(input);

        r.IsValid.Should().BeTrue();
        r.DonemDetaylar.Should().HaveCount(1);
        r.FaizTutari.Should().BeApproximately(9000m, 50m);
    }
}
