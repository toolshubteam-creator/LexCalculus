using System.Globalization;
using LexCalculus.Core.Calculators.Common;

namespace LexCalculus.Core.Calculators.VergiIdare;

/// <summary>
/// G4 KDV İadesi — 3065 s.K. m.32. İadeye konu KDV = max(0, indirim KDV -
/// hesaplanan KDV); mahsup edilen düşülerek net iade tutarı bulunur. Başvuru
/// türü (indirimli oran, ihraç kayıtlı, diğer) raporlama amaçlıdır — formül
/// her durumda aynıdır.
///
/// Saf hesap — DB bağımlılığı yok, parametresiz.
/// </summary>
public sealed class KdvIadesiCalculator : ICalculator<KdvIadesiInput, KdvIadesiResult>
{
    public CalculatorMetadata Metadata { get; } = new()
    {
        Slug = "kdv-iadesi",
        Category = CalculatorCategory.VergiIdare,
        Title = "KDV İadesi",
        ShortDescription = "3065 s.K. m.32 — indirilebilir KDV'nin hesaplanan KDV'yi aşan kısmı iadeye konu; mahsup edilen tutar düşülerek net iade hesabı.",
        LegalReference = "3065 s.K. m.32",
        Status = CalculatorStatus.Active,
        DisplayNumber = "35",
        Keywords = new[] { "kdv iadesi", "3065 sayılı kanun", "indirilebilir kdv", "ihraç kayıtlı", "mahsup", "iade" }
    };

    public Task<KdvIadesiResult> CalculateAsync(KdvIadesiInput input, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        var result = new KdvIadesiResult();

        if (input.ToplamHesaplananKDV is null or < 0)
            result.ValidationErrors[nameof(input.ToplamHesaplananKDV)] = "Hesaplanan KDV negatif olamaz.";
        if (input.ToplamIndirimKDV is null or < 0)
            result.ValidationErrors[nameof(input.ToplamIndirimKDV)] = "İndirim KDV negatif olamaz.";
        if (input.MahsupEdilenKDV is < 0)
            result.ValidationErrors[nameof(input.MahsupEdilenKDV)] = "Mahsup edilen KDV negatif olamaz.";

        if (result.ValidationErrors.Count > 0)
        {
            result.IsValid = false;
            return Task.FromResult(result);
        }

        var hesaplanan = input.ToplamHesaplananKDV!.Value;
        var indirim = input.ToplamIndirimKDV!.Value;
        var mahsup = input.MahsupEdilenKDV ?? 0m;

        var iadeyeKonu = Math.Max(0m, indirim - hesaplanan);
        var iade = Math.Max(0m, iadeyeKonu - mahsup);

        result.IadeyeKonuKDV = iadeyeKonu;
        result.MahsupSonrasiTutar = iade;
        result.IadeTutari = iade;

        var tr = new CultureInfo("tr-TR");

        result.Rows.Add(new() { Key = "Toplam Hesaplanan KDV (Satış)", Value = hesaplanan.ToString("N2", tr) + " TL" });
        result.Rows.Add(new() { Key = "Toplam İndirim KDV (Alış)", Value = indirim.ToString("N2", tr) + " TL" });
        result.Rows.Add(new() { Key = "İadeye Konu KDV", Value = iadeyeKonu.ToString("N2", tr) + " TL" });
        if (mahsup > 0m)
            result.Rows.Add(new() { Key = "Mahsup Edilen", Value = "-" + mahsup.ToString("N2", tr) + " TL" });
        result.Rows.Add(new() { Key = "Net İade Tutarı", Value = iade.ToString("N2", tr) + " TL", IsHighlighted = true });

        if (iadeyeKonu == 0m)
            result.Warnings.Add("İndirim KDV hesaplanan KDV'yi aşmamaktadır; iadeye konu tutar bulunmamaktadır.");
        else if (iade == 0m && mahsup > 0m)
            result.Warnings.Add("İadeye konu tutar mahsup edilen miktar kadar veya daha düşük olduğundan net iade çıkmamıştır.");

        result.TotalAmount = iade;
        result.TotalLabel = "Net İade Tutarı";
        result.Unit = "TL";
        result.Note = SonucNote();
        return Task.FromResult(result);
    }

    private static string SonucNote() =>
        "<strong>Yöntem:</strong> 3065 s.K. m.32 — İadeye konu KDV = max(0, indirim KDV − hesaplanan KDV); " +
        "mahsup edilen tutar düşülerek net iade bulunur. <strong>Önemli:</strong> İadenin gerçekleşmesi mahsup " +
        "öncelikleri (vergi/SGK borçları), dönem KDV beyannamesi tutarlılığı, belge düzeni (faturalar, beyannameler) " +
        "ve YMM raporu/inceleme gibi koşullara bağlıdır. <strong>Bu sonuç vergi dairesi onayı yerine geçmez.</strong>";
}
