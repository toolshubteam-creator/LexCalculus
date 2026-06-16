using FluentAssertions;
using LexCalculus.Core.Calculators.Bilirkisi;
using LexCalculus.Core.Entities.Calculators;
using LexCalculus.Infrastructure.Calculators;
using LexCalculus.Tests.TestHelpers;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace LexCalculus.Tests.Calculators.Bilirkisi;

public class HakkaniyetliTazminatCalculatorTests : SqlServerTestBase
{
    private static readonly DateTime Effective = new(2026, 1, 1);

    private static IDistributedCache CreateCache() =>
        new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));

    private (HakkaniyetliTazminatCalculator calc, LexCalculus.Infrastructure.Data.ApplicationDbContext ctx) Build()
    {
        var ctx = _db.Create();
        ctx.Set<FormulaParameter>().AddRange(
            // Ekonomik durum.
            new FormulaParameter { ToolSlug = "hakkaniyetli-tazminat", Key = "ekonomik.zor", Value = 1.2m, EffectiveDate = Effective },
            new FormulaParameter { ToolSlug = "hakkaniyetli-tazminat", Key = "ekonomik.normal", Value = 1.0m, EffectiveDate = Effective },
            new FormulaParameter { ToolSlug = "hakkaniyetli-tazminat", Key = "ekonomik.refah", Value = 0.8m, EffectiveDate = Effective },
            // Olay ağırlığı.
            new FormulaParameter { ToolSlug = "hakkaniyetli-tazminat", Key = "olay.hafif", Value = 0.8m, EffectiveDate = Effective },
            new FormulaParameter { ToolSlug = "hakkaniyetli-tazminat", Key = "olay.normal", Value = 1.0m, EffectiveDate = Effective },
            new FormulaParameter { ToolSlug = "hakkaniyetli-tazminat", Key = "olay.agir", Value = 1.3m, EffectiveDate = Effective },
            // Yaş.
            new FormulaParameter { ToolSlug = "hakkaniyetli-tazminat", Key = "yas.genc", Value = 1.1m, EffectiveDate = Effective },
            new FormulaParameter { ToolSlug = "hakkaniyetli-tazminat", Key = "yas.orta", Value = 1.0m, EffectiveDate = Effective },
            new FormulaParameter { ToolSlug = "hakkaniyetli-tazminat", Key = "yas.ileri", Value = 0.9m, EffectiveDate = Effective }
        );
        ctx.SaveChanges();

        var paramSvc = new FormulaParameterService(ctx, CreateCache(),
            new NullActivityLogService(), NullLogger<FormulaParameterService>.Instance);
        return (new HakkaniyetliTazminatCalculator(paramSvc), ctx);
    }

    [Fact]
    public async Task Reference_BaseCase_AllNormalMultipliers_100k()
    {
        // 100k × 1.0 × 1.0 × 1.0 × 1.0 × 1.0 = 100.000
        var (calc, ctx) = Build();
        await using var _ = ctx;

        var r = await calc.CalculateAsync(new HakkaniyetliTazminatInput
        {
            BazTazminat = 100_000m,
            KusurOrani = 1.0m,
            EkonomikDurum = EkonomikDurum.Normal,
            OlayAgirligi = OlayAgirligi.Normal,
            YasKategorisi = YasKategorisi.OrtaYas,
            AsOfDate = Effective
        });

        r.IsValid.Should().BeTrue();
        r.HesaplananTazminat.Should().Be(100_000m);
    }

    [Fact]
    public async Task Reference_PessimisticScenario_FullFault_Poor_Severe_Young_120120()
    {
        // 100k × 0.7 × 1.2 × 1.3 × 1.1 = 120.120
        var (calc, ctx) = Build();
        await using var _ = ctx;

        var r = await calc.CalculateAsync(new HakkaniyetliTazminatInput
        {
            BazTazminat = 100_000m,
            KusurOrani = 0.7m,
            EkonomikDurum = EkonomikDurum.Zor,
            OlayAgirligi = OlayAgirligi.Agir,
            YasKategorisi = YasKategorisi.Genc,
            AsOfDate = Effective
        });

        r.HesaplananTazminat.Should().Be(120_120m);
        r.EkonomikDurumKats.Should().Be(1.2m);
        r.OlayAgirligiKats.Should().Be(1.3m);
        r.YasKats.Should().Be(1.1m);
    }

    [Fact]
    public async Task Reference_OptimizedScenario_HalfFault_Wealthy_Light_Old_28800()
    {
        // 100k × 0.5 × 0.8 × 0.8 × 0.9 = 28.800
        var (calc, ctx) = Build();
        await using var _ = ctx;

        var r = await calc.CalculateAsync(new HakkaniyetliTazminatInput
        {
            BazTazminat = 100_000m,
            KusurOrani = 0.5m,
            EkonomikDurum = EkonomikDurum.Refah,
            OlayAgirligi = OlayAgirligi.Hafif,
            YasKategorisi = YasKategorisi.Ileri,
            AsOfDate = Effective
        });

        r.HesaplananTazminat.Should().Be(28_800m);
    }

    [Fact]
    public async Task PartialFault_50Percent_HalvesBase()
    {
        // 200k × 0.5 (kusur) × 1.0 × 1.0 × 1.0 = 100.000
        var (calc, ctx) = Build();
        await using var _ = ctx;

        var r = await calc.CalculateAsync(new HakkaniyetliTazminatInput
        {
            BazTazminat = 200_000m,
            KusurOrani = 0.5m,
            EkonomikDurum = EkonomikDurum.Normal,
            OlayAgirligi = OlayAgirligi.Normal,
            YasKategorisi = YasKategorisi.OrtaYas,
            AsOfDate = Effective
        });

        r.HesaplananTazminat.Should().Be(100_000m);
    }

    [Fact]
    public async Task ValidationError_FaultOutOfRange()
    {
        var (calc, ctx) = Build();
        await using var _ = ctx;

        var r = await calc.CalculateAsync(new HakkaniyetliTazminatInput
        {
            BazTazminat = 100_000m,
            KusurOrani = 1.5m, // > 1 → invalid
            EkonomikDurum = EkonomikDurum.Normal,
            OlayAgirligi = OlayAgirligi.Normal,
            YasKategorisi = YasKategorisi.OrtaYas,
            AsOfDate = Effective
        });

        r.IsValid.Should().BeFalse();
        r.ValidationErrors.Should().ContainKey(nameof(HakkaniyetliTazminatInput.KusurOrani));
    }
}
