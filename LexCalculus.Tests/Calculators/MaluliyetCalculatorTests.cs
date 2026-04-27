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

public class MaluliyetCalculatorTests
{
    private static IDistributedCache CreateCache() =>
        new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));

    private static (MaluliyetCalculator calc, LexCalculus.Infrastructure.Data.ApplicationDbContext ctx) Build()
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
                new() { Yas = 40, Cinsiyet = Cinsiyet.Erkek, BekledigiYasam = 34.93m }
            }
        };
        ctx.Set<LifeTable>().Add(trh);
        ctx.SaveChanges();

        var lifeTable = new LifeTableService(ctx, CreateCache(), NullLogger<LifeTableService>.Instance);
        var actuarial = new ActuarialService();
        return (new MaluliyetCalculator(lifeTable, actuarial), ctx);
    }

    [Fact]
    public async Task Referans_30_Yas_Erkek_Yuzde_35_Kayip()
    {
        var (calc, ctx) = Build();
        await using var _ = ctx;

        var input = new MaluliyetInput
        {
            OlayTarihi = new DateTime(2025, 1, 1),
            YaralananDogumTarihi = new DateTime(1995, 1, 1),
            Cinsiyet = Cinsiyet.Erkek,
            AylikGelir = 30000m,
            IsGucuKaybiOrani = 35m,
            YillikIskontoOrani = 3m,
            PasifDonemGelirOrani = 50
        };

        var r = await calc.CalculateAsync(input);

        r.IsValid.Should().BeTrue();
        r.YaralananYas.Should().Be(30);
        r.BekledigiYasam.Should().Be(44.45m);
        r.AktifYil.Should().Be(35);
        r.PasifYil.Should().Be(9);
        r.YillikGelir.Should().Be(360000m);
        r.YillikKayip.Should().Be(126000m);
        r.ToplamTazminat.Should().BeGreaterThan(0m);
    }

    [Fact]
    public async Task Yuzde_100_Kayip_Yillik_Kayip_Tum_Gelirine_Esit()
    {
        var (calc, ctx) = Build();
        await using var _ = ctx;

        var input = new MaluliyetInput
        {
            OlayTarihi = new DateTime(2025, 1, 1),
            YaralananDogumTarihi = new DateTime(1995, 1, 1),
            Cinsiyet = Cinsiyet.Erkek,
            AylikGelir = 30000m,
            IsGucuKaybiOrani = 100m
        };

        var r = await calc.CalculateAsync(input);

        r.IsValid.Should().BeTrue();
        r.YillikKayip.Should().Be(360000m);
    }

    [Fact]
    public async Task Iskonto_Sifir_Sade_Carpim()
    {
        var (calc, ctx) = Build();
        await using var _ = ctx;

        var input = new MaluliyetInput
        {
            OlayTarihi = new DateTime(2025, 1, 1),
            YaralananDogumTarihi = new DateTime(1995, 1, 1),
            Cinsiyet = Cinsiyet.Erkek,
            AylikGelir = 30000m,
            IsGucuKaybiOrani = 50m,
            YillikIskontoOrani = 0m,
            PasifDonemGelirOrani = 50
        };

        var r = await calc.CalculateAsync(input);

        // Aktif: 360000 × 0.50 × 35 = 6.300.000
        r.AktifPV.Should().Be(6_300_000m);
        // Pasif: 360000 × 0.50 × 0.50 × 9 = 810.000
        r.PasifPV.Should().Be(810_000m);
        r.ToplamTazminat.Should().Be(7_110_000m);
    }

    [Fact]
    public async Task Sifir_Kayip_Validation_Hatasi()
    {
        var (calc, ctx) = Build();
        await using var _ = ctx;

        var input = new MaluliyetInput
        {
            OlayTarihi = new DateTime(2025, 1, 1),
            YaralananDogumTarihi = new DateTime(1995, 1, 1),
            Cinsiyet = Cinsiyet.Erkek,
            AylikGelir = 30000m,
            IsGucuKaybiOrani = 0m
        };

        var r = await calc.CalculateAsync(input);

        r.IsValid.Should().BeFalse();
        r.ValidationErrors.Should().ContainKey(nameof(MaluliyetInput.IsGucuKaybiOrani));
    }

    [Fact]
    public async Task Yuzde_100_Uzeri_Kayip_Validation_Hatasi()
    {
        var (calc, ctx) = Build();
        await using var _ = ctx;

        var input = new MaluliyetInput
        {
            OlayTarihi = new DateTime(2025, 1, 1),
            YaralananDogumTarihi = new DateTime(1995, 1, 1),
            Cinsiyet = Cinsiyet.Erkek,
            AylikGelir = 30000m,
            IsGucuKaybiOrani = 150m
        };

        var r = await calc.CalculateAsync(input);

        r.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Olay_Dogum_Once_Validation_Hatasi()
    {
        var (calc, ctx) = Build();
        await using var _ = ctx;

        var input = new MaluliyetInput
        {
            OlayTarihi = new DateTime(1990, 1, 1),
            YaralananDogumTarihi = new DateTime(1995, 1, 1),
            AylikGelir = 30000m,
            IsGucuKaybiOrani = 35m
        };

        var r = await calc.CalculateAsync(input);

        r.IsValid.Should().BeFalse();
    }
}
