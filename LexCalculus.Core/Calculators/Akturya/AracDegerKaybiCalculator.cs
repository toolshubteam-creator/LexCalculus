using System.Globalization;
using LexCalculus.Core.Calculators.Common;
using LexCalculus.Core.Models.Calculators;

namespace LexCalculus.Core.Calculators.Akturya;

/// <summary>
/// Vehicle diminished value compensation (Araç Değer Kaybı).
///
/// Legal basis: TBK m.49 + Yargıtay 17. HD case law + Sigorta Tahkim
/// Komisyonu (Insurance Arbitration Commission) decisions.
///
/// Methodology (TRAMER baseline):
///   1. Damage ratio = (pre - post) / pre
///   2. Age factor: newer cars suffer more diminished value
///      0-1: 1.0,  2: 0.85,  3: 0.70,  4-5: 0.50,  6-10: 0.30,  11+: 0.10
///   3. Mileage factor: lower km cars affected more
///      &lt;30k: 1.0,  30-100k: 0.80,  100-200k: 0.50,  &gt;=200k: 0.20
///   4. Diminished value = pre × damage ratio × age factor × mileage factor
///
/// Pert (total loss) flag: if damage ratio &gt; 30%, the calculator warns that
/// this may be a total loss case — diminished value methodology may not apply.
///
/// Statute of limitations: 2 years (TTK m.1483 for KTK, TBK m.72 for tort).
/// </summary>
public sealed class AracDegerKaybiCalculator : ICalculator<AracDegerKaybiInput, AracDegerKaybiResult>
{
    private const decimal PertEsigi = 0.30m;

    public CalculatorMetadata Metadata { get; } = new()
    {
        Slug = "arac-deger-kaybi",
        Category = CalculatorCategory.Akturya,
        Title = "Araç Değer Kaybı",
        ShortDescription = "TRAMER yöntemi: hasar oranı, araç yaşı ve kilometresi faktörleriyle değer kaybı hesabı.",
        LegalReference = "TBK m.49 / Yargıtay 17. HD",
        Status = CalculatorStatus.Active,
        DisplayNumber = "12",
        Keywords = new[] { "araç değer kaybı", "TRAMER", "kasko", "trafik kazası" }
    };

    public Task<AracDegerKaybiResult> CalculateAsync(AracDegerKaybiInput input, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        var result = new AracDegerKaybiResult();

        if (input.OlayTarihi is null)
            result.ValidationErrors[nameof(input.OlayTarihi)] = "Olay tarihi boş olamaz.";
        if (input.KazadanOncekiDeger is null or <= 0)
            result.ValidationErrors[nameof(input.KazadanOncekiDeger)] = "Kazadan önceki değer pozitif olmalıdır.";
        if (input.KazadanSonrakiDeger is null or <= 0)
            result.ValidationErrors[nameof(input.KazadanSonrakiDeger)] = "Kazadan sonraki değer pozitif olmalıdır.";
        if (input.AracYas is null or < 0)
            result.ValidationErrors[nameof(input.AracYas)] = "Araç yaşı negatif olamaz.";
        if (input.AracKm is null or < 0)
            result.ValidationErrors[nameof(input.AracKm)] = "Araç kilometresi negatif olamaz.";

        if (input.KazadanOncekiDeger is not null and > 0
            && input.KazadanSonrakiDeger is not null and > 0
            && input.KazadanSonrakiDeger >= input.KazadanOncekiDeger)
        {
            result.ValidationErrors[nameof(input.KazadanSonrakiDeger)] =
                "Kazadan sonraki değer, kazadan önceki değerden küçük olmalıdır.";
        }

        if (result.ValidationErrors.Count > 0)
        {
            result.IsValid = false;
            return Task.FromResult(result);
        }

        var oncekiDeger = input.KazadanOncekiDeger!.Value;
        var sonrakiDeger = input.KazadanSonrakiDeger!.Value;
        var aracYas = input.AracYas!.Value;
        var aracKm = input.AracKm!.Value;

        var hasarTutari = oncekiDeger - sonrakiDeger;
        var hasarOrani = hasarTutari / oncekiDeger;

        var yasFaktoru = HesaplaYasFaktoru(aracYas);
        var kmFaktoru = HesaplaKmFaktoru(aracKm);

        var degerKaybi = oncekiDeger * hasarOrani * yasFaktoru * kmFaktoru;

        var pertRiski = hasarOrani > PertEsigi;

        result.HasarTutari = Math.Round(hasarTutari, 2);
        result.HasarOrani = hasarOrani;
        result.YasFaktoru = yasFaktoru;
        result.KmFaktoru = kmFaktoru;
        result.DegerKaybi = Math.Round(degerKaybi, 2);
        result.PertRiski = pertRiski;

        var tr = new CultureInfo("tr-TR");
        result.TotalAmount = result.DegerKaybi;
        result.TotalLabel = "Araç Değer Kaybı Tazminatı";
        result.Unit = "TL";

        result.Rows.Add(new CalculationResultRow { Key = "Kazadan Önceki Değer", Value = oncekiDeger.ToString("N2", tr) + " TL" });
        result.Rows.Add(new CalculationResultRow { Key = "Kazadan Sonraki Değer", Value = sonrakiDeger.ToString("N2", tr) + " TL" });
        result.Rows.Add(new CalculationResultRow { Key = "Hasar Tutarı", Value = result.HasarTutari.ToString("N2", tr) + " TL" });
        result.Rows.Add(new CalculationResultRow { Key = "Hasar Oranı", Value = $"%{hasarOrani * 100:F2}" });
        result.Rows.Add(new CalculationResultRow { Key = "Araç Yaşı", Value = $"{aracYas} yıl" });
        result.Rows.Add(new CalculationResultRow { Key = "Yaş Faktörü", Value = $"{yasFaktoru:F2}" });
        result.Rows.Add(new CalculationResultRow { Key = "Araç Kilometresi", Value = aracKm.ToString("N0", tr) + " km" });
        result.Rows.Add(new CalculationResultRow { Key = "Km Faktörü", Value = $"{kmFaktoru:F2}" });

        if (pertRiski)
        {
            result.Warnings.Add($"Hasar oranı %{hasarOrani * 100:F2} ile %30 üstündedir. Bu durumda araç \"pert\" sayılarak değer kaybı yerine total kayıp tazminatı talep edilmesi gerekebilir; bilirkişi raporuna başvurulmalıdır.");
        }

        result.Note = "<strong>Yöntem (TRAMER baseline):</strong> Kazadan önceki değer × hasar oranı × yaş faktörü × km faktörü. " +
                      "<strong>Yaş Faktörleri:</strong> 0-1 yıl: 1.0; 2: 0.85; 3: 0.70; 4-5: 0.50; 6-10: 0.30; 11+: 0.10. " +
                      "<strong>Km Faktörleri:</strong> &lt;30k: 1.0; 30-100k: 0.80; 100-200k: 0.50; &gt;=200k: 0.20. " +
                      "<strong>Pert Eşiği:</strong> Hasar oranı %30 üzerinde ise araç pert sayılır, total kayıp tazminatı kuralları uygulanır. " +
                      "<strong>Mevzuat:</strong> TBK m.49 + Yargıtay 17. HD içtihatları. " +
                      "<strong>Önemli:</strong> Bu hesap baseline'dır; eksper raporu ve mahkeme takdiri ile farklılaşabilir.";

        return Task.FromResult(result);
    }

    private static decimal HesaplaYasFaktoru(int yas) => yas switch
    {
        <= 1 => 1.0m,
        2 => 0.85m,
        3 => 0.70m,
        >= 4 and <= 5 => 0.50m,
        >= 6 and <= 10 => 0.30m,
        _ => 0.10m
    };

    private static decimal HesaplaKmFaktoru(int km) => km switch
    {
        < 30000 => 1.0m,
        < 100000 => 0.80m,
        < 200000 => 0.50m,
        _ => 0.20m
    };
}
