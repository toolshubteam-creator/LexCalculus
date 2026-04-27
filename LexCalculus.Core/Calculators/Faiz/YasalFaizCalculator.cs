using System.Globalization;
using LexCalculus.Core.Calculators.Common;
using LexCalculus.Core.Interfaces;
using LexCalculus.Core.Models.Calculators;

namespace LexCalculus.Core.Calculators.Faiz;

/// <summary>
/// Statutory interest calculator (Yasal Faiz, 3095 s.K. m.1).
///
/// Methodology: simple interest, computed period-by-period.
/// Each sub-period (between rate changes) uses its own annual rate.
///
/// Formula per period:
///   interest = principal × annualRate × (days / 365)
///
/// Total interest = sum of period interests.
/// </summary>
public sealed class YasalFaizCalculator : ICalculator<YasalFaizInput, YasalFaizResult>
{
    private const string Slug = "yasal-faiz";

    private readonly IInterestRateService _rateService;

    public YasalFaizCalculator(IInterestRateService rateService)
    {
        _rateService = rateService ?? throw new ArgumentNullException(nameof(rateService));
    }

    public CalculatorMetadata Metadata { get; } = new()
    {
        Slug = Slug,
        Category = CalculatorCategory.Faiz,
        Title = "Yasal Faiz",
        ShortDescription = "3095 s.K. m.1 uyarınca dönemsel yıllık oran tablosuyla basit faiz hesabı.",
        LegalReference = "3095 s.K. m.1",
        Status = CalculatorStatus.Active,
        DisplayNumber = "13",
        Keywords = new[] { "yasal faiz", "3095", "basit faiz", "alacak" }
    };

    public async Task<YasalFaizResult> CalculateAsync(YasalFaizInput input, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        var result = new YasalFaizResult();

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

        var donemler = await _rateService.GetRatePeriodsAsync(Slug, "yillik-oran", bas, bitis, cancellationToken);

        if (donemler.Count == 0)
        {
            result.Warnings.Add("Belirtilen tarih aralığı için yasal faiz oranı tablosunda kayıt bulunamadı.");
            result.IsValid = false;
            return result;
        }

        var gunYil = (decimal)(int)input.GunYili;
        decimal toplamFaiz = 0m;
        var donemDetaylar = new List<FaizDonemDetay>();

        foreach (var donem in donemler)
        {
            var faizTutari = anaPara * donem.AnnualRate * (donem.Days / gunYil);
            faizTutari = Math.Round(faizTutari, 2, MidpointRounding.AwayFromZero);
            toplamFaiz += faizTutari;

            donemDetaylar.Add(new FaizDonemDetay
            {
                Baslangic = donem.Start,
                Bitis = donem.End,
                Gun = donem.Days,
                YillikOran = donem.AnnualRate,
                FaizTutari = faizTutari
            });
        }

        var toplamGun = (bitis - bas).Days + 1;
        var toplamTutar = anaPara + toplamFaiz;

        result.AnaPara = anaPara;
        result.ToplamGun = toplamGun;
        result.ToplamFaiz = toplamFaiz;
        result.ToplamTutar = toplamTutar;
        result.DonemDetaylar = donemDetaylar;

        var tr = new CultureInfo("tr-TR");
        result.TotalAmount = toplamTutar;
        result.TotalLabel = "Toplam Tutar (Ana Para + Faiz)";
        result.Unit = "TL";

        result.Rows.Add(new CalculationResultRow { Key = "Ana Para", Value = anaPara.ToString("N2", tr) + " TL" });
        result.Rows.Add(new CalculationResultRow { Key = "Toplam Süre", Value = $"{toplamGun} gün" });
        result.Rows.Add(new CalculationResultRow { Key = "Gün/Yıl Bazı", Value = $"{(int)input.GunYili} gün" });
        result.Rows.Add(new CalculationResultRow { Key = "Toplam Faiz", Value = toplamFaiz.ToString("N2", tr) + " TL", IsHighlighted = true });
        result.Rows.Add(new CalculationResultRow { Key = "Dönem Sayısı", Value = donemler.Count.ToString() });

        result.Note = "<strong>Yöntem:</strong> 3095 s.K. m.1 — basit faiz, formula: ana × yıllık oran × (gün/yıl bazı). " +
                      "Her oran değişikliği döneminde ayrı hesaplanır, toplanır. " +
                      "<strong>Mevzuat:</strong> 3095 s.K. m.1. " +
                      "<strong>Uyarı:</strong> 2024 itibariyle yasal faiz %9 — TÜFE üzerinde olabilir; gerçek zarar için ticari faiz veya temerrüt faizi araştırılmalıdır.";

        return result;
    }
}
