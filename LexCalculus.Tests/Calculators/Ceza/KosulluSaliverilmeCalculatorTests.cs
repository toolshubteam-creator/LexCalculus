using FluentAssertions;
using LexCalculus.Core.Calculators.Ceza;
using LexCalculus.Core.Services;
using Xunit;

namespace LexCalculus.Tests.Calculators.Ceza;

// Saf hesap — DB gerekmez. 5275 s.K. m.107 referans oranlar (2/3, 3/4).
public class KosulluSaliverilmeCalculatorTests
{
    private readonly KosulluSaliverilmeCalculator _calc = new(new CriminalCalendarService());

    [Fact]
    public async Task Reference_Genel_2_3_Oran_6YilCeza()
    {
        // 5275 s.K. m.107/1: 6 yıl (2190 gün) × 2/3 = 1460 gün infaz.
        var giris = new DateTime(2026, 1, 1);
        var input = new KosulluSaliverilmeInput
        {
            MahkumiyetGun = 2190,
            CezaevineGirisTarihi = giris,
            SucTipi = SucTipi.Genel,
            TutuklulukGun = 0,
            AsOfDate = giris
        };

        var r = await _calc.CalculateAsync(input);

        r.IsValid.Should().BeTrue();
        r.NetInfazSuresi.Should().Be(1460);
        r.SartliTahliyeTarihi.Should().Be(DateOnly.FromDateTime(giris).AddDays(1460));
        r.HesaplananOran.Should().BeApproximately(2m / 3m, 0.0001m);
    }

    [Fact]
    public async Task Reference_Teror_3_4_Oran_8YilCeza()
    {
        // 8 yıl (2920 gün) × 3/4 = 2190 gün infaz.
        var giris = new DateTime(2026, 1, 1);
        var input = new KosulluSaliverilmeInput
        {
            MahkumiyetGun = 2920,
            CezaevineGirisTarihi = giris,
            SucTipi = SucTipi.Teror,
            TutuklulukGun = 0,
            AsOfDate = giris
        };

        var r = await _calc.CalculateAsync(input);

        r.IsValid.Should().BeTrue();
        r.NetInfazSuresi.Should().Be(2190);
        r.HesaplananOran.Should().Be(0.75m);
        r.SartliTahliyeTarihi.Should().Be(DateOnly.FromDateTime(giris).AddDays(2190));
    }

    [Fact]
    public async Task CinselSuc_3_4_Oran_Applied()
    {
        // TCK m.102/103/104/105 → 3/4. 4000 × 0.75 = 3000.
        var input = new KosulluSaliverilmeInput
        {
            MahkumiyetGun = 4000,
            CezaevineGirisTarihi = new DateTime(2026, 1, 1),
            SucTipi = SucTipi.CinselSuc,
            TutuklulukGun = 0,
            AsOfDate = new DateTime(2026, 1, 1)
        };

        var r = await _calc.CalculateAsync(input);

        r.IsValid.Should().BeTrue();
        r.HesaplananOran.Should().Be(0.75m);
        r.NetInfazSuresi.Should().Be(3000);
    }

    [Fact]
    public async Task TutuklulukMahsubu_NetSureDusuyor()
    {
        // 6 yıl × 2/3 = 1460 gün; tutukluluk 200 gün düşüldükten sonra net 1260.
        var giris = new DateTime(2026, 1, 1);
        var input = new KosulluSaliverilmeInput
        {
            MahkumiyetGun = 2190,
            CezaevineGirisTarihi = giris,
            SucTipi = SucTipi.Genel,
            TutuklulukGun = 200,
            AsOfDate = giris
        };

        var r = await _calc.CalculateAsync(input);

        r.IsValid.Should().BeTrue();
        r.NetInfazSuresi.Should().Be(1260);
        r.SartliTahliyeTarihi.Should().Be(DateOnly.FromDateTime(giris).AddDays(1260));
    }

    [Fact]
    public async Task SartliTahliyeTarihi_DateCalculation_GirisPlusNetInfaz()
    {
        // 6 yıl × 2/3 = 1460; tutukluluk 0 → tahliye giriş + 1460 gün.
        // Referans tarih için kalan gün hesabı: 1460 gün ileri.
        var giris = new DateOnly(2026, 1, 1);
        var input = new KosulluSaliverilmeInput
        {
            MahkumiyetGun = 2190,
            CezaevineGirisTarihi = giris.ToDateTime(TimeOnly.MinValue),
            SucTipi = SucTipi.Genel,
            AsOfDate = giris.ToDateTime(TimeOnly.MinValue)
        };

        var r = await _calc.CalculateAsync(input);

        r.SartliTahliyeTarihi.Should().Be(giris.AddDays(1460));
        r.KalanGunSayisi.Should().Be(1460);
    }

    [Fact]
    public async Task ValidationErrors_NegativeAndExcessTutukluluk()
    {
        // Mahkumiyet 0/eksi → validation hatası.
        var r1 = await _calc.CalculateAsync(new KosulluSaliverilmeInput
        {
            MahkumiyetGun = 0,
            CezaevineGirisTarihi = new DateTime(2026, 1, 1),
            SucTipi = SucTipi.Genel
        });
        r1.IsValid.Should().BeFalse();
        r1.ValidationErrors.Should().ContainKey("MahkumiyetGun");

        // Tutukluluk net infaz süresini aşıyor → validation hatası.
        // 100 × 2/3 = 67 → tutukluluk 200 → red.
        var r2 = await _calc.CalculateAsync(new KosulluSaliverilmeInput
        {
            MahkumiyetGun = 100,
            CezaevineGirisTarihi = new DateTime(2026, 1, 1),
            SucTipi = SucTipi.Genel,
            TutuklulukGun = 200
        });
        r2.IsValid.Should().BeFalse();
        r2.ValidationErrors.Should().ContainKey("TutuklulukGun");
    }
}
