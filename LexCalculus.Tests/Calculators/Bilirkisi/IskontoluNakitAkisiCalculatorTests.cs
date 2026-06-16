using FluentAssertions;
using LexCalculus.Core.Calculators.Bilirkisi;
using LexCalculus.Infrastructure.Calculators;
using Xunit;

namespace LexCalculus.Tests.Calculators.Bilirkisi;

// Saf hesap — DB gerekmez. IActuarialService reuse (Faz 2).
public class IskontoluNakitAkisiCalculatorTests
{
    private readonly IskontoluNakitAkisiCalculator _calc = new(new ActuarialService());

    [Fact]
    public async Task Standard_AnnuityPresentValue_ReferenceCalculation()
    {
        // PV = 100k × [(1 - 1.05^-10) / 0.05]
        //   1.05^10 = 1.628894627...
        //   (1 - 1/1.628894627) / 0.05 = (1 - 0.613913253) / 0.05 = 0.386086747 / 0.05 = 7.721734930
        //   PV ≈ 772.173,49
        var r = await _calc.CalculateAsync(new IskontoluNakitAkisiInput
        {
            YillikNetGelir = 100_000m,
            IskontoOraniYuzde = 5m,
            YilSayisi = 10
        });

        r.IsValid.Should().BeTrue();
        r.BugunkuDeger.Should().BeApproximately(772_173.49m, 0.50m);
        r.YillikNetGelir.Should().Be(100_000m);
        r.YilSayisi.Should().Be(10);
    }

    [Fact]
    public async Task ZeroRate_BoundaryCase_PvEqualsNominal()
    {
        // r = 0 → PV = G × N (faizsiz toplam). 50k × 20 = 1.000.000.
        var r = await _calc.CalculateAsync(new IskontoluNakitAkisiInput
        {
            YillikNetGelir = 50_000m,
            IskontoOraniYuzde = 0m,
            YilSayisi = 20
        });

        r.IsValid.Should().BeTrue();
        r.BugunkuDeger.Should().Be(1_000_000m);
        r.Warnings.Should().NotBeEmpty();
    }

    [Fact]
    public async Task LongTerm_30Year_DiscountEffect()
    {
        // 30 yıl × %10 ile büyük iskonto etkisi: faktör ≈ 9.426914
        // PV ≈ 100k × 9.426914 = 942.691
        var r = await _calc.CalculateAsync(new IskontoluNakitAkisiInput
        {
            YillikNetGelir = 100_000m,
            IskontoOraniYuzde = 10m,
            YilSayisi = 30
        });

        r.IsValid.Should().BeTrue();
        r.BugunkuDeger.Should().BeApproximately(942_691.42m, 1m);
    }

    [Fact]
    public async Task ValidationError_NegativeValues()
    {
        var r = await _calc.CalculateAsync(new IskontoluNakitAkisiInput
        {
            YillikNetGelir = -100m,
            IskontoOraniYuzde = 5m,
            YilSayisi = 10
        });

        r.IsValid.Should().BeFalse();
        r.ValidationErrors.Should().ContainKey(nameof(IskontoluNakitAkisiInput.YillikNetGelir));
    }
}
