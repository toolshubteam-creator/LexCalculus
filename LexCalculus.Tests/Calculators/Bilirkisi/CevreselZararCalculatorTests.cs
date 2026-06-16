using FluentAssertions;
using LexCalculus.Core.Calculators.Bilirkisi;
using Xunit;

namespace LexCalculus.Tests.Calculators.Bilirkisi;

// Saf hesap — DB gerekmez, parametresiz.
public class CevreselZararCalculatorTests
{
    private readonly CevreselZararCalculator _calc = new();

    [Fact]
    public async Task Standard_AllKalemler_NoBilirkisiMaliyet()
    {
        // 100k + 250k + 75k = 425k toplam zarar; bilirkişi maliyet 0 → toplam tazminat 425k.
        var r = await _calc.CalculateAsync(new CevreselZararInput
        {
            DogrudanZarar = 100_000m,
            RestorasyonMaliyeti = 250_000m,
            EkosistemKaybi = 75_000m,
            BilirkisiMaliyetOraniYuzde = 0m
        });

        r.IsValid.Should().BeTrue();
        r.ToplamZarar.Should().Be(425_000m);
        r.BilirkisiMaliyet.Should().Be(0m);
        r.ToplamTazminat.Should().Be(425_000m);
    }

    [Fact]
    public async Task WithBilirkisiMaliyet_10Percent_AddedToTotal()
    {
        // 425k × %10 = 42.500 bilirkişi; toplam 467.500.
        var r = await _calc.CalculateAsync(new CevreselZararInput
        {
            DogrudanZarar = 100_000m,
            RestorasyonMaliyeti = 250_000m,
            EkosistemKaybi = 75_000m,
            BilirkisiMaliyetOraniYuzde = 10m
        });

        r.ToplamZarar.Should().Be(425_000m);
        r.BilirkisiMaliyet.Should().Be(42_500m);
        r.ToplamTazminat.Should().Be(467_500m);
    }

    [Fact]
    public async Task ZeroEkosistemKaybi_OnlyDirectAndRestoration()
    {
        // 50k + 200k + 0 = 250k.
        var r = await _calc.CalculateAsync(new CevreselZararInput
        {
            DogrudanZarar = 50_000m,
            RestorasyonMaliyeti = 200_000m,
            EkosistemKaybi = 0m
        });

        r.IsValid.Should().BeTrue();
        r.ToplamZarar.Should().Be(250_000m);
    }

    [Fact]
    public async Task ValidationError_NegativeValues()
    {
        var r = await _calc.CalculateAsync(new CevreselZararInput
        {
            DogrudanZarar = -100m,
            RestorasyonMaliyeti = 100m,
            EkosistemKaybi = 100m
        });

        r.IsValid.Should().BeFalse();
        r.ValidationErrors.Should().ContainKey(nameof(CevreselZararInput.DogrudanZarar));
    }
}
