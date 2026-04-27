using System.Globalization;
using LexCalculus.Core.Calculators.Common;
using LexCalculus.Core.Interfaces;
using LexCalculus.Core.Models.Calculators;

namespace LexCalculus.Core.Calculators.Akturya;

/// <summary>
/// Caregiver expense compensation (Bakıcı Gideri Tazminatı).
///
/// Legal basis: TBK m.54 (continuing damages following injury).
///
/// Methodology:
///   1. Determine victim's expected remaining lifetime (TRH 2010, eX)
///   2. Effective monthly cost = monthly cost × care need ratio
///      (e.g., 6h/day care = 25%, 24h care = 100%)
///   3. Annual cost = effective monthly × 12
///   4. Total compensation = AnnuityPresentValue(annual cost, eX, discount)
///
/// Note: No active/passive split — care is needed regardless of working status.
/// Care need ratio is determined by Adli Tıp Kurumu / Sağlık Kurulu reports.
/// </summary>
public sealed class BakiciGideriCalculator : ICalculator<BakiciGideriInput, BakiciGideriResult>
{
    private readonly ILifeTableService _lifeTable;
    private readonly IActuarialService _actuarial;

    public BakiciGideriCalculator(ILifeTableService lifeTable, IActuarialService actuarial)
    {
        _lifeTable = lifeTable ?? throw new ArgumentNullException(nameof(lifeTable));
        _actuarial = actuarial ?? throw new ArgumentNullException(nameof(actuarial));
    }

    public CalculatorMetadata Metadata { get; } = new()
    {
        Slug = "bakici-gideri",
        Category = CalculatorCategory.Akturya,
        Title = "Bakıcı Gideri Tazminatı",
        ShortDescription = "Yaralının kalan ömrü boyunca bakım ihtiyacının iskonto edilmiş peşin değer hesabı.",
        LegalReference = "TBK m.54",
        Status = CalculatorStatus.Active,
        DisplayNumber = "11",
        Keywords = new[] { "bakıcı gideri", "bakım", "refakatçi", "aktüerya" }
    };

    public async Task<BakiciGideriResult> CalculateAsync(BakiciGideriInput input, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        var result = new BakiciGideriResult();

        if (input.OlayTarihi is null)
            result.ValidationErrors[nameof(input.OlayTarihi)] = "Olay tarihi boş olamaz.";
        if (input.YaralananDogumTarihi is null)
            result.ValidationErrors[nameof(input.YaralananDogumTarihi)] = "Yaralanan kişi doğum tarihi boş olamaz.";
        if (input.AylikBakiciMaliyeti is null or <= 0)
            result.ValidationErrors[nameof(input.AylikBakiciMaliyeti)] = "Aylık maliyet pozitif olmalıdır.";
        if (input.BakimIhtiyacOrani is null or <= 0 or > 100)
            result.ValidationErrors[nameof(input.BakimIhtiyacOrani)] = "Bakım oranı %1-100 arası olmalıdır.";

        if (input.OlayTarihi is not null && input.YaralananDogumTarihi is not null
            && input.OlayTarihi < input.YaralananDogumTarihi)
        {
            result.ValidationErrors[nameof(input.OlayTarihi)] = "Olay tarihi doğum tarihinden sonra olmalıdır.";
        }

        if (result.ValidationErrors.Count > 0)
        {
            result.IsValid = false;
            return result;
        }

        var olay = input.OlayTarihi!.Value;
        var dogum = input.YaralananDogumTarihi!.Value;
        var aylikMaliyet = input.AylikBakiciMaliyeti!.Value;
        var bakimOrani = input.BakimIhtiyacOrani!.Value / 100m;
        var iskonto = input.YillikIskontoOrani / 100m;

        var yas = _actuarial.HesaplaYas(dogum, olay);
        var ex = await _lifeTable.GetBekledigiYasamAsync(yas, input.Cinsiyet, ct: cancellationToken);

        if (ex is null)
        {
            result.ValidationErrors[nameof(input.YaralananDogumTarihi)] =
                $"Yaralanan kişinin yaşı ({yas}) için TRH 2010 yaşam tablosunda veri bulunamadı.";
            result.IsValid = false;
            return result;
        }

        var destekSuresiYil = (int)Math.Floor((double)ex.Value);
        var aylikEffektif = aylikMaliyet * bakimOrani;
        var yillikMaliyet = aylikEffektif * 12m;

        var toplam = _actuarial.AnnuityPresentValue(yillikMaliyet, destekSuresiYil, iskonto);

        result.YaralananYas = yas;
        result.BekledigiYasam = ex.Value;
        result.DestekSuresiYil = destekSuresiYil;
        result.AylikMaliyet = aylikMaliyet;
        result.AylikEffektifMaliyet = Math.Round(aylikEffektif, 2);
        result.YillikMaliyet = Math.Round(yillikMaliyet, 2);
        result.ToplamTazminat = Math.Round(toplam, 2);

        var tr = new CultureInfo("tr-TR");
        result.TotalAmount = result.ToplamTazminat;
        result.TotalLabel = "Toplam Bakıcı Gideri Tazminatı";
        result.Unit = "TL";

        result.Rows.Add(new CalculationResultRow { Key = "Yaralanan Yaş", Value = $"{yas} ({input.Cinsiyet})" });
        result.Rows.Add(new CalculationResultRow { Key = "Beklenen Yaşam (TRH 2010)", Value = $"{ex.Value:F2} yıl" });
        result.Rows.Add(new CalculationResultRow { Key = "Destek Süresi", Value = $"{destekSuresiYil} yıl" });
        result.Rows.Add(new CalculationResultRow { Key = "Aylık Bakıcı Maliyeti", Value = aylikMaliyet.ToString("N2", tr) + " TL" });
        result.Rows.Add(new CalculationResultRow { Key = "Bakım İhtiyaç Oranı", Value = $"%{input.BakimIhtiyacOrani:0.##}" });
        result.Rows.Add(new CalculationResultRow { Key = "Aylık Efektif Maliyet", Value = result.AylikEffektifMaliyet.ToString("N2", tr) + " TL" });
        result.Rows.Add(new CalculationResultRow { Key = "Yıllık Toplam Maliyet", Value = result.YillikMaliyet.ToString("N2", tr) + " TL" });
        result.Rows.Add(new CalculationResultRow { Key = "İskonto Oranı", Value = $"%{input.YillikIskontoOrani:0.##} yıllık" });

        result.Note = "<strong>Yöntem:</strong> Aylık bakıcı maliyetinin, bakım ihtiyaç oranıyla çarpımı, yaralının kalan beklenen yaşamı süresince iskonto edilmiş bugünkü değer (annuity present value). " +
                      "<strong>Tablo:</strong> TRH 2010 — Türkiye Hayat Tablosu. " +
                      "<strong>Bakım İhtiyaç Oranı:</strong> Adli Tıp / Sağlık Kurulu raporu ile tespit edilir. " +
                      "(Örnek: günlük 6 saat refakatçi ≈ %25, 24 saat tam bakım = %100.) " +
                      "<strong>Mevzuat:</strong> TBK m.54. " +
                      "<strong>Önemli:</strong> Bu hesap aktüeryal tahmindir; mahkeme bilirkişi raporuyla kesinleşir.";

        return result;
    }
}
