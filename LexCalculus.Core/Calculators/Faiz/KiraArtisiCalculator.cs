using System.Globalization;
using LexCalculus.Core.Calculators.Common;
using LexCalculus.Core.Enums;
using LexCalculus.Core.Interfaces;
using LexCalculus.Core.Models.Calculators;
using LexCalculus.Core.Services;

namespace LexCalculus.Core.Calculators.Faiz;

/// <summary>
/// Rent Increase Determination Calculator (Kira Artış Tespiti, TBK m.344).
///
/// LEGAL BASIS:
///   - 6098 sayılı TBK m.344/1 (18.01.2019 değişikliği): TÜFE 12 aylık ortalama üst sınır
///   - 7409 sayılı Kanun (RG 11.06.2022 + uzatma): konut 11.06.2022-01.07.2024 azami %25
///   - 1 Temmuz 2020'den itibaren çatılı işyeri konut ile aynı (TÜFE üst sınırı)
///
/// FORMULA:
///   1. TÜFE oranı = yenileme tarihinden bir önceki ayın TÜFE 12 ay ort. (DB veya override)
///   2. Sözleşme oranı varsa: min(sözleşme, TÜFE)
///   3. Konut + 11.06.2022-01.07.2024 arası: min(yukarıdaki, 25)
///   4. İşyeri için %25 sınırı YOK
///   5. Yeni Kira = Mevcut Kira × (1 + uygulanacak_oran / 100)
/// </summary>
public sealed class KiraArtisiCalculator : ICalculator<KiraArtisiInput, KiraArtisiResult>
{
    private const string Slug = "kira-artisi";
    private static readonly DateTime YuzdeYirmiBesBaslangic = new(2022, 6, 11);
    private static readonly DateTime YuzdeYirmiBesBitis = new(2024, 7, 1);

    private readonly ITUFEService _tufeService;

    public KiraArtisiCalculator(ITUFEService tufeService)
    {
        _tufeService = tufeService ?? throw new ArgumentNullException(nameof(tufeService));
    }

    public CalculatorMetadata Metadata { get; } = new()
    {
        Slug = Slug,
        Category = CalculatorCategory.Faiz,
        Title = "Kira Artış Tespiti",
        ShortDescription = "TBK m.344 — konut ve çatılı işyeri kira sözleşmelerinin yenilenmesinde TÜFE 12 aylık ortalama üst sınırı.",
        LegalReference = "TBK m.344",
        Status = CalculatorStatus.Active,
        DisplayNumber = "16",
        Keywords = new[] { "kira artışı", "TÜFE 12 ay", "TBK 344", "konut kirası", "işyeri kirası", "kira zammı" }
    };

    public async Task<KiraArtisiResult> CalculateAsync(KiraArtisiInput input, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        var result = new KiraArtisiResult();

        if (input.MevcutKira is null or <= 0)
            result.ValidationErrors[nameof(input.MevcutKira)] = "Mevcut kira pozitif olmalıdır.";
        if (input.YenilenmeTarihi is null)
            result.ValidationErrors[nameof(input.YenilenmeTarihi)] = "Yenileme tarihi boş olamaz.";

        if (result.ValidationErrors.Count > 0)
        {
            result.IsValid = false;
            return result;
        }

        var mevcutKira = input.MevcutKira!.Value;
        var yenileme = input.YenilenmeTarihi!.Value;

        decimal tufeOrani;
        DateTime kullanilanAy;
        string tufeKaynagi;

        if (input.TUFEOverride.HasValue)
        {
            tufeOrani = input.TUFEOverride.Value;
            kullanilanAy = new DateTime(yenileme.Year, yenileme.Month, 1).AddMonths(-1);
            tufeKaynagi = "Manuel giriş (Override)";
            result.Warnings.Add(
                "TÜFE oranı manuel olarak girildi. Lütfen TÜİK verisini doğrulayın: data.tuik.gov.tr");
        }
        else
        {
            var (oran, ay, bulundu) = await _tufeService.GetKiraArtisOraniAsync(yenileme, cancellationToken);
            if (!bulundu)
            {
                result.ValidationErrors[nameof(input.YenilenmeTarihi)] =
                    $"{ay:MMMM yyyy} ayı için sistemde TÜFE verisi yok. " +
                    "Lütfen TÜİK'ten doğrulayıp 'TÜFE Override' alanına manuel girin: data.tuik.gov.tr";
                result.IsValid = false;
                return result;
            }
            tufeOrani = oran!.Value;
            kullanilanAy = ay;
            tufeKaynagi = "Sistem verisi (TÜİK referansı)";
        }

        result.TUFEOrani = tufeOrani;
        result.KullanilanTUFEAyi = kullanilanAy;
        result.TUFEKaynagi = tufeKaynagi;
        result.SozlesmeOrani = input.SozlesmeOrani;

        decimal uygulanacakOran;
        string aciklama;

        if (input.SozlesmeOrani.HasValue)
        {
            if (input.SozlesmeOrani.Value <= tufeOrani)
            {
                uygulanacakOran = input.SozlesmeOrani.Value;
                result.SozlesmeOraniUygulandi = true;
                aciklama = $"Sözleşme oranı (%{input.SozlesmeOrani.Value:N2}) TÜFE'den (%{tufeOrani:N2}) düşük olduğu için sözleşme oranı uygulandı.";
            }
            else
            {
                uygulanacakOran = tufeOrani;
                result.SozlesmeOraniUygulandi = false;
                aciklama = $"Sözleşme oranı (%{input.SozlesmeOrani.Value:N2}) TÜFE üst sınırını (%{tufeOrani:N2}) aştığı için TBK m.344/1 uyarınca TÜFE oranı uygulandı.";
                result.Warnings.Add(
                    "Sözleşmedeki artış oranı yasal üst sınırı aşıyor. TBK m.344/1 emredici hükmü uyarınca TÜFE 12 aylık ortalama uygulanır; aksi anlaşmalar geçersizdir.");
            }
        }
        else
        {
            uygulanacakOran = tufeOrani;
            aciklama = $"Sözleşmede artış oranı belirtilmediği için TBK m.344/1 uyarınca TÜFE 12 aylık ortalama (%{tufeOrani:N2}) uygulandı.";
        }

        var yuzde25DonemiInde = yenileme >= YuzdeYirmiBesBaslangic && yenileme < YuzdeYirmiBesBitis;

        if (input.MulkTipi == MulkTipi.Konut && yuzde25DonemiInde && uygulanacakOran > 25m)
        {
            result.Warnings.Add(
                $"7409 sayılı Kanun (TBK Geçici Madde) uyarınca, 11.06.2022 - 01.07.2024 arasında yenilenen konut kiralarında azami artış oranı %25'tir. " +
                $"Hesaplanan oran (%{uygulanacakOran:N2}) bu sınırın üzerinde olduğu için %25 uygulanmıştır.");
            uygulanacakOran = 25m;
            result.YuzdeYirmiBesSiniriUygulandi = true;
            aciklama = "Konut + 11.06.2022-01.07.2024 dönemi: Azami %25 yasal sınırı uygulandı.";
        }
        else if (input.MulkTipi == MulkTipi.CatliIsyeri && yuzde25DonemiInde)
        {
            result.Warnings.Add(
                "11.06.2022-01.07.2024 dönemindeki %25 sınırlaması SADECE konut kiraları için geçerlidir. Çatılı işyeri kiraları bu dönemde de TÜFE üst sınırına tabidir.");
        }

        result.UygulanacakOran = uygulanacakOran;
        result.UygulanacakOranAciklama = aciklama;
        result.MevcutKira = mevcutKira;
        result.ArtisTutari = Math.Round(mevcutKira * (uygulanacakOran / 100m), 2, MidpointRounding.AwayFromZero);
        result.YeniKira = Math.Round(mevcutKira + result.ArtisTutari, 2, MidpointRounding.AwayFromZero);

        result.Warnings.Add(
            "Bu hesap TBK m.344/1 üst sınırını gösterir. Tarafların yasal üst sınırın altında bir oran üzerinde anlaşması mümkündür.");

        var tr = new CultureInfo("tr-TR");
        result.TotalAmount = result.YeniKira;
        result.TotalLabel = "Yeni Kira Bedeli";
        result.Unit = "TL";

        result.Rows.Add(new CalculationResultRow { Key = "Mevcut Kira", Value = mevcutKira.ToString("N2", tr) + " TL" });
        result.Rows.Add(new CalculationResultRow { Key = "Artış Tutarı", Value = result.ArtisTutari.ToString("N2", tr) + " TL" });
        result.Rows.Add(new CalculationResultRow { Key = "Uygulanan Oran", Value = $"%{uygulanacakOran:N2}", IsHighlighted = true });
        result.Rows.Add(new CalculationResultRow { Key = "─── DETAY ───", Value = "" });
        result.Rows.Add(new CalculationResultRow { Key = $"TÜFE 12 Ay Ort. ({kullanilanAy:MMMM yyyy})", Value = $"%{tufeOrani:N2}" });
        result.Rows.Add(new CalculationResultRow { Key = "TÜFE Kaynağı", Value = tufeKaynagi });

        if (input.SozlesmeOrani.HasValue)
        {
            result.Rows.Add(new CalculationResultRow
            {
                Key = "Sözleşme Oranı",
                Value = $"%{input.SozlesmeOrani.Value:N2} ({(result.SozlesmeOraniUygulandi ? "uygulandı" : "üst sınır aştı, kullanılmadı")})"
            });
        }

        if (result.YuzdeYirmiBesSiniriUygulandi)
        {
            result.Rows.Add(new CalculationResultRow { Key = "%25 Yasal Sınırı", Value = "Uygulandı (konut, 2022-2024)", IsHighlighted = true });
        }

        result.Note = "<strong>TBK m.344/1:</strong> Yenilenen kira dönemlerinde uygulanacak artış, bir önceki kira yılındaki TÜFE 12 aylık ortalama değişim oranını geçemez. " +
                      "Sözleşmede TÜFE'den yüksek bir oran belirlenmiş olsa dahi, yasal üst sınır geçerlidir. " +
                      "<strong>5 yıl kuralı:</strong> Sözleşme süresi 5 yılı aşmışsa, kira tespit davası açılarak yeni kira bedeli mahkemece belirlenebilir (TBK m.344/3). " +
                      "<strong>Önemli:</strong> Bu hesap üst sınırı gösterir; tarafların altında anlaşması mümkündür.";

        return result;
    }
}
