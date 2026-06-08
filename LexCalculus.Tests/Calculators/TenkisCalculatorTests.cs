using FluentAssertions;
using LexCalculus.Core.Calculators.AileMiras;
using LexCalculus.Core.Services;
using Xunit;

namespace LexCalculus.Tests.Calculators;

// Calculator saf servisi sarar — DB gerekmez.
public class TenkisCalculatorTests
{
    private static TenkisCalculator Build() => new(new InheritanceDistributionService());

    private static MirasciYapisiInput EsVeCocuk(int cocuk) =>
        new() { SagKalanEsVar = true, SagCocukSayisi = cocuk };

    [Fact]
    public async Task NoTasarruf_NoTenkis()
    {
        var calc = Build();
        var input = new TenkisInput { ToplamMalvarligi = 1_000_000m, Yapi = EsVeCocuk(1) };

        var r = await calc.CalculateAsync(input);

        r.IsValid.Should().BeTrue();
        r.SakliPayIhlali.Should().BeFalse();
        r.IhlalTutari.Should().Be(0m);
        // Saklı: eş 250.000 (¼ × 1.0), çocuk 375.000 (¾ × ½) → 625.000; nisap 375.000.
        r.ToplamSakliPay.Should().Be(625_000m);
        r.TasarrufNisabi.Should().Be(375_000m);
    }

    [Fact]
    public async Task VasiyetIhlali_TenkisVasiyet()
    {
        var calc = Build();
        var input = new TenkisInput { ToplamMalvarligi = 1_000_000m, Yapi = EsVeCocuk(1), VasiyetnameTutari = 600_000m };

        var r = await calc.CalculateAsync(input);

        r.IsValid.Should().BeTrue();
        r.SakliPayIhlali.Should().BeTrue();
        r.IhlalTutari.Should().Be(225_000m); // 600.000 - 375.000
        var vasiyet = r.TenkisKalemleri.Single(k => k.Tur == "vasiyet");
        vasiyet.TenkisTutari.Should().Be(225_000m);
    }

    [Fact]
    public async Task BagisIhlali_TenkisSonBagistan()
    {
        var calc = Build();
        var input = new TenkisInput
        {
            ToplamMalvarligi = 400_000m,
            Yapi = EsVeCocuk(1),
            Bagislar = new List<BagisGirdi> { new() { Tarih = new DateTime(2020, 1, 1), Tutar = 600_000m, AliciTanim = "X" } }
        };

        var r = await calc.CalculateAsync(input);

        r.IsValid.Should().BeTrue();
        r.TenkiseEsasMatrah.Should().Be(1_000_000m); // 400.000 + 600.000
        r.SakliPayIhlali.Should().BeTrue();
        r.IhlalTutari.Should().Be(225_000m);
        var bagis = r.TenkisKalemleri.Single(k => k.Tur == "bagis");
        bagis.TenkisTutari.Should().Be(225_000m);
    }

    [Fact]
    public async Task MixedIhlal_OnceVasiyetSonraBagis()
    {
        var calc = Build();
        var input = new TenkisInput
        {
            ToplamMalvarligi = 500_000m,
            Yapi = EsVeCocuk(1),
            VasiyetnameTutari = 300_000m,
            Bagislar = new List<BagisGirdi> { new() { Tarih = new DateTime(2019, 1, 1), Tutar = 400_000m, AliciTanim = "X" } }
        };

        var r = await calc.CalculateAsync(input);

        r.IsValid.Should().BeTrue();
        // TEM 900.000; saklı eş 225.000 + çocuk 337.500 = 562.500; nisap 337.500; tasarruf 700.000; ihlal 362.500.
        r.IhlalTutari.Should().Be(362_500m);
        // TMK m.561: önce vasiyet tamamı (300.000), kalan 62.500 bağıştan.
        r.TenkisKalemleri.Single(k => k.Tur == "vasiyet").TenkisTutari.Should().Be(300_000m);
        r.TenkisKalemleri.Single(k => k.Tur == "bagis").TenkisTutari.Should().Be(62_500m);
    }

    [Fact]
    public async Task CokluBagis_SonBagisOnce()
    {
        var calc = Build();
        // Çocuk tek mirasçı (saklı pay ½), malvarlığı 0, 3 eşit bağış farklı tarihlerde.
        var input = new TenkisInput
        {
            ToplamMalvarligi = 0m,
            Yapi = new MirasciYapisiInput { SagCocukSayisi = 1 },
            Bagislar = new List<BagisGirdi>
            {
                new() { Tarih = new DateTime(2018, 1, 1), Tutar = 200_000m, AliciTanim = "Eski" },
                new() { Tarih = new DateTime(2020, 1, 1), Tutar = 200_000m, AliciTanim = "Orta" },
                new() { Tarih = new DateTime(2022, 1, 1), Tutar = 200_000m, AliciTanim = "Yeni" }
            }
        };

        var r = await calc.CalculateAsync(input);

        r.IsValid.Should().BeTrue();
        // TEM 600.000; çocuk saklı pay 300.000; nisap 300.000; tasarruf 600.000; ihlal 300.000.
        r.IhlalTutari.Should().Be(300_000m);
        // Son bağıştan geriye: 2022 tamamı (200.000), 2020 kısmen (100.000), 2018 dokunulmaz (0).
        r.TenkisKalemleri.Single(k => k.Tanim.Contains("2022")).TenkisTutari.Should().Be(200_000m);
        r.TenkisKalemleri.Single(k => k.Tanim.Contains("2020")).TenkisTutari.Should().Be(100_000m);
        r.TenkisKalemleri.Single(k => k.Tanim.Contains("2018")).TenkisTutari.Should().Be(0m);
    }

    [Fact]
    public async Task CocukSakliPayi_TMK506()
    {
        var calc = Build();
        // Çocuk tek mirasçı: yasal pay 1.0, saklı pay oranı ½ → saklı pay = ½ × TEM.
        var input = new TenkisInput { ToplamMalvarligi = 1_000_000m, Yapi = new MirasciYapisiInput { SagCocukSayisi = 1 } };

        var r = await calc.CalculateAsync(input);

        r.IsValid.Should().BeTrue();
        var cocuk = r.SakliPaylar.Single(s => s.MirasciTuru == "cocuk");
        cocuk.YasalPayKesri.Should().Be(1.0m);
        cocuk.SakliPayOrani.Should().Be(0.5m);
        cocuk.SakliPayTutari.Should().Be(500_000m);
    }

    [Fact]
    public async Task Reference_FullCycle_TMK561()
    {
        var calc = Build();
        // Eş + 2 çocuk, malvarlığı 800.000, vasiyet 200.000, bağış 300.000 (2021).
        var input = new TenkisInput
        {
            ToplamMalvarligi = 800_000m,
            Yapi = EsVeCocuk(2),
            VasiyetnameTutari = 200_000m,
            Bagislar = new List<BagisGirdi> { new() { Tarih = new DateTime(2021, 1, 1), Tutar = 300_000m, AliciTanim = "X" } }
        };

        var r = await calc.CalculateAsync(input);

        r.IsValid.Should().BeTrue();
        // TEM 1.100.000; saklı eş 275.000 + çocuk 2×206.250 = 687.500; nisap 412.500; tasarruf 500.000; ihlal 87.500.
        r.TenkiseEsasMatrah.Should().Be(1_100_000m);
        r.ToplamSakliPay.Should().Be(687_500m);
        r.IhlalTutari.Should().Be(87_500m);
        // İhlal vasiyetten karşılanır (87.500 < 200.000); bağışa dokunulmaz.
        r.TenkisKalemleri.Single(k => k.Tur == "vasiyet").TenkisTutari.Should().Be(87_500m);
        r.TenkisKalemleri.Single(k => k.Tur == "bagis").TenkisTutari.Should().Be(0m);
    }
}
