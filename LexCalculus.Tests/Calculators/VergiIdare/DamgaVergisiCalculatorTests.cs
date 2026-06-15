using FluentAssertions;
using LexCalculus.Core.Calculators.VergiIdare;
using LexCalculus.Core.Entities.Calculators;
using LexCalculus.Infrastructure.Calculators;
using LexCalculus.Tests.TestHelpers;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace LexCalculus.Tests.Calculators.VergiIdare;

public class DamgaVergisiCalculatorTests : SqlServerTestBase
{
    private static readonly DateTime Effective = new(2026, 1, 1);

    private static IDistributedCache CreateCache() =>
        new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));

    private (DamgaVergisiCalculator calc, LexCalculus.Infrastructure.Data.ApplicationDbContext ctx) Build()
    {
        var ctx = _db.Create();
        ctx.Set<FormulaParameter>().AddRange(
            new FormulaParameter { ToolSlug = "damga-vergisi", Key = "oran.genel-sozlesme", Value = 0.00948m, EffectiveDate = Effective },
            new FormulaParameter { ToolSlug = "damga-vergisi", Key = "oran.kira-mukavelesi", Value = 0.00189m, EffectiveDate = Effective },
            new FormulaParameter { ToolSlug = "damga-vergisi", Key = "oran.ihale-karari", Value = 0.00569m, EffectiveDate = Effective },
            new FormulaParameter { ToolSlug = "damga-vergisi", Key = "oran.makbuz", Value = 0.00948m, EffectiveDate = Effective },
            new FormulaParameter { ToolSlug = "damga-vergisi", Key = "oran.diger", Value = 0.00948m, EffectiveDate = Effective },
            new FormulaParameter { ToolSlug = "damga-vergisi", Key = "azami-sinir", Value = 5_281_302.40m, EffectiveDate = Effective }
        );
        ctx.SaveChanges();

        var paramSvc = new FormulaParameterService(ctx, CreateCache(),
            new NullActivityLogService(), NullLogger<FormulaParameterService>.Instance);
        return (new DamgaVergisiCalculator(paramSvc), ctx);
    }

    [Fact]
    public async Task GenelSozlesme_StandardCalculation()
    {
        // 1.000.000 × ‰9,48 = 9.480 TL.
        var (calc, ctx) = Build();
        await using var _ = ctx;

        var r = await calc.CalculateAsync(new DamgaVergisiInput
        {
            BelgeTuru = DamgaBelgeTuru.GenelSozlesme,
            DegerTutari = 1_000_000m,
            BelgeAdedi = 1,
            AsOfDate = Effective
        });

        r.IsValid.Should().BeTrue();
        r.OranYuzde.Should().Be(0.00948m);
        r.OdenecekVergi.Should().Be(9_480m);
        r.AzamiSinirUygulandi.Should().BeFalse();
    }

    [Fact]
    public async Task KiraMukavelesi_LowerRate()
    {
        // 1.000.000 × ‰1,89 = 1.890 TL.
        var (calc, ctx) = Build();
        await using var _ = ctx;

        var r = await calc.CalculateAsync(new DamgaVergisiInput
        {
            BelgeTuru = DamgaBelgeTuru.KiraMukavelesi,
            DegerTutari = 1_000_000m,
            BelgeAdedi = 1,
            AsOfDate = Effective
        });

        r.OranYuzde.Should().Be(0.00189m);
        r.OdenecekVergi.Should().Be(1_890m);
    }

    [Fact]
    public async Task AzamiSinir_HesaplananUstu_Capped()
    {
        // 1.000.000.000 (1 milyar) × ‰9,48 = 9.480.000.000 → cap 5.281.302,40 TL.
        var (calc, ctx) = Build();
        await using var _ = ctx;

        var r = await calc.CalculateAsync(new DamgaVergisiInput
        {
            BelgeTuru = DamgaBelgeTuru.GenelSozlesme,
            DegerTutari = 1_000_000_000m,
            BelgeAdedi = 1,
            AsOfDate = Effective
        });

        r.AzamiSinirUygulandi.Should().BeTrue();
        r.OdenecekVergi.Should().Be(5_281_302.40m);
        r.Warnings.Should().NotBeEmpty();
    }

    [Fact]
    public async Task BelgeAdedi_Multiplier()
    {
        // 100.000 × ‰9,48 = 948 TL × 5 adet = 4.740 TL.
        var (calc, ctx) = Build();
        await using var _ = ctx;

        var r = await calc.CalculateAsync(new DamgaVergisiInput
        {
            BelgeTuru = DamgaBelgeTuru.GenelSozlesme,
            DegerTutari = 100_000m,
            BelgeAdedi = 5,
            AsOfDate = Effective
        });

        r.OdenecekVergi.Should().Be(4_740m);
    }

    [Fact]
    public async Task ValidationError_NegativeDeger()
    {
        var (calc, ctx) = Build();
        await using var _ = ctx;

        var r = await calc.CalculateAsync(new DamgaVergisiInput
        {
            BelgeTuru = DamgaBelgeTuru.GenelSozlesme,
            DegerTutari = -100m,
            BelgeAdedi = 1,
            AsOfDate = Effective
        });

        r.IsValid.Should().BeFalse();
        r.ValidationErrors.Should().ContainKey(nameof(DamgaVergisiInput.DegerTutari));
    }
}
