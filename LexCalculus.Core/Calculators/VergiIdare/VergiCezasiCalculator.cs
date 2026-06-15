using System.Globalization;
using LexCalculus.Core.Calculators.Common;
using LexCalculus.Core.Interfaces;

namespace LexCalculus.Core.Calculators.VergiIdare;

/// <summary>
/// G5 Vergi Cezası ve Gecikme Faizi — 213 s.K. m.341-376 + m.112 (faiz) /
/// 6183 s.K. m.51 (zam).
///
/// CEZA:
///   VergiZiyai (m.341) → asıl × %50
///   Kacakcilik (m.359) → asıl × %100
///   Usulsuzluk (m.352) → maktu (kullanıcı girer)
///
/// GECİKME FAİZİ (m.112):
///   Vade'den ödeme tarihine kadar her ay için basit faiz: asıl × aylık oran ×
///   ay sayısı. Yıl geçişinde oran değişebileceği için her takvim yılı ayrı
///   dönem olarak hesaplanır (her dönem o yılın aylık oranı × o yıldaki ay
///   sayısı). Kümülatif/birleşik DEĞİL — basit faiz.
///
/// AY SAYISI:
///   Tam ay = vade ayından ödeme ayına kadar sayılan tam aylar. Vade ile
///   ödeme aynı ay olursa 0 ay (faiz yok). 213 s.K. m.112/1 "ay" cinsinden
///   ölçer; gün artıkları yuvarlanır (yukarı, lehine değil aleyhine — kanun
///   "kesir ay tama iblağ" der; biz inflation defense için pratik kural:
///   ay başı sayım).
///
/// Aylık oran zaman-versiyonlu FormulaParameter:
///   GecikmeFaizi → "*/gecikme-faizi.aylik-oran"
///   GecikmeZammi → "*/gecikme-zammi.aylik-oran"
/// </summary>
public sealed class VergiCezasiCalculator : ICalculator<VergiCezasiInput, VergiCezasiResult>
{
    private readonly IFormulaParameterService _params;

    public VergiCezasiCalculator(IFormulaParameterService parameters)
    {
        _params = parameters ?? throw new ArgumentNullException(nameof(parameters));
    }

    public CalculatorMetadata Metadata { get; } = new()
    {
        Slug = "vergi-cezasi",
        Category = CalculatorCategory.VergiIdare,
        Title = "Vergi Cezası ve Gecikme Faizi",
        ShortDescription = "213 s.K. m.341-376 + m.112 — vergi ziyaı (%50) / kaçakçılık (%100) / usulsüzlük (maktu) cezası ve vade-ödeme arası dönem bazlı gecikme faizi hesabı.",
        LegalReference = "213 s.K. m.341-376",
        Status = CalculatorStatus.Active,
        DisplayNumber = "36",
        Keywords = new[] { "vergi cezası", "213 sayılı kanun", "vergi ziyaı", "kaçakçılık", "gecikme faizi", "gecikme zammı", "usulsüzlük" }
    };

    public async Task<VergiCezasiResult> CalculateAsync(VergiCezasiInput input, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        var result = new VergiCezasiResult();

        if (input.AsilVergi is null or <= 0)
            result.ValidationErrors[nameof(input.AsilVergi)] = "Asıl vergi pozitif olmalıdır.";
        if (input.VadeTarihi is null)
            result.ValidationErrors[nameof(input.VadeTarihi)] = "Vade tarihi boş olamaz.";
        if (input.OdemeTarihi is null)
            result.ValidationErrors[nameof(input.OdemeTarihi)] = "Ödeme tarihi boş olamaz.";

        if (result.ValidationErrors.Count > 0)
        {
            result.IsValid = false;
            return result;
        }

        var vade = DateOnly.FromDateTime(input.VadeTarihi!.Value);
        var odeme = DateOnly.FromDateTime(input.OdemeTarihi!.Value);

        if (odeme < vade)
        {
            result.ValidationErrors[nameof(input.OdemeTarihi)] =
                "Ödeme tarihi vade tarihinden önce olamaz.";
            result.IsValid = false;
            return result;
        }

        if (input.CezaTuru == VergiCezaTuru.Usulsuzluk && input.UsulsuzlukTutari is null or < 0)
        {
            result.ValidationErrors[nameof(input.UsulsuzlukTutari)] =
                "Usulsüzlük türünde maktu ceza tutarı girilmelidir.";
            result.IsValid = false;
            return result;
        }

        var asil = input.AsilVergi!.Value;
        var ceza = input.CezaTuru switch
        {
            VergiCezaTuru.VergiZiyai => Math.Round(asil * 0.50m, 2, MidpointRounding.AwayFromZero),
            VergiCezaTuru.Kacakcilik => Math.Round(asil * 1.00m, 2, MidpointRounding.AwayFromZero),
            VergiCezaTuru.Usulsuzluk => Math.Round(input.UsulsuzlukTutari!.Value, 2, MidpointRounding.AwayFromZero),
            _ => 0m
        };

        var toplamAy = (odeme.Year - vade.Year) * 12 + (odeme.Month - vade.Month);
        if (toplamAy < 0) toplamAy = 0;

        var faizKey = input.FaizTuru == FaizTuru.GecikmeZammi
            ? "gecikme-zammi.aylik-oran"
            : "gecikme-faizi.aylik-oran";

        var donemler = new List<GecikmeDonemi>();
        decimal toplamFaiz = 0m;

        if (toplamAy > 0)
        {
            // Her takvim yılı ayrı dönem: o yıldaki ay sayısı × o yılın aylık oranı.
            var ilkAy = vade.AddMonths(0); // başlangıç ay
            var sonAy = odeme; // bitiş
            var imlec = ilkAy;
            var kalan = toplamAy;

            while (kalan > 0)
            {
                var yilSonu = new DateOnly(imlec.Year, 12, 1);
                var yilKalanAy = (yilSonu.Year - imlec.Year) * 12 + (yilSonu.Month - imlec.Month) + 1; // mevcut ay dahil yıl sonuna kadar
                var donemAy = Math.Min(kalan, yilKalanAy);

                var aylikOran = await GetParamAsync(faizKey, new DateTime(imlec.Year, 1, 1), cancellationToken);
                var donemFaiz = Math.Round(asil * aylikOran * donemAy, 2, MidpointRounding.AwayFromZero);

                donemler.Add(new GecikmeDonemi
                {
                    Yil = imlec.Year,
                    AySayisi = donemAy,
                    AylikOran = aylikOran,
                    DonemFaizi = donemFaiz
                });

                toplamFaiz += donemFaiz;
                imlec = imlec.AddMonths(donemAy);
                kalan -= donemAy;
            }
        }

        toplamFaiz = Math.Round(toplamFaiz, 2, MidpointRounding.AwayFromZero);
        var toplamOdenecek = asil + ceza + toplamFaiz;

        result.AsilVergi = asil;
        result.CezaTutari = ceza;
        result.GecikmeFaiziDonemleri = donemler;
        result.ToplamGecikmeFaizi = toplamFaiz;
        result.ToplamOdenecekTutar = toplamOdenecek;
        result.ToplamAySayisi = toplamAy;

        var tr = new CultureInfo("tr-TR");

        result.Rows.Add(new() { Key = "Asıl Vergi", Value = asil.ToString("N2", tr) + " TL" });
        result.Rows.Add(new() { Key = "Ceza Türü", Value = CezaAdi(input.CezaTuru) });
        result.Rows.Add(new() { Key = "Ceza Tutarı", Value = ceza.ToString("N2", tr) + " TL" });
        result.Rows.Add(new() { Key = "Vade Tarihi", Value = vade.ToString("dd MMMM yyyy", tr) });
        result.Rows.Add(new() { Key = "Ödeme Tarihi", Value = odeme.ToString("dd MMMM yyyy", tr) });
        result.Rows.Add(new() { Key = "Toplam Gecikme (ay)", Value = $"{toplamAy} ay" });

        if (toplamAy == 0)
        {
            result.Rows.Add(new() { Key = "Gecikme Faizi", Value = "0,00 TL (vade ve ödeme aynı ayda)" });
        }
        else
        {
            foreach (var d in donemler)
                result.Rows.Add(new()
                {
                    Key = $"Gecikme Faizi {d.Yil} ({d.AySayisi} ay × %{(d.AylikOran * 100m).ToString("0.##", tr)})",
                    Value = d.DonemFaizi.ToString("N2", tr) + " TL"
                });
            result.Rows.Add(new() { Key = "Toplam Gecikme Faizi", Value = toplamFaiz.ToString("N2", tr) + " TL" });
        }

        result.Rows.Add(new() { Key = "Toplam Ödenecek Tutar", Value = toplamOdenecek.ToString("N2", tr) + " TL", IsHighlighted = true });

        result.TotalAmount = toplamOdenecek;
        result.TotalLabel = "Toplam Ödenecek";
        result.Unit = "TL";
        result.Note = SonucNote();
        return result;
    }

    private async Task<decimal> GetParamAsync(string key, DateTime asOf, CancellationToken ct)
    {
        var value = await _params.GetValueAsync("*", key, asOf, ct);
        return value ?? throw new InvalidOperationException(
            $"Gecikme faiz parametresi yok: (*, {key}, {asOf:yyyy-MM-dd}). " +
            $"Yönetici '*/{key}' parametresini eklemelidir.");
    }

    private static string CezaAdi(VergiCezaTuru t) => t switch
    {
        VergiCezaTuru.VergiZiyai => "Vergi Ziyaı (m.341 — %50)",
        VergiCezaTuru.Kacakcilik => "Kaçakçılık (m.359 — %100)",
        VergiCezaTuru.Usulsuzluk => "Usulsüzlük (m.352 — maktu)",
        _ => t.ToString()
    };

    private static string SonucNote() =>
        "<strong>Yöntem:</strong> 213 s.K. m.341-376 — Asıl vergi üzerinden ceza (vergi ziyaı %50, kaçakçılık " +
        "%100, usulsüzlük maktu); m.112 (veya 6183 s.K. m.51) uyarınca vade-ödeme arası her ay için basit gecikme " +
        "faizi/zammı (kümülatif değil). Yıl geçişinde aylık oran o yıla göre uygulanır. " +
        "<strong>Önemli:</strong> Pişmanlık (m.371), uzlaşma (m.376), ödeme planı ve indirim hakları ayrıca " +
        "değerlendirilir. Aylık oranlar TCMB/Maliye Bakanlığı referansına bağlıdır; fiili borç bakiyesi vergi " +
        "dairesinden teyit alınmalıdır. <strong>Bu sonuç vergi dairesi takip tutarı yerine geçmez.</strong>";
}
