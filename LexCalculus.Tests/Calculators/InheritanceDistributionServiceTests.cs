using FluentAssertions;
using LexCalculus.Core.Services;
using Xunit;

namespace LexCalculus.Tests.Calculators;

// Saf hesap servisi — DB gerekmez.
public class InheritanceDistributionServiceTests
{
    private readonly InheritanceDistributionService _svc = new();

    [Fact]
    public void SakliPayOrani_TMK506_TumTurler()
    {
        _svc.SakliPayOrani("cocuk").Should().Be(0.5m);
        _svc.SakliPayOrani("torun").Should().Be(0.5m);
        _svc.SakliPayOrani("ana").Should().Be(0.25m);
        _svc.SakliPayOrani("baba").Should().Be(0.25m);
        // Eş 1./2. zümre ile tamamı, diğer hâller ¾.
        _svc.SakliPayOrani("es", 1).Should().Be(1.0m);
        _svc.SakliPayOrani("es", 2).Should().Be(1.0m);
        _svc.SakliPayOrani("es", 3).Should().Be(0.75m);
        _svc.SakliPayOrani("es").Should().Be(0.75m);
        // Saklı paysız mirasçılar
        _svc.SakliPayOrani("kardes").Should().Be(0m);
        _svc.SakliPayOrani("yegen").Should().Be(0m);
        _svc.SakliPayOrani("dede-nine").Should().Be(0m);
    }

    [Fact]
    public void Dagit_OlmusKardes_HalefiyetYegenler_2Derece()
    {
        // Ana-baba ölmüş, 1 sağ kardeş + 1 ölmüş kardeş (2 yeğen), eş yok → 2. derece.
        var yapi = new MirasciYapisi
        {
            KardesSayisi = 1,
            OlmusKardesler = new[] { new OlmusKardes { Tanim = "ölmüş kardeş", YeginSayisi = 2 } }
        };

        var d = _svc.Dagit(yapi, 1_200_000m);

        d.AktifDerece.Should().Be(2);
        // 2 kardeş kökü → perKardes 1/2. Sağ kardeş 1/2; her yeğen (1/2)/2 = 1/4.
        d.Paylar.Single(p => p.MirasciTuru == "kardes").PayKesri.Should().Be(0.5m);
        d.Paylar.Where(p => p.MirasciTuru == "yegen").Should().HaveCount(2);
        d.Paylar.First(p => p.MirasciTuru == "yegen").PayKesri.Should().Be(0.25m);
        d.Paylar.Sum(p => p.PayKesri).Should().BeApproximately(1.0m, 0.0001m);
    }
}
