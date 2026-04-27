using FluentAssertions;
using LexCalculus.Core.Calculators.Akturya;
using Xunit;

namespace LexCalculus.Tests.Calculators;

public class AracDegerKaybiCalculatorTests
{
    private readonly AracDegerKaybiCalculator _calc = new();

    [Fact]
    public async Task Yeni_Arac_Az_Km_Tam_Faktor()
    {
        var input = new AracDegerKaybiInput
        {
            OlayTarihi = new DateTime(2025, 1, 1),
            KazadanOncekiDeger = 800000m,
            KazadanSonrakiDeger = 720000m,
            AracYas = 0,
            AracKm = 5000
        };

        var r = await _calc.CalculateAsync(input);

        r.IsValid.Should().BeTrue();
        r.HasarTutari.Should().Be(80000m);
        r.HasarOrani.Should().Be(0.10m);
        r.YasFaktoru.Should().Be(1.0m);
        r.KmFaktoru.Should().Be(1.0m);
        r.DegerKaybi.Should().Be(80000m);
        r.PertRiski.Should().BeFalse();
    }

    [Fact]
    public async Task Yasli_Arac_Cok_Km_Az_Faktor()
    {
        var input = new AracDegerKaybiInput
        {
            OlayTarihi = new DateTime(2025, 1, 1),
            KazadanOncekiDeger = 200000m,
            KazadanSonrakiDeger = 180000m,
            AracYas = 12,
            AracKm = 250000
        };

        var r = await _calc.CalculateAsync(input);

        r.YasFaktoru.Should().Be(0.10m);
        r.KmFaktoru.Should().Be(0.20m);
        r.DegerKaybi.Should().Be(400m);
    }

    [Fact]
    public async Task Pert_Esigi_Uzerinde_Uyari_Verir()
    {
        var input = new AracDegerKaybiInput
        {
            OlayTarihi = new DateTime(2025, 1, 1),
            KazadanOncekiDeger = 800000m,
            KazadanSonrakiDeger = 520000m,
            AracYas = 1,
            AracKm = 20000
        };

        var r = await _calc.CalculateAsync(input);

        r.IsValid.Should().BeTrue();
        r.PertRiski.Should().BeTrue();
        r.Warnings.Should().Contain(w => w.Contains("pert"));
    }

    [Theory]
    [InlineData(0, 1.0)]
    [InlineData(1, 1.0)]
    [InlineData(2, 0.85)]
    [InlineData(3, 0.70)]
    [InlineData(4, 0.50)]
    [InlineData(5, 0.50)]
    [InlineData(7, 0.30)]
    [InlineData(10, 0.30)]
    [InlineData(15, 0.10)]
    public async Task Yas_Faktoru_Tablosu_Dogru(int yas, double beklenen)
    {
        var input = new AracDegerKaybiInput
        {
            OlayTarihi = new DateTime(2025, 1, 1),
            KazadanOncekiDeger = 100000m,
            KazadanSonrakiDeger = 90000m,
            AracYas = yas,
            AracKm = 5000
        };

        var r = await _calc.CalculateAsync(input);

        r.YasFaktoru.Should().Be((decimal)beklenen);
    }

    [Theory]
    [InlineData(5000, 1.0)]
    [InlineData(29999, 1.0)]
    [InlineData(50000, 0.80)]
    [InlineData(99999, 0.80)]
    [InlineData(150000, 0.50)]
    [InlineData(199999, 0.50)]
    [InlineData(250000, 0.20)]
    public async Task Km_Faktoru_Tablosu_Dogru(int km, double beklenen)
    {
        var input = new AracDegerKaybiInput
        {
            OlayTarihi = new DateTime(2025, 1, 1),
            KazadanOncekiDeger = 100000m,
            KazadanSonrakiDeger = 90000m,
            AracYas = 1,
            AracKm = km
        };

        var r = await _calc.CalculateAsync(input);

        r.KmFaktoru.Should().Be((decimal)beklenen);
    }

    [Fact]
    public async Task Sonraki_Onceki_Den_Buyuk_Validation_Hatasi()
    {
        var input = new AracDegerKaybiInput
        {
            OlayTarihi = new DateTime(2025, 1, 1),
            KazadanOncekiDeger = 100000m,
            KazadanSonrakiDeger = 110000m,
            AracYas = 1,
            AracKm = 5000
        };

        var r = await _calc.CalculateAsync(input);

        r.IsValid.Should().BeFalse();
        r.ValidationErrors.Should().ContainKey(nameof(AracDegerKaybiInput.KazadanSonrakiDeger));
    }
}
