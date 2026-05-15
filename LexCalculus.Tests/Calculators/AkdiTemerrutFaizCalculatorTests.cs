using FluentAssertions;
using LexCalculus.Core.Calculators.Faiz;
using LexCalculus.Core.Entities.Calculators;
using LexCalculus.Core.Enums;
using LexCalculus.Infrastructure.Calculators;
using LexCalculus.Tests.TestHelpers;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace LexCalculus.Tests.Calculators;

public class AkdiTemerrutFaizCalculatorTests : SqlServerTestBase
{
    private (AkdiTemerrutFaizCalculator calc, LexCalculus.Infrastructure.Data.ApplicationDbContext ctx) Build()
    {
        var ctx = _db.Create();
        ctx.Set<FormulaParameter>().AddRange(
            new FormulaParameter { ToolSlug = "tcmb-avans", Key = "yillik-oran", Value = 0.4425m, EffectiveDate = new DateTime(2023, 12, 23) },
            new FormulaParameter { ToolSlug = "tcmb-avans", Key = "yillik-oran", Value = 0.5175m, EffectiveDate = new DateTime(2024, 4, 1) }
        );
        ctx.SaveChanges();

        var ticariSvc = new Three095CommercialRateService(ctx, NullLogger<Three095CommercialRateService>.Instance);
        return (new AkdiTemerrutFaizCalculator(ticariSvc), ctx);
    }

    [Fact]
    public async Task Sozlesme_Orani_3095_Den_Yuksek_Akdi_Uygulanir()
    {
        var (calc, ctx) = Build();
        await using var _ = ctx;

        var input = new AkdiTemerrutFaizInput
        {
            AnaPara = 100000m,
            TemerrutTarihi = new DateTime(2024, 1, 1),
            HesapTarihi = new DateTime(2024, 12, 31),
            SozlesmeOranlari = new List<SozlesmeOranDonem>
            {
                new() { BaslangicTarihi = new DateTime(2024, 1, 1), YillikOran = 60m }
            }
        };

        var r = await calc.CalculateAsync(input);

        r.IsValid.Should().BeTrue();
        r.AkdiFaizTutari.Should().BeGreaterThan(r.Yasal3095FaizTutari);
        r.UcMaddesi120AltSiniri.Should().BeFalse();
        r.UygulanacakSecim.Should().Contain("Sözleşme");
    }

    [Fact]
    public async Task Sozlesme_Orani_3095_Den_Dusuk_Yasal_Uygulanir()
    {
        var (calc, ctx) = Build();
        await using var _ = ctx;

        var input = new AkdiTemerrutFaizInput
        {
            AnaPara = 100000m,
            TemerrutTarihi = new DateTime(2024, 1, 1),
            HesapTarihi = new DateTime(2024, 12, 31),
            SozlesmeOranlari = new List<SozlesmeOranDonem>
            {
                new() { BaslangicTarihi = new DateTime(2024, 1, 1), YillikOran = 5m }
            }
        };

        var r = await calc.CalculateAsync(input);

        r.IsValid.Should().BeTrue();
        r.UcMaddesi120AltSiniri.Should().BeTrue();
        r.UygulanacakSecim.Should().Contain("3095");
        r.Warnings.Should().Contain(w => w.Contains("TBK m.120/2"));
    }

    [Fact]
    public async Task Birden_Fazla_Donem_Calistir()
    {
        var (calc, ctx) = Build();
        await using var _ = ctx;

        var input = new AkdiTemerrutFaizInput
        {
            AnaPara = 100000m,
            TemerrutTarihi = new DateTime(2024, 1, 1),
            HesapTarihi = new DateTime(2024, 12, 31),
            SozlesmeOranlari = new List<SozlesmeOranDonem>
            {
                new() { BaslangicTarihi = new DateTime(2024, 1, 1), YillikOran = 60m },
                new() { BaslangicTarihi = new DateTime(2024, 7, 1), YillikOran = 80m }
            }
        };

        var r = await calc.CalculateAsync(input);

        r.IsValid.Should().BeTrue();
        r.AkdiDonemDetaylar.Should().HaveCount(2);
        r.AkdiDonemDetaylar[0].YillikOran.Should().Be(0.60m);
        r.AkdiDonemDetaylar[1].YillikOran.Should().Be(0.80m);
    }

    [Fact]
    public async Task Bilesik_Faiz_Sadece_Tacirler_Arasi_Uygulanir()
    {
        var (calc, ctx) = Build();
        await using var _ = ctx;

        var inputBilesik = new AkdiTemerrutFaizInput
        {
            AnaPara = 100000m,
            TemerrutTarihi = new DateTime(2024, 1, 1),
            HesapTarihi = new DateTime(2024, 12, 31),
            FaizYontemi = FaizYontemi.Bilesik,
            BilesikDonemi = BilesikDonemi.Aylik,
            TaclrlerArasiYaziliSozlesme = false,
            SozlesmeOranlari = new List<SozlesmeOranDonem>
            {
                new() { BaslangicTarihi = new DateTime(2024, 1, 1), YillikOran = 60m }
            }
        };

        var r = await calc.CalculateAsync(inputBilesik);

        r.IsValid.Should().BeTrue();
        r.Warnings.Should().Contain(w => w.Contains("tacirler arası"));
        r.FaizYontemiAciklama.Should().Be("Basit faiz");
    }

    [Fact]
    public async Task Bilesik_Tacirler_Arasi_Onayli_Bilesik_Uygulanir()
    {
        var (calc, ctx) = Build();
        await using var _ = ctx;

        var input = new AkdiTemerrutFaizInput
        {
            AnaPara = 100000m,
            TemerrutTarihi = new DateTime(2024, 1, 1),
            HesapTarihi = new DateTime(2024, 12, 31),
            FaizYontemi = FaizYontemi.Bilesik,
            BilesikDonemi = BilesikDonemi.Aylik,
            TaclrlerArasiYaziliSozlesme = true,
            SozlesmeOranlari = new List<SozlesmeOranDonem>
            {
                new() { BaslangicTarihi = new DateTime(2024, 1, 1), YillikOran = 30m }
            }
        };

        var r = await calc.CalculateAsync(input);

        r.IsValid.Should().BeTrue();
        r.FaizYontemiAciklama.Should().Contain("Bileşik");
        r.AkdiFaizTutari.Should().BeGreaterThan(34000m);
        r.AkdiFaizTutari.Should().BeLessThan(35000m);
    }

    [Fact]
    public async Task Ahlaka_Aykiri_Yuksek_Oran_Uyari()
    {
        var (calc, ctx) = Build();
        await using var _ = ctx;

        var input = new AkdiTemerrutFaizInput
        {
            AnaPara = 100000m,
            TemerrutTarihi = new DateTime(2024, 1, 1),
            HesapTarihi = new DateTime(2024, 12, 31),
            SozlesmeOranlari = new List<SozlesmeOranDonem>
            {
                new() { BaslangicTarihi = new DateTime(2024, 1, 1), YillikOran = 150m }
            }
        };

        var r = await calc.CalculateAsync(input);

        r.IsValid.Should().BeTrue();
        r.Warnings.Should().Contain(w => w.Contains("TBK m.27"));
    }

    [Fact]
    public async Task Sozlesme_Donemleri_Bos_Validation_Hatasi()
    {
        var (calc, ctx) = Build();
        await using var _ = ctx;

        var input = new AkdiTemerrutFaizInput
        {
            AnaPara = 100000m,
            TemerrutTarihi = new DateTime(2024, 1, 1),
            HesapTarihi = new DateTime(2024, 12, 31),
            SozlesmeOranlari = new List<SozlesmeOranDonem>()
        };

        var r = await calc.CalculateAsync(input);

        r.IsValid.Should().BeFalse();
    }
}
