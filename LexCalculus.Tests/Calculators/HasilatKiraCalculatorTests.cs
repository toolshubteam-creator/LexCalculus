using FluentAssertions;
using LexCalculus.Core.Calculators.Gayrimenkul;
using Xunit;

namespace LexCalculus.Tests.Calculators;

// Parametresiz, saf hesap — DB gerekmez (FormulaParameter yok).
public class HasilatKiraCalculatorTests
{
    private static HasilatKiraCalculator Build() => new();

    [Fact]
    public async Task CiroBazli_NoMinMax_SimpleCalculation()
    {
        var calc = Build();
        var input = new HasilatKiraInput { Ciro = 500_000m, HasilatOrani = 8m };

        var r = await calc.CalculateAsync(input);

        r.IsValid.Should().BeTrue();
        r.HesaplananKira.Should().Be(40_000m); // 500.000 × %8
        r.OdenecekKira.Should().Be(40_000m);
        r.HangiKuralDevreyeGirdi.Should().Be(HasilatKiraKurali.CiroBazli);
    }

    [Fact]
    public async Task MinimumGuvence_LowCiro_MinKiraReturned()
    {
        var calc = Build();
        var input = new HasilatKiraInput { Ciro = 100_000m, HasilatOrani = 8m, MinimumKira = 20_000m };

        var r = await calc.CalculateAsync(input);

        r.IsValid.Should().BeTrue();
        r.HesaplananKira.Should().Be(8_000m); // ciro bazlı tabandan düşük
        r.OdenecekKira.Should().Be(20_000m);  // minimum güvence
        r.HangiKuralDevreyeGirdi.Should().Be(HasilatKiraKurali.MinimumGuvence);
    }

    [Fact]
    public async Task MaksimumTavan_HighCiro_MaxKiraReturned()
    {
        var calc = Build();
        var input = new HasilatKiraInput { Ciro = 1_000_000m, HasilatOrani = 8m, MaksimumKira = 50_000m };

        var r = await calc.CalculateAsync(input);

        r.IsValid.Should().BeTrue();
        r.HesaplananKira.Should().Be(80_000m); // ciro bazlı tavanı aşıyor
        r.OdenecekKira.Should().Be(50_000m);   // maksimum tavan
        r.HangiKuralDevreyeGirdi.Should().Be(HasilatKiraKurali.MaksimumTavan);
    }

    [Fact]
    public async Task AllThreeRules_CorrectClamp_CiroWithinBounds()
    {
        var calc = Build();
        // Ciro bazlı 40.000; taban 20.000, tavan 50.000 → arada, clamp yok.
        var input = new HasilatKiraInput
        {
            Ciro = 500_000m,
            HasilatOrani = 8m,
            MinimumKira = 20_000m,
            MaksimumKira = 50_000m
        };

        var r = await calc.CalculateAsync(input);

        r.IsValid.Should().BeTrue();
        r.OdenecekKira.Should().Be(40_000m);
        r.HangiKuralDevreyeGirdi.Should().Be(HasilatKiraKurali.CiroBazli);
    }

    [Fact]
    public async Task NegativeCiro_ValidationError()
    {
        var calc = Build();
        var input = new HasilatKiraInput { Ciro = -100m, HasilatOrani = 8m };

        var r = await calc.CalculateAsync(input);

        r.IsValid.Should().BeFalse();
        r.ValidationErrors.Should().ContainKey(nameof(HasilatKiraInput.Ciro));
    }

    [Fact]
    public async Task RatioOutOfRange_ValidationError()
    {
        var calc = Build();
        var input = new HasilatKiraInput { Ciro = 500_000m, HasilatOrani = 150m };

        var r = await calc.CalculateAsync(input);

        r.IsValid.Should().BeFalse();
        r.ValidationErrors.Should().ContainKey(nameof(HasilatKiraInput.HasilatOrani));
    }
}
