using System.Globalization;
using LexCalculus.Core.Calculators.Common;
using LexCalculus.Core.Services;

namespace LexCalculus.Core.Calculators.VergiIdare;

/// <summary>
/// G1 Veraset ve İntikal Vergisi — 7338 s.K. m.4 (istisnalar) + m.16 (oranlar).
/// 2026 tarifesi (RG 31.12.2025/33124 5.Mük., 57 Seri No'lu Tebliğ):
///   Veraset           istisnası: füruğ/eş kişi başı 2.907.136 TL
///                                yalnız eş, füruğ yok 5.817.845 TL
///   İvazsız intikal   istisnası: 66.935 TL
/// Dilim setleri ITaxBracketService üzerinden sorgulanır
/// ("veraset-vergisi/veraset" veya "veraset-vergisi/ivazsiz").
///
/// İstisna tutarları 2026 değerleri olarak SABİT kodlanmıştır (yıllık olarak
/// Resmi Gazete tebliği ile güncellenir, ilk yılda parametre konvansiyonuna
/// taşıma maliyeti düşüktür — bkz. tech-debt #50).
/// </summary>
public sealed class VerasetVergisiCalculator : ICalculator<VerasetVergisiInput, VerasetVergisiResult>
{
    // 2026 istisna tutarları (57 Seri No'lu Tebliğ).
    private const decimal IstisnaFurugVeEs = 2_907_136m;
    private const decimal IstisnaSadeceEs = 5_817_845m;
    private const decimal IstisnaIvazsiz = 66_935m;

    private readonly ITaxBracketService _brackets;

    public VerasetVergisiCalculator(ITaxBracketService brackets)
    {
        _brackets = brackets ?? throw new ArgumentNullException(nameof(brackets));
    }

    public CalculatorMetadata Metadata { get; } = new()
    {
        Slug = "veraset-vergisi",
        Category = CalculatorCategory.VergiIdare,
        Title = "Veraset ve İntikal Vergisi",
        ShortDescription = "7338 s.K. m.4 + m.16 — veraset veya ivazsız intikalde 2026 tarifesi ve istisnalarına göre dilim bazlı vergi hesabı (RG 31.12.2025/33124 5.Mük., 57 Seri No'lu Tebliğ).",
        LegalReference = "7338 s.K. m.4, m.16",
        Status = CalculatorStatus.Active,
        DisplayNumber = "32",
        Keywords = new[] { "veraset vergisi", "intikal vergisi", "7338 sayılı kanun", "miras vergisi", "bağış vergisi", "ivazsız intikal" }
    };

    public async Task<VerasetVergisiResult> CalculateAsync(VerasetVergisiInput input, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        var result = new VerasetVergisiResult();

        if (input.BrutDeger is null or <= 0)
            result.ValidationErrors[nameof(input.BrutDeger)] = "Brüt intikal değeri pozitif olmalıdır.";
        if (input.MirascıSayisi <= 0)
            result.ValidationErrors[nameof(input.MirascıSayisi)] = "Mirasçı sayısı pozitif olmalıdır.";

        if (result.ValidationErrors.Count > 0)
        {
            result.IsValid = false;
            return result;
        }

        var asOf = input.AsOfDate ?? DateTime.UtcNow.Date;
        var brut = input.BrutDeger!.Value;

        var kisiBasiIstisna = input.IntikalTuru switch
        {
            IntikalTuru.VerasetFurugVeEs => IstisnaFurugVeEs,
            IntikalTuru.VerasetSadeceEs => IstisnaSadeceEs,
            IntikalTuru.Ivazsiz => IstisnaIvazsiz,
            _ => 0m
        };

        var mirascıSayisi = input.IntikalTuru == IntikalTuru.Ivazsiz ? 1 : input.MirascıSayisi;
        var toplamIstisna = kisiBasiIstisna * mirascıSayisi;
        var vergilendirilebilir = Math.Max(0m, brut - toplamIstisna);

        var dilimSlug = input.IntikalTuru == IntikalTuru.Ivazsiz
            ? "veraset-vergisi/ivazsiz"
            : "veraset-vergisi/veraset";

        var hesap = await _brackets.HesaplaAsync(dilimSlug, vergilendirilebilir, asOf, cancellationToken);

        result.IntikalTuru = input.IntikalTuru;
        result.BrutDeger = brut;
        result.KisiBasinaIstisna = kisiBasiIstisna;
        result.ToplamIstisna = toplamIstisna;
        result.VergilendirilebilirTutar = vergilendirilebilir;
        result.DilimDetaylar = hesap.DilimDetaylar;
        result.ToplamVergi = hesap.ToplamVergi;

        var tr = new CultureInfo("tr-TR");

        result.Rows.Add(new() { Key = "İntikal Türü", Value = IntikalTuruAdi(input.IntikalTuru) });
        result.Rows.Add(new() { Key = "Brüt İntikal Değeri", Value = brut.ToString("N2", tr) + " TL" });
        result.Rows.Add(new() { Key = "Kişi Başına İstisna", Value = kisiBasiIstisna.ToString("N2", tr) + " TL" });
        if (mirascıSayisi > 1)
            result.Rows.Add(new() { Key = $"Mirasçı Sayısı (× {mirascıSayisi})", Value = toplamIstisna.ToString("N2", tr) + " TL toplam istisna" });
        result.Rows.Add(new() { Key = "Vergilendirilebilir Tutar", Value = vergilendirilebilir.ToString("N2", tr) + " TL" });

        if (vergilendirilebilir == 0m)
        {
            result.Rows.Add(new() { Key = "Toplam Vergi", Value = "0,00 TL (istisna sınırı aşılmadı)", IsHighlighted = true });
        }
        else
        {
            foreach (var d in hesap.DilimDetaylar)
            {
                var sinir = d.MaxAmount.HasValue ? d.MaxAmount.Value.ToString("N0", tr) : "∞";
                result.Rows.Add(new()
                {
                    Key = $"Dilim {d.Sira} ({d.MinAmount.ToString("N0", tr)}-{sinir} × %{(d.Rate * 100m).ToString("0.##", tr)})",
                    Value = d.DilimVergisi.ToString("N2", tr) + " TL"
                });
            }
            result.Rows.Add(new() { Key = "Toplam Vergi", Value = hesap.ToplamVergi.ToString("N2", tr) + " TL", IsHighlighted = true });
        }

        result.TotalAmount = hesap.ToplamVergi;
        result.TotalLabel = "Toplam Vergi";
        result.Unit = "TL";
        result.Note = SonucNote();
        return result;
    }

    private static string IntikalTuruAdi(IntikalTuru tip) => tip switch
    {
        IntikalTuru.VerasetFurugVeEs => "Veraset (füruğ veya eş varsa, kişi başı istisna)",
        IntikalTuru.VerasetSadeceEs => "Veraset (yalnız eş, füruğ yok)",
        IntikalTuru.Ivazsiz => "İvazsız İntikal / Bağış",
        _ => tip.ToString()
    };

    private static string SonucNote() =>
        "<strong>Yöntem:</strong> Brüt değerden 7338 s.K. m.4 istisnası düşülür (veraset hâlinde kişi başına, ivazsızda sabit), " +
        "kalan tutara 2026 tarifesinden (m.16) marjinal dilim hesabı uygulanır. " +
        "<strong>Önemli:</strong> Akrabalık derecesine göre indirim (m.18), özel istisnalar (engelli, vakıf vb.) ve " +
        "diğer indirimler ayrıca değerlendirilir. Beyan ve ödeme süreleri 7338 s.K. m.19-20'ye tabidir. " +
        "<strong>Bu sonuç vergi dairesi tarafından kesin tarh edilmiş tutar yerine geçmez.</strong>";
}
