using FluentAssertions;
using LexCalculus.Core.Calculators.Akturya;
using Xunit;

namespace LexCalculus.Tests.Calculators;

public class GeciciIsGoremezlikCalculatorTests
{
    private readonly GeciciIsGoremezlikCalculator _calc = new();

    [Fact]
    public async Task Referans_30000_Brut_90_Gun_SGK_Mahsup_Acik()
    {
        var input = new GeciciIsGoremezlikInput
        {
            OlayTarihi = new DateTime(2025, 1, 1),
            SureGun = 90,
            AylikBrutUcret = 30000m,
            SgkOrani = 66.67m,
            SgkMahsup = true
        };

        var r = await _calc.CalculateAsync(input);

        r.IsValid.Should().BeTrue();
        r.GunlukBrut.Should().Be(1000m);
        r.BrutMahrumTutar.Should().Be(90000m);
        r.SgkOdenegi.Should().BeApproximately(60003m, 1m);
        r.NetMahrumTutar.Should().BeApproximately(29997m, 1m);
    }

    [Fact]
    public async Task SGK_Mahsup_Kapali_Tum_Brut_Talep_Edilebilir()
    {
        var input = new GeciciIsGoremezlikInput
        {
            OlayTarihi = new DateTime(2025, 1, 1),
            SureGun = 90,
            AylikBrutUcret = 30000m,
            SgkMahsup = false
        };

        var r = await _calc.CalculateAsync(input);

        r.IsValid.Should().BeTrue();
        r.SgkOdenegi.Should().Be(0m);
        r.NetMahrumTutar.Should().Be(r.BrutMahrumTutar);
        r.NetMahrumTutar.Should().Be(90000m);
        r.Warnings.Should().Contain(w => w.Contains("SGK mahsubu kapalı"));
    }

    [Fact]
    public async Task Bir_Gunluk_Sure_Hesabi_Dogru()
    {
        var input = new GeciciIsGoremezlikInput
        {
            OlayTarihi = new DateTime(2025, 1, 1),
            SureGun = 1,
            AylikBrutUcret = 30000m,
            SgkOrani = 66.67m,
            SgkMahsup = true
        };

        var r = await _calc.CalculateAsync(input);

        r.IsValid.Should().BeTrue();
        r.GunlukBrut.Should().Be(1000m);
        r.BrutMahrumTutar.Should().Be(1000m);
    }

    [Fact]
    public async Task Sifir_Sure_Validation_Hatasi()
    {
        var input = new GeciciIsGoremezlikInput
        {
            OlayTarihi = new DateTime(2025, 1, 1),
            SureGun = 0,
            AylikBrutUcret = 30000m
        };

        var r = await _calc.CalculateAsync(input);

        r.IsValid.Should().BeFalse();
        r.ValidationErrors.Should().ContainKey(nameof(GeciciIsGoremezlikInput.SureGun));
    }

    [Fact]
    public async Task Negatif_Brut_Validation_Hatasi()
    {
        var input = new GeciciIsGoremezlikInput
        {
            OlayTarihi = new DateTime(2025, 1, 1),
            SureGun = 90,
            AylikBrutUcret = -100m
        };

        var r = await _calc.CalculateAsync(input);

        r.IsValid.Should().BeFalse();
    }
}
