using FluentAssertions;
using LexCalculus.Core.Calculators.Ceza;
using LexCalculus.Core.Services;
using Xunit;

namespace LexCalculus.Tests.Calculators.Ceza;

// Saf hesap — DB gerekmez. Takvim servisi sadece tarih aritmetiği için inject.
public class CezaErtelemeCalculatorTests
{
    private readonly CezaErtelemeCalculator _calc = new(new CriminalCalendarService());

    [Fact]
    public async Task Standard_OneYearSentence_Eligible()
    {
        // 1 yıl (365 gün) hapis, sicil temiz → uygun.
        var input = new CezaErtelemeInput
        {
            VerilenCezaGun = 365,
            CezaTuru = CezaTuru.HapisYetiskin,
            ErtelemeSuresi = ErtelemeSuresi.IkiYil,
            KararTarihi = new DateTime(2026, 1, 15),
            AdliSicilTemiz = true
        };

        var r = await _calc.CalculateAsync(input);

        r.IsValid.Should().BeTrue();
        r.ErtelemeyeUygunMu.Should().BeTrue();
        r.UygunsuzlukSebebi.Should().BeNull();
        r.UygulananUstSinirGun.Should().Be(730);
    }

    [Fact]
    public async Task CezaUstu2Yil_Yetiskin_Uygunsuz()
    {
        // 731 gün (2 yıl + 1 gün) → yetişkin için üst sınırı aşar.
        var input = new CezaErtelemeInput
        {
            VerilenCezaGun = 731,
            CezaTuru = CezaTuru.HapisYetiskin,
            ErtelemeSuresi = ErtelemeSuresi.IkiYil,
            KararTarihi = new DateTime(2026, 1, 15),
            AdliSicilTemiz = true
        };

        var r = await _calc.CalculateAsync(input);

        r.IsValid.Should().BeTrue();
        r.ErtelemeyeUygunMu.Should().BeFalse();
        r.UygunsuzlukSebebi.Should().Contain("üst sınır");
        r.UygunsuzlukSebebi.Should().Contain("731");
    }

    [Fact]
    public async Task SicilTemizDegil_Uygunsuz()
    {
        var input = new CezaErtelemeInput
        {
            VerilenCezaGun = 365,
            CezaTuru = CezaTuru.HapisYetiskin,
            ErtelemeSuresi = ErtelemeSuresi.BirYil,
            KararTarihi = new DateTime(2026, 1, 15),
            AdliSicilTemiz = false
        };

        var r = await _calc.CalculateAsync(input);

        r.ErtelemeyeUygunMu.Should().BeFalse();
        r.UygunsuzlukSebebi.Should().Contain("kasıtlı suç").And.Contain("3 ay");
    }

    [Fact]
    public async Task CocukCeza_3YilLimit_AllowsAbove2Years()
    {
        // Çocuk için 800 gün (≈ 2 yıl 2 ay) → yetişkin için aşar, çocuk için uygun.
        var input = new CezaErtelemeInput
        {
            VerilenCezaGun = 800,
            CezaTuru = CezaTuru.HapisCocuk,
            ErtelemeSuresi = ErtelemeSuresi.UcYil,
            KararTarihi = new DateTime(2026, 1, 15),
            AdliSicilTemiz = true
        };

        var r = await _calc.CalculateAsync(input);

        r.IsValid.Should().BeTrue();
        r.ErtelemeyeUygunMu.Should().BeTrue();
        r.UygulananUstSinirGun.Should().Be(1095);
    }

    [Fact]
    public async Task AdliPara_Uygunsuz_TCK51HapisOzgu()
    {
        var input = new CezaErtelemeInput
        {
            VerilenCezaGun = 100,
            CezaTuru = CezaTuru.AdliPara,
            ErtelemeSuresi = ErtelemeSuresi.BirYil,
            KararTarihi = new DateTime(2026, 1, 15),
            AdliSicilTemiz = true
        };

        var r = await _calc.CalculateAsync(input);

        r.ErtelemeyeUygunMu.Should().BeFalse();
        r.UygunsuzlukSebebi.Should().Contain("Adli para");
    }

    [Fact]
    public async Task ErtelemeSureBitis_DateCalculation_KararPlus24Ay()
    {
        var karar = new DateTime(2026, 1, 15);
        var input = new CezaErtelemeInput
        {
            VerilenCezaGun = 365,
            CezaTuru = CezaTuru.HapisYetiskin,
            ErtelemeSuresi = ErtelemeSuresi.IkiYil,
            KararTarihi = karar,
            AdliSicilTemiz = true,
            DenetimliSerbestlikAy = 12
        };

        var r = await _calc.CalculateAsync(input);

        r.ErtelemeyeUygunMu.Should().BeTrue();
        r.ErtelemeBitisTarihi.Should().Be(new DateOnly(2028, 1, 15));
        r.DenetimliSerbestlikBitisTarihi.Should().Be(new DateOnly(2027, 1, 15));
    }
}
