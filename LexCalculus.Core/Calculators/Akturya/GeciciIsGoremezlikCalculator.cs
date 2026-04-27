using System.Globalization;
using LexCalculus.Core.Calculators.Common;
using LexCalculus.Core.Models.Calculators;

namespace LexCalculus.Core.Calculators.Akturya;

/// <summary>
/// Temporary disability compensation (Geçici İş Göremezlik Tazminatı).
///
/// Legal basis: 5510 s.K. m.18 (SGK temporary disability allowance) +
/// TBK m.54 (residual responsibility of tortfeasor).
///
/// Methodology:
///   1. Daily gross = monthly gross / 30 (standard accounting convention)
///   2. Gross loss = days × daily gross
///   3. SGK allowance = gross loss × SGK rate (default 66.67% per 5510 s.K.)
///   4. Net loss (claim against tortfeasor) = gross loss - SGK allowance
///
/// SGK pays 2/3 (66.67%); the responsible party covers the remaining 1/3
/// gap when fault is established (TBK m.54). User can disable SGK offset
/// for cases where SGK didn't pay (uninsured worker, denied claim, etc.).
///
/// Statute of limitations: 2 years (TBK m.72).
/// </summary>
public sealed class GeciciIsGoremezlikCalculator
    : ICalculator<GeciciIsGoremezlikInput, GeciciIsGoremezlikResult>
{
    private const decimal AylikGunSayisi = 30m;

    public CalculatorMetadata Metadata { get; } = new()
    {
        Slug = "gecici-is-goremezlik",
        Category = CalculatorCategory.Akturya,
        Title = "Geçici İş Göremezlik Tazminatı",
        ShortDescription = "Geçici çalışamama süresi için günlük gelir × gün sayısı − SGK ödeneği farkı.",
        LegalReference = "5510 s.K. m.18 / TBK m.54",
        Status = CalculatorStatus.Active,
        DisplayNumber = "10",
        Keywords = new[] { "geçici iş göremezlik", "SGK ödeneği", "kazanç kaybı", "aktüerya" }
    };

    public Task<GeciciIsGoremezlikResult> CalculateAsync(GeciciIsGoremezlikInput input, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        var result = new GeciciIsGoremezlikResult();

        if (input.OlayTarihi is null)
            result.ValidationErrors[nameof(input.OlayTarihi)] = "Olay tarihi boş olamaz.";
        if (input.SureGun is null or <= 0)
            result.ValidationErrors[nameof(input.SureGun)] = "Süre pozitif olmalıdır.";
        if (input.AylikBrutUcret is null or <= 0)
            result.ValidationErrors[nameof(input.AylikBrutUcret)] = "Brüt ücret pozitif olmalıdır.";

        if (result.ValidationErrors.Count > 0)
        {
            result.IsValid = false;
            return Task.FromResult(result);
        }

        var brut = input.AylikBrutUcret!.Value;
        var sureGun = input.SureGun!.Value;
        var sgkOranNet = input.SgkOrani / 100m;

        var gunlukBrut = Math.Round(brut / AylikGunSayisi, 2, MidpointRounding.AwayFromZero);
        var brutMahrum = Math.Round(gunlukBrut * sureGun, 2, MidpointRounding.AwayFromZero);
        var sgkOdenegi = input.SgkMahsup
            ? Math.Round(brutMahrum * sgkOranNet, 2, MidpointRounding.AwayFromZero)
            : 0m;
        var netMahrum = brutMahrum - sgkOdenegi;

        result.GunlukBrut = gunlukBrut;
        result.SureGun = sureGun;
        result.BrutMahrumTutar = brutMahrum;
        result.SgkOdenegi = sgkOdenegi;
        result.NetMahrumTutar = netMahrum;

        var tr = new CultureInfo("tr-TR");
        result.TotalAmount = netMahrum;
        result.TotalLabel = "Net Talep Edilebilir Tutar";
        result.Unit = "TL";

        result.Rows.Add(new CalculationResultRow { Key = "İş Göremezlik Süresi", Value = $"{sureGun} gün" });
        result.Rows.Add(new CalculationResultRow { Key = "Aylık Brüt Ücret", Value = brut.ToString("N2", tr) + " TL" });
        result.Rows.Add(new CalculationResultRow { Key = "Günlük Brüt (aylık/30)", Value = gunlukBrut.ToString("N2", tr) + " TL" });
        result.Rows.Add(new CalculationResultRow { Key = "Brüt Mahrum Kalınan Tutar", Value = brutMahrum.ToString("N2", tr) + " TL", IsHighlighted = true });

        if (input.SgkMahsup)
        {
            result.Rows.Add(new CalculationResultRow { Key = $"SGK Ödeneği (%{input.SgkOrani:0.##})", Value = sgkOdenegi.ToString("N2", tr) + " TL" });
        }
        else
        {
            result.Warnings.Add("SGK mahsubu kapalı. Bu, SGK'nın ödeneği vermediği veya kayıt dışı çalışma durumlarında kullanılır.");
        }

        result.Note = "<strong>Hesap:</strong> Günlük brüt × gün sayısı − SGK ödeneği = sorumlu taraftan istenebilecek net fark. " +
                      "<strong>SGK Oranı:</strong> 5510 s.K. m.18 uyarınca 2/3 (yaklaşık %66.67); SGK kayıt dışı çalışmaya ödeme yapmaz. " +
                      "<strong>Mevzuat:</strong> 5510 s.K. m.18 + TBK m.54. " +
                      "<strong>Zamanaşımı:</strong> 2 yıl (TBK m.72).";

        return Task.FromResult(result);
    }
}
