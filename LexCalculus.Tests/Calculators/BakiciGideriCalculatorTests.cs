using FluentAssertions;
using LexCalculus.Core.Calculators.Akturya;
using LexCalculus.Core.Entities.Calculators;
using LexCalculus.Core.Enums;
using LexCalculus.Infrastructure.Calculators;
using LexCalculus.Tests.TestHelpers;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace LexCalculus.Tests.Calculators;

public class BakiciGideriCalculatorTests
{
    private static IDistributedCache CreateCache() =>
        new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));

    private static (BakiciGideriCalculator calc, LexCalculus.Infrastructure.Data.ApplicationDbContext ctx) Build()
    {
        var ctx = TestDbContextFactory.Create();
        var trh = new LifeTable
        {
            Code = "TRH-2010",
            Name = "TRH 2010",
            EffectiveDate = new DateTime(2010, 1, 1),
            IsActive = true,
            Rows = new List<LifeTableRow>
            {
                new() { Yas = 30, Cinsiyet = Cinsiyet.Erkek, BekledigiYasam = 44.45m },
                new() { Yas = 30, Cinsiyet = Cinsiyet.Kadin, BekledigiYasam = 49.00m },
                new() { Yas = 50, Cinsiyet = Cinsiyet.Erkek, BekledigiYasam = 25.79m }
            }
        };
        ctx.Set<LifeTable>().Add(trh);
        ctx.SaveChanges();

        var lifeTable = new LifeTableService(ctx, CreateCache(), NullLogger<LifeTableService>.Instance);
        var actuarial = new ActuarialService();
        return (new BakiciGideriCalculator(lifeTable, actuarial), ctx);
    }

    [Fact]
    public async Task Tam_Bakim_Yuzde_100_Dogru_Hesaplar()
    {
        var (calc, ctx) = Build();
        await using var _ = ctx;

        var input = new BakiciGideriInput
        {
            OlayTarihi = new DateTime(2025, 1, 1),
            YaralananDogumTarihi = new DateTime(1995, 1, 1),
            Cinsiyet = Cinsiyet.Erkek,
            AylikBakiciMaliyeti = 26000m,
            BakimIhtiyacOrani = 100m,
            YillikIskontoOrani = 3.0m
        };

        var r = await calc.CalculateAsync(input);

        r.IsValid.Should().BeTrue();
        r.YaralananYas.Should().Be(30);
        r.DestekSuresiYil.Should().Be(44);
        r.AylikEffektifMaliyet.Should().Be(26000m);
        r.YillikMaliyet.Should().Be(312000m);
        r.ToplamTazminat.Should().BeGreaterThan(0m);
    }

    [Fact]
    public async Task Yarim_Bakim_Yuzde_50_Yarim_Maliyet()
    {
        var (calc, ctx) = Build();
        await using var _ = ctx;

        var input = new BakiciGideriInput
        {
            OlayTarihi = new DateTime(2025, 1, 1),
            YaralananDogumTarihi = new DateTime(1995, 1, 1),
            Cinsiyet = Cinsiyet.Erkek,
            AylikBakiciMaliyeti = 26000m,
            BakimIhtiyacOrani = 50m,
            YillikIskontoOrani = 3.0m
        };

        var r = await calc.CalculateAsync(input);

        r.AylikEffektifMaliyet.Should().Be(13000m);
        r.YillikMaliyet.Should().Be(156000m);
    }

    [Fact]
    public async Task Iskonto_Sifir_Sade_Carpim()
    {
        var (calc, ctx) = Build();
        await using var _ = ctx;

        var input = new BakiciGideriInput
        {
            OlayTarihi = new DateTime(2025, 1, 1),
            YaralananDogumTarihi = new DateTime(1995, 1, 1),
            Cinsiyet = Cinsiyet.Erkek,
            AylikBakiciMaliyeti = 26000m,
            BakimIhtiyacOrani = 100m,
            YillikIskontoOrani = 0m
        };

        var r = await calc.CalculateAsync(input);

        // 312000 × 44 yıl = 13.728.000 (iskonto yok)
        r.ToplamTazminat.Should().Be(13_728_000m);
    }

    [Fact]
    public async Task Yasli_Birey_Daha_Az_Toplam()
    {
        var (calc, ctx) = Build();
        await using var _ = ctx;

        var input = new BakiciGideriInput
        {
            OlayTarihi = new DateTime(2025, 1, 1),
            YaralananDogumTarihi = new DateTime(1975, 1, 1),
            Cinsiyet = Cinsiyet.Erkek,
            AylikBakiciMaliyeti = 26000m,
            BakimIhtiyacOrani = 100m,
            YillikIskontoOrani = 3.0m
        };

        var r = await calc.CalculateAsync(input);

        r.IsValid.Should().BeTrue();
        r.YaralananYas.Should().Be(50);
        r.DestekSuresiYil.Should().Be(25);
    }

    [Fact]
    public async Task Sifir_Bakim_Orani_Validation_Hatasi()
    {
        var (calc, ctx) = Build();
        await using var _ = ctx;

        var input = new BakiciGideriInput
        {
            OlayTarihi = new DateTime(2025, 1, 1),
            YaralananDogumTarihi = new DateTime(1995, 1, 1),
            Cinsiyet = Cinsiyet.Erkek,
            AylikBakiciMaliyeti = 26000m,
            BakimIhtiyacOrani = 0m
        };

        var r = await calc.CalculateAsync(input);

        r.IsValid.Should().BeFalse();
    }
}
