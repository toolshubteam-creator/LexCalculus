using System.Globalization;
using LexCalculus.Core.Calculators.Common;
using LexCalculus.Core.Models.Calculators;
using LexCalculus.Core.Services;

namespace LexCalculus.Core.Calculators.AileMiras;

/// <summary>
/// Tenkis (abatement) calculator — TMK m.506 (saklı pay) + m.560-571.
///
/// Method:
///   1. Tenkise esas matrah (TEM) = ölüm anı net malvarlığı + sağlar arası
///      bağışlar (geri eklenir, TMK m.565).
///   2. Yasal pay dağılımı (IInheritanceDistributionService) → saklı paylı her
///      mirasçının saklı payı = yasal pay kesri × saklı pay oranı × TEM.
///   3. Tasarruf nisabı = TEM − toplam saklı pay.
///   4. Toplam tasarruf (vasiyet + bağışlar) tasarruf nisabını aşıyorsa ihlal var.
///   5. Tenkis sırası (TMK m.561): önce ölüme bağlı tasarruf (vasiyet), yetmezse
///      sağlar arası bağışlar EN YENİSİNDEN geriye doğru; aynı tarihli bağışlar
///      orantılı tenkis edilir.
/// </summary>
public sealed class TenkisCalculator : ICalculator<TenkisInput, TenkisResult>
{
    private readonly IInheritanceDistributionService _dagitim;

    public TenkisCalculator(IInheritanceDistributionService dagitim)
    {
        _dagitim = dagitim ?? throw new ArgumentNullException(nameof(dagitim));
    }

    public CalculatorMetadata Metadata { get; } = new()
    {
        Slug = "tenkis",
        Category = CalculatorCategory.AileMiras,
        Title = "Tenkis Hesaplama",
        ShortDescription = "TMK m.506/560-571 — saklı pay ihlali ve tenkis; sağlar arası bağışlar geri eklenir (m.565), tenkis sırası önce vasiyet sonra son bağıştan geriye (m.561).",
        LegalReference = "TMK m.560-571",
        Status = CalculatorStatus.Active,
        DisplayNumber = "26",
        Keywords = new[] { "tenkis", "saklı pay", "TMK 560", "vasiyet", "bağış", "tasarruf nisabı" }
    };

    public Task<TenkisResult> CalculateAsync(TenkisInput input, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        var result = new TenkisResult();

        if (input.ToplamMalvarligi is null or < 0)
            result.ValidationErrors[nameof(input.ToplamMalvarligi)] = "Malvarlığı negatif olamaz.";
        if (input.VasiyetnameTutari is < 0)
            result.ValidationErrors[nameof(input.VasiyetnameTutari)] = "Vasiyet tutarı negatif olamaz.";
        foreach (var (b, i) in input.Bagislar.Select((b, i) => (b, i)))
        {
            if (b.Tutar is > 0 && b.Tarih is null)
                result.ValidationErrors[$"{nameof(input.Bagislar)}[{i}].{nameof(BagisGirdi.Tarih)}"] =
                    $"{i + 1}. bağışın tarihi girilmelidir (tenkis sırası için).";
        }

        var yapi = input.Yapi.ToYapi();
        var dagilim = _dagitim.Dagit(yapi, null);
        if (dagilim.Paylar.Count == 0)
            result.ValidationErrors[nameof(input.Yapi)] =
                "Hiç yasal mirasçı girilmedi. Saklı pay/tenkis için en az bir saklı paylı mirasçı gerekir.";

        if (result.ValidationErrors.Count > 0)
        {
            result.IsValid = false;
            return Task.FromResult(result);
        }

        var tr = new CultureInfo("tr-TR");

        var gecerliBagislar = input.Bagislar.Where(b => b.Tutar is > 0).ToList();
        var bagisToplam = gecerliBagislar.Sum(b => b.Tutar!.Value);
        var vasiyet = input.VasiyetnameTutari ?? 0m;
        var tem = input.ToplamMalvarligi!.Value + bagisToplam;
        result.TenkiseEsasMatrah = tem;

        // Saklı paylar
        decimal toplamSakliPay = 0m;
        foreach (var p in dagilim.Paylar)
        {
            var oran = _dagitim.SakliPayOrani(p.MirasciTuru, dagilim.AktifDerece);
            if (oran <= 0m) continue;

            var tutar = Math.Round(p.PayKesri * oran * tem, 2, MidpointRounding.AwayFromZero);
            toplamSakliPay += tutar;
            result.SakliPaylar.Add(new SakliPaySatiri
            {
                Tanim = p.Tanim,
                MirasciTuru = p.MirasciTuru,
                YasalPayKesri = p.PayKesri,
                SakliPayOrani = oran,
                SakliPayTutari = tutar
            });
        }

        result.ToplamSakliPay = toplamSakliPay;
        result.TasarrufNisabi = tem - toplamSakliPay;
        result.ToplamTasarruf = vasiyet + bagisToplam;
        var ihlal = Math.Round(result.ToplamTasarruf - result.TasarrufNisabi, 2, MidpointRounding.AwayFromZero);
        result.SakliPayIhlali = ihlal > 0m;
        result.IhlalTutari = result.SakliPayIhlali ? ihlal : 0m;

        // Tenkis sırası (TMK m.561): önce vasiyet, sonra son bağıştan geriye.
        var kalanIhlal = result.IhlalTutari;

        if (vasiyet > 0m)
        {
            var vasiyetTenkis = Math.Min(kalanIhlal, vasiyet);
            kalanIhlal -= vasiyetTenkis;
            result.TenkisKalemleri.Add(new TenkisKalemi
            {
                Tur = "vasiyet",
                Tanim = "Vasiyetname",
                OrijinalTutar = vasiyet,
                TenkisTutari = vasiyetTenkis
            });
        }

        // Bağışlar: en yeni tarihten geriye; aynı tarih grubunda orantılı.
        var gruplar = gecerliBagislar
            .GroupBy(b => b.Tarih!.Value)
            .OrderByDescending(g => g.Key);

        foreach (var grup in gruplar)
        {
            var grupToplam = grup.Sum(b => b.Tutar!.Value);
            var grupTenkis = Math.Min(kalanIhlal, grupToplam);
            kalanIhlal -= grupTenkis;

            foreach (var b in grup)
            {
                var bTenkis = grupToplam > 0m
                    ? Math.Round(grupTenkis * (b.Tutar!.Value / grupToplam), 2, MidpointRounding.AwayFromZero)
                    : 0m;
                var alici = string.IsNullOrWhiteSpace(b.AliciTanim) ? "bağış" : b.AliciTanim!;
                result.TenkisKalemleri.Add(new TenkisKalemi
                {
                    Tur = "bagis",
                    Tanim = $"{alici} ({b.Tarih!.Value:dd.MM.yyyy})",
                    OrijinalTutar = b.Tutar!.Value,
                    TenkisTutari = bTenkis
                });
            }
        }

        // Sonuç satırları
        foreach (var s in result.SakliPaylar)
            result.Rows.Add(new CalculationResultRow
            {
                Key = $"Saklı pay — {s.Tanim}",
                Value = $"{s.SakliPayTutari.ToString("N2", tr)} TL (yasal pay %{(s.YasalPayKesri * 100m).ToString("0.##", tr)} × saklı %{(s.SakliPayOrani * 100m).ToString("0.##", tr)})"
            });

        result.Rows.Add(new CalculationResultRow { Key = "Tenkise Esas Matrah (malvarlığı + bağışlar)", Value = tem.ToString("N2", tr) + " TL" });
        result.Rows.Add(new CalculationResultRow { Key = "Toplam Saklı Pay", Value = toplamSakliPay.ToString("N2", tr) + " TL" });
        result.Rows.Add(new CalculationResultRow { Key = "Tasarruf Nisabı", Value = result.TasarrufNisabi.ToString("N2", tr) + " TL" });
        result.Rows.Add(new CalculationResultRow { Key = "Toplam Tasarruf (vasiyet + bağış)", Value = result.ToplamTasarruf.ToString("N2", tr) + " TL" });

        if (result.SakliPayIhlali)
        {
            result.Rows.Add(new CalculationResultRow { Key = "Saklı Pay İhlali (tenkis edilecek)", Value = result.IhlalTutari.ToString("N2", tr) + " TL", IsHighlighted = true });
            foreach (var k in result.TenkisKalemleri.Where(k => k.TenkisTutari > 0m))
                result.Rows.Add(new CalculationResultRow
                {
                    Key = $"Tenkis — {k.Tanim}",
                    Value = $"{k.TenkisTutari.ToString("N2", tr)} TL (kalan {k.KalanTutar.ToString("N2", tr)} TL)"
                });
        }
        else
        {
            result.Rows.Add(new CalculationResultRow { Key = "Sonuç", Value = "Saklı pay ihlali yok; tenkis gerekmez.", IsHighlighted = true });
        }

        result.TotalAmount = result.IhlalTutari;
        result.TotalLabel = "Tenkis Edilecek Tutar";
        result.Unit = "TL";

        result.Note = "<strong>Yöntem:</strong> Tenkise esas matrah = ölüm anı malvarlığı + sağlar arası bağışlar (TMK m.565). " +
                      "Saklı pay = yasal pay × saklı pay oranı (TMK m.506). Tasarruf nisabını aşan kısım tenkise tabidir; " +
                      "sıra önce vasiyet, sonra son bağıştan geriye (TMK m.561). " +
                      "<strong>Önemli:</strong> Mirastan ıskat, mirastan yoksunluk, bağışın geri istenmesi (TMK m.567) gibi durumlar ayrıca değerlendirilir. Bu sonuç mahkeme kararı yerine geçmez.";

        return Task.FromResult(result);
    }
}
