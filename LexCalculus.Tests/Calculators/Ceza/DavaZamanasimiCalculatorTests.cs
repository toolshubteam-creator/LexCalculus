using FluentAssertions;
using LexCalculus.Core.Calculators.Ceza;
using Xunit;

namespace LexCalculus.Tests.Calculators.Ceza;

// Saf hesap — DB gerekmez, parametresiz (Adım 7.10 cleanup — tech-debt #49 ÇÖZÜLDÜ).
public class DavaZamanasimiCalculatorTests
{
    private readonly DavaZamanasimiCalculator _calc = new();

    [Fact]
    public async Task Reference_TCK66_Orta_15Yil_NoKesinti()
    {
        // 6 yıl hapis (Orta) → asli 15 yıl, mutlak 22.5 yıl.
        var suc = new DateTime(2010, 1, 1);
        var input = new DavaZamanasimiInput
        {
            SucIslemeTarihi = suc,
            SucAgirligi = SucAgirligi.Orta,
            AsOfDate = new DateTime(2024, 1, 1)
        };

        var r = await _calc.CalculateAsync(input);

        r.IsValid.Should().BeTrue();
        r.AsliZamanasimiSuresiYil.Should().Be(15);
        r.MutlakZamanasimiSuresiYil.Should().Be(22.5m);
        r.AsliZamanasimiBitis.Should().Be(new DateOnly(2025, 1, 1));
        r.MutlakSinirUygulandi.Should().BeFalse();
        r.ZamanasimineUgradiMi.Should().BeFalse();
    }

    [Fact]
    public async Task Reference_TCK66_Uzun_20Yil_NoKesinti()
    {
        // 25 yıl hapis (Uzun) → asli 20 yıl, mutlak 30 yıl.
        var input = new DavaZamanasimiInput
        {
            SucIslemeTarihi = new DateTime(2000, 6, 15),
            SucAgirligi = SucAgirligi.Uzun,
            AsOfDate = new DateTime(2026, 1, 1)
        };

        var r = await _calc.CalculateAsync(input);

        r.AsliZamanasimiSuresiYil.Should().Be(20);
        r.AsliZamanasimiBitis.Should().Be(new DateOnly(2020, 6, 15));
        r.ZamanasimineUgradiMi.Should().BeTrue();
    }

    [Fact]
    public async Task Kesinti_AsliBaslamadan_AsliMaxedAt_15()
    {
        // Orta suç (asli 15, mutlak 22.5). Suç 2010, kesinti 2018, AsOfDate 2024.
        // Mutlak bitiş 2032-07; son kesintiden 15 yıl = 2033-01 → mutlak'ı aşmaz değil mi?
        // 2010-01-01 + 22.5 yıl ≈ 2032-07-02; 2018-01-01 + 15 yıl = 2033-01-01 → MutlakSinir DEVREDE!
        // Etkin bitiş = 2032-07-02 (mutlak).
        var input = new DavaZamanasimiInput
        {
            SucIslemeTarihi = new DateTime(2010, 1, 1),
            SucAgirligi = SucAgirligi.Orta,
            Kesintiler = new List<KesintiGirdi>
            {
                new() { Tarih = new DateTime(2018, 1, 1), IslemTuru = "İddianame" }
            },
            AsOfDate = new DateTime(2024, 1, 1)
        };

        var r = await _calc.CalculateAsync(input);

        r.SonBaslangicTarihi.Should().Be(new DateOnly(2018, 1, 1));
        r.MutlakSinirUygulandi.Should().BeTrue();
        // Mutlak bitiş suç tarihi + 22.5 yıl ≈ 2032-07-02 (sabit; cap uygulandı).
        r.AsliZamanasimiBitis.Should().Be(r.MutlakZamanasimiBitis);
        r.ZamanasimineUgradiMi.Should().BeFalse(); // 2024 < 2032
    }

    [Fact]
    public async Task Kesinti_TCK67_MutlakLimit_AsliCapped()
    {
        // Kısa suç (asli 8, mutlak 12). Suç 2010, kesinti 2015.
        // Mutlak bitiş = 2010 + 12 = 2022; kesintiden asli = 2015 + 8 = 2023 → cap 2022.
        var input = new DavaZamanasimiInput
        {
            SucIslemeTarihi = new DateTime(2010, 1, 1),
            SucAgirligi = SucAgirligi.Kisa,
            Kesintiler = new List<KesintiGirdi>
            {
                new() { Tarih = new DateTime(2015, 1, 1) }
            },
            AsOfDate = new DateTime(2020, 1, 1)
        };

        var r = await _calc.CalculateAsync(input);

        r.MutlakSinirUygulandi.Should().BeTrue();
        r.AsliZamanasimiBitis.Should().Be(r.MutlakZamanasimiBitis);
        // Mutlak bitiş yaklaşık 2022-01 (8 × 1.5 = 12 yıl).
        r.MutlakZamanasimiBitis.Year.Should().Be(2022);
    }

    [Fact]
    public async Task AgırlastırılmısMuebbet_30Yil()
    {
        var input = new DavaZamanasimiInput
        {
            SucIslemeTarihi = new DateTime(2020, 1, 1),
            SucAgirligi = SucAgirligi.AgirlastirilmisMuebbet,
            AsOfDate = new DateTime(2026, 1, 1)
        };

        var r = await _calc.CalculateAsync(input);

        r.AsliZamanasimiSuresiYil.Should().Be(30);
        r.MutlakZamanasimiSuresiYil.Should().Be(45m);
        r.AsliZamanasimiBitis.Should().Be(new DateOnly(2050, 1, 1));
    }

    [Fact]
    public async Task ZamanasimineUgradi_PastDate()
    {
        // Kısa suç asli 8 yıl. 2000 suç + 8 = 2008; AsOf 2024 → geçmiş.
        var input = new DavaZamanasimiInput
        {
            SucIslemeTarihi = new DateTime(2000, 1, 1),
            SucAgirligi = SucAgirligi.Kisa,
            AsOfDate = new DateTime(2024, 1, 1)
        };

        var r = await _calc.CalculateAsync(input);

        r.ZamanasimineUgradiMi.Should().BeTrue();
        r.KalanGun.Should().BeLessThan(0);
    }

    [Fact]
    public async Task KalanGun_Calculation_FromAsOfDate()
    {
        // Orta (asli 15). Suç 2020-01-01 → asli bitiş 2035-01-01. AsOf 2026-01-01.
        // Kalan ≈ 9 yıl × 365 = ~3287 gün.
        var asOf = new DateTime(2026, 1, 1);
        var input = new DavaZamanasimiInput
        {
            SucIslemeTarihi = new DateTime(2020, 1, 1),
            SucAgirligi = SucAgirligi.Orta,
            AsOfDate = asOf
        };

        var r = await _calc.CalculateAsync(input);

        var expectedBitis = new DateOnly(2035, 1, 1);
        var expectedKalan = expectedBitis.DayNumber - DateOnly.FromDateTime(asOf).DayNumber;
        r.KalanGun.Should().Be(expectedKalan);
        r.ZamanasimineUgradiMi.Should().BeFalse();
    }
}
