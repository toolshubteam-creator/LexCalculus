using System.Globalization;
using LexCalculus.Core.Calculators.Common;
using LexCalculus.Core.Interfaces;
using LexCalculus.Core.Models.Calculators;

namespace LexCalculus.Core.Calculators.Faiz;

/// <summary>
/// Commercial default interest calculator (Ticari Temerrüt Faizi, 3095 s.K. m.2).
///
/// Computes BOTH the statutory (yasal) interest AND the commercial default
/// interest in parallel, presenting both to the user. Per 3095 m.2, the
/// creditor may choose whichever is HIGHER. The calculator highlights the
/// recommended choice.
///
/// Algorithm:
///   - Statutory: simple interest using yasal-faiz rates (3095 m.1)
///   - Commercial: simple interest using TCMB avans rates filtered through
///     the 3095 m.2 6-month period rule + 5 percentage point threshold
///   - No compound interest (3095 m.3)
/// </summary>
public sealed class TicariTemerrutFaizCalculator : ICalculator<TicariTemerrutFaizInput, TicariTemerrutFaizResult>
{
    private const string Slug = "ticari-temerrut-faizi";

    private readonly IInterestRateService _yasalRateService;
    private readonly IThree095CommercialRateService _ticariRateService;

    public TicariTemerrutFaizCalculator(
        IInterestRateService yasalRateService,
        IThree095CommercialRateService ticariRateService)
    {
        _yasalRateService = yasalRateService ?? throw new ArgumentNullException(nameof(yasalRateService));
        _ticariRateService = ticariRateService ?? throw new ArgumentNullException(nameof(ticariRateService));
    }

    public CalculatorMetadata Metadata { get; } = new()
    {
        Slug = Slug,
        Category = CalculatorCategory.Faiz,
        Title = "Ticari Temerrüt Faizi",
        ShortDescription = "3095 s.K. m.2 — yasal faiz ile TCMB avans tabanlı ticari temerrüt faizini paralel hesap, yüksek olanı işaretle.",
        LegalReference = "3095 s.K. m.2",
        Status = CalculatorStatus.Active,
        DisplayNumber = "14",
        Keywords = new[] { "ticari faiz", "temerrüt", "3095 m.2", "TCMB avans" }
    };

    public async Task<TicariTemerrutFaizResult> CalculateAsync(TicariTemerrutFaizInput input, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        var result = new TicariTemerrutFaizResult();

        if (input.AnaPara is null or <= 0)
            result.ValidationErrors[nameof(input.AnaPara)] = "Ana para pozitif olmalıdır.";
        if (input.BaslangicTarihi is null)
            result.ValidationErrors[nameof(input.BaslangicTarihi)] = "Başlangıç tarihi boş olamaz.";
        if (input.HesapTarihi is null)
            result.ValidationErrors[nameof(input.HesapTarihi)] = "Hesap tarihi boş olamaz.";

        if (input.BaslangicTarihi is not null && input.HesapTarihi is not null
            && input.HesapTarihi <= input.BaslangicTarihi)
        {
            result.ValidationErrors[nameof(input.HesapTarihi)] = "Hesap tarihi başlangıçtan sonra olmalıdır.";
        }

        if (result.ValidationErrors.Count > 0)
        {
            result.IsValid = false;
            return result;
        }

        var anaPara = input.AnaPara!.Value;
        var bas = input.BaslangicTarihi!.Value;
        var bitis = input.HesapTarihi!.Value;
        var gunYil = (decimal)(int)input.GunYili;

        // Yasal faiz hesabı
        var yasalDonemler = await _yasalRateService.GetRatePeriodsAsync("yasal-faiz", "yillik-oran", bas, bitis, cancellationToken);
        var yasalDetay = HesaplaDonemler(anaPara, yasalDonemler, gunYil);
        var yasalFaiz = yasalDetay.Sum(d => d.FaizTutari);

        // Ticari temerrüt faizi hesabı (3095 m.2)
        var ticariDonemler = await _ticariRateService.GetCommercialPeriodsAsync(bas, bitis, cancellationToken);
        var ticariDetay = HesaplaDonemler(anaPara, ticariDonemler, gunYil);
        var ticariFaiz = ticariDetay.Sum(d => d.FaizTutari);

        if (yasalDonemler.Count == 0 && ticariDonemler.Count == 0)
        {
            result.Warnings.Add("Belirtilen tarih aralığı için hem yasal hem ticari faiz oranı tablolarında kayıt bulunamadı.");
            result.IsValid = false;
            return result;
        }

        var ticariYuksek = ticariFaiz > yasalFaiz;
        var onerilenSecim = ticariYuksek ? "Ticari Temerrüt Faizi" : "Yasal Faiz";
        var onerilenTutar = anaPara + Math.Max(yasalFaiz, ticariFaiz);

        var toplamGun = (bitis - bas).Days + 1;

        result.AnaPara = anaPara;
        result.ToplamGun = toplamGun;
        result.YasalFaizTutari = Math.Round(yasalFaiz, 2);
        result.YasalToplamTutar = Math.Round(anaPara + yasalFaiz, 2);
        result.YasalDonemDetaylar = yasalDetay;
        result.TicariFaizTutari = Math.Round(ticariFaiz, 2);
        result.TicariToplamTutar = Math.Round(anaPara + ticariFaiz, 2);
        result.TicariDonemDetaylar = ticariDetay;
        result.OnerilenSecim = onerilenSecim;
        result.OnerilenTutar = Math.Round(onerilenTutar, 2);

        var tr = new CultureInfo("tr-TR");
        result.TotalAmount = result.OnerilenTutar;
        result.TotalLabel = $"Önerilen: {onerilenSecim}";
        result.Unit = "TL";

        result.Rows.Add(new CalculationResultRow { Key = "Ana Para", Value = anaPara.ToString("N2", tr) + " TL" });
        result.Rows.Add(new CalculationResultRow { Key = "Toplam Süre", Value = $"{toplamGun} gün" });
        result.Rows.Add(new CalculationResultRow { Key = "Gün/Yıl Bazı", Value = $"{gunYil:0} gün" });
        result.Rows.Add(new CalculationResultRow { Key = "─── KARŞILAŞTIRMA ───", Value = "" });
        result.Rows.Add(new CalculationResultRow {
            Key = "Yasal Faiz (3095 m.1)",
            Value = result.YasalFaizTutari.ToString("N2", tr) + " TL",
            IsHighlighted = !ticariYuksek
        });
        result.Rows.Add(new CalculationResultRow {
            Key = "Ticari Temerrüt Faizi (3095 m.2)",
            Value = result.TicariFaizTutari.ToString("N2", tr) + " TL",
            IsHighlighted = ticariYuksek
        });
        result.Rows.Add(new CalculationResultRow {
            Key = "Fark",
            Value = Math.Abs(result.TicariFaizTutari - result.YasalFaizTutari).ToString("N2", tr) + " TL"
        });

        var aymYururluk = new DateTime(2026, 8, 1);
        if (bitis >= aymYururluk)
        {
            result.Warnings.Add(
                "AYM 22.07.2025 K.2025/164 — Hesap tarihiniz 01.08.2026 sonrasını içeriyor. " +
                "Sözleşmeden kaynaklanmayan borç ilişkileri için yasal faiz uygulanması iptal edilmiştir; " +
                "bu durum ticari temerrüt faizi seçimini etkileyebilir."
            );
        }

        result.Note = "<strong>3095 s.K. m.2:</strong> Ticari işlerde temerrüt faizi, yasal faiz ile TCMB'nin önceki yılın 31 Aralık'taki avans oranından <em>yüksek olanı</em> tercih edilebilir. " +
                      "<strong>5 Puan Kuralı:</strong> 30 Haziran avans oranı, önceki yılın 31 Aralık'tan 5 puan veya daha fazla farklıysa, yılın ikinci yarısı yeni oranı kullanır. Aksi halde ilk yarı oranı devam eder. " +
                      "<strong>Yıl içinde TCMB'nin yaptığı diğer değişiklikler 3095 hesabında yok hükmündedir.</strong> " +
                      "<strong>Mürekkep Faiz:</strong> 3095 s.K. m.3 uyarınca yasaktır. " +
                      "<strong>Önemli:</strong> Bu hesap aktarım amaçlıdır; alacaklı dilekçesinde yasal veya ticari faizden hangisini talep edeceğini belirtir.";

        return result;
    }

    private static List<FaizDonemDetay> HesaplaDonemler(decimal anaPara, IReadOnlyList<InterestRatePeriod> donemler, decimal gunYil)
    {
        var detay = new List<FaizDonemDetay>();
        foreach (var donem in donemler)
        {
            var faizTutari = anaPara * donem.AnnualRate * (donem.Days / gunYil);
            faizTutari = Math.Round(faizTutari, 2, MidpointRounding.AwayFromZero);
            detay.Add(new FaizDonemDetay
            {
                Baslangic = donem.Start,
                Bitis = donem.End,
                Gun = donem.Days,
                YillikOran = donem.AnnualRate,
                FaizTutari = faizTutari
            });
        }
        return detay;
    }
}
