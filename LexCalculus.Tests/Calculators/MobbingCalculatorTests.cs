using FluentAssertions;
using LexCalculus.Core.Calculators.IsHukuku;
using LexCalculus.Infrastructure.Calculators;
using LexCalculus.Tests.TestHelpers;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace LexCalculus.Tests.Calculators;

public class MobbingCalculatorTests
{
    private static IDistributedCache CreateCache() =>
        new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));

    private static MobbingCalculator Build()
    {
        var ctx = TestDbContextFactory.Create();
        var paramSvc = new FormulaParameterService(ctx, CreateCache(), new NullActivityLogService(), NullLogger<FormulaParameterService>.Instance);
        return new MobbingCalculator(paramSvc);
    }

    [Theory]
    [InlineData(MobbingSiddeti.Hafif, 1, 3)]
    [InlineData(MobbingSiddeti.Orta, 3, 6)]
    [InlineData(MobbingSiddeti.Agir, 6, 12)]
    [InlineData(MobbingSiddeti.CokAgir, 12, 24)]
    public async Task Siddet_Tabani_Dogru(MobbingSiddeti siddet, int beklenenTaban, int beklenenUst)
    {
        var calc = Build();

        var input = new MobbingInput
        {
            BrutAylikUcret = 30000m,
            SureAy = 12,
            Siddet = siddet,
            IsverenTipi = IsverenKonumu.OzelSektor
        };

        var r = await calc.CalculateAsync(input);

        r.IsValid.Should().BeTrue();
        r.TabanAyKatsayisi.Should().Be(beklenenTaban);
        r.UstAyKatsayisi.Should().Be(beklenenUst);
    }

    [Fact]
    public async Task Saglik_Raporu_Ust_Sinira_3_Ay_Ekler()
    {
        var calc = Build();

        var input = new MobbingInput
        {
            BrutAylikUcret = 30000m,
            SureAy = 12,
            Siddet = MobbingSiddeti.Orta,
            SaglikRaporu = true,
            IsverenTipi = IsverenKonumu.OzelSektor
        };

        var r = await calc.CalculateAsync(input);

        r.UstAyKatsayisi.Should().Be(9);
        r.UstSinirTutar.Should().Be(270000m);
    }

    [Fact]
    public async Task Tum_Faktorler_Birlikte_Kumulatif_Eklenir()
    {
        var calc = Build();

        var input = new MobbingInput
        {
            BrutAylikUcret = 30000m,
            SureAy = 36,
            Siddet = MobbingSiddeti.CokAgir,
            SaglikRaporu = true,
            IstifaSebebi = true,
            IsverenTipi = IsverenKonumu.BuyukHolding
        };

        var r = await calc.CalculateAsync(input);

        r.UstAyKatsayisi.Should().Be(32);
        r.EmsalKarakteristik.Should().HaveCount(4);
    }

    [Fact]
    public async Task Az_Sure_Mahkemeye_Uygun_Degil_Uyari()
    {
        var calc = Build();

        var input = new MobbingInput
        {
            BrutAylikUcret = 30000m,
            SureAy = 3,
            Siddet = MobbingSiddeti.Orta,
            IsverenTipi = IsverenKonumu.OzelSektor
        };

        var r = await calc.CalculateAsync(input);

        r.IsValid.Should().BeTrue();
        r.MahkemeyeUygunDelilVarMi.Should().BeFalse();
        r.Warnings.Should().Contain(w => w.Contains("6 ay"));
    }

    [Fact]
    public async Task Agir_Mobbing_Saglik_Raporu_Yoksa_Uyari()
    {
        var calc = Build();

        var input = new MobbingInput
        {
            BrutAylikUcret = 30000m,
            SureAy = 18,
            Siddet = MobbingSiddeti.Agir,
            SaglikRaporu = false,
            IsverenTipi = IsverenKonumu.OzelSektor
        };

        var r = await calc.CalculateAsync(input);

        r.Warnings.Should().Contain(w => w.Contains("sağlık raporu"));
    }

    [Fact]
    public async Task Onerilen_Tutar_Aralik_Icinde()
    {
        var calc = Build();

        var input = new MobbingInput
        {
            BrutAylikUcret = 30000m,
            SureAy = 12,
            Siddet = MobbingSiddeti.Orta,
            IsverenTipi = IsverenKonumu.OzelSektor
        };

        var r = await calc.CalculateAsync(input);

        r.OnerilenTutar.Should().BeGreaterThanOrEqualTo(r.AltSinirTutar);
        r.OnerilenTutar.Should().BeLessThanOrEqualTo(r.UstSinirTutar);
    }

    [Fact]
    public async Task Negatif_Brut_Validation_Hatasi()
    {
        var calc = Build();

        var input = new MobbingInput
        {
            BrutAylikUcret = -100m,
            SureAy = 12,
            Siddet = MobbingSiddeti.Orta
        };

        var r = await calc.CalculateAsync(input);

        r.IsValid.Should().BeFalse();
        r.ValidationErrors.Should().ContainKey(nameof(MobbingInput.BrutAylikUcret));
    }
}
