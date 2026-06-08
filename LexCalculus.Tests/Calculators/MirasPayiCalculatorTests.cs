using FluentAssertions;
using LexCalculus.Core.Calculators.AileMiras;
using LexCalculus.Core.Services;
using Xunit;

namespace LexCalculus.Tests.Calculators;

// Calculator saf servisi (InheritanceDistributionService) sarar — DB gerekmez.
public class MirasPayiCalculatorTests
{
    private static MirasPayiCalculator Build() => new(new InheritanceDistributionService());

    private static MirasPayiSatiri? Pay(MirasPayiResult r, string tur) =>
        r.PayListesi.FirstOrDefault(p => p.MirasciTuru == tur);

    [Fact]
    public async Task EsVeIkiCocuk_StandardDistribution()
    {
        var calc = Build();
        var input = new MirasPayiInput
        {
            ToplamMalvarligi = 800_000m,
            Yapi = new MirasciYapisiInput { SagKalanEsVar = true, SagCocukSayisi = 2 }
        };

        var r = await calc.CalculateAsync(input);

        r.IsValid.Should().BeTrue();
        r.AktifDerece.Should().Be(1);
        Pay(r, "es")!.PayKesri.Should().Be(0.25m);
        Pay(r, "es")!.PayTutari.Should().Be(200_000m);
        r.PayListesi.Where(p => p.MirasciTuru == "cocuk").Should().HaveCount(2);
        r.PayListesi.First(p => p.MirasciTuru == "cocuk").PayKesri.Should().Be(0.375m); // 3/8
        r.PayListesi.First(p => p.MirasciTuru == "cocuk").PayTutari.Should().Be(300_000m);
    }

    [Fact]
    public async Task EsVeAnaBaba_2DereceDistribution()
    {
        var calc = Build();
        var input = new MirasPayiInput
        {
            ToplamMalvarligi = 400_000m,
            Yapi = new MirasciYapisiInput { SagKalanEsVar = true, AnaSag = true, BabaSag = true }
        };

        var r = await calc.CalculateAsync(input);

        r.IsValid.Should().BeTrue();
        r.AktifDerece.Should().Be(2);
        Pay(r, "es")!.PayKesri.Should().Be(0.5m);
        Pay(r, "ana")!.PayKesri.Should().Be(0.25m);
        Pay(r, "baba")!.PayKesri.Should().Be(0.25m);
        Pay(r, "ana")!.PayTutari.Should().Be(100_000m);
    }

    [Fact]
    public async Task SadeceEs_TumMiras()
    {
        var calc = Build();
        var input = new MirasPayiInput
        {
            ToplamMalvarligi = 500_000m,
            Yapi = new MirasciYapisiInput { SagKalanEsVar = true }
        };

        var r = await calc.CalculateAsync(input);

        r.IsValid.Should().BeTrue();
        r.AktifDerece.Should().Be(0);
        Pay(r, "es")!.PayKesri.Should().Be(1.0m);
        Pay(r, "es")!.PayTutari.Should().Be(500_000m);
    }

    [Fact]
    public async Task SadeceCocuklar_EsitDagilim()
    {
        var calc = Build();
        var input = new MirasPayiInput
        {
            ToplamMalvarligi = 900_000m,
            Yapi = new MirasciYapisiInput { SagCocukSayisi = 3 }
        };

        var r = await calc.CalculateAsync(input);

        r.IsValid.Should().BeTrue();
        r.AktifDerece.Should().Be(1);
        r.PayListesi.Where(p => p.MirasciTuru == "cocuk").Should().HaveCount(3);
        r.PayListesi.First(p => p.MirasciTuru == "cocuk").PayKesri.Should().BeApproximately(0.3333m, 0.001m);
        r.PayListesi.First(p => p.MirasciTuru == "cocuk").PayTutari.Should().Be(300_000m);
        r.PayListesi.Sum(p => p.PayTutari).Should().Be(900_000m);
    }

    [Fact]
    public async Task OlmusCocuk_HalefiyetTorunlar()
    {
        var calc = Build();
        // 1 sağ çocuk + 1 ölmüş çocuk (2 torun), eş yok.
        var input = new MirasPayiInput
        {
            ToplamMalvarligi = 800_000m,
            Yapi = new MirasciYapisiInput
            {
                SagCocukSayisi = 1,
                OlmusCocuklar = new List<OlmusCocukGirdi> { new() { Tanim = "A", TorunSayisi = 2 } }
            }
        };

        var r = await calc.CalculateAsync(input);

        r.IsValid.Should().BeTrue();
        r.AktifDerece.Should().Be(1);
        // 2 kök → sağ çocuk 1/2 = 400.000; her torun (1/2)/2 = 1/4 = 200.000.
        r.PayListesi.First(p => p.MirasciTuru == "cocuk").PayTutari.Should().Be(400_000m);
        r.PayListesi.Where(p => p.MirasciTuru == "torun").Should().HaveCount(2);
        r.PayListesi.First(p => p.MirasciTuru == "torun").PayTutari.Should().Be(200_000m);
        r.PayListesi.Sum(p => p.PayTutari).Should().Be(800_000m);
    }

    [Fact]
    public async Task EsVe3Derece_DedeNine()
    {
        var calc = Build();
        var input = new MirasPayiInput
        {
            ToplamMalvarligi = 800_000m,
            Yapi = new MirasciYapisiInput { SagKalanEsVar = true, DedeNineSayisi = 2 }
        };

        var r = await calc.CalculateAsync(input);

        r.IsValid.Should().BeTrue();
        r.AktifDerece.Should().Be(3);
        Pay(r, "es")!.PayKesri.Should().Be(0.75m);
        Pay(r, "es")!.PayTutari.Should().Be(600_000m);
        r.PayListesi.Where(p => p.MirasciTuru == "dede-nine").Should().HaveCount(2);
        r.PayListesi.First(p => p.MirasciTuru == "dede-nine").PayTutari.Should().Be(100_000m); // 1/8
    }

    [Fact]
    public async Task HicMirasci_Reddedildi()
    {
        var calc = Build();
        var input = new MirasPayiInput
        {
            ToplamMalvarligi = 500_000m,
            Yapi = new MirasciYapisiInput() // hiç mirasçı yok
        };

        var r = await calc.CalculateAsync(input);

        r.IsValid.Should().BeFalse();
        r.ValidationErrors.Should().ContainKey(nameof(MirasPayiInput.Yapi));
    }

    [Fact]
    public async Task Reference_TMK495_499_StandardSplit()
    {
        var calc = Build();
        // TMK m.499/1 + m.495: eş + 1 çocuk → eş ¼, çocuk ¾.
        var input = new MirasPayiInput
        {
            ToplamMalvarligi = 1_000_000m,
            Yapi = new MirasciYapisiInput { SagKalanEsVar = true, SagCocukSayisi = 1 }
        };

        var r = await calc.CalculateAsync(input);

        r.IsValid.Should().BeTrue();
        Pay(r, "es")!.PayKesri.Should().Be(0.25m);
        Pay(r, "es")!.PayTutari.Should().Be(250_000m);
        Pay(r, "cocuk")!.PayKesri.Should().Be(0.75m);
        Pay(r, "cocuk")!.PayTutari.Should().Be(750_000m);
        r.PayListesi.Sum(p => p.PayKesri).Should().Be(1.0m);
    }
}
