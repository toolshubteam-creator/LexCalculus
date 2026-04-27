using LexCalculus.Core.Entities.Calculators;
using LexCalculus.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LexCalculus.Infrastructure.Persistence.Seed;

/// <summary>
/// One-time backfill of ExpectedUpdateFrequency / LastUpdatedDate / Notes
/// for FormulaParameter rows that existed before Adım 2.20.
/// Idempotent: skips rows that already have ExpectedUpdateFrequency set.
/// </summary>
public static class FormulaParameterMetadataBackfill
{
    private static readonly Dictionary<string, (string Freq, string Notes)> Map = new()
    {
        ["yasal-faiz"] = ("OnLawChange",
            "BKK / Cumhurbaşkanı Kararı ile değişir, Resmi Gazete takip edilir. " +
            "Mevcut tarihçe: 01.01.2006 → %9 (BKK 2005/9831), 01.06.2024 → %24 (CBK 8485)."),

        ["tcmb-avans"] = ("Biannual",
            "TCMB Reeskont ve Avans Faiz Oranları. Yarı yıl başı (Ocak / Temmuz) kontrol edilir. " +
            "Kaynak: tcmb.gov.tr"),

        ["yasal-faiz-orani-yillik"] = ("OnLawChange",
            "Yasal Faiz oranı (3095 m.1). BKK / CBK ile değişir."),

        ["kidem-tazminati-tavan"] = ("Biannual",
            "Kıdem tazminatı tavanı her yıl Ocak ve Temmuz'da güncellenir. " +
            "Kaynak: Hazine ve Maliye Bakanlığı genelgesi."),

        ["asgari-ucret-brut"] = ("Yearly",
            "Asgari Ücret Tespit Komisyonu kararıyla genelde Aralık-Ocak'ta belirlenir. " +
            "Bazı yıllarda Haziran-Temmuz ara zammı yapılabilir. Kaynak: Resmi Gazete."),

        ["ihbar-tazminati"] = ("Static",
            "4857 m.17 — kıdem süresine göre sabit gün sayıları (2/4/6/8 hafta)."),

        ["damga-vergisi-orani"] = ("OnLawChange",
            "488 sayılı Damga Vergisi Kanunu eki tablolarına göre. CBK ile değişir."),
    };

    public static async Task BackfillAsync(ApplicationDbContext ctx, CancellationToken ct = default)
    {
        var rows = await ctx.Set<FormulaParameter>()
            .Where(p => p.ExpectedUpdateFrequency == null)
            .ToListAsync(ct);

        var changed = 0;
        foreach (var row in rows)
        {
            if (!Map.TryGetValue(row.ToolSlug, out var meta)) continue;

            row.ExpectedUpdateFrequency = meta.Freq;
            row.Notes = meta.Notes;
            row.LastUpdatedDate = row.EffectiveDate;
            changed++;
        }

        if (changed > 0)
            await ctx.SaveChangesAsync(ct);
    }
}
