using FluentAssertions;
using LexCalculus.Infrastructure.Calculators;
using Xunit;

namespace LexCalculus.Tests.Calculators;

public class ActuarialServiceTests
{
    private readonly ActuarialService _svc = new();

    [Fact]
    public void HesaplaYas_30_Yasinda_Birey_Dogru_Hesaplar()
    {
        var dogum = new DateTime(1995, 6, 15);
        var ref_ = new DateTime(2025, 6, 15);

        _svc.HesaplaYas(dogum, ref_).Should().Be(30);
    }

    [Fact]
    public void HesaplaYas_Dogum_Gunu_Henuz_Gelmemisse_Bir_Eksik()
    {
        var dogum = new DateTime(1995, 6, 15);
        var ref_ = new DateTime(2025, 6, 14);

        _svc.HesaplaYas(dogum, ref_).Should().Be(29);
    }

    [Fact]
    public void AnnuityPV_Sifir_Iskonto_Sade_Carpim()
    {
        _svc.AnnuityPresentValue(100m, 10, 0m).Should().Be(1000m);
    }

    [Fact]
    public void AnnuityPV_Pozitif_Iskonto_Toplamdan_Az()
    {
        var pv = _svc.AnnuityPresentValue(100m, 10, 0.05m);

        pv.Should().BeApproximately(772.17m, 0.5m);
        pv.Should().BeLessThan(1000m, "pozitif iskonto ile peşin değer toplam tutardan az olmalı");
    }

    [Fact]
    public void AnnuityPV_Sifir_Yil_Sifir()
    {
        _svc.AnnuityPresentValue(100m, 0, 0.05m).Should().Be(0m);
    }

    [Fact]
    public void AktifDonemYili_30_Yasinda_Birey_35_Yil()
    {
        _svc.AktifDonemYili(30).Should().Be(35);
    }

    [Fact]
    public void AktifDonemYili_Emekli_Yasini_Gectiyse_Sifir()
    {
        _svc.AktifDonemYili(70).Should().Be(0);
    }

    [Fact]
    public void PasifDonemYili_30_Yasinda_44_eX_14_Yil_Pasif()
    {
        var pasif = _svc.PasifDonemYili(30, 44.45m);

        pasif.Should().Be(9);
    }
}
