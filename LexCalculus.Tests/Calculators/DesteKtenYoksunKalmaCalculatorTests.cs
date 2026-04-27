using FluentAssertions;
using LexCalculus.Core.Calculators.Akturya;
using LexCalculus.Core.Entities.Calculators;
using LexCalculus.Core.Enums;
using LexCalculus.Infrastructure.Calculators;
using LexCalculus.Tests.TestHelpers;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace LexCalculus.Tests.Calculators;

public class DesteKtenYoksunKalmaCalculatorTests
{
    private static IDistributedCache CreateCache() =>
        new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));

    private static (DesteKtenYoksunKalmaCalculator calc, LexCalculus.Infrastructure.Data.ApplicationDbContext ctx) Build()
    {
        var ctx = TestDbContextFactory.Create();
        var trh = new LifeTable
        {
            Code = "TRH-2010",
            Name = "TRH 2010",
            EffectiveDate = new DateTime(2010, 1, 1),
            IsActive = true,
            Rows = new List<LifeTableRow>
            {
                new() { Yas = 30, Cinsiyet = Cinsiyet.Erkek, BekledigiYasam = 44.45m },
                new() { Yas = 30, Cinsiyet = Cinsiyet.Kadin, BekledigiYasam = 49.00m },
                new() { Yas = 40, Cinsiyet = Cinsiyet.Erkek, BekledigiYasam = 34.93m },
                new() { Yas = 40, Cinsiyet = Cinsiyet.Kadin, BekledigiYasam = 39.23m }
            }
        };
        ctx.Set<LifeTable>().Add(trh);
        ctx.SaveChanges();

        var lifeTable = new LifeTableService(ctx, CreateCache(), NullLogger<LifeTableService>.Instance);
        var actuarial = new ActuarialService();
        var calc = new DesteKtenYoksunKalmaCalculator(lifeTable, actuarial);
        return (calc, ctx);
    }

    [Fact]
    public async Task Sadece_Es_Hesabi_Pozitif_Tutar_Doner()
    {
        var (calc, ctx) = Build();
        await using var _ = ctx;

        var input = new DesteKtenYoksunKalmaInput
        {
            OlayTarihi = new DateTime(2025, 1, 1),
            OlenDogumTarihi = new DateTime(1995, 1, 1),
            OlenCinsiyet = Cinsiyet.Erkek,
            AylikGelir = 30000m,
            EsVarMi = true,
            EsDogumTarihi = new DateTime(1997, 1, 1),
            EsCinsiyet = Cinsiyet.Kadin,
            YillikIskontoOrani = 3.0m,
            PasifDonemGelirOrani = 50
        };

        var r = await calc.CalculateAsync(input);

        r.IsValid.Should().BeTrue();
        r.OlenYas.Should().Be(30);
        r.OlenBekledigiYasam.Should().Be(44.45m);
        r.AktifYil.Should().Be(35);
        r.PasifYil.Should().Be(9);
        r.EsPay.Should().Be(0.50m);
        r.EsToplamTutar.Should().BeGreaterThan(0m);
        r.CocuklarToplamTutar.Should().Be(0m);
    }

    [Fact]
    public async Task Bireysel_Cocuk_Yaslari_Farkli_Destek_Sureleri()
    {
        var (calc, ctx) = Build();
        await using var _ = ctx;

        var input = new DesteKtenYoksunKalmaInput
        {
            OlayTarihi = new DateTime(2025, 1, 1),
            OlenDogumTarihi = new DateTime(1985, 1, 1),
            OlenCinsiyet = Cinsiyet.Erkek,
            AylikGelir = 30000m,
            EsVarMi = true,
            EsDogumTarihi = new DateTime(1987, 1, 1),
            Cocuklar = new List<CocukInput>
            {
                new() { DogumTarihi = new DateTime(2020, 1, 1), Cinsiyet = Cinsiyet.Erkek, Ogrenci = false },
                new() { DogumTarihi = new DateTime(2010, 1, 1), Cinsiyet = Cinsiyet.Kadin, Ogrenci = false }
            }
        };

        var r = await calc.CalculateAsync(input);

        r.IsValid.Should().BeTrue();
        r.CocukSayisi.Should().Be(2);
        r.CocukDetaylar.Should().HaveCount(2);

        r.CocukDetaylar[0].OlayAnindakiYas.Should().Be(5);
        r.CocukDetaylar[0].DestekSuresi.Should().Be(13);
        r.CocukDetaylar[0].Cinsiyet.Should().Be(Cinsiyet.Erkek);

        r.CocukDetaylar[1].OlayAnindakiYas.Should().Be(15);
        r.CocukDetaylar[1].DestekSuresi.Should().Be(7);
        r.CocukDetaylar[1].Cinsiyet.Should().Be(Cinsiyet.Kadin);

        r.CocukDetaylar[0].Tutar.Should().NotBe(r.CocukDetaylar[1].Tutar);
        r.CocukDetaylar[0].Tutar.Should().BeGreaterThan(r.CocukDetaylar[1].Tutar,
            "5 yaşındaki erkek 13 yıl destek alır, 15 yaşındaki kız 7 yıl");
    }

    [Fact]
    public async Task Ogrenci_Cocuk_25_Yasa_Kadar_Destek()
    {
        var (calc, ctx) = Build();
        await using var _ = ctx;

        var input = new DesteKtenYoksunKalmaInput
        {
            OlayTarihi = new DateTime(2025, 1, 1),
            OlenDogumTarihi = new DateTime(1985, 1, 1),
            OlenCinsiyet = Cinsiyet.Erkek,
            AylikGelir = 30000m,
            Cocuklar = new List<CocukInput>
            {
                new() { DogumTarihi = new DateTime(2010, 1, 1), Cinsiyet = Cinsiyet.Erkek, Ogrenci = true }
            }
        };

        var r = await calc.CalculateAsync(input);

        r.IsValid.Should().BeTrue();
        r.CocukDetaylar[0].BagimsizlikYasi.Should().Be(25);
        r.CocukDetaylar[0].DestekSuresi.Should().Be(10);
    }

    [Fact]
    public async Task Ogrenci_Kiz_Cocuk_25_Yasa_Kadar_Destek()
    {
        var (calc, ctx) = Build();
        await using var _ = ctx;

        var input = new DesteKtenYoksunKalmaInput
        {
            OlayTarihi = new DateTime(2025, 1, 1),
            OlenDogumTarihi = new DateTime(1985, 1, 1),
            OlenCinsiyet = Cinsiyet.Erkek,
            AylikGelir = 30000m,
            Cocuklar = new List<CocukInput>
            {
                new() { DogumTarihi = new DateTime(2007, 1, 1), Cinsiyet = Cinsiyet.Kadin, Ogrenci = true }
            }
        };

        var r = await calc.CalculateAsync(input);

        r.IsValid.Should().BeTrue();
        r.CocukDetaylar[0].BagimsizlikYasi.Should().Be(25);
        r.CocukDetaylar[0].DestekSuresi.Should().Be(7);
    }

    [Fact]
    public async Task Es_Plus_2_Cocuk_Pay_Tablosu_20_20_20()
    {
        var (calc, ctx) = Build();
        await using var _ = ctx;

        var input = new DesteKtenYoksunKalmaInput
        {
            OlayTarihi = new DateTime(2025, 1, 1),
            OlenDogumTarihi = new DateTime(1985, 1, 1),
            OlenCinsiyet = Cinsiyet.Erkek,
            AylikGelir = 30000m,
            EsVarMi = true,
            EsDogumTarihi = new DateTime(1987, 1, 1),
            Cocuklar = new List<CocukInput>
            {
                new() { DogumTarihi = new DateTime(2017, 1, 1), Cinsiyet = Cinsiyet.Erkek },
                new() { DogumTarihi = new DateTime(2019, 1, 1), Cinsiyet = Cinsiyet.Kadin }
            }
        };

        var r = await calc.CalculateAsync(input);

        r.EsPay.Should().Be(0.20m);
        r.CocukPayPerCocuk.Should().Be(0.20m);
    }

    [Fact]
    public async Task Sadece_Cocuk_Toplam_Yarisi_Esit_Bolunur()
    {
        var (calc, ctx) = Build();
        await using var _ = ctx;

        var input = new DesteKtenYoksunKalmaInput
        {
            OlayTarihi = new DateTime(2025, 1, 1),
            OlenDogumTarihi = new DateTime(1985, 1, 1),
            OlenCinsiyet = Cinsiyet.Erkek,
            AylikGelir = 30000m,
            EsVarMi = false,
            Cocuklar = new List<CocukInput>
            {
                new() { DogumTarihi = new DateTime(2015, 1, 1), Cinsiyet = Cinsiyet.Erkek },
                new() { DogumTarihi = new DateTime(2015, 1, 1), Cinsiyet = Cinsiyet.Erkek }
            }
        };

        var r = await calc.CalculateAsync(input);

        r.EsPay.Should().Be(0m);
        r.CocukPayPerCocuk.Should().Be(0.25m);
    }

    [Fact]
    public async Task Es_Yok_Cocuk_Yok_Validation_Hatasi()
    {
        var (calc, ctx) = Build();
        await using var _ = ctx;

        var input = new DesteKtenYoksunKalmaInput
        {
            OlayTarihi = new DateTime(2025, 1, 1),
            OlenDogumTarihi = new DateTime(1985, 1, 1),
            OlenCinsiyet = Cinsiyet.Erkek,
            AylikGelir = 30000m,
            EsVarMi = false,
            Cocuklar = new List<CocukInput>()
        };

        var r = await calc.CalculateAsync(input);

        r.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Cocuk_Dogum_Olay_Sonrasi_Validation_Hatasi()
    {
        var (calc, ctx) = Build();
        await using var _ = ctx;

        var input = new DesteKtenYoksunKalmaInput
        {
            OlayTarihi = new DateTime(2020, 1, 1),
            OlenDogumTarihi = new DateTime(1985, 1, 1),
            OlenCinsiyet = Cinsiyet.Erkek,
            AylikGelir = 30000m,
            Cocuklar = new List<CocukInput>
            {
                new() { DogumTarihi = new DateTime(2025, 1, 1), Cinsiyet = Cinsiyet.Erkek }
            }
        };

        var r = await calc.CalculateAsync(input);

        r.IsValid.Should().BeFalse();
        r.ValidationErrors.Should().ContainKey("Cocuklar[0].DogumTarihi");
    }

    [Fact]
    public async Task Iskonto_Sifir_Sade_Carpim_Sonucu()
    {
        var (calc, ctx) = Build();
        await using var _ = ctx;

        var input = new DesteKtenYoksunKalmaInput
        {
            OlayTarihi = new DateTime(2025, 1, 1),
            OlenDogumTarihi = new DateTime(1995, 1, 1),
            OlenCinsiyet = Cinsiyet.Erkek,
            AylikGelir = 30000m,
            EsVarMi = true,
            EsDogumTarihi = new DateTime(1997, 1, 1),
            YillikIskontoOrani = 0m,
            PasifDonemGelirOrani = 50
        };

        var r = await calc.CalculateAsync(input);

        r.IsValid.Should().BeTrue();
        // Eş aktif: 30000×12 × 0.50 × 35 = 6.300.000
        // Eş pasif: 30000×12 × 0.50 × 0.50 × 9 = 810.000
        // Toplam: 7.110.000
        r.EsToplamTutar.Should().BeApproximately(7_110_000m, 100m);
    }
}
