using System.Globalization;
using LexCalculus.Core.Calculators.Common;
using LexCalculus.Core.Models.Calculators;

namespace LexCalculus.Core.Calculators.Gayrimenkul;

/// <summary>
/// Hâsılat (turnover-based) rent calculator — the AVM / shopping-mall rent model.
///
/// Legal basis: TBK genel hükümler + ticari kira pratikleri. The rent is a
/// percentage of the tenant's turnover (ciro), optionally floored by a minimum
/// guarantee and capped by a maximum:
///
///   hesaplanan kira = ciro × hâsılat oranı
///   ödenecek kira   = clamp(hesaplanan, minimum?, maksimum?)
///
/// The applied rule (ciro-based / minimum guarantee / maximum cap) is reported
/// explicitly. No FormulaParameters — every figure is contract input.
/// </summary>
public sealed class HasilatKiraCalculator : ICalculator<HasilatKiraInput, HasilatKiraResult>
{
    public CalculatorMetadata Metadata { get; } = new()
    {
        Slug = "hasilat-kira",
        Category = CalculatorCategory.Gayrimenkul,
        Title = "Hâsılat Kira Hesaplama",
        ShortDescription = "TBK + ticari kira pratikleri — ciro üzerinden hâsılat oranıyla kira; minimum güvence ve maksimum tavan sınırlarıyla (AVM kira modeli).",
        LegalReference = "TBK genel hükümler",
        Status = CalculatorStatus.Active,
        DisplayNumber = "22",
        Keywords = new[] { "hâsılat kira", "ciro kira", "AVM", "ticari kira", "minimum güvence", "kira tavanı" }
    };

    public Task<HasilatKiraResult> CalculateAsync(HasilatKiraInput input, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        var result = new HasilatKiraResult();

        if (input.Ciro is null or < 0)
            result.ValidationErrors[nameof(input.Ciro)] = "Ciro negatif olamaz.";
        if (input.HasilatOrani is null or <= 0 or > 100)
            result.ValidationErrors[nameof(input.HasilatOrani)] = "Hâsılat oranı %0.01-100 arası olmalıdır.";
        if (input.MinimumKira is < 0)
            result.ValidationErrors[nameof(input.MinimumKira)] = "Minimum kira negatif olamaz.";
        if (input.MaksimumKira is < 0)
            result.ValidationErrors[nameof(input.MaksimumKira)] = "Maksimum kira negatif olamaz.";
        if (input.MinimumKira is not null && input.MaksimumKira is not null
            && input.MinimumKira > input.MaksimumKira)
        {
            result.ValidationErrors[nameof(input.MaksimumKira)] = "Maksimum kira, minimum kiradan küçük olamaz.";
        }

        if (result.ValidationErrors.Count > 0)
        {
            result.IsValid = false;
            return Task.FromResult(result);
        }

        var tr = new CultureInfo("tr-TR");
        var donem = input.DonemTuru == HasilatDonemTuru.Yillik ? "yıllık" : "aylık";

        var hesaplanan = Math.Round(input.Ciro!.Value * (input.HasilatOrani!.Value / 100m), 2, MidpointRounding.AwayFromZero);
        var odenecek = hesaplanan;
        var kural = HasilatKiraKurali.CiroBazli;

        // Önce minimum güvence (taban), sonra maksimum tavan.
        if (input.MinimumKira is not null && odenecek < input.MinimumKira.Value)
        {
            odenecek = input.MinimumKira.Value;
            kural = HasilatKiraKurali.MinimumGuvence;
        }
        if (input.MaksimumKira is not null && odenecek > input.MaksimumKira.Value)
        {
            odenecek = input.MaksimumKira.Value;
            kural = HasilatKiraKurali.MaksimumTavan;
        }

        result.HesaplananKira = hesaplanan;
        result.OdenecekKira = odenecek;
        result.HangiKuralDevreyeGirdi = kural;

        result.Rows.Add(new CalculationResultRow { Key = $"Ciro ({donem})", Value = input.Ciro.Value.ToString("N2", tr) + " TL" });
        result.Rows.Add(new CalculationResultRow { Key = "Hâsılat Oranı", Value = $"%{input.HasilatOrani.Value.ToString("0.##", tr)}" });
        result.Rows.Add(new CalculationResultRow { Key = "Hesaplanan Kira (ciro × oran)", Value = hesaplanan.ToString("N2", tr) + " TL" });
        if (input.MinimumKira is not null)
            result.Rows.Add(new CalculationResultRow { Key = "Minimum Kira Güvencesi", Value = input.MinimumKira.Value.ToString("N2", tr) + " TL" });
        if (input.MaksimumKira is not null)
            result.Rows.Add(new CalculationResultRow { Key = "Maksimum Kira Tavanı", Value = input.MaksimumKira.Value.ToString("N2", tr) + " TL" });

        var kuralAciklama = kural switch
        {
            HasilatKiraKurali.MinimumGuvence => "Minimum güvence devreye girdi (ciro bazlı kira tabanın altında kaldı).",
            HasilatKiraKurali.MaksimumTavan => "Maksimum tavan devreye girdi (ciro bazlı kira tavanı aştı).",
            _ => "Ciro bazlı kira uygulandı (sınırlar devreye girmedi)."
        };
        result.Rows.Add(new CalculationResultRow { Key = "Uygulanan Kural", Value = kuralAciklama, IsHighlighted = true });

        result.TotalAmount = odenecek;
        result.TotalLabel = $"Ödenecek Kira ({donem})";
        result.Unit = "TL";

        result.Note = "<strong>Yöntem:</strong> Hâsılat kira = ciro × hâsılat oranı; varsa minimum güvence (taban) ve maksimum tavan ile sınırlanır. " +
                      "<strong>Mevzuat:</strong> TBK genel hükümler + ticari kira pratikleri (AVM kira modeli). " +
                      "<strong>Önemli:</strong> Bu hesap sözleşmede belirtilen oran ve sınırlara göre dönem kirasını verir. Damga vergisi, KDV ve sözleşme şartları ayrıca değerlendirilir. Bu sonuç sözleşme detaylarının yerine geçmez.";

        return Task.FromResult(result);
    }
}
