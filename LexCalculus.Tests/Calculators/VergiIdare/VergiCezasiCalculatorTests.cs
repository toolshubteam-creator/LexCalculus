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

public class VergiCezasiCalculatorTests : SqlServerTestBase
{
    private static IDistributedCache CreateCache() =>
        new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));

    private (VergiCezasiCalculator calc, LexCalculus.Infrastructure.Data.ApplicationDbContext ctx) Build()
    {
        var ctx = _db.Create();
        // Yıllık aylık oran seed (2024-2026, 0.035 sabit).
        ctx.Set<FormulaParameter>().AddRange(
            new FormulaParameter { ToolSlug = "*", Key = "gecikme-faizi.aylik-oran", Value = 0.035m, EffectiveDate = new DateTime(2024, 1, 1) },
            new FormulaParameter { ToolSlug = "*", Key = "gecikme-faizi.aylik-oran", Value = 0.035m, EffectiveDate = new DateTime(2025, 1, 1) },
            new FormulaParameter { ToolSlug = "*", Key = "gecikme-faizi.aylik-oran", Value = 0.035m, EffectiveDate = new DateTime(2026, 1, 1) },
            new FormulaParameter { ToolSlug = "*", Key = "gecikme-zammi.aylik-oran", Value = 0.035m, EffectiveDate = new DateTime(2026, 1, 1) }
        );
        ctx.SaveChanges();

        var paramSvc = new FormulaParameterService(ctx, CreateCache(),
            new NullActivityLogService(), NullLogger<FormulaParameterService>.Instance);
        return (new VergiCezasiCalculator(paramSvc), ctx);
    }

    [Fact]
    public async Task Reference_VergiZiyai_12Ay_StandardCalculation()
    {
        // 100k vergi ziyaı, 12 ay gecikme (2026 içinde), 2026 oran %3,5:
        //   Ceza = 100k × 50% = 50k
        //   Faiz = 100k × 0.035 × 12 = 42k
        //   Toplam = 100k + 50k + 42k = 192k
        var (calc, ctx) = Build();
        await using var _ = ctx;

        var r = await calc.CalculateAsync(new VergiCezasiInput
        {
            AsilVergi = 100_000m,
            VadeTarihi = new DateTime(2026, 1, 15),
            OdemeTarihi = new DateTime(2027, 1, 15),
            CezaTuru = VergiCezaTuru.VergiZiyai,
            FaizTuru = FaizTuru.GecikmeFaizi
        });

        r.IsValid.Should().BeTrue();
        r.CezaTutari.Should().Be(50_000m);
        r.ToplamGecikmeFaizi.Should().Be(42_000m);
        r.ToplamOdenecekTutar.Should().Be(192_000m);
        r.ToplamAySayisi.Should().Be(12);
    }

    [Fact]
    public async Task Reference_Kacakcilik_6Ay_HigherCeza()
    {
        // 200k kaçakçılık, 6 ay (2026 içinde):
        //   Ceza = 200k × 100% = 200k
        //   Faiz = 200k × 0.035 × 6 = 42k
        //   Toplam = 200k + 200k + 42k = 442k
        var (calc, ctx) = Build();
        await using var _ = ctx;

        var r = await calc.CalculateAsync(new VergiCezasiInput
        {
            AsilVergi = 200_000m,
            VadeTarihi = new DateTime(2026, 1, 15),
            OdemeTarihi = new DateTime(2026, 7, 15),
            CezaTuru = VergiCezaTuru.Kacakcilik,
            FaizTuru = FaizTuru.GecikmeFaizi
        });

        r.CezaTutari.Should().Be(200_000m);
        r.ToplamGecikmeFaizi.Should().Be(42_000m);
        r.ToplamOdenecekTutar.Should().Be(442_000m);
    }

    [Fact]
    public async Task GecikmeFaizi_MultiYear_DonemSegmentation()
    {
        // Vade 2024-06-01 → Ödeme 2026-06-01 = 24 ay.
        // Yıl segmentleri: 2024 (Haz-Ara dahil = 7 ay), 2025 (12 ay), 2026 (Oca-May = 5 ay).
        // Tüm yıllar 0.035 oranında → 100k × 0.035 × 24 = 84k toplam.
        var (calc, ctx) = Build();
        await using var _ = ctx;

        var r = await calc.CalculateAsync(new VergiCezasiInput
        {
            AsilVergi = 100_000m,
            VadeTarihi = new DateTime(2024, 6, 1),
            OdemeTarihi = new DateTime(2026, 6, 1),
            CezaTuru = VergiCezaTuru.VergiZiyai,
            FaizTuru = FaizTuru.GecikmeFaizi
        });

        r.ToplamAySayisi.Should().Be(24);
        r.GecikmeFaiziDonemleri.Should().HaveCount(3);
        r.GecikmeFaiziDonemleri[0].Yil.Should().Be(2024);
        r.GecikmeFaiziDonemleri[0].AySayisi.Should().Be(7);
        r.GecikmeFaiziDonemleri[1].Yil.Should().Be(2025);
        r.GecikmeFaiziDonemleri[1].AySayisi.Should().Be(12);
        r.GecikmeFaiziDonemleri[2].Yil.Should().Be(2026);
        r.GecikmeFaiziDonemleri[2].AySayisi.Should().Be(5);
        r.ToplamGecikmeFaizi.Should().Be(84_000m);
    }

    [Fact]
    public async Task VadeOdemeAyni_NoFaiz()
    {
        var (calc, ctx) = Build();
        await using var _ = ctx;

        var r = await calc.CalculateAsync(new VergiCezasiInput
        {
            AsilVergi = 100_000m,
            VadeTarihi = new DateTime(2026, 3, 1),
            OdemeTarihi = new DateTime(2026, 3, 28),
            CezaTuru = VergiCezaTuru.VergiZiyai,
            FaizTuru = FaizTuru.GecikmeFaizi
        });

        r.ToplamAySayisi.Should().Be(0);
        r.ToplamGecikmeFaizi.Should().Be(0m);
        r.CezaTutari.Should().Be(50_000m);
        r.ToplamOdenecekTutar.Should().Be(150_000m);
    }

    [Fact]
    public async Task Usulsuzluk_BasicCase()
    {
        // Usulsüzlük maktu 5k + 6 ay × 100k × 0.035 = 21k faiz + 100k asıl = 126k.
        var (calc, ctx) = Build();
        await using var _ = ctx;

        var r = await calc.CalculateAsync(new VergiCezasiInput
        {
            AsilVergi = 100_000m,
            VadeTarihi = new DateTime(2026, 1, 1),
            OdemeTarihi = new DateTime(2026, 7, 1),
            CezaTuru = VergiCezaTuru.Usulsuzluk,
            UsulsuzlukTutari = 5_000m,
            FaizTuru = FaizTuru.GecikmeFaizi
        });

        r.IsValid.Should().BeTrue();
        r.CezaTutari.Should().Be(5_000m);
        r.ToplamGecikmeFaizi.Should().Be(21_000m);
        r.ToplamOdenecekTutar.Should().Be(126_000m);
    }

    [Fact]
    public async Task ValidationError_OdemeBeforeVade()
    {
        var (calc, ctx) = Build();
        await using var _ = ctx;

        var r = await calc.CalculateAsync(new VergiCezasiInput
        {
            AsilVergi = 100_000m,
            VadeTarihi = new DateTime(2026, 6, 1),
            OdemeTarihi = new DateTime(2026, 1, 1),
            CezaTuru = VergiCezaTuru.VergiZiyai
        });

        r.IsValid.Should().BeFalse();
        r.ValidationErrors.Should().ContainKey(nameof(VergiCezasiInput.OdemeTarihi));
    }
}
