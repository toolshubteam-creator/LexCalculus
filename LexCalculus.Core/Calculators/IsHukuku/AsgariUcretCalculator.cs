using System.Globalization;
using LexCalculus.Core.Calculators.Common;
using LexCalculus.Core.Interfaces;
using LexCalculus.Core.Models.Calculators;

namespace LexCalculus.Core.Calculators.IsHukuku;

/// <summary>
/// Minimum wage compliance calculator (Asgari Ücret Uyumluluk Kontrolü).
///
/// Iterates over each month in the requested period, looks up the statutory
/// minimum wage in force at the START of that month (FormulaParameters with
/// EffectiveDate &lt;= month-start), and compares against the actual paid wage.
/// Reports the cumulative shortfall plus simple interest at the legal rate.
///
/// Phase 2 simplifications:
///   - Constant gross wage assumed for the full period (real cases may have raises)
///   - Simple interest at a flat annual rate (parameter "yasal-faiz-orani-yillik")
///   - No partial-month proration (each month treated atomically)
///
/// Phase 3 will add: per-month wage entry, periodic legal interest table.
///
/// Statute of limitations: 5 years (m.32).
/// </summary>
public sealed class AsgariUcretCalculator : ICalculator<AsgariUcretInput, AsgariUcretResult>
{
    private const string Slug = "asgari-ucret-kontrol";
    private const string ParamAsgariBrut = "asgari-ucret-brut";
    private const string ParamYasalFaiz = "yasal-faiz-orani-yillik";

    private readonly IFormulaParameterService _params;

    public AsgariUcretCalculator(IFormulaParameterService parameters)
    {
        _params = parameters ?? throw new ArgumentNullException(nameof(parameters));
    }

    public CalculatorMetadata Metadata { get; } = new()
    {
        Slug = Slug,
        Category = CalculatorCategory.IsHukuku,
        Title = "Asgari Ücret Uyumluluk Kontrolü",
        ShortDescription = "Girilen ücretin dönemsel asgari ücretle karşılaştırması, eksik ödeme tespiti, birikmiş alacak ve yasal faiz hesabı.",
        LegalReference = "Çalışma Bakanlığı / 4857 s.K.",
        Status = CalculatorStatus.Active,
        DisplayNumber = "06",
        Keywords = new[] { "asgari ücret", "eksik ödeme", "alacak", "iş hukuku" }
    };

    public async Task<AsgariUcretResult> CalculateAsync(AsgariUcretInput input, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        var result = new AsgariUcretResult();

        if (input.BaslangicTarihi is null)
            result.ValidationErrors[nameof(input.BaslangicTarihi)] = "Başlangıç tarihi boş olamaz.";
        if (input.BitisTarihi is null)
            result.ValidationErrors[nameof(input.BitisTarihi)] = "Bitiş tarihi boş olamaz.";
        if (input.OdenenBrutAylik is null or <= 0)
            result.ValidationErrors[nameof(input.OdenenBrutAylik)] = "Ödenen ücret pozitif olmalıdır.";

        if (input.BaslangicTarihi is not null && input.BitisTarihi is not null
            && input.BitisTarihi <= input.BaslangicTarihi)
        {
            result.ValidationErrors[nameof(input.BitisTarihi)] = "Bitiş tarihi başlangıç tarihinden sonra olmalıdır.";
        }

        if (result.ValidationErrors.Count > 0)
        {
            result.IsValid = false;
            return result;
        }

        var bas = new DateTime(input.BaslangicTarihi!.Value.Year, input.BaslangicTarihi.Value.Month, 1);
        var bitis = new DateTime(input.BitisTarihi!.Value.Year, input.BitisTarihi.Value.Month, 1);
        var odenen = input.OdenenBrutAylik!.Value;

        var detaylar = new List<AylikDetay>();
        decimal toplamEksik = 0m;

        for (var iter = bas; iter <= bitis; iter = iter.AddMonths(1))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var asgari = await _params.GetValueAsync("*", ParamAsgariBrut, iter, cancellationToken);
            if (asgari is null)
            {
                result.Warnings.Add($"{iter:yyyy-MM} için asgari ücret parametresi bulunamadı; bu ay hesaba dahil edilmedi.");
                continue;
            }

            var eksik = Math.Max(0m, asgari.Value - odenen);
            detaylar.Add(new AylikDetay
            {
                Ay = iter,
                AsgariBrut = asgari.Value,
                OdenenBrut = odenen,
                Eksiklik = eksik
            });

            toplamEksik += eksik;
        }

        var eksikAy = detaylar.Count(d => d.Eksiklik > 0);

        decimal yasalFaiz = 0m;
        if (input.FaizDahil && toplamEksik > 0)
        {
            var faizOrani = await _params.GetValueAsync("*", ParamYasalFaiz, bitis, cancellationToken)
                          ?? 0.18m;

            decimal toplamFaizTutari = 0m;
            foreach (var d in detaylar.Where(x => x.Eksiklik > 0))
            {
                var aylikSure = ((bitis.Year - d.Ay.Year) * 12) + (bitis.Month - d.Ay.Month);
                aylikSure = Math.Max(0, aylikSure);
                var faizPart = d.Eksiklik * (decimal)aylikSure / 12m * faizOrani;
                toplamFaizTutari += faizPart;
            }

            yasalFaiz = Math.Round(toplamFaizTutari, 2, MidpointRounding.AwayFromZero);
        }

        var toplamAlacak = toplamEksik + yasalFaiz;

        result.ToplamAy = detaylar.Count;
        result.EksikAy = eksikAy;
        result.ToplamEksikBrut = toplamEksik;
        result.YasalFaiz = yasalFaiz;
        result.ToplamAlacak = toplamAlacak;
        result.AylikDetaylar = detaylar;

        var tr = new CultureInfo("tr-TR");
        result.TotalAmount = toplamAlacak;
        result.TotalLabel = "Toplam Brüt Alacak";
        result.Unit = "TL";

        result.Rows.Add(new CalculationResultRow { Key = "İncelenen Toplam Ay", Value = detaylar.Count.ToString() });
        result.Rows.Add(new CalculationResultRow { Key = "Eksik Ödenmiş Ay", Value = eksikAy.ToString() });
        result.Rows.Add(new CalculationResultRow { Key = "Toplam Eksik Brüt", Value = toplamEksik.ToString("N2", tr) + " TL" });

        if (input.FaizDahil)
        {
            result.Rows.Add(new CalculationResultRow { Key = "Yasal Faiz (basit, %18 yıllık)", Value = yasalFaiz.ToString("N2", tr) + " TL" });
        }

        if (toplamEksik == 0)
        {
            result.Rows.Add(new CalculationResultRow {
                Key = "Sonuç",
                Value = "✓ Eksik ödeme tespit edilmedi"
            });
            result.Warnings.Add("İncelenen dönem boyunca ödenen ücret tüm aylarda asgari ücretin üstünde. Eksik ödeme yoktur.");
        }

        result.Note = "<strong>Hesap Yöntemi:</strong> Her ay için Çalışma Bakanlığı'nca yayımlanan asgari ücret ile ödenen brüt ücret karşılaştırılır; fark biriktirilir. " +
                      "<strong>Faiz:</strong> Faz 2 basitleştirmesi olarak yıllık %18 sabit oran kullanılmıştır; gerçek hesaplamada 3095 sayılı Kanun'un dönemsel oranları uygulanmalıdır. " +
                      "<strong>Zamanaşımı:</strong> 5 yıl (m.32).";

        return result;
    }
}
