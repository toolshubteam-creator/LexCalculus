using System.Globalization;
using LexCalculus.Core.Calculators.Common;
using LexCalculus.Core.Models.Calculators;

namespace LexCalculus.Core.Calculators.Gayrimenkul;

/// <summary>
/// Kat karşılığı inşaat (construction-in-return) share calculator.
///
/// Legal basis: TBK genel hükümler + Yargıtay içtihadı. The land owner gives the
/// land; the contractor builds and both share the finished project. The share is
/// determined either:
///
///   - Oransal: arsa payı oranı = arsa değeri / (arsa değeri + inşaat maliyeti).
///     Owner share = toplam proje değeri × oran; contractor gets the rest.
///   - Sabit: a fixed contractual owner ratio (e.g. %40).
///
/// The output is a VALUE distribution. Which concrete independent units (daire)
/// go to whom, external cost items, and undertakings are contract details — the
/// approximate unit counts here are advisory only (see the UI warning).
///
/// No FormulaParameters — every figure is user input.
/// </summary>
public sealed class KatKarsiligiInsaatCalculator : ICalculator<KatKarsiligiInsaatInput, KatKarsiligiInsaatResult>
{
    public CalculatorMetadata Metadata { get; } = new()
    {
        Slug = "kat-karsiligi-insaat",
        Category = CalculatorCategory.Gayrimenkul,
        Title = "Kat Karşılığı İnşaat Paylaşımı",
        ShortDescription = "TBK + içtihat — arsa sahibi ve müteahhit arasında proje değerinin oransal (değer bazlı) veya sabit sözleşme oranıyla paylaşımı.",
        LegalReference = "TBK genel hükümler",
        Status = CalculatorStatus.Active,
        DisplayNumber = "21",
        Keywords = new[] { "kat karşılığı", "inşaat", "müteahhit", "arsa payı", "paylaşım", "kentsel dönüşüm" }
    };

    public Task<KatKarsiligiInsaatResult> CalculateAsync(KatKarsiligiInsaatInput input, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        var result = new KatKarsiligiInsaatResult();

        if (input.ToplamProjeDegeri is null or <= 0)
            result.ValidationErrors[nameof(input.ToplamProjeDegeri)] = "Toplam proje değeri pozitif olmalıdır.";

        if (input.Yontem == KatKarsiligiYontemi.Oransal)
        {
            if (input.ArsaDegeri is null or < 0)
                result.ValidationErrors[nameof(input.ArsaDegeri)] = "Oransal yöntemde arsa değeri negatif olamaz.";
            if (input.ToplamInsaatMaliyeti is null or < 0)
                result.ValidationErrors[nameof(input.ToplamInsaatMaliyeti)] = "Oransal yöntemde inşaat maliyeti negatif olamaz.";
            if (input.ArsaDegeri is not null && input.ToplamInsaatMaliyeti is not null
                && input.ArsaDegeri + input.ToplamInsaatMaliyeti <= 0)
            {
                result.ValidationErrors[nameof(input.ArsaDegeri)] = "Arsa değeri ve inşaat maliyeti toplamı pozitif olmalıdır.";
            }
        }
        else // Sabit
        {
            if (input.ArsaSahibiOrani < 0 || input.ArsaSahibiOrani > 100)
                result.ValidationErrors[nameof(input.ArsaSahibiOrani)] = "Arsa sahibi oranı %0-100 arası olmalıdır.";
        }

        if (result.ValidationErrors.Count > 0)
        {
            result.IsValid = false;
            return Task.FromResult(result);
        }

        var tr = new CultureInfo("tr-TR");
        var projeDeger = input.ToplamProjeDegeri!.Value;

        decimal oran;
        if (input.Yontem == KatKarsiligiYontemi.Oransal)
        {
            var arsa = input.ArsaDegeri!.Value;
            var maliyet = input.ToplamInsaatMaliyeti!.Value;
            oran = arsa / (arsa + maliyet);

            result.Rows.Add(new CalculationResultRow { Key = "Arsa Değeri", Value = arsa.ToString("N2", tr) + " TL" });
            result.Rows.Add(new CalculationResultRow { Key = "Toplam İnşaat Maliyeti", Value = maliyet.ToString("N2", tr) + " TL" });
        }
        else
        {
            oran = input.ArsaSahibiOrani / 100m;
            result.Rows.Add(new CalculationResultRow { Key = "Sözleşme Oranı (Arsa Sahibi)", Value = $"%{input.ArsaSahibiOrani.ToString("0.##", tr)}" });
        }

        var arsaSahibiPay = Math.Round(projeDeger * oran, 2, MidpointRounding.AwayFromZero);
        var muteahhitPay = Math.Round(projeDeger - arsaSahibiPay, 2, MidpointRounding.AwayFromZero);

        result.ArsaOrani = oran;
        result.ArsaSahibiPay = arsaSahibiPay;
        result.MuteahhitPay = muteahhitPay;

        if (input.ToplamBagimsizBolumSayisi is > 0)
        {
            var toplamBolum = input.ToplamBagimsizBolumSayisi.Value;
            result.YaklasikArsaSahibiBolumSayisi = Math.Round(toplamBolum * oran, 2, MidpointRounding.AwayFromZero);
            result.YaklasikMuteahhitBolumSayisi = Math.Round(toplamBolum - (toplamBolum * oran), 2, MidpointRounding.AwayFromZero);
        }

        result.Rows.Add(new CalculationResultRow { Key = "Toplam Proje Değeri", Value = projeDeger.ToString("N2", tr) + " TL" });
        result.Rows.Add(new CalculationResultRow { Key = "Arsa Sahibi Oranı", Value = $"%{(oran * 100m).ToString("0.##", tr)}" });
        result.Rows.Add(new CalculationResultRow { Key = "Arsa Sahibi Payı", Value = arsaSahibiPay.ToString("N2", tr) + " TL", IsHighlighted = true });
        result.Rows.Add(new CalculationResultRow { Key = "Müteahhit Payı", Value = muteahhitPay.ToString("N2", tr) + " TL", IsHighlighted = true });

        if (input.ToplamBagimsizBolumSayisi is > 0)
        {
            result.Rows.Add(new CalculationResultRow
            {
                Key = "Yaklaşık Bölüm Dağılımı",
                Value = $"Arsa sahibi ~{result.YaklasikArsaSahibiBolumSayisi.ToString("0.##", tr)} / Müteahhit ~{result.YaklasikMuteahhitBolumSayisi.ToString("0.##", tr)}"
            });
        }

        result.TotalAmount = arsaSahibiPay;
        result.TotalLabel = "Arsa Sahibi Payı";
        result.Unit = "TL";

        result.Note = "<strong>Yöntem:</strong> " +
                      (input.Yontem == KatKarsiligiYontemi.Oransal
                          ? "Oransal — arsa payı oranı = arsa değeri / (arsa değeri + inşaat maliyeti)."
                          : "Sabit — sözleşmede belirlenen arsa sahibi oranı.") +
                      " <strong>Mevzuat:</strong> TBK genel hükümler + Yargıtay içtihadı. " +
                      "<strong>Önemli:</strong> Bu hesap değer dağılımı seviyesinde sonuç verir. Somut sözleşme detayları (hangi bağımsız bölüm kime, dış kalemler, taahhütler) ayrı düzenlenir. Bu sonuç sözleşme yerine geçmez.";

        return Task.FromResult(result);
    }
}
