using LexCalculus.Core.Entities.Calculators;
using LexCalculus.Core.Enums;
using LexCalculus.Infrastructure.Data.SeedData.LifeTableData;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LexCalculus.Infrastructure.Data.SeedData;

/// <summary>
/// Seeds life tables (TRH 2010 etc.) on startup. Idempotent: existing tables
/// are matched by Code and skipped; rows are skipped if (LifeTableId, Yas, Cinsiyet)
/// already exists. Re-running on a populated DB is a no-op.
/// </summary>
public static class LifeTableSeeder
{
    public static async Task SeedAsync(ApplicationDbContext db, ILogger logger, CancellationToken ct = default)
    {
        await SeedTrh2010Async(db, logger, ct);
        await db.SaveChangesAsync(ct);
    }

    private static async Task SeedTrh2010Async(ApplicationDbContext db, ILogger logger, CancellationToken ct)
    {
        const string Code = "TRH-2010";

        var existing = await db.Set<LifeTable>()
            .Include(t => t.Rows)
            .FirstOrDefaultAsync(t => t.Code == Code, ct);

        if (existing is null)
        {
            existing = new LifeTable
            {
                Code = Code,
                Name = "Türkiye Hayat Tablosu 2010",
                EffectiveDate = new DateTime(2010, 1, 1),
                Source = "T.C. Hazine Müsteşarlığı",
                IsActive = true,
                Note = "Aktüeryal hesaplamalarda kullanılan resmi Türkiye yaşam tablosu. Production'da resmi yayın ile doğrulanmalıdır."
            };
            db.Set<LifeTable>().Add(existing);
            await db.SaveChangesAsync(ct);
            logger.LogInformation("Seeded LifeTable: {Code}", Code);
        }

        var existingKeys = existing.Rows
            .Select(r => (r.Yas, r.Cinsiyet))
            .ToHashSet();

        int yeniSatir = 0;
        foreach (var (yas, erkek, kadin) in Trh2010Data.Rows)
        {
            if (!existingKeys.Contains((yas, Cinsiyet.Erkek)))
            {
                db.Set<LifeTableRow>().Add(new LifeTableRow
                {
                    LifeTableId = existing.Id,
                    Yas = yas,
                    Cinsiyet = Cinsiyet.Erkek,
                    BekledigiYasam = erkek
                });
                yeniSatir++;
            }
            if (!existingKeys.Contains((yas, Cinsiyet.Kadin)))
            {
                db.Set<LifeTableRow>().Add(new LifeTableRow
                {
                    LifeTableId = existing.Id,
                    Yas = yas,
                    Cinsiyet = Cinsiyet.Kadin,
                    BekledigiYasam = kadin
                });
                yeniSatir++;
            }
        }

        if (yeniSatir > 0)
        {
            logger.LogInformation("Seeded {Count} new rows for LifeTable {Code}", yeniSatir, Code);
        }
    }
}
