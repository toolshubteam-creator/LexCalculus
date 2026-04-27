using LexCalculus.Core.Entities.Calculators;
using LexCalculus.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LexCalculus.Infrastructure.Persistence.Seed;

/// <summary>
/// TÜFE 12 aylık ortalama değişim oranı seed.
/// Source: Legalbank.net (TÜİK referansı, doğrulanmış).
/// 64 ay: 2020 Aralık - 2026 Mart.
///
/// Used by: Kira Artışı (TBK m.344) calculator via ITUFEService.
///
/// Update procedure (manual, until Phase 3 admin panel):
///   1. Visit data.tuik.gov.tr each month after the 3rd
///   2. Locate "Tüketici Fiyat Endeksi" bulletin for prior month
///   3. Find "on iki aylık ortalamalara göre" change rate
///   4. Add new FormulaParameter row with Key="YYYY-MM", Value=rate
///   5. Set LastUpdatedDate to TÜİK announcement date
/// </summary>
public static class TUFESeedData
{
    private const string ToolSlug = "tufe-12-ay-ort";
    private const string Frequency = "Monthly";
    private const string SeedNotes =
        "TÜİK her ayın 3'ünde saat 10:00'da yayınlar. Kaynak: data.tuik.gov.tr. " +
        "TBK m.344/1 uyarınca konut ve çatılı işyeri kira artış üst sınırı için kullanılır.";

    private static readonly (string YearMonth, decimal Rate)[] Veriler =
    {
        ("2020-12", 12.28m),
        ("2021-01", 12.53m), ("2021-02", 12.81m), ("2021-03", 13.18m),
        ("2021-04", 13.70m), ("2021-05", 14.13m), ("2021-06", 14.55m),
        ("2021-07", 15.15m), ("2021-08", 15.78m), ("2021-09", 16.42m),
        ("2021-10", 17.09m), ("2021-11", 17.71m), ("2021-12", 19.60m),
        ("2022-01", 22.58m), ("2022-02", 25.98m), ("2022-03", 29.88m),
        ("2022-04", 34.46m), ("2022-05", 39.33m), ("2022-06", 44.54m),
        ("2022-07", 49.65m), ("2022-08", 54.69m), ("2022-09", 59.91m),
        ("2022-10", 65.26m), ("2022-11", 70.36m), ("2022-12", 72.31m),
        ("2023-01", 72.45m), ("2023-02", 71.83m), ("2023-03", 70.20m),
        ("2023-04", 67.20m), ("2023-05", 63.72m), ("2023-06", 59.95m),
        ("2023-07", 57.45m), ("2023-08", 56.28m), ("2023-09", 55.30m),
        ("2023-10", 54.26m), ("2023-11", 53.40m), ("2023-12", 53.86m),
        ("2024-01", 54.72m), ("2024-02", 55.91m), ("2024-03", 57.50m),
        ("2024-04", 59.64m), ("2024-05", 62.51m), ("2024-06", 65.07m),
        ("2024-07", 65.93m), ("2024-08", 64.91m), ("2024-09", 63.47m),
        ("2024-10", 62.02m), ("2024-11", 60.45m), ("2024-12", 58.51m),
        ("2025-01", 56.35m), ("2025-02", 53.83m), ("2025-03", 51.26m),
        ("2025-04", 48.73m), ("2025-05", 45.80m), ("2025-06", 43.23m),
        ("2025-07", 41.13m), ("2025-08", 39.62m), ("2025-09", 38.36m),
        ("2025-10", 37.15m), ("2025-11", 35.91m), ("2025-12", 34.88m),
        ("2026-01", 33.98m), ("2026-02", 33.39m), ("2026-03", 32.82m),
    };

    public static async Task SeedAsync(ApplicationDbContext ctx, CancellationToken ct = default)
    {
        var existing = await ctx.Set<FormulaParameter>()
            .Where(p => p.ToolSlug == ToolSlug)
            .Select(p => p.Key)
            .ToListAsync(ct);

        var toAdd = new List<FormulaParameter>();
        foreach (var (yearMonth, rate) in Veriler)
        {
            if (existing.Contains(yearMonth)) continue;

            var parts = yearMonth.Split('-');
            var year = int.Parse(parts[0]);
            var month = int.Parse(parts[1]);

            var effectiveDate = new DateTime(year, month, 1);
            var announcementDate = effectiveDate.AddMonths(1).AddDays(2);

            toAdd.Add(new FormulaParameter
            {
                ToolSlug = ToolSlug,
                Key = yearMonth,
                Value = rate,
                EffectiveDate = effectiveDate,
                ExpectedUpdateFrequency = Frequency,
                LastUpdatedDate = announcementDate,
                Notes = SeedNotes
            });
        }

        if (toAdd.Count > 0)
        {
            await ctx.Set<FormulaParameter>().AddRangeAsync(toAdd, ct);
            await ctx.SaveChangesAsync(ct);
        }
    }
}
