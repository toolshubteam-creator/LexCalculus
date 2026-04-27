using System.Globalization;
using LexCalculus.Core.Calculators.Common;
using LexCalculus.Core.Enums;
using LexCalculus.Core.Interfaces;
using LexCalculus.Core.Models.Calculators;

namespace LexCalculus.Core.Calculators.Akturya;

/// <summary>
/// Loss of support calculator (Destekten Yoksun Kalma Tazminatı).
///
/// Legal basis: TBK m.53 + Yargıtay Hukuk Genel Kurulu kararları.
///
/// Methodology:
///   1. Determine deceased's expected lifetime (TRH 2010, eX)
///   2. Active period: deceased's age → retirement age (65)
///   3. Passive period: 65 → eX-implied death age, income reduced (default 50%)
///   4. Family share allocation per Yargıtay HGK case law:
///      - Spouse only:                  spouse 50%
///      - Spouse + 1 child:              spouse 25%, child 25%
///      - Spouse + 2 children:           spouse 20%, each child 20%
///      - Spouse + 3 children:           spouse 15%, each child 15%
///      - Spouse + 4+ children:          spouse 15%, each child capped (total ≤75%)
///      - Children only:                 total 50% split equally
///   5. Each child receives support until INDIVIDUAL independence age:
///      - Boy: 18 (or 25 if student)
///      - Girl: 22 (or 25 if student)
///      Capped to deceased's remaining lifetime
///   6. Per-child Annuity PV via IActuarialService
///   7. Sum: total compensation = spouse + sum of children
///
/// CRITICAL: Children entered INDIVIDUALLY (each with own birth date and
/// student status). A 5yo and 15yo receive 13 vs 3 years of support — they
/// cannot be averaged without ~30% accuracy loss.
/// </summary>
public sealed class DesteKtenYoksunKalmaCalculator
    : ICalculator<DesteKtenYoksunKalmaInput, DesteKtenYoksunKalmaResult>
{
    private const int EmeklilikYasi = 65;
    private const int ErkekBagimsizlikYasi = 18;
    private const int KizBagimsizlikYasi = 22;
    private const int OgrenciBagimsizlikYasi = 25;

    private readonly ILifeTableService _lifeTable;
    private readonly IActuarialService _actuarial;

    public DesteKtenYoksunKalmaCalculator(ILifeTableService lifeTable, IActuarialService actuarial)
    {
        _lifeTable = lifeTable ?? throw new ArgumentNullException(nameof(lifeTable));
        _actuarial = actuarial ?? throw new ArgumentNullException(nameof(actuarial));
    }

    public CalculatorMetadata Metadata { get; } = new()
    {
        Slug = "destekten-yoksun-kalma",
        Category = CalculatorCategory.Akturya,
        Title = "Destekten Yoksun Kalma Tazminatı",
        ShortDescription = "TRH 2010 yaşam tablosu, aktif/pasif dönem progresif rant, eş ve bireysel çocuk paylarıyla aktüeryal hesap.",
        LegalReference = "TBK m.53 / Yargıtay HGK",
        Status = CalculatorStatus.Active,
        DisplayNumber = "08",
        Keywords = new[] { "destekten yoksun kalma", "destek tazminatı", "aktüerya", "TRH 2010" }
    };

    public async Task<DesteKtenYoksunKalmaResult> CalculateAsync(
        DesteKtenYoksunKalmaInput input, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        var result = new DesteKtenYoksunKalmaResult();

        var cocuklar = input.Cocuklar ?? new List<CocukInput>();

        if (input.OlayTarihi is null)
            result.ValidationErrors[nameof(input.OlayTarihi)] = "Olay tarihi boş olamaz.";
        if (input.OlenDogumTarihi is null)
            result.ValidationErrors[nameof(input.OlenDogumTarihi)] = "Ölen kişinin doğum tarihi boş olamaz.";
        if (input.AylikGelir is null or <= 0)
            result.ValidationErrors[nameof(input.AylikGelir)] = "Aylık gelir pozitif olmalıdır.";

        if (input.OlayTarihi is not null && input.OlenDogumTarihi is not null
            && input.OlayTarihi < input.OlenDogumTarihi)
        {
            result.ValidationErrors[nameof(input.OlayTarihi)] = "Olay tarihi doğum tarihinden sonra olmalıdır.";
        }

        if (input.EsVarMi && input.EsDogumTarihi is null)
        {
            result.ValidationErrors[nameof(input.EsDogumTarihi)] = "Eş seçilmişse eş doğum tarihi gereklidir.";
        }

        if (!input.EsVarMi && cocuklar.Count == 0)
        {
            result.ValidationErrors[nameof(input.EsVarMi)] = "En az bir destek alacak yakın (eş veya çocuk) belirtilmelidir.";
        }

        for (int i = 0; i < cocuklar.Count; i++)
        {
            var c = cocuklar[i];
            if (c.DogumTarihi is null)
            {
                result.ValidationErrors[$"Cocuklar[{i}].{nameof(CocukInput.DogumTarihi)}"] = $"Çocuk #{i + 1} doğum tarihi boş olamaz.";
            }
            else if (input.OlayTarihi is not null && c.DogumTarihi > input.OlayTarihi)
            {
                result.ValidationErrors[$"Cocuklar[{i}].{nameof(CocukInput.DogumTarihi)}"] = $"Çocuk #{i + 1} doğum tarihi olay tarihinden sonra olamaz.";
            }
        }

        if (result.ValidationErrors.Count > 0)
        {
            result.IsValid = false;
            return result;
        }

        var olay = input.OlayTarihi!.Value;
        var olenDogum = input.OlenDogumTarihi!.Value;
        var aylikGelir = input.AylikGelir!.Value;
        var iskonto = input.YillikIskontoOrani / 100m;
        var pasifOran = input.PasifDonemGelirOrani / 100m;

        var olenYas = _actuarial.HesaplaYas(olenDogum, olay);
        var olenEx = await _lifeTable.GetBekledigiYasamAsync(olenYas, input.OlenCinsiyet, ct: cancellationToken);

        if (olenEx is null)
        {
            result.ValidationErrors[nameof(input.OlenDogumTarihi)] =
                $"Ölen kişi yaşı ({olenYas}) için TRH 2010 yaşam tablosunda veri bulunamadı.";
            result.IsValid = false;
            return result;
        }

        var aktifYil = _actuarial.AktifDonemYili(olenYas, EmeklilikYasi);
        var pasifYil = _actuarial.PasifDonemYili(olenYas, olenEx.Value, EmeklilikYasi);
        var olenKalanYasam = aktifYil + pasifYil;

        var yillikGelir = aylikGelir * 12m;

        var (esPay, cocukPay) = HesaplaPaylar(input.EsVarMi, cocuklar.Count);

        decimal esAktif = 0m, esPasif = 0m;
        if (input.EsVarMi && esPay > 0)
        {
            var esYillikPay = yillikGelir * esPay;
            esAktif = _actuarial.AnnuityPresentValue(esYillikPay, aktifYil, iskonto);
            esPasif = _actuarial.AnnuityPresentValue(esYillikPay * pasifOran, pasifYil, iskonto);
        }

        var cocukDetaylar = new List<CocukDetay>();
        decimal cocuklarToplam = 0m;

        if (cocuklar.Count > 0 && cocukPay > 0)
        {
            var perCocukYillikPay = yillikGelir * cocukPay;

            foreach (var cocuk in cocuklar)
            {
                var cocukYas = _actuarial.HesaplaYas(cocuk.DogumTarihi!.Value, olay);

                int bagimsizlikYasi;
                if (cocuk.Ogrenci)
                {
                    bagimsizlikYasi = OgrenciBagimsizlikYasi;
                }
                else
                {
                    bagimsizlikYasi = cocuk.Cinsiyet == Cinsiyet.Erkek ? ErkekBagimsizlikYasi : KizBagimsizlikYasi;
                }

                var destekSuresi = Math.Max(0, bagimsizlikYasi - cocukYas);
                destekSuresi = Math.Min(destekSuresi, olenKalanYasam);

                var pv = _actuarial.AnnuityPresentValue(perCocukYillikPay, destekSuresi, iskonto);

                var cinsiyetAdi = cocuk.Cinsiyet == Cinsiyet.Erkek ? "Erkek" : "Kız";
                var ogrenciNotu = cocuk.Ogrenci ? " (öğrenci)" : "";
                var aciklama = $"{cinsiyetAdi} çocuk{ogrenciNotu}, olay tarihinde {cocukYas} yaş — {bagimsizlikYasi} yaşa kadar";

                cocukDetaylar.Add(new CocukDetay
                {
                    Cinsiyet = cocuk.Cinsiyet,
                    OlayAnindakiYas = cocukYas,
                    BagimsizlikYasi = bagimsizlikYasi,
                    DestekSuresi = destekSuresi,
                    Tutar = pv,
                    Aciklama = aciklama
                });
                cocuklarToplam += pv;
            }
        }

        var esToplam = esAktif + esPasif;
        var toplam = esToplam + cocuklarToplam;

        result.OlenYas = olenYas;
        result.OlenBekledigiYasam = olenEx.Value;
        result.AktifYil = aktifYil;
        result.PasifYil = pasifYil;
        result.EsPay = esPay;
        result.EsPayTutarAktif = Math.Round(esAktif, 2);
        result.EsPayTutarPasif = Math.Round(esPasif, 2);
        result.EsToplamTutar = Math.Round(esToplam, 2);
        result.CocukSayisi = cocuklar.Count;
        result.CocukPayPerCocuk = cocukPay;
        result.CocukDetaylar = cocukDetaylar;
        result.CocuklarToplamTutar = Math.Round(cocuklarToplam, 2);
        result.ToplamTazminat = Math.Round(toplam, 2);

        var tr = new CultureInfo("tr-TR");
        result.TotalAmount = result.ToplamTazminat;
        result.TotalLabel = "Toplam Destekten Yoksun Kalma Tazminatı";
        result.Unit = "TL";

        result.Rows.Add(new CalculationResultRow { Key = "Ölen Kişi Yaşı", Value = $"{olenYas} ({input.OlenCinsiyet})" });
        result.Rows.Add(new CalculationResultRow { Key = "Beklenen Yaşam (TRH 2010)", Value = $"{olenEx.Value:F2} yıl" });
        result.Rows.Add(new CalculationResultRow { Key = "Aktif Dönem", Value = $"{aktifYil} yıl" });
        result.Rows.Add(new CalculationResultRow { Key = "Pasif Dönem", Value = $"{pasifYil} yıl" });
        result.Rows.Add(new CalculationResultRow { Key = "Yıllık Gelir", Value = yillikGelir.ToString("N2", tr) + " TL" });
        result.Rows.Add(new CalculationResultRow { Key = "İskonto Oranı", Value = $"%{input.YillikIskontoOrani:0.##} yıllık" });

        if (input.EsVarMi)
        {
            result.Rows.Add(new CalculationResultRow { Key = "Eş Payı", Value = $"%{esPay * 100:0.##}" });
            result.Rows.Add(new CalculationResultRow { Key = "Eş Aktif Dönem PV", Value = result.EsPayTutarAktif.ToString("N2", tr) + " TL" });
            result.Rows.Add(new CalculationResultRow { Key = "Eş Pasif Dönem PV", Value = result.EsPayTutarPasif.ToString("N2", tr) + " TL" });
            result.Rows.Add(new CalculationResultRow { Key = "Eş Toplam", Value = result.EsToplamTutar.ToString("N2", tr) + " TL", IsHighlighted = true });
        }

        if (cocuklar.Count > 0)
        {
            result.Rows.Add(new CalculationResultRow { Key = "Çocuk Sayısı", Value = cocuklar.Count.ToString() });
            result.Rows.Add(new CalculationResultRow { Key = "Her Çocuk Payı", Value = $"%{cocukPay * 100:0.##}" });
            result.Rows.Add(new CalculationResultRow { Key = "Çocuklar Toplam PV", Value = result.CocuklarToplamTutar.ToString("N2", tr) + " TL", IsHighlighted = true });
        }

        result.Note = "<strong>Yöntem:</strong> Yıllık net gelirin aktif (çalışma) ve pasif (emeklilik, %" +
                      input.PasifDonemGelirOrani + ") dönemlerde aile üyelerine düşen paylarının iskonto edilmiş bugünkü değeri (annuity present value). " +
                      "<strong>Tablo:</strong> TRH 2010 — Türkiye Hayat Tablosu (Hazine Müsteşarlığı). " +
                      "<strong>Çocuk Bağımsızlık Yaşı:</strong> Erkek 18, kız 22, öğrenci 25. Her çocuk için bireysel hesaplanır. " +
                      "<strong>Mevzuat:</strong> TBK m.53 + Yargıtay HGK içtihatları. " +
                      "<strong>Önemli:</strong> Bu hesap aktüeryal tahmindir; mahkeme bilirkişi raporuyla kesinleşir.";

        return result;
    }

    private static (decimal esPay, decimal cocukPay) HesaplaPaylar(bool esVarMi, int cocukSayisi)
    {
        if (!esVarMi && cocukSayisi == 0) return (0m, 0m);

        if (esVarMi && cocukSayisi == 0) return (0.50m, 0m);

        if (!esVarMi && cocukSayisi > 0)
        {
            return (0m, 0.50m / cocukSayisi);
        }

        return cocukSayisi switch
        {
            1 => (0.25m, 0.25m),
            2 => (0.20m, 0.20m),
            3 => (0.15m, 0.15m),
            _ => (0.15m, Math.Min(0.15m, (0.75m - 0.15m) / cocukSayisi))
        };
    }
}
