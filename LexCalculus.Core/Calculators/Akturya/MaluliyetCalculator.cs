using System.Globalization;
using LexCalculus.Core.Calculators.Common;
using LexCalculus.Core.Interfaces;
using LexCalculus.Core.Models.Calculators;

namespace LexCalculus.Core.Calculators.Akturya;

/// <summary>
/// Disability compensation calculator (Maluliyet / İş Gücü Kaybı Tazminatı).
///
/// Legal basis: TBK m.54 + 5510 s.K. (SGK).
///
/// Methodology:
///   1. Determine injured person's expected lifetime (TRH 2010, eX)
///   2. Active period: current age → 65
///      Yearly loss = annual income × loss-of-earning-capacity ratio
///   3. Passive period: 65 → eX-implied death age
///      Yearly loss = annual income × loss ratio × passive income ratio (default 50%)
///   4. Annuity PV for both periods
///   5. Total compensation = active PV + passive PV
///
/// Note: Loss-of-earning-capacity ratio is determined by Adli Tıp Kurumu /
/// SGK medical board reports; the calculator accepts it as user input.
/// </summary>
public sealed class MaluliyetCalculator : ICalculator<MaluliyetInput, MaluliyetResult>
{
    private const int EmeklilikYasi = 65;

    private readonly ILifeTableService _lifeTable;
    private readonly IActuarialService _actuarial;

    public MaluliyetCalculator(ILifeTableService lifeTable, IActuarialService actuarial)
    {
        _lifeTable = lifeTable ?? throw new ArgumentNullException(nameof(lifeTable));
        _actuarial = actuarial ?? throw new ArgumentNullException(nameof(actuarial));
    }

    public CalculatorMetadata Metadata { get; } = new()
    {
        Slug = "maluliyet-tazminati",
        Category = CalculatorCategory.Akturya,
        Title = "Maluliyet Tazminatı",
        ShortDescription = "İş gücü kaybı oranına göre kalan ömür boyunca aktüeryal tazminat hesabı (TRH 2010 + peşin değer).",
        LegalReference = "TBK m.54 / 5510 s.K.",
        Status = CalculatorStatus.Active,
        DisplayNumber = "09",
        Keywords = new[] { "maluliyet", "iş gücü kaybı", "aktüerya", "tazminat" }
    };

    public async Task<MaluliyetResult> CalculateAsync(MaluliyetInput input, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        var result = new MaluliyetResult();

        if (input.OlayTarihi is null)
            result.ValidationErrors[nameof(input.OlayTarihi)] = "Olay tarihi boş olamaz.";
        if (input.YaralananDogumTarihi is null)
            result.ValidationErrors[nameof(input.YaralananDogumTarihi)] = "Yaralanan kişi doğum tarihi boş olamaz.";
        if (input.AylikGelir is null or <= 0)
            result.ValidationErrors[nameof(input.AylikGelir)] = "Gelir pozitif olmalıdır.";
        if (input.IsGucuKaybiOrani is null or <= 0 or > 100)
            result.ValidationErrors[nameof(input.IsGucuKaybiOrani)] = "İş gücü kaybı oranı %0.1-100 arası olmalıdır.";

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
        var aylik = input.AylikGelir!.Value;
        var kayipOran = input.IsGucuKaybiOrani!.Value / 100m;
        var iskonto = input.YillikIskontoOrani / 100m;
        var pasifOran = input.PasifDonemGelirOrani / 100m;

        var yas = _actuarial.HesaplaYas(dogum, olay);
        var ex = await _lifeTable.GetBekledigiYasamAsync(yas, input.Cinsiyet, ct: cancellationToken);

        if (ex is null)
        {
            result.ValidationErrors[nameof(input.YaralananDogumTarihi)] =
                $"Yaralanan kişinin yaşı ({yas}) için TRH 2010 yaşam tablosunda veri bulunamadı.";
            result.IsValid = false;
            return result;
        }

        var aktifYil = _actuarial.AktifDonemYili(yas, EmeklilikYasi);
        var pasifYil = _actuarial.PasifDonemYili(yas, ex.Value, EmeklilikYasi);

        var yillikGelir = aylik * 12m;
        var yillikKayip = yillikGelir * kayipOran;

        var aktifPV = _actuarial.AnnuityPresentValue(yillikKayip, aktifYil, iskonto);
        var pasifPV = _actuarial.AnnuityPresentValue(yillikKayip * pasifOran, pasifYil, iskonto);
        var toplam = aktifPV + pasifPV;

        result.YaralananYas = yas;
        result.BekledigiYasam = ex.Value;
        result.AktifYil = aktifYil;
        result.PasifYil = pasifYil;
        result.YillikGelir = yillikGelir;
        result.YillikKayip = yillikKayip;
        result.AktifPV = Math.Round(aktifPV, 2);
        result.PasifPV = Math.Round(pasifPV, 2);
        result.ToplamTazminat = Math.Round(toplam, 2);

        var tr = new CultureInfo("tr-TR");
        result.TotalAmount = result.ToplamTazminat;
        result.TotalLabel = "Toplam Maluliyet Tazminatı";
        result.Unit = "TL";

        result.Rows.Add(new CalculationResultRow { Key = "Yaralanan Yaş", Value = $"{yas} ({input.Cinsiyet})" });
        result.Rows.Add(new CalculationResultRow { Key = "Beklenen Yaşam (TRH 2010)", Value = $"{ex.Value:F2} yıl" });
        result.Rows.Add(new CalculationResultRow { Key = "Aktif Dönem", Value = $"{aktifYil} yıl" });
        result.Rows.Add(new CalculationResultRow { Key = "Pasif Dönem", Value = $"{pasifYil} yıl" });
        result.Rows.Add(new CalculationResultRow { Key = "Yıllık Gelir", Value = yillikGelir.ToString("N2", tr) + " TL" });
        result.Rows.Add(new CalculationResultRow { Key = "İş Gücü Kaybı Oranı", Value = $"%{input.IsGucuKaybiOrani:0.##}" });
        result.Rows.Add(new CalculationResultRow { Key = "Yıllık Kayıp", Value = yillikKayip.ToString("N2", tr) + " TL" });
        result.Rows.Add(new CalculationResultRow { Key = "İskonto Oranı", Value = $"%{input.YillikIskontoOrani:0.##} yıllık" });
        result.Rows.Add(new CalculationResultRow { Key = "Aktif Dönem PV", Value = result.AktifPV.ToString("N2", tr) + " TL" });
        result.Rows.Add(new CalculationResultRow { Key = "Pasif Dönem PV", Value = result.PasifPV.ToString("N2", tr) + " TL" });

        result.Note = "<strong>Yöntem:</strong> Yıllık gelirin iş gücü kaybı oranıyla çarpımı, aktif (çalışma) ve pasif (emeklilik, %" +
                      input.PasifDonemGelirOrani + ") dönemleri için iskonto edilmiş bugünkü değer (annuity present value). " +
                      "<strong>Tablo:</strong> TRH 2010 — Türkiye Hayat Tablosu. " +
                      "<strong>İş Gücü Kaybı:</strong> Adli Tıp Kurumu / SGK Sağlık Kurulu raporu ile tespit edilir. " +
                      "<strong>Mevzuat:</strong> TBK m.54 + 5510 s.K. " +
                      "<strong>Önemli:</strong> Bu hesap aktüeryal tahmindir; mahkeme bilirkişi raporuyla kesinleşir.";

        return result;
    }
}
