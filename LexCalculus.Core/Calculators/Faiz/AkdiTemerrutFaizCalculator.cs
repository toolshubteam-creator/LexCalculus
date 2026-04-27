using System.Globalization;
using LexCalculus.Core.Calculators.Common;
using LexCalculus.Core.Enums;
using LexCalculus.Core.Interfaces;
using LexCalculus.Core.Models.Calculators;

namespace LexCalculus.Core.Calculators.Faiz;

/// <summary>
/// Contractual default interest calculator (Akdî Temerrüt Faizi, TBK m.120).
///
/// User provides one or more contract rate periods. Calculator computes:
/// 1. Akdî hesap with user-provided rates (simple or compound)
/// 2. 3095 m.2 parallel hesap (lower bound check per TBK m.120/2)
/// 3. Recommends the higher one (TBK m.120/2 statutory floor)
///
/// Compound interest only valid if TaclrlerArasiYaziliSozlesme = true (TTK exception
/// to 3095 m.3). Otherwise compound option ignored with warning.
///
/// TBK m.27 (ahlaka aykırı sözleşme) warning if ortalama oran > %100 yıllık.
/// </summary>
public sealed class AkdiTemerrutFaizCalculator : ICalculator<AkdiTemerrutFaizInput, AkdiTemerrutFaizResult>
{
    private const string Slug = "akdi-temerrut-faizi";
    private const decimal AhlakaAykiriEsigi = 1.00m;

    private readonly IThree095CommercialRateService _ticariRateService;

    public AkdiTemerrutFaizCalculator(IThree095CommercialRateService ticariRateService)
    {
        _ticariRateService = ticariRateService ?? throw new ArgumentNullException(nameof(ticariRateService));
    }

    public CalculatorMetadata Metadata { get; } = new()
    {
        Slug = Slug,
        Category = CalculatorCategory.Faiz,
        Title = "Akdî Temerrüt Faizi",
        ShortDescription = "TBK m.120 — sözleşmede belirlenen temerrüt faizi oranlarıyla hesap; 3095 m.2 alt sınırı paralel kontrol.",
        LegalReference = "TBK m.120 + 3095 s.K. m.2",
        Status = CalculatorStatus.Active,
        DisplayNumber = "15",
        Keywords = new[] { "akdi temerrüt", "sözleşme faizi", "TBK 120", "bileşik faiz" }
    };

    public async Task<AkdiTemerrutFaizResult> CalculateAsync(AkdiTemerrutFaizInput input, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        var result = new AkdiTemerrutFaizResult();

        if (input.AnaPara is null or <= 0)
            result.ValidationErrors[nameof(input.AnaPara)] = "Ana para pozitif olmalıdır.";
        if (input.TemerrutTarihi is null)
            result.ValidationErrors[nameof(input.TemerrutTarihi)] = "Temerrüt tarihi boş olamaz.";
        if (input.HesapTarihi is null)
            result.ValidationErrors[nameof(input.HesapTarihi)] = "Hesap tarihi boş olamaz.";

        if (input.TemerrutTarihi is not null && input.HesapTarihi is not null
            && input.HesapTarihi <= input.TemerrutTarihi)
        {
            result.ValidationErrors[nameof(input.HesapTarihi)] = "Hesap tarihi temerrüt tarihinden sonra olmalıdır.";
        }

        if (input.SozlesmeOranlari.Count == 0)
        {
            result.ValidationErrors[nameof(input.SozlesmeOranlari)] = "En az bir sözleşme dönemi girilmelidir.";
        }
        else
        {
            for (var i = 0; i < input.SozlesmeOranlari.Count; i++)
            {
                var d = input.SozlesmeOranlari[i];
                if (d.BaslangicTarihi is null || d.YillikOran is null or <= 0)
                {
                    result.ValidationErrors[$"SozlesmeOranlari[{i}]"] = $"Dönem {i + 1}: tarih ve oran zorunlu.";
                }
            }
        }

        if (result.ValidationErrors.Count > 0)
        {
            result.IsValid = false;
            return result;
        }

        var anaPara = input.AnaPara!.Value;
        var temerrut = input.TemerrutTarihi!.Value;
        var bitis = input.HesapTarihi!.Value;
        var gunYil = (decimal)(int)input.GunYili;

        var siraliDonemler = input.SozlesmeOranlari
            .OrderBy(d => d.BaslangicTarihi)
            .ToList();

        var akdiDonemler = AkdiDonemleriCikar(siraliDonemler, temerrut, bitis);
        var yasalDonemler = await _ticariRateService.GetCommercialPeriodsAsync(temerrut, bitis, cancellationToken);

        decimal akdiFaiz;
        var akdiDetay = new List<FaizDonemDetay>();

        if (input.FaizYontemi == FaizYontemi.Bilesik && input.TaclrlerArasiYaziliSozlesme)
        {
            akdiFaiz = HesaplaBilesik(anaPara, akdiDonemler, gunYil, (int)input.BilesikDonemi, akdiDetay);
        }
        else
        {
            if (input.FaizYontemi == FaizYontemi.Bilesik && !input.TaclrlerArasiYaziliSozlesme)
            {
                result.Warnings.Add(
                    "Bileşik faiz seçeneği ancak tacirler arası yazılı sözleşmelerde geçerlidir (3095 m.3 + TTK). " +
                    "Bu hesapta basit faiz uygulandı."
                );
            }
            akdiFaiz = HesaplaBasit(anaPara, akdiDonemler, gunYil, akdiDetay);
        }

        var yasalDetay = new List<FaizDonemDetay>();
        var yasalFaiz = HesaplaBasit(anaPara, yasalDonemler, gunYil, yasalDetay);

        var yasalYuksek = yasalFaiz > akdiFaiz;
        var uygulanacak = yasalYuksek ? yasalFaiz : akdiFaiz;
        var uygulanacakSecim = yasalYuksek
            ? "3095 m.2 (TBK m.120/2 alt sınırı uygulandı — sözleşme oranı düşük)"
            : "Sözleşme oranı (akdî)";

        var toplamGun = (bitis - temerrut).Days + 1;
        var agirlikliToplam = akdiDonemler.Sum(d => d.AnnualRate * d.Days);
        var ortalamaOran = toplamGun > 0 ? agirlikliToplam / toplamGun : 0m;

        result.AnaPara = anaPara;
        result.ToplamGun = toplamGun;
        result.FaizYontemiAciklama = (input.FaizYontemi == FaizYontemi.Bilesik && input.TaclrlerArasiYaziliSozlesme)
            ? $"Bileşik faiz ({(int)input.BilesikDonemi}/yıl dönemli)"
            : "Basit faiz";

        result.AkdiFaizTutari = Math.Round(akdiFaiz, 2);
        result.AkdiToplamTutar = Math.Round(anaPara + akdiFaiz, 2);
        result.AkdiDonemDetaylar = akdiDetay;
        result.Yasal3095FaizTutari = Math.Round(yasalFaiz, 2);
        result.Yasal3095ToplamTutar = Math.Round(anaPara + yasalFaiz, 2);
        result.Yasal3095DonemDetaylar = yasalDetay;
        result.UygulanacakSecim = uygulanacakSecim;
        result.UygulanacakTutar = Math.Round(anaPara + uygulanacak, 2);
        result.UcMaddesi120AltSiniri = yasalYuksek;
        result.OrtalamaYillikOran = ortalamaOran;

        var tr = new CultureInfo("tr-TR");
        result.TotalAmount = result.UygulanacakTutar;
        result.TotalLabel = "Uygulanacak Tutar";
        result.Unit = "TL";

        result.Rows.Add(new CalculationResultRow { Key = "Ana Para", Value = anaPara.ToString("N2", tr) + " TL" });
        result.Rows.Add(new CalculationResultRow { Key = "Toplam Süre", Value = $"{toplamGun} gün" });
        result.Rows.Add(new CalculationResultRow { Key = "Faiz Yöntemi", Value = result.FaizYontemiAciklama });
        result.Rows.Add(new CalculationResultRow { Key = "Ortalama Yıllık Oran", Value = $"%{ortalamaOran * 100:F2}" });

        result.Rows.Add(new CalculationResultRow { Key = "─── KARŞILAŞTIRMA ───", Value = "" });
        result.Rows.Add(new CalculationResultRow {
            Key = "Akdî (sözleşme oranı)",
            Value = result.AkdiFaizTutari.ToString("N2", tr) + " TL",
            IsHighlighted = !yasalYuksek
        });
        result.Rows.Add(new CalculationResultRow {
            Key = "3095 m.2 (alt sınır)",
            Value = result.Yasal3095FaizTutari.ToString("N2", tr) + " TL",
            IsHighlighted = yasalYuksek
        });

        if (ortalamaOran > AhlakaAykiriEsigi)
        {
            result.Warnings.Add(
                $"Ortalama yıllık oran %{ortalamaOran * 100:F2} — TBK m.27 uyarınca ahlaka aykırı yüksek faiz iddiası gündeme gelebilir. " +
                "Mahkeme indirim uygulayabilir; bilirkişi raporu alınması önerilir."
            );
        }

        if (yasalYuksek)
        {
            result.Warnings.Add(
                "TBK m.120/2: Sözleşmede belirlenen temerrüt faizi 3095 m.2 oranından düşük olamaz. " +
                $"Sözleşme oranı yerine 3095 m.2 oranı uygulandı (fark: {(yasalFaiz - akdiFaiz):N2} TL)."
            );
        }

        var aymYururluk = new DateTime(2026, 8, 1);
        if (bitis >= aymYururluk)
        {
            result.Warnings.Add(
                "AYM 22.07.2025 K.2025/164 — Hesap tarihiniz 01.08.2026 sonrasını içeriyor. " +
                "3095 m.2'nin sözleşmeden kaynaklanmayan borç ilişkileri yönünden iptali, akdî temerrüt faizi alt sınırı kontrolünü etkileyebilir."
            );
        }

        result.Note = "<strong>TBK m.120:</strong> Sözleşmede temerrüt faizi belirlenmişse o oran uygulanır; ancak <strong>m.120/2</strong> uyarınca bu oran 3095 m.2'de belirtilen orandan az olamaz. " +
                      "<strong>TBK m.27:</strong> Ahlaka aykırı yüksek oranlar mahkemece indirim konusudur. " +
                      "<strong>Bileşik Faiz:</strong> 3095 m.3 yasaktır, istisnai olarak TTK kapsamında tacirler arası yazılı sözleşmede uygulanabilir. " +
                      "<strong>Önemli:</strong> Bu hesap aktarım amaçlıdır; sözleşmenin geçerliliği ve TBK m.27 indirimi mahkeme kararıdır.";

        return result;
    }

    private static List<InterestRatePeriod> AkdiDonemleriCikar(
        List<SozlesmeOranDonem> sozlesmeDonemler, DateTime temerrut, DateTime bitis)
    {
        var donemler = new List<InterestRatePeriod>();

        for (var i = 0; i < sozlesmeDonemler.Count; i++)
        {
            var d = sozlesmeDonemler[i];
            var donemBas = d.BaslangicTarihi!.Value;
            var donemBitis = i + 1 < sozlesmeDonemler.Count
                ? sozlesmeDonemler[i + 1].BaslangicTarihi!.Value.AddDays(-1)
                : bitis;

            var efektifBas = donemBas < temerrut ? temerrut : donemBas;
            var efektifBitis = donemBitis > bitis ? bitis : donemBitis;

            if (efektifBitis >= efektifBas)
            {
                donemler.Add(new InterestRatePeriod(
                    efektifBas, efektifBitis, d.YillikOran!.Value / 100m));
            }
        }

        return donemler;
    }

    private static decimal HesaplaBasit(decimal anaPara, IReadOnlyList<InterestRatePeriod> donemler, decimal gunYil, List<FaizDonemDetay> detay)
    {
        decimal toplam = 0m;
        foreach (var donem in donemler)
        {
            var faiz = anaPara * donem.AnnualRate * (donem.Days / gunYil);
            faiz = Math.Round(faiz, 2, MidpointRounding.AwayFromZero);
            toplam += faiz;

            detay.Add(new FaizDonemDetay
            {
                Baslangic = donem.Start,
                Bitis = donem.End,
                Gun = donem.Days,
                YillikOran = donem.AnnualRate,
                FaizTutari = faiz
            });
        }
        return toplam;
    }

    private static decimal HesaplaBilesik(decimal anaPara, IReadOnlyList<InterestRatePeriod> donemler, decimal gunYil, int donemSayisi, List<FaizDonemDetay> detay)
    {
        decimal mevcutBakiye = anaPara;

        foreach (var donem in donemler)
        {
            var sureYil = (decimal)donem.Days / gunYil;
            var donemBasinaOran = donem.AnnualRate / donemSayisi;
            var ussel = donemSayisi * sureYil;

            var carpan = (decimal)Math.Pow((double)(1 + donemBasinaOran), (double)ussel);

            var donemSonuBakiye = mevcutBakiye * carpan;
            var donemFaizi = Math.Round(donemSonuBakiye - mevcutBakiye, 2, MidpointRounding.AwayFromZero);

            detay.Add(new FaizDonemDetay
            {
                Baslangic = donem.Start,
                Bitis = donem.End,
                Gun = donem.Days,
                YillikOran = donem.AnnualRate,
                FaizTutari = donemFaizi
            });

            mevcutBakiye = donemSonuBakiye;
        }

        return mevcutBakiye - anaPara;
    }
}
