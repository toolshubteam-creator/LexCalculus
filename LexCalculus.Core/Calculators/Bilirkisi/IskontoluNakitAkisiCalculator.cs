using System.Globalization;
using LexCalculus.Core.Calculators.Common;
using LexCalculus.Core.Interfaces;

namespace LexCalculus.Core.Calculators.Bilirkisi;

/// <summary>
/// I2 İskontolu Nakit Akışı — sonlu süreli yıllık gelirin bugünkü değeri
/// (annuity-certain present value). Yargıtay HGK içtihatlarına uygun anüite
/// hesabı; bilirkişi rapor şablonlarında tazminat alternatif değer
/// projeksiyonlarında kullanılır.
///
/// Formula (Faz 2 <see cref="IActuarialService.AnnuityPresentValue"/>):
///   PV = G × [(1 - (1 + r)^(-N)) / r]   r &gt; 0
///   PV = G × N                          r == 0  (faizsiz toplam)
///
/// Saf hesap — DB bağımlılığı yok, parametresiz; IActuarialService inject.
/// </summary>
public sealed class IskontoluNakitAkisiCalculator : ICalculator<IskontoluNakitAkisiInput, IskontoluNakitAkisiResult>
{
    private readonly IActuarialService _actuary;

    public IskontoluNakitAkisiCalculator(IActuarialService actuary)
    {
        _actuary = actuary ?? throw new ArgumentNullException(nameof(actuary));
    }

    public CalculatorMetadata Metadata { get; } = new()
    {
        Slug = "iskontolu-nakit-akisi",
        Category = CalculatorCategory.Bilirkisi,
        Title = "İskontolu Nakit Akışı (PV)",
        ShortDescription = "Yargıtay HGK içtihatlarına uygun anüite formülü — yıllık net gelir, iskonto oranı ve yıl sayısı ile bugünkü değer hesabı (PV).",
        LegalReference = "Yargıtay HGK içtihatları",
        Status = CalculatorStatus.Active,
        DisplayNumber = "41",
        Keywords = new[] { "iskontolu nakit", "PV", "present value", "anüite", "bugünkü değer", "tazminat anüite" }
    };

    public Task<IskontoluNakitAkisiResult> CalculateAsync(IskontoluNakitAkisiInput input, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        var result = new IskontoluNakitAkisiResult();

        if (input.YillikNetGelir is null or <= 0)
            result.ValidationErrors[nameof(input.YillikNetGelir)] = "Yıllık net gelir pozitif olmalıdır.";
        if (input.IskontoOraniYuzde is null or < 0)
            result.ValidationErrors[nameof(input.IskontoOraniYuzde)] = "İskonto oranı negatif olamaz.";
        if (input.YilSayisi is null or <= 0)
            result.ValidationErrors[nameof(input.YilSayisi)] = "Yıl sayısı pozitif olmalıdır.";

        if (result.ValidationErrors.Count > 0)
        {
            result.IsValid = false;
            return Task.FromResult(result);
        }

        var gelir = input.YillikNetGelir!.Value;
        var oranYuzde = input.IskontoOraniYuzde!.Value;
        var n = input.YilSayisi!.Value;
        var oran = oranYuzde / 100m;

        var pv = _actuary.AnnuityPresentValue(gelir, n, oran);

        result.YillikNetGelir = gelir;
        result.IskontoOraniYuzde = oranYuzde;
        result.YilSayisi = n;
        result.BugunkuDeger = pv;

        var tr = new CultureInfo("tr-TR");

        result.Rows.Add(new() { Key = "Yıllık Net Gelir (G)", Value = gelir.ToString("N2", tr) + " TL" });
        result.Rows.Add(new() { Key = "İskonto Oranı (r)", Value = $"%{oranYuzde.ToString("0.##", tr)} (yıllık)" });
        result.Rows.Add(new() { Key = "Yıl Sayısı (N)", Value = $"{n} yıl" });
        result.Rows.Add(new()
        {
            Key = "Formül",
            Value = oran == 0m
                ? "PV = G × N (sıfır iskonto)"
                : "PV = G × [(1 − (1 + r)^(−N)) / r]"
        });
        result.Rows.Add(new() { Key = "Bugünkü Değer (PV)", Value = pv.ToString("N2", tr) + " TL", IsHighlighted = true });

        if (oran == 0m)
            result.Warnings.Add("İskonto oranı sıfır girildi; bugünkü değer toplam nominal gelirdir (G × N).");

        result.TotalAmount = pv;
        result.TotalLabel = "Bugünkü Değer (PV)";
        result.Unit = "TL";
        result.Note = SonucNote();
        return Task.FromResult(result);
    }

    private static string SonucNote() =>
        "<strong>Yöntem:</strong> Anüite-certain bugünkü değer formülü — sonlu süreli yıllık gelirin tek seferlik " +
        "lump-sum karşılığı. Yargıtay HGK içtihatlarına uygun. <strong>Önemli:</strong> İskonto oranı seçimi bilirkişi " +
        "takdirine bağlıdır (genelde %5-10 bandı). Enflasyon, ücret artışı, vergisel etkiler gibi reel değişkenler " +
        "ayrıca değerlendirilir. <strong>Bu sonuç bilirkişi raporu yerine geçmez.</strong>";
}
