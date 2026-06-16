using FluentAssertions;
using LexCalculus.Core.Calculators.Ticaret;
using Xunit;

namespace LexCalculus.Tests.Calculators.Ticaret;

// Saf hesap — DB gerekmez, parametresiz.
public class KarPayiCalculatorTests
{
    private readonly KarPayiCalculator _calc = new();

    [Fact]
    public async Task Standard_AllSteps_Correct()
    {
        // Net kâr 1M, sermaye 5M, mevcut yedek 0, özel yedek yok.
        //   Yasal yedek limit: 5M × %20 = 1M; ayrılan min(1M × %5 = 50k, 1M) = 50k
        //   Dağıtılabilir: 1M - 50k = 950k
        //   Birinci temettü asgari: 5M × %5 = 250k; min(250k, 950k) = 250k
        //   Kalan: 950k - 250k = 700k
        //   Özel yedek: 0 (uygulanmıyor)
        //   İkinci temettü: 700k
        //   Toplam: 250k + 700k = 950k
        var r = await _calc.CalculateAsync(new KarPayiInput
        {
            NetKar = 1_000_000m,
            KanuniSermaye = 5_000_000m,
            MevcutYasalYedek = 0m,
            OzelYedekUygulanir = false
        });

        r.IsValid.Should().BeTrue();
        r.YasalYedekAyrilan.Should().Be(50_000m);
        r.YasalYedekLimit.Should().Be(1_000_000m);
        r.BirinciTemettu.Should().Be(250_000m);
        r.OzelYedekAyrilan.Should().Be(0m);
        r.IkinciTemettu.Should().Be(700_000m);
        r.ToplamTemettu.Should().Be(950_000m);
    }

    [Fact]
    public async Task YasalYedek_LimitUlasildi_StopAccumulating()
    {
        // Mevcut yedek 1M (sermaye %20 cap'i dolu). Yeni yedek 0.
        //   Dağıtılabilir: 1M (kesintisiz)
        //   Birinci temettü: 250k
        //   İkinci temettü: 750k
        //   Toplam: 1M
        var r = await _calc.CalculateAsync(new KarPayiInput
        {
            NetKar = 1_000_000m,
            KanuniSermaye = 5_000_000m,
            MevcutYasalYedek = 1_000_000m,
            OzelYedekUygulanir = false
        });

        r.YasalYedekAyrilan.Should().Be(0m);
        r.YasalYedekLimitDoldu.Should().BeTrue();
        r.ToplamTemettu.Should().Be(1_000_000m);
        r.Warnings.Should().Contain(w => w.Contains("yasal yedek", StringComparison.OrdinalIgnoreCase) ||
                                          w.Contains("sınırına", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task OzelYedek_Uygulaniyor_KalanIcinden10Yuzde()
    {
        // Net 1M, sermaye 5M, mevcut yedek 0, özel yedek var.
        //   Yasal yedek: 50k
        //   Dağıtılabilir: 950k
        //   Birinci temettü: 250k
        //   Kalan: 700k → özel yedek 70k → ikinci temettü 630k
        //   Toplam: 250k + 630k = 880k
        var r = await _calc.CalculateAsync(new KarPayiInput
        {
            NetKar = 1_000_000m,
            KanuniSermaye = 5_000_000m,
            MevcutYasalYedek = 0m,
            OzelYedekUygulanir = true
        });

        r.OzelYedekAyrilan.Should().Be(70_000m);
        r.IkinciTemettu.Should().Be(630_000m);
        r.ToplamTemettu.Should().Be(880_000m);
    }

    [Fact]
    public async Task OzelYedek_Uygulanmiyor_TumKalanIkinciTemettu()
    {
        var r = await _calc.CalculateAsync(new KarPayiInput
        {
            NetKar = 500_000m,
            KanuniSermaye = 2_000_000m,
            MevcutYasalYedek = 0m,
            OzelYedekUygulanir = false
        });

        // Yasal yedek: 25k; Dağıt: 475k; Birinci: 2M × 5% = 100k; Kalan: 375k; Özel yok; İkinci: 375k.
        r.OzelYedekAyrilan.Should().Be(0m);
        r.IkinciTemettu.Should().Be(375_000m);
    }

    [Fact]
    public async Task KarLow_BirinciTemettuCapped_TumKar()
    {
        // Net 100k, sermaye 5M (birinci asgari 250k > dağıtılabilir).
        //   Yasal yedek: 5k; Dağıt: 95k; Birinci: min(250k, 95k) = 95k; İkinci: 0; Toplam: 95k.
        var r = await _calc.CalculateAsync(new KarPayiInput
        {
            NetKar = 100_000m,
            KanuniSermaye = 5_000_000m,
            MevcutYasalYedek = 0m,
            OzelYedekUygulanir = false
        });

        r.BirinciTemettu.Should().Be(95_000m);
        r.IkinciTemettu.Should().Be(0m);
        r.ToplamTemettu.Should().Be(95_000m);
        r.Warnings.Should().Contain(w => w.Contains("birinci temett", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidationError_NegativeKar()
    {
        var r = await _calc.CalculateAsync(new KarPayiInput
        {
            NetKar = -100m,
            KanuniSermaye = 1_000_000m
        });

        r.IsValid.Should().BeFalse();
        r.ValidationErrors.Should().ContainKey(nameof(KarPayiInput.NetKar));
    }
}
