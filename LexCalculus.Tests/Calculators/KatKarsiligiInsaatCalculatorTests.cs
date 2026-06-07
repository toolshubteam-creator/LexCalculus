using FluentAssertions;
using LexCalculus.Core.Calculators.Gayrimenkul;
using Xunit;

namespace LexCalculus.Tests.Calculators;

// Parametresiz, saf hesap — DB gerekmez (FormulaParameter yok).
public class KatKarsiligiInsaatCalculatorTests
{
    private static KatKarsiligiInsaatCalculator Build() => new();

    [Fact]
    public async Task Oransal_StandardCase_CorrectDistribution()
    {
        var calc = Build();
        // Arsa 4M, inşaat 6M → arsa oranı 0.40; proje 10M → arsa 4M, müteahhit 6M.
        var input = new KatKarsiligiInsaatInput
        {
            Yontem = KatKarsiligiYontemi.Oransal,
            ArsaDegeri = 4_000_000m,
            ToplamInsaatMaliyeti = 6_000_000m,
            ToplamProjeDegeri = 10_000_000m
        };

        var r = await calc.CalculateAsync(input);

        r.IsValid.Should().BeTrue();
        r.ArsaOrani.Should().BeApproximately(0.40m, 0.0001m);
        r.ArsaSahibiPay.Should().Be(4_000_000m);
        r.MuteahhitPay.Should().Be(6_000_000m);
    }

    [Fact]
    public async Task Sabit_60_40_Distribution()
    {
        var calc = Build();
        var input = new KatKarsiligiInsaatInput
        {
            Yontem = KatKarsiligiYontemi.Sabit,
            ArsaSahibiOrani = 40m,
            ToplamProjeDegeri = 10_000_000m
        };

        var r = await calc.CalculateAsync(input);

        r.IsValid.Should().BeTrue();
        r.ArsaSahibiPay.Should().Be(4_000_000m);
        r.MuteahhitPay.Should().Be(6_000_000m); // %60 müteahhit
    }

    [Fact]
    public async Task Oransal_ZeroArsaValue_AllToMuteahhit()
    {
        var calc = Build();
        var input = new KatKarsiligiInsaatInput
        {
            Yontem = KatKarsiligiYontemi.Oransal,
            ArsaDegeri = 0m,
            ToplamInsaatMaliyeti = 6_000_000m,
            ToplamProjeDegeri = 10_000_000m
        };

        var r = await calc.CalculateAsync(input);

        r.IsValid.Should().BeTrue();
        r.ArsaOrani.Should().Be(0m);
        r.ArsaSahibiPay.Should().Be(0m);
        r.MuteahhitPay.Should().Be(10_000_000m);
    }

    [Fact]
    public async Task Sabit_PercentageOutOfRange_ValidationError()
    {
        var calc = Build();
        var input = new KatKarsiligiInsaatInput
        {
            Yontem = KatKarsiligiYontemi.Sabit,
            ArsaSahibiOrani = 150m, // %100 üstü
            ToplamProjeDegeri = 10_000_000m
        };

        var r = await calc.CalculateAsync(input);

        r.IsValid.Should().BeFalse();
        r.ValidationErrors.Should().ContainKey(nameof(KatKarsiligiInsaatInput.ArsaSahibiOrani));
    }

    [Fact]
    public async Task NegativeProjectValue_ValidationError()
    {
        var calc = Build();
        var input = new KatKarsiligiInsaatInput
        {
            Yontem = KatKarsiligiYontemi.Oransal,
            ArsaDegeri = 4_000_000m,
            ToplamInsaatMaliyeti = 6_000_000m,
            ToplamProjeDegeri = 0m
        };

        var r = await calc.CalculateAsync(input);

        r.IsValid.Should().BeFalse();
        r.ValidationErrors.Should().ContainKey(nameof(KatKarsiligiInsaatInput.ToplamProjeDegeri));
    }

    [Fact]
    public async Task BolumSayisi_ApproximateDistribution()
    {
        var calc = Build();
        var input = new KatKarsiligiInsaatInput
        {
            Yontem = KatKarsiligiYontemi.Sabit,
            ArsaSahibiOrani = 40m,
            ToplamProjeDegeri = 10_000_000m,
            ToplamBagimsizBolumSayisi = 10
        };

        var r = await calc.CalculateAsync(input);

        r.IsValid.Should().BeTrue();
        r.YaklasikArsaSahibiBolumSayisi.Should().Be(4m);   // 10 × 0.40
        r.YaklasikMuteahhitBolumSayisi.Should().Be(6m);
    }
}
