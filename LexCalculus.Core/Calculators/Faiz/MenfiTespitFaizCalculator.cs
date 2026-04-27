using System.Globalization;
using LexCalculus.Core.Calculators.Common;
using LexCalculus.Core.Enums;
using LexCalculus.Core.Interfaces;
using LexCalculus.Core.Models.Calculators;

namespace LexCalculus.Core.Calculators.Faiz;

/// <summary>
/// Negative Declaratory Interest Calculator (Menfi Tespit Faizi, TBK m.78-79).
///
/// LEGAL BASIS:
///   - 6098 sayılı TBK m.78: Sebepsiz zenginleşme — istirdat
///   - 6098 sayılı TBK m.79: İyiniyet/kötüniyet ayrımı — iade kapsamı
///   - 3095 sayılı Kanun m.1: Uygulanacak yasal faiz oranı
///
/// FORMULA:
///   1. Faiz başlangıç tarihi seçimi:
///      - Kötüniyetli alacaklı: tahsil tarihinden itibaren (TBK m.79/2)
///      - İyiniyetli alacaklı: iade talep tarihinden itibaren (TBK m.79/1)
///   2. Yasal faiz oranı 3095 m.1'e göre dönemler arası bölünür (IInterestRateService)
///   3. Basit faiz: Faiz = Tutar × Oran × (Gün / GunYili)
///
/// AYM K.2025/164 (22.07.2025): 3095 m.1'in sözleşmeden kaynaklanmayan borç ilişkileri
/// için iptali, 01.08.2026'da yürürlüğe girer. Sebepsiz zenginleşme klasik olarak
/// "sözleşmeden kaynaklanmayan" borç ilişkisidir → bu calculator AYM kararından
/// doğrudan etkilenir. Hesap tarihi 01.08.2026 sonrasındaysa sarı uyarı.
/// </summary>
public sealed class MenfiTespitFaizCalculator : ICalculator<MenfiTespitFaizInput, MenfiTespitFaizResult>
{
    private const string Slug = "menfi-tespit-faizi";

    private readonly IInterestRateService _legalInterestService;

    public MenfiTespitFaizCalculator(IInterestRateService legalInterestService)
    {
        _legalInterestService = legalInterestService ?? throw new ArgumentNullException(nameof(legalInterestService));
    }

    public CalculatorMetadata Metadata { get; } = new()
    {
        Slug = Slug,
        Category = CalculatorCategory.Faiz,
        Title = "Menfi Tespit Faizi",
        ShortDescription = "TBK m.78-79 — sebepsiz zenginleşme istirdadında 3095 m.1 yasal faiz hesabı; alacaklı kötüniyet/iyiniyet ayrımı.",
        LegalReference = "TBK m.78-79 + 3095 s.K. m.1",
        Status = CalculatorStatus.Active,
        DisplayNumber = "17",
        Keywords = new[] { "menfi tespit", "sebepsiz zenginleşme", "istirdat", "haksız tahsil", "TBK 78" }
    };

    public async Task<MenfiTespitFaizResult> CalculateAsync(MenfiTespitFaizInput input, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        var result = new MenfiTespitFaizResult();

        if (input.HaksizTahsilTutari is null or <= 0)
            result.ValidationErrors[nameof(input.HaksizTahsilTutari)] = "Haksız tahsil tutarı pozitif olmalıdır.";
        if (input.TahsilTarihi is null)
            result.ValidationErrors[nameof(input.TahsilTarihi)] = "Tahsil tarihi boş olamaz.";
        if (input.HesapTarihi is null)
            result.ValidationErrors[nameof(input.HesapTarihi)] = "Hesap tarihi boş olamaz.";

        if (!input.AlacakliKotuNiyetli && input.IadeTalepTarihi is null)
        {
            result.ValidationErrors[nameof(input.IadeTalepTarihi)] =
                "İyiniyetli alacaklı durumunda iade talep tarihi zorunludur (TBK m.79/1).";
        }

        if (input.TahsilTarihi is not null && input.HesapTarihi is not null
            && input.HesapTarihi <= input.TahsilTarihi)
        {
            result.ValidationErrors[nameof(input.HesapTarihi)] = "Hesap tarihi tahsil tarihinden sonra olmalıdır.";
        }

        if (!input.AlacakliKotuNiyetli && input.IadeTalepTarihi is not null
            && input.TahsilTarihi is not null && input.IadeTalepTarihi < input.TahsilTarihi)
        {
            result.ValidationErrors[nameof(input.IadeTalepTarihi)] =
                "İade talep tarihi tahsil tarihinden önce olamaz.";
        }

        if (result.ValidationErrors.Count > 0)
        {
            result.IsValid = false;
            return result;
        }

        var tutar = input.HaksizTahsilTutari!.Value;
        var tahsil = input.TahsilTarihi!.Value;
        var bitis = input.HesapTarihi!.Value;
        var gunYil = (decimal)(int)input.GunYili;

        DateTime faizBaslangic;
        string baslangicAciklama;

        if (input.AlacakliKotuNiyetli)
        {
            faizBaslangic = tahsil;
            baslangicAciklama = "Alacaklı kötüniyetli kabul edildiği için faiz, tahsil/ödeme tarihinden itibaren işler (TBK m.79/2).";
        }
        else
        {
            faizBaslangic = input.IadeTalepTarihi!.Value;
            baslangicAciklama = "Alacaklı iyiniyetli kabul edildiği için faiz, iade talep tarihinden itibaren işler (TBK m.79/1).";
        }

        var donemler = await _legalInterestService.GetRatePeriodsAsync(
            "yasal-faiz", "yillik-oran", faizBaslangic, bitis, cancellationToken);

        decimal toplamFaiz = 0m;
        var detaylar = new List<FaizDonemDetay>();

        foreach (var donem in donemler)
        {
            var faiz = tutar * donem.AnnualRate * (donem.Days / gunYil);
            faiz = Math.Round(faiz, 2, MidpointRounding.AwayFromZero);
            toplamFaiz += faiz;

            detaylar.Add(new FaizDonemDetay
            {
                Baslangic = donem.Start,
                Bitis = donem.End,
                Gun = donem.Days,
                YillikOran = donem.AnnualRate,
                FaizTutari = faiz
            });
        }

        result.HaksizTahsilTutari = tutar;
        result.FaizBaslangicTarihi = faizBaslangic;
        result.FaizBaslangicAciklama = baslangicAciklama;
        result.ToplamGun = (bitis - faizBaslangic).Days + 1;
        result.FaizTutari = Math.Round(toplamFaiz, 2);
        result.IadeEdilecekToplamTutar = Math.Round(tutar + toplamFaiz, 2);
        result.DonemDetaylar = detaylar;

        var tr = new CultureInfo("tr-TR");
        result.TotalAmount = result.IadeEdilecekToplamTutar;
        result.TotalLabel = "İade Edilecek Toplam Tutar";
        result.Unit = "TL";

        result.Rows.Add(new CalculationResultRow { Key = "Haksız Tahsil Tutarı", Value = tutar.ToString("N2", tr) + " TL" });
        result.Rows.Add(new CalculationResultRow { Key = "Faiz Başlangıç", Value = faizBaslangic.ToString("dd.MM.yyyy") });
        result.Rows.Add(new CalculationResultRow { Key = "Hesap Tarihi", Value = bitis.ToString("dd.MM.yyyy") });
        result.Rows.Add(new CalculationResultRow { Key = "Toplam Süre", Value = $"{result.ToplamGun} gün" });
        result.Rows.Add(new CalculationResultRow { Key = "Faiz Tutarı", Value = result.FaizTutari.ToString("N2", tr) + " TL", IsHighlighted = true });

        var aymYururluk = new DateTime(2026, 8, 1);
        if (bitis >= aymYururluk)
        {
            result.Warnings.Add(
                "ÖNEMLİ — AYM 22.07.2025 K.2025/164 kararı: 3095 s.K. m.1 sözleşmeden kaynaklanmayan borç ilişkileri yönünden iptal edildi (yürürlük: 01.08.2026). " +
                "Sebepsiz zenginleşmeden doğan borçlar bu kapsamda değerlendirilmektedir. " +
                "Hesap tarihiniz iptal yürürlük tarihinden sonradır — yeni yasal düzenleme çıkmadıysa bu hesap tartışmalı olabilir; lütfen güncel mevzuatı kontrol edin."
            );
        }

        if (input.AlacakliKotuNiyetli)
        {
            result.Warnings.Add(
                "Kötüniyet, alacaklının tahsil sırasında borcun mevcut olmadığını bildiği veya bilmesi gerektiği anlamına gelir. İspat yükü, kötüniyet iddiasında bulunan tarafa aittir."
            );
        }

        result.Note = "<strong>TBK m.78:</strong> Borçlu olmadığı şeyi ödeyen kimse, ödeme zamanında borçlu olmadığını ispatlayarak iadesini talep edebilir. " +
                      "<strong>m.79:</strong> İyiniyetli alacaklı yalnızca elindeki zenginleşmeyi iade ederken, kötüniyetli alacaklı tüm tutarı tahsil tarihinden itibaren faiziyle iade eder. " +
                      "<strong>Faiz oranı:</strong> 3095 s.K. m.1 yasal faiz oranı uygulanır; bileşik faiz yasaktır (m.3). " +
                      "<strong>Önemli:</strong> Kötüniyet/iyiniyet tespiti mahkeme kararıdır; bu hesap senaryo tabanlıdır.";

        return result;
    }
}
