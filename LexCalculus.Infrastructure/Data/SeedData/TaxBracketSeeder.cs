using LexCalculus.Core.Entities.Calculators;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LexCalculus.Infrastructure.Data.SeedData;

/// <summary>
/// 2026 vergi dilim tarifesini seed eder (Charter Karar 1). Idempotent:
/// (ToolSlug + Sira + EffectiveDate) üçlüsüyle eşleşen mevcut satırlar atlanır.
///
/// Veri kaynağı: Resmi Gazete 31.12.2025 / 33124 (5. Mükerrer) — 57 Seri No'lu
/// Veraset ve İntikal Vergisi Kanunu Genel Tebliği. Veraset ve ivazsız
/// intikal için iki ayrı dilim seti (her biri 5 dilim, son dilim MaxAmount=null).
/// </summary>
public static class TaxBracketSeeder
{
    public static async Task SeedAsync(ApplicationDbContext db, ILogger logger, CancellationToken ct = default)
    {
        var seeds = Build2026Tarifesi();

        var existing = await db.TaxBrackets
            .Where(b => seeds.Select(s => s.ToolSlug).Distinct().Contains(b.ToolSlug))
            .Select(b => new { b.ToolSlug, b.Sira, b.EffectiveDate })
            .ToListAsync(ct);
        var existingKeys = existing
            .Select(e => (e.ToolSlug, e.Sira, e.EffectiveDate))
            .ToHashSet();

        var toAdd = seeds
            .Where(s => !existingKeys.Contains((s.ToolSlug, s.Sira, s.EffectiveDate)))
            .ToList();

        if (toAdd.Count == 0)
        {
            logger.LogInformation("TaxBracketSeeder: dilim satırları zaten mevcut, ekleme yapılmadı.");
            return;
        }

        db.TaxBrackets.AddRange(toAdd);
        await db.SaveChangesAsync(ct);

        logger.LogInformation("TaxBracketSeeder: {Count} dilim satırı eklendi.", toAdd.Count);
    }

    private static IReadOnlyList<TaxBracket> Build2026Tarifesi()
    {
        var ef = new DateTime(2026, 1, 1);
        const string src = "RG 31.12.2025/33124 (5. Mükerrer) — 57 Seri No'lu VİV Genel Tebliği";

        return new[]
        {
            // ----- Veraset yoluyla intikal (7338 s.K. m.16/1-a) -----
            B("veraset-vergisi/veraset", 1,            0m,    3_000_000m,  0.01m, ef, src, "Dilim 1: %1"),
            B("veraset-vergisi/veraset", 2,    3_000_000m,   10_000_000m,  0.03m, ef, src, "Dilim 2: %3"),
            B("veraset-vergisi/veraset", 3,   10_000_000m,   25_000_000m,  0.05m, ef, src, "Dilim 3: %5"),
            B("veraset-vergisi/veraset", 4,   25_000_000m,   55_000_000m,  0.07m, ef, src, "Dilim 4: %7"),
            B("veraset-vergisi/veraset", 5,   55_000_000m,           null, 0.10m, ef, src, "Dilim 5 (sınırsız): %10"),

            // ----- İvazsız intikal / bağış (7338 s.K. m.16/1-b) -----
            B("veraset-vergisi/ivazsiz", 1,            0m,    3_000_000m,  0.10m, ef, src, "Dilim 1: %10"),
            B("veraset-vergisi/ivazsiz", 2,    3_000_000m,   10_000_000m,  0.15m, ef, src, "Dilim 2: %15"),
            B("veraset-vergisi/ivazsiz", 3,   10_000_000m,   25_000_000m,  0.20m, ef, src, "Dilim 3: %20"),
            B("veraset-vergisi/ivazsiz", 4,   25_000_000m,   55_000_000m,  0.25m, ef, src, "Dilim 4: %25"),
            B("veraset-vergisi/ivazsiz", 5,   55_000_000m,           null, 0.30m, ef, src, "Dilim 5 (sınırsız): %30"),
        };
    }

    private static TaxBracket B(string slug, int sira, decimal min, decimal? max, decimal rate,
        DateTime ef, string src, string note) => new()
    {
        ToolSlug = slug,
        Sira = sira,
        MinAmount = min,
        MaxAmount = max,
        Rate = rate,
        EffectiveDate = ef,
        Source = src,
        Note = note,
        IsAutoUpdated = false
    };
}
