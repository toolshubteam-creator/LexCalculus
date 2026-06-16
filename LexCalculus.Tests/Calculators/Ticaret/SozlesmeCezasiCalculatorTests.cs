using FluentAssertions;
using LexCalculus.Core.Calculators.Ticaret;
using Xunit;

namespace LexCalculus.Tests.Calculators.Ticaret;

// Saf hesap — DB gerekmez, parametresiz.
public class SozlesmeCezasiCalculatorTests
{
    private readonly SozlesmeCezasiCalculator _calc = new();

    [Fact]
    public async Task SabitTutar_StandardCase()
    {
        // 100k asıl + 30k sabit ceza → kat 0.3, standart.
        var r = await _calc.CalculateAsync(new SozlesmeCezasiInput
        {
            AsilBorc = 100_000m,
            CezaSekli = CezaSekli.SabitTutar,
            BelirlenenCeza = 30_000m
        });

        r.IsValid.Should().BeTrue();
        r.HesaplananCeza.Should().Be(30_000m);
        r.AsilBorcKati.Should().Be(0.3m);
        r.FahisDegerlendirmesi.Should().Be(FahisDegerlendirmesi.Standart);
        // Her durumda hâkim takdir uyarısı olmalı.
        r.Warnings.Should().Contain(w => w.Contains("m.182"));
    }

    [Fact]
    public async Task YuzdeOran_StandardCase()
    {
        // 100k asıl × %25 = 25k ceza, kat 0.25.
        var r = await _calc.CalculateAsync(new SozlesmeCezasiInput
        {
            AsilBorc = 100_000m,
            CezaSekli = CezaSekli.YuzdeOran,
            BelirlenenOran = 25m
        });

        r.IsValid.Should().BeTrue();
        r.HesaplananCeza.Should().Be(25_000m);
        r.AsilBorcKati.Should().Be(0.25m);
        r.FahisDegerlendirmesi.Should().Be(FahisDegerlendirmesi.Standart);
    }

    [Fact]
    public async Task Fahis_Uyari_AsilBorc2Katindan()
    {
        // 100k asıl + 250k ceza → kat 2.5 → fahiş.
        var r = await _calc.CalculateAsync(new SozlesmeCezasiInput
        {
            AsilBorc = 100_000m,
            CezaSekli = CezaSekli.SabitTutar,
            BelirlenenCeza = 250_000m
        });

        r.FahisDegerlendirmesi.Should().Be(FahisDegerlendirmesi.Fahis);
        r.AsilBorcKati.Should().Be(2.5m);
        r.Warnings.Should().Contain(w => w.Contains("FAHİŞ"));
    }

    [Fact]
    public async Task DikkatEdici_AsilBorcUstu_Below2x()
    {
        // 100k asıl + 150k ceza → kat 1.5 → dikkat edici.
        var r = await _calc.CalculateAsync(new SozlesmeCezasiInput
        {
            AsilBorc = 100_000m,
            CezaSekli = CezaSekli.SabitTutar,
            BelirlenenCeza = 150_000m
        });

        r.FahisDegerlendirmesi.Should().Be(FahisDegerlendirmesi.DikkatEdici);
        r.AsilBorcKati.Should().Be(1.5m);
        r.Warnings.Should().Contain(w => w.Contains("asıl borc"));
    }

    [Fact]
    public async Task ValidationError_NegativeBorc()
    {
        var r = await _calc.CalculateAsync(new SozlesmeCezasiInput
        {
            AsilBorc = -1m,
            CezaSekli = CezaSekli.SabitTutar,
            BelirlenenCeza = 100m
        });

        r.IsValid.Should().BeFalse();
        r.ValidationErrors.Should().ContainKey(nameof(SozlesmeCezasiInput.AsilBorc));
    }
}
