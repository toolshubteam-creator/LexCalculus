using FluentAssertions;
using LexCalculus.Core.Calculators.Ceza;
using Xunit;

namespace LexCalculus.Tests.Calculators.Ceza;

// Saf hesap — DB gerekmez, parametresiz, servis injection yok.
public class AdliParaCezasiCalculatorTests
{
    private readonly AdliParaCezasiCalculator _calc = new();

    [Fact]
    public async Task Direkt_StandardCalculation_GunMiktar()
    {
        // 90 gün × 50 TL = 4500 TL.
        var input = new AdliParaCezasiInput
        {
            HesapTuru = AdliParaHesapTuru.Direkt,
            GunSayisi = 90,
            GunlukMiktar = 50m
        };

        var r = await _calc.CalculateAsync(input);

        r.IsValid.Should().BeTrue();
        r.ToplamCeza.Should().Be(4500m);
        r.EtkinGunSayisi.Should().Be(90);
        r.UygulananGunlukMiktar.Should().Be(50m);
    }

    [Fact]
    public async Task HapisCevrim_BasicConversion()
    {
        // 180 gün hapis × 75 TL = 13500 TL.
        var input = new AdliParaCezasiInput
        {
            HesapTuru = AdliParaHesapTuru.HapisCevrim,
            HapisGun = 180,
            CevrimGunlukMiktar = 75m
        };

        var r = await _calc.CalculateAsync(input);

        r.IsValid.Should().BeTrue();
        r.ToplamCeza.Should().Be(13_500m);
        r.EtkinGunSayisi.Should().Be(180);
    }

    [Fact]
    public async Task GunSayisi_BoundaryValues_5And730()
    {
        // Sınır 5 gün × 20 TL = 100 TL.
        var r1 = await _calc.CalculateAsync(new AdliParaCezasiInput
        {
            HesapTuru = AdliParaHesapTuru.Direkt,
            GunSayisi = 5,
            GunlukMiktar = 20m
        });
        r1.IsValid.Should().BeTrue();
        r1.ToplamCeza.Should().Be(100m);

        // Üst sınır 730 gün × 100 TL = 73000 TL.
        var r2 = await _calc.CalculateAsync(new AdliParaCezasiInput
        {
            HesapTuru = AdliParaHesapTuru.Direkt,
            GunSayisi = 730,
            GunlukMiktar = 100m
        });
        r2.IsValid.Should().BeTrue();
        r2.ToplamCeza.Should().Be(73_000m);
    }

    [Fact]
    public async Task Direkt_MissingFields_ValidationError()
    {
        // Direkt seçildi ama GunSayisi ve GunlukMiktar boş.
        var r = await _calc.CalculateAsync(new AdliParaCezasiInput
        {
            HesapTuru = AdliParaHesapTuru.Direkt
        });

        r.IsValid.Should().BeFalse();
        r.ValidationErrors.Should().ContainKey("GunSayisi");
        r.ValidationErrors.Should().ContainKey("GunlukMiktar");
    }

    [Fact]
    public async Task NegativeValues_ValidationError()
    {
        // Negatif gün sayısı.
        var r = await _calc.CalculateAsync(new AdliParaCezasiInput
        {
            HesapTuru = AdliParaHesapTuru.Direkt,
            GunSayisi = -10,
            GunlukMiktar = 50m
        });

        r.IsValid.Should().BeFalse();
        r.ValidationErrors.Should().ContainKey("GunSayisi");
    }

    [Fact]
    public async Task HapisCevrim_MissingFields_ValidationError()
    {
        // Çevrim seçildi ama HapisGun ve CevrimGunlukMiktar boş.
        var r = await _calc.CalculateAsync(new AdliParaCezasiInput
        {
            HesapTuru = AdliParaHesapTuru.HapisCevrim
        });

        r.IsValid.Should().BeFalse();
        r.ValidationErrors.Should().ContainKey("HapisGun");
        r.ValidationErrors.Should().ContainKey("CevrimGunlukMiktar");
    }
}
