using FluentAssertions;
using LexCalculus.Core.Calculators.Ceza;
using Xunit;

namespace LexCalculus.Tests.Calculators.Ceza;

// Saf hesap — DB gerekmez, parametresiz (Adım 7.4 D4/D5 pattern).
public class TutuklulukMahsubuCalculatorTests
{
    private readonly TutuklulukMahsubuCalculator _calc = new();

    [Fact]
    public async Task Standard_DateRange_DayCount()
    {
        // 2026-01-01 - 2026-01-31 = 31 gün (inclusive).
        var input = new TutuklulukMahsubuInput
        {
            TutuklulukBaslangic = new DateTime(2026, 1, 1),
            TutuklulukBitis = new DateTime(2026, 1, 31)
        };

        var r = await _calc.CalculateAsync(input);

        r.IsValid.Should().BeTrue();
        r.TutuklulukGunleri.Should().Be(31);
        r.MahsupTutari.Should().BeNull();
    }

    [Fact]
    public async Task SameDay_OneDay()
    {
        // Aynı gün tutukluluk = 1 gün (inclusive).
        var input = new TutuklulukMahsubuInput
        {
            TutuklulukBaslangic = new DateTime(2026, 5, 10),
            TutuklulukBitis = new DateTime(2026, 5, 10)
        };

        var r = await _calc.CalculateAsync(input);

        r.IsValid.Should().BeTrue();
        r.TutuklulukGunleri.Should().Be(1);
    }

    [Fact]
    public async Task AdliParaMahsubu_MultipleCalculation()
    {
        // 100 gün × 50 TL = 5000 TL mahsup.
        var input = new TutuklulukMahsubuInput
        {
            TutuklulukBaslangic = new DateTime(2026, 1, 1),
            TutuklulukBitis = new DateTime(2026, 4, 10), // 100 gün (1 Ocak + 99 = 10 Nisan, inclusive 100)
            AdliParaMahsubu = true,
            GunlukMiktar = 50m
        };

        var r = await _calc.CalculateAsync(input);

        r.IsValid.Should().BeTrue();
        r.TutuklulukGunleri.Should().Be(100);
        r.MahsupTutari.Should().Be(5_000m);
    }

    [Fact]
    public async Task BitisBeforeBaslangic_ValidationError()
    {
        var input = new TutuklulukMahsubuInput
        {
            TutuklulukBaslangic = new DateTime(2026, 6, 1),
            TutuklulukBitis = new DateTime(2026, 1, 1)
        };

        var r = await _calc.CalculateAsync(input);

        r.IsValid.Should().BeFalse();
        r.ValidationErrors.Should().ContainKey(nameof(input.TutuklulukBitis));
    }

    [Fact]
    public async Task AdliParaWithoutMiktar_ValidationError()
    {
        var input = new TutuklulukMahsubuInput
        {
            TutuklulukBaslangic = new DateTime(2026, 1, 1),
            TutuklulukBitis = new DateTime(2026, 1, 31),
            AdliParaMahsubu = true,
            GunlukMiktar = null
        };

        var r = await _calc.CalculateAsync(input);

        r.IsValid.Should().BeFalse();
        r.ValidationErrors.Should().ContainKey(nameof(input.GunlukMiktar));
    }
}
