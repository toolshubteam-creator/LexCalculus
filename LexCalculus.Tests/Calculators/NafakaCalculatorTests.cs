using FluentAssertions;
using LexCalculus.Core.Calculators.AileMiras;
using LexCalculus.Core.Entities.Calculators;
using LexCalculus.Infrastructure.Calculators;
using LexCalculus.Infrastructure.Services;
using LexCalculus.Tests.TestHelpers;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace LexCalculus.Tests.Calculators;

public class NafakaCalculatorTests : SqlServerTestBase
{
    private static readonly DateTime Effective = new(2026, 1, 1);

    private static IDistributedCache CreateCache() =>
        new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));

    private (NafakaCalculator calc, LexCalculus.Infrastructure.Data.ApplicationDbContext ctx) Build()
    {
        var ctx = _db.Create();
        ctx.Set<FormulaParameter>().AddRange(
            // İştirak katsayıları
            new FormulaParameter { ToolSlug = "nafaka", Key = "istirak.baz-oran.1cocuk", Value = 0.20m, EffectiveDate = Effective },
            new FormulaParameter { ToolSlug = "nafaka", Key = "istirak.baz-oran.2cocuk", Value = 0.15m, EffectiveDate = Effective },
            new FormulaParameter { ToolSlug = "nafaka", Key = "istirak.baz-oran.3plus", Value = 0.12m, EffectiveDate = Effective },
            new FormulaParameter { ToolSlug = "nafaka", Key = "istirak.yas-katsayisi.0-6", Value = 1.0m, EffectiveDate = Effective },
            new FormulaParameter { ToolSlug = "nafaka", Key = "istirak.yas-katsayisi.7-11", Value = 1.1m, EffectiveDate = Effective },
            new FormulaParameter { ToolSlug = "nafaka", Key = "istirak.yas-katsayisi.12-17", Value = 1.2m, EffectiveDate = Effective },
            new FormulaParameter { ToolSlug = "nafaka", Key = "istirak.egitim-katsayisi.anaokul", Value = 1.0m, EffectiveDate = Effective },
            new FormulaParameter { ToolSlug = "nafaka", Key = "istirak.egitim-katsayisi.ilkokul", Value = 1.05m, EffectiveDate = Effective },
            new FormulaParameter { ToolSlug = "nafaka", Key = "istirak.egitim-katsayisi.ortaokul", Value = 1.1m, EffectiveDate = Effective },
            new FormulaParameter { ToolSlug = "nafaka", Key = "istirak.egitim-katsayisi.lise", Value = 1.2m, EffectiveDate = Effective },
            new FormulaParameter { ToolSlug = "nafaka", Key = "istirak.egitim-katsayisi.universite", Value = 1.4m, EffectiveDate = Effective },
            new FormulaParameter { ToolSlug = "nafaka", Key = "istirak.sehir-katsayisi.buyuksehir", Value = 1.15m, EffectiveDate = Effective },
            // Yoksulluk katsayıları
            new FormulaParameter { ToolSlug = "nafaka", Key = "yoksulluk.oran", Value = 0.30m, EffectiveDate = Effective },
            new FormulaParameter { ToolSlug = "nafaka", Key = "yoksulluk.evlilik-suresi.0-2", Value = 0.5m, EffectiveDate = Effective },
            new FormulaParameter { ToolSlug = "nafaka", Key = "yoksulluk.evlilik-suresi.3-5", Value = 0.8m, EffectiveDate = Effective },
            new FormulaParameter { ToolSlug = "nafaka", Key = "yoksulluk.evlilik-suresi.6-10", Value = 1.0m, EffectiveDate = Effective },
            new FormulaParameter { ToolSlug = "nafaka", Key = "yoksulluk.evlilik-suresi.11plus", Value = 1.2m, EffectiveDate = Effective },
            // Asgari ücret (global) — alt sınır
            new FormulaParameter { ToolSlug = "*", Key = "asgari-ucret-brut", Value = 33000.00m, EffectiveDate = Effective },
            // TÜFE 12 aylık ortalama (artış referansı) — slug tufe-12-ay-ort, key yyyy-MM
            new FormulaParameter { ToolSlug = "tufe-12-ay-ort", Key = "2026-05", Value = 30.0m, EffectiveDate = new DateTime(2026, 5, 1) }
        );
        ctx.SaveChanges();

        var paramService = new FormulaParameterService(ctx, CreateCache(), new NullActivityLogService(), NullLogger<FormulaParameterService>.Instance);
        var tufeService = new TUFEService(ctx, NullLogger<TUFEService>.Instance);
        return (new NafakaCalculator(paramService, tufeService), ctx);
    }

    [Fact]
    public async Task Istirak_OneChild_StandardCase()
    {
        var (calc, ctx) = Build();
        await using var _ = ctx;

        // 60.000 × %20 × 1.0 (0-6 yaş) × 1.0 (anaokul) × 1.0 (diğer) = 12.000; alt sınır 8.250 altında değil.
        var input = new NafakaInput
        {
            AsOfDate = Effective,
            NafakaTuru = NafakaTuru.Istirak,
            HesapTuru = NafakaHesapTuru.YeniHesap,
            YukumluNetGelir = 60_000m,
            Sehir = SehirTuru.Diger,
            Cocuklar = new List<NafakaCocukGirdi> { new() { Yas = 5, EgitimSeviyesi = EgitimSeviyesi.Anaokul } }
        };

        var r = await calc.CalculateAsync(input);

        r.IsValid.Should().BeTrue();
        r.OnerilenAylikNafaka.Should().Be(12_000m);
        r.MinimumUygulandi.Should().BeFalse();
    }

    [Fact]
    public async Task Istirak_TwoChildren_LowerPerChild()
    {
        var (calc, ctx) = Build();
        await using var _ = ctx;

        // 2 çocuk → çocuk başına %15. Her biri 60.000 × %15 = 9.000 (< tek çocuk 12.000).
        var input = new NafakaInput
        {
            AsOfDate = Effective,
            NafakaTuru = NafakaTuru.Istirak,
            HesapTuru = NafakaHesapTuru.YeniHesap,
            YukumluNetGelir = 60_000m,
            Sehir = SehirTuru.Diger,
            Cocuklar = new List<NafakaCocukGirdi>
            {
                new() { Yas = 5, EgitimSeviyesi = EgitimSeviyesi.Anaokul },
                new() { Yas = 5, EgitimSeviyesi = EgitimSeviyesi.Anaokul }
            }
        };

        var r = await calc.CalculateAsync(input);

        r.IsValid.Should().BeTrue();
        (r.OnerilenAylikNafaka / 2m).Should().Be(9_000m); // çocuk başına 9.000 < 12.000
        r.OnerilenAylikNafaka.Should().Be(18_000m);
    }

    [Fact]
    public async Task Yoksulluk_GelirFarki_StandardCalculation()
    {
        var (calc, ctx) = Build();
        await using var _ = ctx;

        // Fark 30.000 × %30 × 1.0 (6-10 yıl) = 9.000.
        var input = new NafakaInput
        {
            AsOfDate = Effective,
            NafakaTuru = NafakaTuru.Yoksulluk,
            HesapTuru = NafakaHesapTuru.YeniHesap,
            YuksekGelirEs = 50_000m,
            DusukGelirEs = 20_000m,
            EvlilikSuresiAy = 96 // 8 yıl
        };

        var r = await calc.CalculateAsync(input);

        r.IsValid.Should().BeTrue();
        r.OnerilenAylikNafaka.Should().Be(9_000m);
    }

    [Fact]
    public async Task Yoksulluk_ShortMarriage_LowCoefficient_Yargitay2HD2024()
    {
        var (calc, ctx) = Build();
        await using var _ = ctx;

        // 14 ay evlilik → 0-2 yıl bandı → katsayı 0.5 (kısa evlilik hakkaniyet, Yargıtay 2. HD 2024).
        // Fark 30.000 × %30 × 0.5 = 4.500.
        var input = new NafakaInput
        {
            AsOfDate = Effective,
            NafakaTuru = NafakaTuru.Yoksulluk,
            HesapTuru = NafakaHesapTuru.YeniHesap,
            YuksekGelirEs = 50_000m,
            DusukGelirEs = 20_000m,
            EvlilikSuresiAy = 14
        };

        var r = await calc.CalculateAsync(input);

        r.IsValid.Should().BeTrue();
        r.OnerilenAylikNafaka.Should().Be(4_500m);
    }

    [Fact]
    public async Task Tedbir_SameAsYoksulluk()
    {
        var (calc, ctx) = Build();
        await using var _ = ctx;

        var input = new NafakaInput
        {
            AsOfDate = Effective,
            NafakaTuru = NafakaTuru.Tedbir,
            HesapTuru = NafakaHesapTuru.YeniHesap,
            YuksekGelirEs = 50_000m,
            DusukGelirEs = 20_000m,
            EvlilikSuresiAy = 96
        };

        var r = await calc.CalculateAsync(input);

        r.IsValid.Should().BeTrue();
        r.OnerilenAylikNafaka.Should().Be(9_000m); // yoksulluk formülüyle aynı tutar
        r.TotalLabel.Should().Contain("Tedbir");
    }

    [Fact]
    public async Task Artis_TufeApplied_CorrectIncrease()
    {
        var (calc, ctx) = Build();
        await using var _ = ctx;

        // Hesap tarihi 2026-06 → bir önceki ay 2026-05 → seed TÜFE %30. 10.000 × 1.30 = 13.000.
        var input = new NafakaInput
        {
            AsOfDate = Effective,
            HesapTuru = NafakaHesapTuru.Artis,
            MevcutAylikNafaka = 10_000m,
            HesapTarihi = new DateTime(2026, 6, 1)
        };

        var r = await calc.CalculateAsync(input);

        r.IsValid.Should().BeTrue();
        r.UygulananTufeOrani.Should().Be(30.0m);
        r.OnerilenAylikNafaka.Should().Be(13_000m);
    }

    [Fact]
    public async Task MinimumAsgariUcret_Floor_Enforced()
    {
        var (calc, ctx) = Build();
        await using var _ = ctx;

        // Düşük gelir: 30.000 × %20 = 6.000 < asgari ücret %25 = 8.250 → alt sınır uygulanır.
        var input = new NafakaInput
        {
            AsOfDate = Effective,
            NafakaTuru = NafakaTuru.Istirak,
            HesapTuru = NafakaHesapTuru.YeniHesap,
            YukumluNetGelir = 30_000m,
            Sehir = SehirTuru.Diger,
            Cocuklar = new List<NafakaCocukGirdi> { new() { Yas = 5, EgitimSeviyesi = EgitimSeviyesi.Anaokul } }
        };

        var r = await calc.CalculateAsync(input);

        r.IsValid.Should().BeTrue();
        r.OnerilenAylikNafaka.Should().Be(8_250m); // 33.000 × %25
        r.MinimumUygulandi.Should().BeTrue();
    }
}
