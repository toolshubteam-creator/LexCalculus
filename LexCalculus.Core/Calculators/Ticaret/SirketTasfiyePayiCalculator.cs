using System.Globalization;
using LexCalculus.Core.Calculators.Common;

namespace LexCalculus.Core.Calculators.Ticaret;

/// <summary>
/// H1 Şirket Tasfiye Payı — TTK m.543 (AŞ) ve m.642 (Ltd). Net tasfiye
/// varlığı (varlık - borç) imtiyazlı pay sahiplerine esas sözleşmeye göre
/// ayrılır; kalan kısım standart ortaklara eşit dağıtılır.
///
/// Basit model: her imtiyazlı pay sahibi için <c>ImtiyazOraniYuzde</c> net
/// varlığın yüzdesi olarak doğrudan uygulanır (esas sözleşme tanımı). Toplam
/// imtiyazlı oran %100'ü aşmamalıdır. İmtiyazlı blok sonrası kalan, standart
/// ortak sayısına eşit bölünür.
///
/// Saf hesap — DB bağımlılığı yok, parametresiz.
/// </summary>
public sealed class SirketTasfiyePayiCalculator : ICalculator<SirketTasfiyePayiInput, SirketTasfiyePayiResult>
{
    public CalculatorMetadata Metadata { get; } = new()
    {
        Slug = "sirket-tasfiye-payi",
        Category = CalculatorCategory.Ticaret,
        Title = "Şirket Tasfiye Payı",
        ShortDescription = "TTK m.543 (AŞ) ve m.642 (Ltd) — net tasfiye varlığı (varlık − borç) üzerinden imtiyazlı pay sahiplerine esas sözleşme oranları, kalan standart ortaklara eşit dağılım.",
        LegalReference = "TTK m.543 / m.642",
        Status = CalculatorStatus.Active,
        DisplayNumber = "37",
        Keywords = new[] { "tasfiye payı", "TTK 543", "TTK 642", "anonim şirket tasfiyesi", "imtiyazlı pay", "tasfiye memuru" }
    };

    public Task<SirketTasfiyePayiResult> CalculateAsync(SirketTasfiyePayiInput input, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        var result = new SirketTasfiyePayiResult();

        if (input.ToplamVarlik is null or < 0)
            result.ValidationErrors[nameof(input.ToplamVarlik)] = "Toplam varlık negatif olamaz.";
        if (input.ToplamBorc is null or < 0)
            result.ValidationErrors[nameof(input.ToplamBorc)] = "Toplam borç negatif olamaz.";
        if (input.StandartOrtakSayisi < 0)
            result.ValidationErrors[nameof(input.StandartOrtakSayisi)] = "Standart ortak sayısı negatif olamaz.";

        var imtiyazliPaylar = input.ImtiyazliPaylar?.Where(p => p.ImtiyazOraniYuzde > 0).ToList()
            ?? new List<ImtiyazliPayGirdi>();
        var toplamImtiyazYuzde = imtiyazliPaylar.Sum(p => p.ImtiyazOraniYuzde);
        if (toplamImtiyazYuzde > 100m)
            result.ValidationErrors[nameof(input.ImtiyazliPaylar)] =
                $"Toplam imtiyaz oranı %100'ü aşamaz (mevcut: %{toplamImtiyazYuzde.ToString("0.##", new CultureInfo("tr-TR"))}).";

        if (input.StandartOrtakSayisi == 0 && toplamImtiyazYuzde < 100m && imtiyazliPaylar.Count == 0)
            result.ValidationErrors[nameof(input.StandartOrtakSayisi)] =
                "En az bir standart ortak veya imtiyazlı pay sahibi belirtilmelidir.";

        if (result.ValidationErrors.Count > 0)
        {
            result.IsValid = false;
            return Task.FromResult(result);
        }

        var net = input.ToplamVarlik!.Value - input.ToplamBorc!.Value;
        result.NetTasfiyeVarliği = net;

        var tr = new CultureInfo("tr-TR");

        if (net <= 0m)
        {
            result.Rows.Add(new() { Key = "Toplam Varlık", Value = input.ToplamVarlik.Value.ToString("N2", tr) + " TL" });
            result.Rows.Add(new() { Key = "Toplam Borç", Value = input.ToplamBorc.Value.ToString("N2", tr) + " TL" });
            result.Rows.Add(new() { Key = "Net Tasfiye Varlığı", Value = net.ToString("N2", tr) + " TL", IsHighlighted = true });
            result.Warnings.Add("Net tasfiye varlığı sıfır veya negatif olduğundan ortaklara dağıtılacak pay bulunmamaktadır. Önce alacaklı talepleri karşılanmalıdır.");

            result.TotalAmount = 0m;
            result.TotalLabel = "Dağıtılacak Pay (Yok)";
            result.Unit = "TL";
            result.Note = SonucNote();
            return Task.FromResult(result);
        }

        // İmtiyazlı blok dağılımı.
        var imtiyazDetay = new List<ImtiyazliPayDagitimSatiri>();
        decimal imtiyazliBlok = 0m;

        var sira = 0;
        foreach (var p in imtiyazliPaylar)
        {
            sira++;
            var tutar = Math.Round(net * (p.ImtiyazOraniYuzde / 100m), 2, MidpointRounding.AwayFromZero);
            imtiyazliBlok += tutar;
            imtiyazDetay.Add(new ImtiyazliPayDagitimSatiri
            {
                OrtakAdi = string.IsNullOrWhiteSpace(p.OrtakAdi) ? $"İmtiyazlı Ortak {sira}" : p.OrtakAdi!,
                PayAdedi = p.PayAdedi,
                ImtiyazOraniYuzde = p.ImtiyazOraniYuzde,
                AlacagiTutar = tutar
            });
        }

        var kalan = net - imtiyazliBlok;
        var standartKisiBasi = input.StandartOrtakSayisi > 0
            ? Math.Round(kalan / input.StandartOrtakSayisi, 2, MidpointRounding.AwayFromZero)
            : 0m;

        var toplamDagitim = imtiyazliBlok + (standartKisiBasi * input.StandartOrtakSayisi);

        result.ImtiyazliPayDagilimi = imtiyazDetay;
        result.ImtiyazliBlokToplam = imtiyazliBlok;
        result.StandartPayKisiBasi = standartKisiBasi;
        result.ToplamPayDagitimi = toplamDagitim;

        result.Rows.Add(new() { Key = "Toplam Varlık", Value = input.ToplamVarlik.Value.ToString("N2", tr) + " TL" });
        result.Rows.Add(new() { Key = "Toplam Borç", Value = "-" + input.ToplamBorc.Value.ToString("N2", tr) + " TL" });
        result.Rows.Add(new() { Key = "Net Tasfiye Varlığı", Value = net.ToString("N2", tr) + " TL", IsHighlighted = true });

        foreach (var d in imtiyazDetay)
            result.Rows.Add(new()
            {
                Key = $"İmtiyazlı: {d.OrtakAdi} (%{d.ImtiyazOraniYuzde.ToString("0.##", tr)})",
                Value = d.AlacagiTutar.ToString("N2", tr) + " TL"
            });

        if (imtiyazDetay.Count > 0)
            result.Rows.Add(new() { Key = "İmtiyazlı Blok Toplamı", Value = imtiyazliBlok.ToString("N2", tr) + " TL" });

        if (input.StandartOrtakSayisi > 0)
        {
            result.Rows.Add(new() { Key = $"Standart Ortak Sayısı (× {input.StandartOrtakSayisi})", Value = kalan.ToString("N2", tr) + " TL toplam" });
            result.Rows.Add(new() { Key = "Standart Pay (kişi başı)", Value = standartKisiBasi.ToString("N2", tr) + " TL", IsHighlighted = true });
        }

        result.TotalAmount = toplamDagitim;
        result.TotalLabel = "Toplam Dağıtım";
        result.Unit = "TL";
        result.Note = SonucNote();
        return Task.FromResult(result);
    }

    private static string SonucNote() =>
        "<strong>Yöntem:</strong> TTK m.543/642 — Tasfiye varlığından borçlar düşülür; varsa imtiyazlı pay sahiplerine " +
        "esas sözleşmedeki oranlar uygulanır, kalan standart ortaklara eşit dağıtılır. " +
        "<strong>Önemli:</strong> Tasfiye memuru raporu, alacaklı önceliği, vergi kesintileri ve son bilanço onayı " +
        "ayrıca değerlendirilir. İmtiyazlı pay sahipleri esas sözleşmeye göre belirlenir. " +
        "<strong>Bu sonuç tasfiye memuru raporu yerine geçmez.</strong>";
}
