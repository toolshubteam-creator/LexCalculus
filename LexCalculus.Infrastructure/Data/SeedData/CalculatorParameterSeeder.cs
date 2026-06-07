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
            new() { ToolSlug = "kidem-tazminati", Key = "tavan", Value = 35058.58m, EffectiveDate = new DateTime(2024, 1, 1),
                Source = "Hazine ve Maliye Bakanlığı 05.01.2024 tarih ve 1 sıra no'lu Mali ve Sosyal Haklar Genelgesi",
                Note = "01.01.2024 - 30.06.2024 dönemi kıdem tazminatı tavanı",
                ExpectedUpdateFrequency = "Biannual",
                LastUpdatedDate = new DateTime(2024, 1, 5),
                Notes = "Kıdem tazminatı tavanı her yıl Ocak ve Temmuz ayında Hazine ve Maliye Bakanlığı genelgesi ile belirlenir. Kaynak: ms.hmb.gov.tr" },
            new() { ToolSlug = "kidem-tazminati", Key = "tavan", Value = 41828.42m, EffectiveDate = new DateTime(2024, 7, 1),
                Source = "Hazine ve Maliye Bakanlığı 05.07.2024 tarihli Mali ve Sosyal Haklar Genelgesi",
                Note = "01.07.2024 - 31.12.2024 dönemi kıdem tazminatı tavanı",
                ExpectedUpdateFrequency = "Biannual",
                LastUpdatedDate = new DateTime(2024, 7, 5),
                Notes = "Kıdem tazminatı tavanı her yıl Ocak ve Temmuz ayında Hazine ve Maliye Bakanlığı genelgesi ile belirlenir. Kaynak: ms.hmb.gov.tr" },
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

            // ----- 3095 s.K. m.2 — Ticari Temerrüt Faizi: TCMB Avans Oranları (HAM TABLO) -----
            // Calculator 5 puan kuralını ve 6 aylık dönem algoritmasını uygular.
            new() { ToolSlug = "tcmb-avans", Key = "yillik-oran", Value = 0.1950m, EffectiveDate = new DateTime(2018, 6, 29), Source = "TCMB", Note = "TCMB avans oranı %19.50" },
            new() { ToolSlug = "tcmb-avans", Key = "yillik-oran", Value = 0.1825m, EffectiveDate = new DateTime(2019, 10, 11), Source = "TCMB", Note = "TCMB avans oranı %18.25" },
            new() { ToolSlug = "tcmb-avans", Key = "yillik-oran", Value = 0.1375m, EffectiveDate = new DateTime(2019, 12, 21), Source = "TCMB", Note = "TCMB avans oranı %13.75" },
            new() { ToolSlug = "tcmb-avans", Key = "yillik-oran", Value = 0.1000m, EffectiveDate = new DateTime(2020, 6, 13), Source = "TCMB", Note = "TCMB avans oranı %10.00" },
            new() { ToolSlug = "tcmb-avans", Key = "yillik-oran", Value = 0.1675m, EffectiveDate = new DateTime(2020, 12, 19), Source = "TCMB", Note = "TCMB avans oranı %16.75" },
            new() { ToolSlug = "tcmb-avans", Key = "yillik-oran", Value = 0.1575m, EffectiveDate = new DateTime(2021, 12, 31), Source = "TCMB", Note = "TCMB avans oranı %15.75" },
            new() { ToolSlug = "tcmb-avans", Key = "yillik-oran", Value = 0.1075m, EffectiveDate = new DateTime(2022, 12, 31), Source = "TCMB", Note = "TCMB avans oranı %10.75" },
            new() { ToolSlug = "tcmb-avans", Key = "yillik-oran", Value = 0.1675m, EffectiveDate = new DateTime(2023, 6, 24), Source = "TCMB", Note = "TCMB avans oranı %16.75" },
            new() { ToolSlug = "tcmb-avans", Key = "yillik-oran", Value = 0.2675m, EffectiveDate = new DateTime(2023, 9, 1), Source = "TCMB", Note = "TCMB avans oranı %26.75" },
            new() { ToolSlug = "tcmb-avans", Key = "yillik-oran", Value = 0.3175m, EffectiveDate = new DateTime(2023, 9, 28), Source = "TCMB", Note = "TCMB avans oranı %31.75" },
            new() { ToolSlug = "tcmb-avans", Key = "yillik-oran", Value = 0.3675m, EffectiveDate = new DateTime(2023, 11, 1), Source = "TCMB", Note = "TCMB avans oranı %36.75" },
            new() { ToolSlug = "tcmb-avans", Key = "yillik-oran", Value = 0.4175m, EffectiveDate = new DateTime(2023, 12, 1), Source = "TCMB", Note = "TCMB avans oranı %41.75" },
            new() { ToolSlug = "tcmb-avans", Key = "yillik-oran", Value = 0.4425m, EffectiveDate = new DateTime(2023, 12, 23), Source = "TCMB", Note = "TCMB avans oranı %44.25" },
            new() { ToolSlug = "tcmb-avans", Key = "yillik-oran", Value = 0.5175m, EffectiveDate = new DateTime(2024, 4, 1), Source = "TCMB", Note = "TCMB avans oranı %51.75" },
            new() { ToolSlug = "tcmb-avans", Key = "yillik-oran", Value = 0.4925m, EffectiveDate = new DateTime(2024, 12, 28), Source = "TCMB", Note = "TCMB avans oranı %49.25" },
            new() { ToolSlug = "tcmb-avans", Key = "yillik-oran", Value = 0.4425m, EffectiveDate = new DateTime(2025, 3, 8), Source = "TCMB", Note = "TCMB avans oranı %44.25" },
            new() { ToolSlug = "tcmb-avans", Key = "yillik-oran", Value = 0.4225m, EffectiveDate = new DateTime(2025, 9, 17), Source = "TCMB", Note = "TCMB avans oranı %42.25" },
            new() { ToolSlug = "tcmb-avans", Key = "yillik-oran", Value = 0.3975m, EffectiveDate = new DateTime(2025, 12, 20), Source = "TCMB", Note = "TCMB avans oranı %39.75" },

            // ----- D1 Arsa Payı (634 s.K. m.3) — değer ağırlıklı yüzölçümü katsayıları -----
            // Kullanım türü katsayıları (mesken = taban 1.0). Admin paneli ileride ayarlayabilir.
            new() { ToolSlug = "arsa-payi", Key = "katsayi.mesken", Value = 1.0m, EffectiveDate = new DateTime(2026, 1, 1), Source = "634 s.K. m.3 — değer ağırlıklı yöntem (taban)", Note = "Mesken kullanım türü katsayısı (taban)" },
            new() { ToolSlug = "arsa-payi", Key = "katsayi.dukkan", Value = 1.3m, EffectiveDate = new DateTime(2026, 1, 1), Source = "634 s.K. m.3 — değer ağırlıklı yöntem", Note = "Dükkan/işyeri katsayısı (mesken'e göre yüksek değer)" },
            new() { ToolSlug = "arsa-payi", Key = "katsayi.bodrum", Value = 0.6m, EffectiveDate = new DateTime(2026, 1, 1), Source = "634 s.K. m.3 — değer ağırlıklı yöntem", Note = "Bodrum katsayısı (mesken'e göre düşük değer)" },
            new() { ToolSlug = "arsa-payi", Key = "katsayi.cati-kati", Value = 0.8m, EffectiveDate = new DateTime(2026, 1, 1), Source = "634 s.K. m.3 — değer ağırlıklı yöntem", Note = "Çatı katı katsayısı" },
            // Kat etkisi: zemin = taban; her üst kat artış oranı kadar değer kazanır.
            new() { ToolSlug = "arsa-payi", Key = "katsayi.kat.zemin", Value = 1.0m, EffectiveDate = new DateTime(2026, 1, 1), Source = "634 s.K. m.3 — değer ağırlıklı yöntem (taban)", Note = "Zemin kat etkisi katsayısı (taban)" },
            new() { ToolSlug = "arsa-payi", Key = "katsayi.kat.ust-artis-orani", Value = 0.05m, EffectiveDate = new DateTime(2026, 1, 1), Source = "634 s.K. m.3 — değer ağırlıklı yöntem", Note = "Her üst kat için artış oranı (1. kat 1.05, 2. kat 1.10, ...)" },

            // ----- D2 Kamulaştırma Bedeli (2942 s.K. m.11) -----
            new() { ToolSlug = "kamulastirma-bedeli", Key = "objektif-artis.max-orani", Value = 1.0m, EffectiveDate = new DateTime(2005, 1, 1), Source = "Yargıtay 5. HD E. 2004/12813 K. 2005/675", Note = "Objektif değer artırıcı unsur tavanı %100 (çarpan 2.0) — içtihat" },
            new() { ToolSlug = "kamulastirma-bedeli", Key = "kapitalizasyon-orani.default", Value = 0.06m, EffectiveDate = new DateTime(2020, 1, 1), Source = "2942 s.K. m.11 gelir yöntemi — referans", Note = "Gelir kapitalizasyonu varsayılan oranı %6 (kullanıcı override edebilir)" },

            // ----- ÜFE yıllık artış oranları (global "*") — D3 Ecrimisil + ileride D-I güncellemeleri -----
            // Yargıtay ecrimisil içtihadı TÜFE değil ÜFE kullanır. Değerler TÜİK yıllık ÜFE (% artış).
            new() { ToolSlug = "*", Key = "ufe.yillik", Value = 25.7m, EffectiveDate = new DateTime(2020, 1, 1), Source = "TÜİK Yİ-ÜFE yıllık", Note = "2020 yıllık ÜFE artışı %25,7" },
            new() { ToolSlug = "*", Key = "ufe.yillik", Value = 79.9m, EffectiveDate = new DateTime(2021, 1, 1), Source = "TÜİK Yİ-ÜFE yıllık", Note = "2021 yıllık ÜFE artışı %79,9" },
            new() { ToolSlug = "*", Key = "ufe.yillik", Value = 97.7m, EffectiveDate = new DateTime(2022, 1, 1), Source = "TÜİK Yİ-ÜFE yıllık", Note = "2022 yıllık ÜFE artışı %97,7" },
            new() { ToolSlug = "*", Key = "ufe.yillik", Value = 64.8m, EffectiveDate = new DateTime(2023, 1, 1), Source = "TÜİK Yİ-ÜFE yıllık", Note = "2023 yıllık ÜFE artışı %64,8" },
            new() { ToolSlug = "*", Key = "ufe.yillik", Value = 51.3m, EffectiveDate = new DateTime(2024, 1, 1), Source = "TÜİK Yİ-ÜFE yıllık", Note = "2024 yıllık ÜFE artışı %51,3" },
            new() { ToolSlug = "*", Key = "ufe.yillik", Value = 33.0m, EffectiveDate = new DateTime(2025, 1, 1), Source = "TÜİK Yİ-ÜFE yıllık — örnek değer", Note = "2025 yıllık ÜFE (örnek; gerçek değerle güncellenmeli)" },
            new() { ToolSlug = "*", Key = "ufe.yillik", Value = 15.0m, EffectiveDate = new DateTime(2026, 1, 1), Source = "TÜİK Yİ-ÜFE yıllık — örnek/yıl içi", Note = "2026 yıl içi ÜFE (örnek placeholder; hukuk profesyoneli güncelleyecek)" }
        };

        foreach (var seed in seeds)
        {
            // Tech-debt madde 2: soft-delete bypass ile mevcut satırı sorgula.
            // Admin paneli kanonik bir seed satırını yanlışlıkla soft-delete ederse
            // global filter onu görmez → INSERT denenir → unique constraint patlar.
            // Çözüm: IgnoreQueryFilters() ile satırı bul; varsa restore et, yoksa ekle.
            var existing = await db.Set<FormulaParameter>()
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(p =>
                    p.ToolSlug == seed.ToolSlug && p.Key == seed.Key && p.EffectiveDate == seed.EffectiveDate, ct);

            if (existing is not null)
            {
                if (existing.IsDeleted)
                {
                    existing.IsDeleted = false;
                    logger.LogWarning(
                        "Kanonik seed satırı soft-deleted bulundu, restore ediliyor: {Slug}/{Key}@{Date:yyyy-MM-dd}",
                        seed.ToolSlug, seed.Key, seed.EffectiveDate);
                }
                else
                {
                    logger.LogDebug("Parameter already exists: {Slug}/{Key}@{Date:yyyy-MM-dd}",
                        seed.ToolSlug, seed.Key, seed.EffectiveDate);
                }
                continue;
            }

            db.Set<FormulaParameter>().Add(seed);
            logger.LogInformation("Seeded parameter: {Slug}/{Key}@{Date:yyyy-MM-dd} = {Value}",
                seed.ToolSlug, seed.Key, seed.EffectiveDate, seed.Value);
        }

        await db.SaveChangesAsync(ct);
    }
}
