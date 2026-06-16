using FluentAssertions;
using LexCalculus.Core.Calculators.Ticaret;
using Xunit;

namespace LexCalculus.Tests.Calculators.Ticaret;

// Saf hesap — DB gerekmez, parametresiz.
public class SirketTasfiyePayiCalculatorTests
{
    private readonly SirketTasfiyePayiCalculator _calc = new();

    [Fact]
    public async Task StandardCase_NoImtiyaz_EqualDistribution()
    {
        // Net 3,5M ÷ 5 ortak = 700.000 / kişi.
        var r = await _calc.CalculateAsync(new SirketTasfiyePayiInput
        {
            ToplamVarlik = 5_000_000m,
            ToplamBorc = 1_500_000m,
            StandartOrtakSayisi = 5
        });

        r.IsValid.Should().BeTrue();
        r.NetTasfiyeVarliği.Should().Be(3_500_000m);
        r.ImtiyazliPayDagilimi.Should().BeEmpty();
        r.StandartPayKisiBasi.Should().Be(700_000m);
        r.ToplamPayDagitimi.Should().Be(3_500_000m);
    }

    [Fact]
    public async Task Imtiyazli_Var_BlokAyrildi()
    {
        // Net 1M; imtiyazlı %30 (300k) + %20 (200k) = 500k blok; kalan 500k / 5 ortak = 100k.
        var r = await _calc.CalculateAsync(new SirketTasfiyePayiInput
        {
            ToplamVarlik = 2_000_000m,
            ToplamBorc = 1_000_000m,
            StandartOrtakSayisi = 5,
            ImtiyazliPaylar = new List<ImtiyazliPayGirdi>
            {
                new() { OrtakAdi = "A", PayAdedi = 100, ImtiyazOraniYuzde = 30m },
                new() { OrtakAdi = "B", PayAdedi = 50, ImtiyazOraniYuzde = 20m }
            }
        });

        r.IsValid.Should().BeTrue();
        r.NetTasfiyeVarliği.Should().Be(1_000_000m);
        r.ImtiyazliBlokToplam.Should().Be(500_000m);
        r.ImtiyazliPayDagilimi.Should().HaveCount(2);
        r.ImtiyazliPayDagilimi[0].AlacagiTutar.Should().Be(300_000m);
        r.ImtiyazliPayDagilimi[1].AlacagiTutar.Should().Be(200_000m);
        r.StandartPayKisiBasi.Should().Be(100_000m);
    }

    [Fact]
    public async Task NegativeNet_UyariMessage()
    {
        // Borç > varlık → net 0'a clamp, uyarı.
        var r = await _calc.CalculateAsync(new SirketTasfiyePayiInput
        {
            ToplamVarlik = 1_000_000m,
            ToplamBorc = 1_500_000m,
            StandartOrtakSayisi = 3
        });

        r.IsValid.Should().BeTrue();
        r.NetTasfiyeVarliği.Should().Be(-500_000m);
        r.Warnings.Should().NotBeEmpty();
        r.TotalAmount.Should().Be(0m);
    }

    [Fact]
    public async Task BorcVarliktanFazla_NoDagitim()
    {
        // Sıfır net → standart pay 0, dağıtım yok.
        var r = await _calc.CalculateAsync(new SirketTasfiyePayiInput
        {
            ToplamVarlik = 500_000m,
            ToplamBorc = 500_000m,
            StandartOrtakSayisi = 2
        });

        r.NetTasfiyeVarliği.Should().Be(0m);
        r.TotalAmount.Should().Be(0m);
    }

    [Fact]
    public async Task ValidationError_ImtiyazlitToplam100Asar()
    {
        var r = await _calc.CalculateAsync(new SirketTasfiyePayiInput
        {
            ToplamVarlik = 1_000_000m,
            ToplamBorc = 0m,
            StandartOrtakSayisi = 1,
            ImtiyazliPaylar = new List<ImtiyazliPayGirdi>
            {
                new() { ImtiyazOraniYuzde = 60m },
                new() { ImtiyazOraniYuzde = 50m }
            }
        });

        r.IsValid.Should().BeFalse();
        r.ValidationErrors.Should().ContainKey(nameof(SirketTasfiyePayiInput.ImtiyazliPaylar));
    }
}
