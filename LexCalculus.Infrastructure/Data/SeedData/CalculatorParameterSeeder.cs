using LexCalculus.Core.Entities.Calculators;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LexCalculus.Infrastructure.Data.SeedData;

/// <summary>
/// Seeds initial FormulaParameters required by Phase 2 calculators.
/// Idempotent: existing rows (matched by ToolSlug + Key + EffectiveDate) are skipped.
///
/// As more calculators come online, their parameters are added here. In Phase 3
/// the admin panel will replace this seeder for ongoing maintenance, but the
/// seeder remains for fresh installs and tests.
/// </summary>
public static class CalculatorParameterSeeder
{
    public static async Task SeedAsync(ApplicationDbContext db, ILogger logger, CancellationToken ct = default)
    {
        var seeds = new List<FormulaParameter>
        {
            new() { ToolSlug = "kidem-tazminati", Key = "tavan", Value = 35058.58m, EffectiveDate = new DateTime(2025, 1, 1), Source = "Çalışma Bakanlığı 2025 ilk yarı", Note = "2025/1 tebliği" },
            new() { ToolSlug = "kidem-tazminati", Key = "tavan", Value = 41828.42m, EffectiveDate = new DateTime(2025, 7, 1), Source = "Çalışma Bakanlığı 2025 ikinci yarı", Note = "2025/2 tebliği" },
            new() { ToolSlug = "kidem-tazminati", Key = "tavan", Value = 53919.68m, EffectiveDate = new DateTime(2026, 1, 1), Source = "Çalışma Bakanlığı 2026 ilk yarı", Note = "2026/1 tebliği — örnek değer, gerçek değerle güncellenmeli" },

            new() { ToolSlug = "*", Key = "damga-vergisi-orani", Value = 0.00759m, EffectiveDate = new DateTime(2020, 1, 1), Source = "488 s.K. — binde 7,59", Note = "Sabit oran (kanun değişikliğine kadar)" },

            new() { ToolSlug = "ihbar-tazminati", Key = "gelir-vergisi-orani-basit", Value = 0.15m, EffectiveDate = new DateTime(2020, 1, 1), Source = "GVK ilk dilim — basitleştirilmiş", Note = "Phase 2 simplification; cumulative bracket table planned for Phase 3" },

            new() { ToolSlug = "*", Key = "asgari-ucret-brut", Value = 20002.50m, EffectiveDate = new DateTime(2024, 1, 1), Source = "Çalışma Bakanlığı 2024", Note = "2024 yılı asgari ücret brüt aylık" },
            new() { ToolSlug = "*", Key = "asgari-ucret-brut", Value = 26005.50m, EffectiveDate = new DateTime(2025, 1, 1), Source = "Çalışma Bakanlığı 2025", Note = "2025 yılı asgari ücret brüt aylık" },
            new() { ToolSlug = "*", Key = "asgari-ucret-brut", Value = 33000.00m, EffectiveDate = new DateTime(2026, 1, 1), Source = "Çalışma Bakanlığı 2026 — örnek değer", Note = "Gerçek 2026 değeriyle güncellenmelidir" },

            new() { ToolSlug = "*", Key = "yasal-faiz-orani-yillik", Value = 0.18m, EffectiveDate = new DateTime(2020, 1, 1), Source = "3095 s.K. (basitleştirilmiş)", Note = "Phase 2 — flat 18% annual; periodic table planned for Phase 3" },

            // ----- 3095 s.K. m.1 — Yasal Faiz (resmi tarihçe, sadece 2 değişiklik) -----
            new() { ToolSlug = "yasal-faiz", Key = "yillik-oran", Value = 0.09m, EffectiveDate = new DateTime(2006, 1, 1), Source = "BKK 2005/9831 — RG 30.12.2005/26039", Note = "Yasal faiz yıllık %9 — 18 yıl sabit kaldı" },
            new() { ToolSlug = "yasal-faiz", Key = "yillik-oran", Value = 0.24m, EffectiveDate = new DateTime(2024, 6, 1), Source = "CBK 8485 — RG 21.05.2024/32552", Note = "Yasal faiz yıllık %24 — Cumhurbaşkanı Kararı" },

            // ----- 3095 s.K. m.2 — Ticari Faiz (TCMB avans + 4 puan) -----
            new() { ToolSlug = "ticari-faiz", Key = "yillik-oran", Value = 0.1875m, EffectiveDate = new DateTime(2022, 12, 1), Source = "TCMB Avans 14.75 + 4", Note = "Ticari faiz %18.75" },
            new() { ToolSlug = "ticari-faiz", Key = "yillik-oran", Value = 0.5475m, EffectiveDate = new DateTime(2024, 3, 1), Source = "TCMB Avans 50.75 + 4", Note = "Ticari faiz %54.75" },
            new() { ToolSlug = "ticari-faiz", Key = "yillik-oran", Value = 0.4925m, EffectiveDate = new DateTime(2025, 4, 1), Source = "TCMB Avans 45.25 + 4", Note = "Ticari faiz %49.25 (örnek 2025)" }
        };

        foreach (var seed in seeds)
        {
            var exists = await db.Set<FormulaParameter>().AnyAsync(p =>
                p.ToolSlug == seed.ToolSlug && p.Key == seed.Key && p.EffectiveDate == seed.EffectiveDate, ct);

            if (exists)
            {
                logger.LogDebug("Parameter already exists: {Slug}/{Key}@{Date:yyyy-MM-dd}", seed.ToolSlug, seed.Key, seed.EffectiveDate);
                continue;
            }

            db.Set<FormulaParameter>().Add(seed);
            logger.LogInformation("Seeded parameter: {Slug}/{Key}@{Date:yyyy-MM-dd} = {Value}",
                seed.ToolSlug, seed.Key, seed.EffectiveDate, seed.Value);
        }

        await db.SaveChangesAsync(ct);
    }
}
