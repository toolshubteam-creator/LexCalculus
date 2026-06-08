using FluentAssertions;
using LexCalculus.Core.Calculators.AileMiras;
using Xunit;

namespace LexCalculus.Tests.Calculators;

// Parametresiz, saf hesap — DB gerekmez (FormulaParameter yok).
public class MalRejimiTasfiyesiCalculatorTests
{
    private static MalRejimiTasfiyesiCalculator Build() => new();

    [Fact]
    public async Task Es1HigherArtikDeger_Es2IsNetCreditor()
    {
        var calc = Build();
        // Eş 1 artık 1.000.000, Eş 2 artık 400.000.
        // Eş 1 katılma = 400.000×½ = 200.000; Eş 2 katılma = 1.000.000×½ = 500.000.
        // Net = 200.000 - 500.000 = -300.000 → DÜŞÜK artık değerli Eş 2 net alacaklıdır.
        var input = new MalRejimiTasfiyesiInput
        {
            Es1EdinilenMal = 1_000_000m, Es1Borc = 0m,
            Es2EdinilenMal = 400_000m, Es2Borc = 0m
        };

        var r = await calc.CalculateAsync(input);

        r.IsValid.Should().BeTrue();
        r.Es1ArtikDeger.Should().Be(1_000_000m);
        r.Es2ArtikDeger.Should().Be(400_000m);
        r.NetAlacak.Should().Be(-300_000m);
        r.AlacakliEs.Should().Be(AlacakliEs.Es2);
        r.TotalAmount.Should().Be(300_000m);
    }

    [Fact]
    public async Task Es2HigherArtikDeger_Es1IsNetCreditor()
    {
        var calc = Build();
        var input = new MalRejimiTasfiyesiInput
        {
            Es1EdinilenMal = 400_000m, Es1Borc = 0m,
            Es2EdinilenMal = 1_000_000m, Es2Borc = 0m
        };

        var r = await calc.CalculateAsync(input);

        r.IsValid.Should().BeTrue();
        r.NetAlacak.Should().Be(300_000m);
        r.AlacakliEs.Should().Be(AlacakliEs.Es1);
        r.TotalAmount.Should().Be(300_000m);
    }

    [Fact]
    public async Task EqualArtikDeger_NoNet()
    {
        var calc = Build();
        var input = new MalRejimiTasfiyesiInput
        {
            Es1EdinilenMal = 500_000m, Es1Borc = 0m,
            Es2EdinilenMal = 500_000m, Es2Borc = 0m
        };

        var r = await calc.CalculateAsync(input);

        r.IsValid.Should().BeTrue();
        r.NetAlacak.Should().Be(0m);
        r.AlacakliEs.Should().Be(AlacakliEs.Esit);
    }

    [Fact]
    public async Task MirasBagis_ExcludedFromTasfiye()
    {
        var calc = Build();
        // Miras/bağış + evlilik öncesi mal kişisel maldır; artık değere DAHİL EDİLMEZ.
        var input = new MalRejimiTasfiyesiInput
        {
            Es1EdinilenMal = 500_000m, Es1Borc = 0m, Es1MirasBagis = 1_000_000m, Es1EvlilikOncesiMal = 250_000m,
            Es2EdinilenMal = 500_000m, Es2Borc = 0m
        };

        var r = await calc.CalculateAsync(input);

        r.IsValid.Should().BeTrue();
        r.Es1ArtikDeger.Should().Be(500_000m);           // miras/bağış ve evlilik öncesi hariç
        r.Es1KisiselMal.Should().Be(1_250_000m);         // 1.000.000 + 250.000
        r.NetAlacak.Should().Be(0m);
        r.AlacakliEs.Should().Be(AlacakliEs.Esit);
    }

    [Fact]
    public async Task NegativeArtikDeger_ZeroFloor()
    {
        var calc = Build();
        // Borç (500.000) > edinilen mal (200.000) → artık değer negatif olmaz, 0 kabul edilir.
        var input = new MalRejimiTasfiyesiInput
        {
            Es1EdinilenMal = 200_000m, Es1Borc = 500_000m,
            Es2EdinilenMal = 400_000m, Es2Borc = 0m
        };

        var r = await calc.CalculateAsync(input);

        r.IsValid.Should().BeTrue();
        r.Es1ArtikDeger.Should().Be(0m);                 // max(0, 200.000 - 500.000)
        r.Es2ArtikDeger.Should().Be(400_000m);
        // Eş 1 katılma = 400.000×½ = 200.000; Eş 2 katılma = 0 → Net +200.000 → Eş 1 alacaklı.
        r.NetAlacak.Should().Be(200_000m);
        r.AlacakliEs.Should().Be(AlacakliEs.Es1);
    }

    [Fact]
    public async Task NegativeInput_ValidationError()
    {
        var calc = Build();
        var input = new MalRejimiTasfiyesiInput { Es1EdinilenMal = -100m, Es2EdinilenMal = 400_000m };

        var r = await calc.CalculateAsync(input);

        r.IsValid.Should().BeFalse();
        r.ValidationErrors.Should().ContainKey(nameof(MalRejimiTasfiyesiInput.Es1EdinilenMal));
    }
}
