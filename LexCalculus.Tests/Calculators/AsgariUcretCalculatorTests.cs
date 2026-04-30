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

public class AsgariUcretCalculatorTests
{
    private static IDistributedCache CreateCache() =>
        new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));

    private static (AsgariUcretCalculator calc, LexCalculus.Infrastructure.Data.ApplicationDbContext ctx) Build()
    {
        var ctx = TestDbContextFactory.Create();
        ctx.Set<FormulaParameter>().AddRange(
            new FormulaParameter { ToolSlug = "*", Key = "asgari-ucret-brut", Value = 20002.50m, EffectiveDate = new DateTime(2024, 1, 1) },
            new FormulaParameter { ToolSlug = "*", Key = "asgari-ucret-brut", Value = 26005.50m, EffectiveDate = new DateTime(2025, 1, 1) },
            new FormulaParameter { ToolSlug = "*", Key = "asgari-ucret-brut", Value = 33000.00m, EffectiveDate = new DateTime(2026, 1, 1) },
            new FormulaParameter { ToolSlug = "*", Key = "yasal-faiz-orani-yillik", Value = 0.18m, EffectiveDate = new DateTime(2020, 1, 1) }
        );
        ctx.SaveChanges();
        var paramSvc = new FormulaParameterService(ctx, CreateCache(), new NullActivityLogService(), NullLogger<FormulaParameterService>.Instance);
        return (new AsgariUcretCalculator(paramSvc), ctx);
    }

    [Fact]
    public async Task Eksik_Yok_Sonuc_Sifir()
    {
        var (calc, ctx) = Build();
        await using var _ = ctx;

        var input = new AsgariUcretInput
        {
            BaslangicTarihi = new DateTime(2024, 1, 1),
            BitisTarihi = new DateTime(2024, 12, 1),
            OdenenBrutAylik = 30000m,
            FaizDahil = false
        };

        var r = await calc.CalculateAsync(input);

        r.IsValid.Should().BeTrue();
        r.ToplamEksikBrut.Should().Be(0m);
        r.EksikAy.Should().Be(0);
        r.ToplamAlacak.Should().Be(0m);
        r.Warnings.Should().Contain(w => w.Contains("Eksik ödeme yoktur"));
    }

    [Fact]
    public async Task Tum_Aylar_Eksik_Toplam_Dogru()
    {
        var (calc, ctx) = Build();
        await using var _ = ctx;

        var input = new AsgariUcretInput
        {
            BaslangicTarihi = new DateTime(2024, 1, 1),
            BitisTarihi = new DateTime(2024, 12, 1),
            OdenenBrutAylik = 15000m,
            FaizDahil = false
        };

        var r = await calc.CalculateAsync(input);

        r.IsValid.Should().BeTrue();
        r.ToplamAy.Should().Be(12);
        r.EksikAy.Should().Be(12);
        r.ToplamEksikBrut.Should().Be(60030m);
    }

    [Fact]
    public async Task Donem_Asgari_Ucret_Degisirken_Dogru_Karsilastirma()
    {
        var (calc, ctx) = Build();
        await using var _ = ctx;

        var input = new AsgariUcretInput
        {
            BaslangicTarihi = new DateTime(2024, 10, 1),
            BitisTarihi = new DateTime(2025, 3, 1),
            OdenenBrutAylik = 22000m,
            FaizDahil = false
        };

        var r = await calc.CalculateAsync(input);

        r.IsValid.Should().BeTrue();
        r.ToplamAy.Should().Be(6);
        r.EksikAy.Should().Be(3);
        r.ToplamEksikBrut.Should().Be(12016.50m);
    }

    [Fact]
    public async Task Faiz_Dahil_Edildiginde_Eklenir()
    {
        var (calc, ctx) = Build();
        await using var _ = ctx;

        var input = new AsgariUcretInput
        {
            BaslangicTarihi = new DateTime(2024, 1, 1),
            BitisTarihi = new DateTime(2024, 12, 1),
            OdenenBrutAylik = 15000m,
            FaizDahil = true
        };

        var r = await calc.CalculateAsync(input);

        r.IsValid.Should().BeTrue();
        r.ToplamEksikBrut.Should().Be(60030m);
        r.YasalFaiz.Should().BeGreaterThan(0m);
        r.ToplamAlacak.Should().Be(r.ToplamEksikBrut + r.YasalFaiz);
    }

    [Fact]
    public async Task Bitis_Baslangictan_Once_Validation_Hatasi()
    {
        var (calc, ctx) = Build();
        await using var _ = ctx;

        var input = new AsgariUcretInput
        {
            BaslangicTarihi = new DateTime(2025, 1, 1),
            BitisTarihi = new DateTime(2024, 1, 1),
            OdenenBrutAylik = 20000m
        };

        var r = await calc.CalculateAsync(input);

        r.IsValid.Should().BeFalse();
        r.ValidationErrors.Should().ContainKey(nameof(AsgariUcretInput.BitisTarihi));
    }

    [Fact]
    public async Task Aylik_Detay_Sadece_Eksik_Aylari_Icermeli()
    {
        var (calc, ctx) = Build();
        await using var _ = ctx;

        var input = new AsgariUcretInput
        {
            BaslangicTarihi = new DateTime(2024, 10, 1),
            BitisTarihi = new DateTime(2025, 3, 1),
            OdenenBrutAylik = 22000m,
            FaizDahil = false
        };

        var r = await calc.CalculateAsync(input);

        r.AylikDetaylar.Count.Should().Be(6);
        r.AylikDetaylar.Count(d => d.Eksiklik > 0).Should().Be(3);
        r.AylikDetaylar.Where(d => d.Eksiklik > 0).Should().AllSatisfy(d =>
            d.Ay.Year.Should().Be(2025));
    }
}
