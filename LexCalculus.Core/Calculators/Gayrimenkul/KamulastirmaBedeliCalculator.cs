using System.Globalization;
using LexCalculus.Core.Calculators.Common;
using LexCalculus.Core.Interfaces;
using LexCalculus.Core.Models.Calculators;

namespace LexCalculus.Core.Calculators.Gayrimenkul;

/// <summary>
/// Expropriation value calculator (Kamulaştırma Bedeli).
///
/// Legal basis: 2942 s.K. (Kamulaştırma Kanunu) m.11. The court expert values
/// the immovable by one of two methods:
///
///   1. Emsal karşılaştırma (comparable sales): emsal birim fiyat × yüzölçümü,
///      adjusted by an "objektif değer artırıcı unsur" ratio. Per Yargıtay 5. HD
///      E. 2004/12813 K. 2005/675, that objective-increase ratio cannot exceed
///      %100 (a multiplier ≤ 2.0); requests above the ceiling are capped.
///
///   2. Gelir kapitalizasyonu (income capitalization) — used for agricultural /
///      income-bearing land: value = yıllık net gelir / kapitalizasyon oranı.
///      This is the perpetuity capitalization the expropriation case-law applies
///      (land yields income indefinitely), NOT a finite annuity.
///
/// A building (yapı) value can be added on top in either method:
/// yapı alanı × yapı birim maliyeti.
///
/// This is a simplified, principle-level estimate — it does not replace a court
/// expert report (see the UI warning).
/// </summary>
public sealed class KamulastirmaBedeliCalculator : ICalculator<KamulastirmaBedeliInput, KamulastirmaBedeliResult>
{
    private const string Slug = "kamulastirma-bedeli";
    private const string ParamObjektifMax = "objektif-artis.max-orani";
    private const string ParamKapitalizasyonDefault = "kapitalizasyon-orani.default";

    private readonly IFormulaParameterService _params;

    public KamulastirmaBedeliCalculator(IFormulaParameterService parameters)
    {
        _params = parameters ?? throw new ArgumentNullException(nameof(parameters));
    }

    public CalculatorMetadata Metadata { get; } = new()
    {
        Slug = Slug,
        Category = CalculatorCategory.Gayrimenkul,
        Title = "Kamulaştırma Bedeli Hesaplama",
        ShortDescription = "2942 s.K. m.11 — emsal karşılaştırma veya gelir kapitalizasyonu yöntemiyle taşınmaz bedeli; objektif değer artışı %100 sınırı (Yargıtay 5. HD).",
        LegalReference = "2942 s.K. m.11",
        Status = CalculatorStatus.Active,
        DisplayNumber = "19",
        Keywords = new[] { "kamulaştırma", "bedel tespiti", "2942", "emsal", "gelir kapitalizasyonu", "objektif artış" }
    };

    public async Task<KamulastirmaBedeliResult> CalculateAsync(KamulastirmaBedeliInput input, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        var result = new KamulastirmaBedeliResult();

        if (input.Yuzolcumu is null or <= 0)
            result.ValidationErrors[nameof(input.Yuzolcumu)] = "Yüzölçümü pozitif olmalıdır.";

        if (input.Yontem == KamulastirmaYontemi.EmsalKarsilastirma)
        {
            if (input.EmsalBirimFiyat is null or <= 0)
                result.ValidationErrors[nameof(input.EmsalBirimFiyat)] = "Emsal karşılaştırma için emsal birim fiyat pozitif olmalıdır.";
            if (input.ObjektifArtisOrani < 0)
                result.ValidationErrors[nameof(input.ObjektifArtisOrani)] = "Objektif artış oranı negatif olamaz.";
        }
        else // GelirKapitalizasyonu
        {
            if (input.YillikNetGelir is null or <= 0)
                result.ValidationErrors[nameof(input.YillikNetGelir)] = "Gelir kapitalizasyonu için yıllık net gelir pozitif olmalıdır.";
            if (input.KapitalizasyonOrani is <= 0)
                result.ValidationErrors[nameof(input.KapitalizasyonOrani)] = "Kapitalizasyon oranı pozitif olmalıdır.";
        }

        if (input.YapiVar)
        {
            if (input.YapiAlani is null or <= 0)
                result.ValidationErrors[nameof(input.YapiAlani)] = "Yapı işaretlendiyse yapı alanı pozitif olmalıdır.";
            if (input.YapiBirimMaliyet is null or <= 0)
                result.ValidationErrors[nameof(input.YapiBirimMaliyet)] = "Yapı işaretlendiyse yapı birim maliyeti pozitif olmalıdır.";
        }

        if (result.ValidationErrors.Count > 0)
        {
            result.IsValid = false;
            return result;
        }

        var asOf = input.AsOfDate;
        var yuzolcumu = input.Yuzolcumu!.Value;
        var tr = new CultureInfo("tr-TR");

        decimal arsaBedeli;
        if (input.Yontem == KamulastirmaYontemi.EmsalKarsilastirma)
        {
            // Objektif değer artışı: Yargıtay 5. HD K. 2005/675 — tavan %100 (çarpan ≤ 2.0).
            var maxOran = await GetParamAsync(ParamObjektifMax, asOf, cancellationToken, fallback: 1.0m);
            var istenenOran = input.ObjektifArtisOrani / 100m;
            var uygulanan = Math.Min(istenenOran, maxOran);
            result.ObjektifArtisCapllendi = istenenOran > maxOran;
            result.ObjektifArtisUygulanan = uygulanan;

            arsaBedeli = input.EmsalBirimFiyat!.Value * yuzolcumu * (1m + uygulanan);
            result.KullanilanYontem = "Emsal Karşılaştırma";

            result.Rows.Add(new CalculationResultRow { Key = "Emsal Birim Fiyat", Value = input.EmsalBirimFiyat.Value.ToString("N2", tr) + " TL/m²" });
            result.Rows.Add(new CalculationResultRow { Key = "Yüzölçümü", Value = yuzolcumu.ToString("N2", tr) + " m²" });
            result.Rows.Add(new CalculationResultRow { Key = "Uygulanan Objektif Artış", Value = $"%{(uygulanan * 100m).ToString("0.##", tr)}" });
            if (result.ObjektifArtisCapllendi)
            {
                result.Warnings.Add($"Objektif değer artış oranı %{(istenenOran * 100m).ToString("0.##", tr)} girildi; " +
                                    $"Yargıtay 5. HD (K. 2005/675) içtihadı gereği %{(maxOran * 100m).ToString("0.##", tr)} tavanı uygulandı.");
            }
        }
        else
        {
            // Gelir kapitalizasyonu: değer = yıllık net gelir / kapitalizasyon oranı (perpetüite).
            var kapOran = input.KapitalizasyonOrani.HasValue
                ? input.KapitalizasyonOrani.Value / 100m
                : await GetParamAsync(ParamKapitalizasyonDefault, asOf, cancellationToken, fallback: 0.06m);

            arsaBedeli = input.YillikNetGelir!.Value / kapOran;
            result.KullanilanYontem = "Gelir Kapitalizasyonu";

            result.Rows.Add(new CalculationResultRow { Key = "Yıllık Net Gelir", Value = input.YillikNetGelir.Value.ToString("N2", tr) + " TL" });
            result.Rows.Add(new CalculationResultRow { Key = "Kapitalizasyon Oranı", Value = $"%{(kapOran * 100m).ToString("0.##", tr)}" });
        }

        arsaBedeli = Math.Round(arsaBedeli, 2, MidpointRounding.AwayFromZero);

        decimal binaBedeli = 0m;
        if (input.YapiVar)
        {
            binaBedeli = Math.Round(input.YapiAlani!.Value * input.YapiBirimMaliyet!.Value, 2, MidpointRounding.AwayFromZero);
            result.Rows.Add(new CalculationResultRow { Key = "Yapı Alanı", Value = input.YapiAlani.Value.ToString("N2", tr) + " m²" });
            result.Rows.Add(new CalculationResultRow { Key = "Yapı Birim Maliyeti", Value = input.YapiBirimMaliyet!.Value.ToString("N2", tr) + " TL/m²" });
        }

        var toplam = arsaBedeli + binaBedeli;

        result.ArsaBedeli = arsaBedeli;
        result.BinaBedeli = binaBedeli;
        result.ToplamBedel = toplam;

        result.Rows.Add(new CalculationResultRow { Key = "Arsa/Arazi Bedeli", Value = arsaBedeli.ToString("N2", tr) + " TL" });
        if (input.YapiVar)
            result.Rows.Add(new CalculationResultRow { Key = "Yapı (Bina) Bedeli", Value = binaBedeli.ToString("N2", tr) + " TL" });

        result.TotalAmount = toplam;
        result.TotalLabel = "Toplam Kamulaştırma Bedeli";
        result.Unit = "TL";

        result.Note = "<strong>Yöntem:</strong> " + result.KullanilanYontem + ". " +
                      "<strong>Mevzuat:</strong> 2942 s.K. (Kamulaştırma Kanunu) m.11. " +
                      "Emsal yönteminde objektif değer artırıcı unsur oranı Yargıtay 5. HD (E. 2004/12813 K. 2005/675) içtihadı gereği %100'ü (çarpan 2.0) aşamaz. " +
                      "<strong>Önemli:</strong> Bu hesaplama prensip seviyesinde bir ön değerlendirmedir; somut davada mahkeme/bilirkişi raporu rayiç değer, konum ve çevresel faktörleri ayrıca değerlendirir. Bu sonuç bilirkişi incelemesi yerine geçmez.";

        return result;
    }

    /// <summary>
    /// Reads a parameter, falling back to a sensible in-code default if the
    /// admin hasn't seeded it (these are advisory ceilings/defaults, not
    /// mandatory like Kıdem tavan — a missing row must not break the tool).
    /// </summary>
    private async Task<decimal> GetParamAsync(string key, DateTime asOf, CancellationToken ct, decimal fallback)
        => await _params.GetValueAsync(Slug, key, asOf, ct) ?? fallback;
}
