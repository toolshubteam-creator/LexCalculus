using FluentAssertions;
using LexCalculus.Core.Services;
using Xunit;

namespace LexCalculus.Tests.Services;

// Saf hesap servisi — DB gerekmez. Singleton seed (Türkiye resmi tatil 2020-2030).
public class CriminalCalendarServiceTests
{
    private readonly CriminalCalendarService _svc = new();

    [Fact]
    public void GunEkle_TumGunler_StandardCalendarAddition()
    {
        var basla = new DateOnly(2026, 1, 1);

        _svc.GunEkle(basla, 10).Should().Be(new DateOnly(2026, 1, 11));
        _svc.GunEkle(basla, 0).Should().Be(basla);
        _svc.GunEkle(basla, -5).Should().Be(new DateOnly(2025, 12, 27));
    }

    [Fact]
    public void GunEkle_IsGunleri_SkipsWeekend()
    {
        // 2026-01-02 Cuma → +3 iş günü → Pzt(05) Sal(06) Çar(07).
        var cuma = new DateOnly(2026, 1, 2);
        var sonuc = _svc.GunEkle(cuma, 3, GunHesabiTuru.IsGunleri);
        sonuc.Should().Be(new DateOnly(2026, 1, 7));
    }

    [Fact]
    public void GunEkle_IsGunleri_SkipsResmiTatil()
    {
        // 2026-04-22 Çarşamba → +1 iş günü; 23 Nisan tatil, 24 Nisan Cuma.
        var oncesi = new DateOnly(2026, 4, 22);
        var sonuc = _svc.GunEkle(oncesi, 1, GunHesabiTuru.IsGunleri);
        sonuc.Should().Be(new DateOnly(2026, 4, 24));
    }

    [Fact]
    public void ResmiTatilMi_BasicDates_TrueForSeedHolidays()
    {
        _svc.ResmiTatilMi(new DateOnly(2026, 1, 1)).Should().BeTrue();      // Yılbaşı
        _svc.ResmiTatilMi(new DateOnly(2026, 10, 29)).Should().BeTrue();    // Cumhuriyet
        _svc.ResmiTatilMi(new DateOnly(2026, 4, 23)).Should().BeTrue();     // 23 Nisan
        _svc.ResmiTatilMi(new DateOnly(2026, 3, 20)).Should().BeTrue();     // Ramazan Bayramı 1. gün
        _svc.ResmiTatilMi(new DateOnly(2026, 5, 27)).Should().BeTrue();     // Kurban Bayramı 1. gün
        _svc.ResmiTatilMi(new DateOnly(2026, 11, 5)).Should().BeFalse();    // sıradan iş günü
    }

    [Fact]
    public void InfazSuresi_RoundingCorrect_AwayFromZero()
    {
        // 100 × 0.6667 = 66.67 → 67 (AwayFromZero).
        _svc.InfazSuresi(100, 2m / 3m).Should().Be(67);

        // 1000 × 0.75 = 750.
        _svc.InfazSuresi(1000, 0.75m).Should().Be(750);

        // 6 yıl (2190 gün) × 2/3 = 1460.
        _svc.InfazSuresi(2190, 2m / 3m).Should().Be(1460);

        // Negatif / sıfır → 0.
        _svc.InfazSuresi(0, 0.5m).Should().Be(0);
        _svc.InfazSuresi(-5, 0.5m).Should().Be(0);
    }
}
