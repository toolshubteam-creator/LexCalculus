using FluentAssertions;
using LexCalculus.Core.Calculators.VergiIdare;
using Xunit;

namespace LexCalculus.Tests.Calculators.VergiIdare;

// Saf hesap — DB gerekmez, parametresiz.
public class KdvIadesiCalculatorTests
{
    private readonly KdvIadesiCalculator _calc = new();

    [Fact]
    public async Task IndirimYuksek_IadeVar()
    {
        // İndirim 200k > Hesaplanan 50k → iade 150k.
        var r = await _calc.CalculateAsync(new KdvIadesiInput
        {
            ToplamHesaplananKDV = 50_000m,
            ToplamIndirimKDV = 200_000m,
            MahsupEdilenKDV = 0m,
            IadeBasvuruTuru = KdvIadeBasvuruTuru.IndirimliOran
        });

        r.IsValid.Should().BeTrue();
        r.IadeyeKonuKDV.Should().Be(150_000m);
        r.IadeTutari.Should().Be(150_000m);
    }

    [Fact]
    public async Task IndirimDusuk_IadeYok()
    {
        // İndirim 30k < Hesaplanan 50k → iadeye konu 0.
        var r = await _calc.CalculateAsync(new KdvIadesiInput
        {
            ToplamHesaplananKDV = 50_000m,
            ToplamIndirimKDV = 30_000m,
            MahsupEdilenKDV = 0m,
            IadeBasvuruTuru = KdvIadeBasvuruTuru.IhracKayitli
        });

        r.IsValid.Should().BeTrue();
        r.IadeyeKonuKDV.Should().Be(0m);
        r.IadeTutari.Should().Be(0m);
        r.Warnings.Should().NotBeEmpty();
    }

    [Fact]
    public async Task MahsupSonrasi_TutarDusuyor()
    {
        // İade konu 150k - mahsup 100k = net 50k.
        var r = await _calc.CalculateAsync(new KdvIadesiInput
        {
            ToplamHesaplananKDV = 50_000m,
            ToplamIndirimKDV = 200_000m,
            MahsupEdilenKDV = 100_000m,
            IadeBasvuruTuru = KdvIadeBasvuruTuru.DigerIade
        });

        r.IadeyeKonuKDV.Should().Be(150_000m);
        r.IadeTutari.Should().Be(50_000m);
    }

    [Fact]
    public async Task ValidationError_NegativeValues()
    {
        var r = await _calc.CalculateAsync(new KdvIadesiInput
        {
            ToplamHesaplananKDV = -100m,
            ToplamIndirimKDV = 200_000m
        });

        r.IsValid.Should().BeFalse();
        r.ValidationErrors.Should().ContainKey(nameof(KdvIadesiInput.ToplamHesaplananKDV));
    }

    [Fact]
    public async Task ZeroValues_NoIade()
    {
        var r = await _calc.CalculateAsync(new KdvIadesiInput
        {
            ToplamHesaplananKDV = 0m,
            ToplamIndirimKDV = 0m
        });

        r.IsValid.Should().BeTrue();
        r.IadeTutari.Should().Be(0m);
    }
}
