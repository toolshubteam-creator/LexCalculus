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

public class FazlaMesaiCalculatorTests : SqlServerTestBase
{
    private static IDistributedCache CreateCache() =>
        new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));

    private (FazlaMesaiCalculator calc, LexCalculus.Infrastructure.Data.ApplicationDbContext ctx) Build()
    {
        var ctx = _db.Create();
        ctx.Set<FormulaParameter>().AddRange(
            new FormulaParameter { ToolSlug = "*", Key = "damga-vergisi-orani", Value = 0.00759m, EffectiveDate = new DateTime(2020, 1, 1) },
            new FormulaParameter { ToolSlug = "ihbar-tazminati", Key = "gelir-vergisi-orani-basit", Value = 0.15m, EffectiveDate = new DateTime(2020, 1, 1) }
        );
        ctx.SaveChanges();
        var paramSvc = new FormulaParameterService(ctx, CreateCache(), new NullActivityLogService(), NullLogger<FormulaParameterService>.Instance);
        return (new FazlaMesaiCalculator(paramSvc), ctx);
    }

    [Fact]
    public async Task Saatlik_Ucret_Formul_Dogru()
    {
        var (calc, ctx) = Build();
        await using var _ = ctx;

        var input = new FazlaMesaiInput
        {
            BrutAylikUcret = 30000m,
            HaftalikNormalSaat = 45,
            FazlaMesaiSaati = 1,
            HesapTarihi = new DateTime(2025, 6, 1)
        };

        var r = await calc.CalculateAsync(input);

        r.IsValid.Should().BeTrue();
        r.SaatlikUcret.Should().BeApproximately(153.9646m, 0.01m);
    }

    [Fact]
    public async Task Reference_Case_Sadece_Fazla_Mesai_10_Saat()
    {
        var (calc, ctx) = Build();
        await using var _ = ctx;

        var input = new FazlaMesaiInput
        {
            BrutAylikUcret = 30000m,
            HaftalikNormalSaat = 45,
            FazlaMesaiSaati = 10,
            HesapTarihi = new DateTime(2025, 6, 1)
        };

        var r = await calc.CalculateAsync(input);

        r.IsValid.Should().BeTrue();
        r.FazlaMesaiTutari.Should().BeApproximately(2309.47m, 1m);
        r.HaftaTatiliTutari.Should().Be(0m);
        r.BayramTutari.Should().Be(0m);
        r.BrutToplam.Should().BeApproximately(2309.47m, 1m);
    }

    [Fact]
    public async Task Hafta_Tatili_Ve_Bayram_Iki_Misli_Carpan()
    {
        var (calc, ctx) = Build();
        await using var _ = ctx;

        var input = new FazlaMesaiInput
        {
            BrutAylikUcret = 30000m,
            HaftalikNormalSaat = 45,
            FazlaMesaiSaati = 0,
            HaftaTatiliSaati = 8,
            BayramSaati = 8,
            HesapTarihi = new DateTime(2025, 6, 1)
        };

        var r = await calc.CalculateAsync(input);

        r.IsValid.Should().BeTrue();
        // 8 sa × 153.9646 × 2.0 = 2463.43 PER category
        r.HaftaTatiliTutari.Should().BeApproximately(2463.43m, 1m);
        r.BayramTutari.Should().BeApproximately(2463.43m, 1m);
        r.BrutToplam.Should().BeApproximately(2463.43m * 2, 2m);
    }

    [Fact]
    public async Task Tum_Kategoriler_Kumulatif_Toplanir()
    {
        var (calc, ctx) = Build();
        await using var _ = ctx;

        var input = new FazlaMesaiInput
        {
            BrutAylikUcret = 30000m,
            HaftalikNormalSaat = 45,
            FazlaMesaiSaati = 10,
            HaftaTatiliSaati = 8,
            BayramSaati = 8,
            HesapTarihi = new DateTime(2025, 6, 1)
        };

        var r = await calc.CalculateAsync(input);

        r.IsValid.Should().BeTrue();
        // FazlaMesai 2309.47 + Hafta 2463.43 + Bayram 2463.43 = 7236.33
        r.BrutToplam.Should().BeApproximately(2309.47m + 2463.43m + 2463.43m, 2m);
    }

    [Fact]
    public async Task Tum_Mesai_Saatleri_Sifir_Validation_Hatasi()
    {
        var (calc, ctx) = Build();
        await using var _ = ctx;

        var input = new FazlaMesaiInput
        {
            BrutAylikUcret = 30000m,
            HesapTarihi = new DateTime(2025, 6, 1)
        };

        var r = await calc.CalculateAsync(input);

        r.IsValid.Should().BeFalse();
        r.ValidationErrors.Should().ContainKey(nameof(FazlaMesaiInput.FazlaMesaiSaati));
    }

    [Fact]
    public async Task Negatif_Brut_Validation_Hatasi()
    {
        var (calc, ctx) = Build();
        await using var _ = ctx;

        var input = new FazlaMesaiInput
        {
            BrutAylikUcret = -100m,
            FazlaMesaiSaati = 10,
            HesapTarihi = new DateTime(2025, 6, 1)
        };

        var r = await calc.CalculateAsync(input);

        r.IsValid.Should().BeFalse();
        r.ValidationErrors.Should().ContainKey(nameof(FazlaMesaiInput.BrutAylikUcret));
    }
}
