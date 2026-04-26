using FluentAssertions;
using LexCalculus.Core.Calculators.IsHukuku;
using LexCalculus.Core.Entities.Calculators;
using LexCalculus.Infrastructure.Calculators;
using LexCalculus.Tests.TestHelpers;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace LexCalculus.Tests.Calculators;

public class YillikIzinCalculatorTests
{
    private static IDistributedCache CreateCache() =>
        new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));

    private static (YillikIzinCalculator calc, LexCalculus.Infrastructure.Data.ApplicationDbContext ctx) Build()
    {
        var ctx = TestDbContextFactory.Create();
        ctx.Set<FormulaParameter>().AddRange(
            new FormulaParameter { ToolSlug = "*", Key = "damga-vergisi-orani", Value = 0.00759m, EffectiveDate = new DateTime(2020, 1, 1) },
            new FormulaParameter { ToolSlug = "ihbar-tazminati", Key = "gelir-vergisi-orani-basit", Value = 0.15m, EffectiveDate = new DateTime(2020, 1, 1) }
        );
        ctx.SaveChanges();
        var paramSvc = new FormulaParameterService(ctx, CreateCache(), NullLogger<FormulaParameterService>.Instance);
        return (new YillikIzinCalculator(paramSvc), ctx);
    }

    [Theory]
    [InlineData(2, 14)]
    [InlineData(4, 14)]
    [InlineData(5, 20)]
    [InlineData(10, 20)]
    [InlineData(15, 26)]
    [InlineData(20, 26)]
    public async Task Yillik_Izin_Tablosu_Dogru(int yil, int beklenenGun)
    {
        var (calc, ctx) = Build();
        await using var _ = ctx;

        var input = new YillikIzinInput
        {
            GirisTarihi = new DateTime(2024, 1, 1).AddYears(-yil),
            CikisTarihi = new DateTime(2024, 1, 1),
            BrutAylikUcret = 30000m
        };

        var r = await calc.CalculateAsync(input);

        r.IsValid.Should().BeTrue();
        r.YillikIzinGunHakki.Should().Be(beklenenGun);
    }

    [Fact]
    public async Task Yas_Ozel_Hukmu_50_Ustu_Icin_Min_20_Gun()
    {
        var (calc, ctx) = Build();
        await using var _ = ctx;

        var input = new YillikIzinInput
        {
            GirisTarihi = new DateTime(2022, 1, 1),
            CikisTarihi = new DateTime(2025, 1, 1),
            DogumTarihi = new DateTime(1969, 6, 1),
            BrutAylikUcret = 30000m
        };

        var r = await calc.CalculateAsync(input);

        r.IsValid.Should().BeTrue();
        r.YasOzelHukum.Should().BeTrue();
        r.YillikIzinGunHakki.Should().Be(20);
    }

    [Fact]
    public async Task Yas_Ozel_Hukmu_18_Alti_Icin_Min_20_Gun()
    {
        var (calc, ctx) = Build();
        await using var _ = ctx;

        var input = new YillikIzinInput
        {
            GirisTarihi = new DateTime(2023, 6, 1),
            CikisTarihi = new DateTime(2024, 6, 15),
            DogumTarihi = new DateTime(2007, 1, 1),
            BrutAylikUcret = 20000m
        };

        var r = await calc.CalculateAsync(input);

        r.IsValid.Should().BeTrue();
        r.YasOzelHukum.Should().BeTrue();
        r.YillikIzinGunHakki.Should().Be(20);
    }

    [Fact]
    public async Task Reference_Case_3_Yil_30000_Brut_42_Gun_Hak()
    {
        var (calc, ctx) = Build();
        await using var _ = ctx;

        var input = new YillikIzinInput
        {
            GirisTarihi = new DateTime(2022, 1, 1),
            CikisTarihi = new DateTime(2025, 1, 1),
            BrutAylikUcret = 30000m,
            KullanilanIzinGunu = 0
        };

        var r = await calc.CalculateAsync(input);

        r.IsValid.Should().BeTrue();
        r.TamYil.Should().Be(3);
        r.YillikIzinGunHakki.Should().Be(14);
        r.ToplamHakEdilenIzin.Should().Be(42);
        r.KullanilmayanIzin.Should().Be(42);

        r.BrutIzinUcreti.Should().Be(42000m);
        r.DamgaVergisi.Should().BeApproximately(318.78m, 0.5m);
        r.GelirVergisi.Should().BeApproximately(6300m, 0.5m);
        r.NetIzinUcreti.Should().BeApproximately(35381.22m, 1m);
    }

    [Fact]
    public async Task Kullanilan_Izin_Hak_Edilen_Den_Fazla_Ise_Sifir_ve_Uyari()
    {
        var (calc, ctx) = Build();
        await using var _ = ctx;

        var input = new YillikIzinInput
        {
            GirisTarihi = new DateTime(2022, 1, 1),
            CikisTarihi = new DateTime(2025, 1, 1),
            BrutAylikUcret = 30000m,
            KullanilanIzinGunu = 100
        };

        var r = await calc.CalculateAsync(input);

        r.IsValid.Should().BeTrue();
        r.KullanilmayanIzin.Should().Be(0);
        r.NetIzinUcreti.Should().Be(0m);
        r.Warnings.Should().Contain(w => w.Contains("hak edilenden fazla"));
    }

    [Fact]
    public async Task Bir_Yildan_Az_Calismada_Validation_Hatasi()
    {
        var (calc, ctx) = Build();
        await using var _ = ctx;

        var input = new YillikIzinInput
        {
            GirisTarihi = new DateTime(2024, 6, 1),
            CikisTarihi = new DateTime(2024, 12, 1),
            BrutAylikUcret = 30000m
        };

        var r = await calc.CalculateAsync(input);

        r.IsValid.Should().BeFalse();
        r.ValidationErrors.Should().ContainKey(nameof(YillikIzinInput.CikisTarihi));
    }
}
