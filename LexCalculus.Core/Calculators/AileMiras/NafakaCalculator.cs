using System.Globalization;
using LexCalculus.Core.Calculators.Common;
using LexCalculus.Core.Interfaces;
using LexCalculus.Core.Models.Calculators;
using LexCalculus.Core.Services;

namespace LexCalculus.Core.Calculators.AileMiras;

/// <summary>
/// Nafaka (alimony / child support) calculator. A single calculator dispatches on
/// nafaka türü (iştirak / yoksulluk / tedbir) and hesap türü (yeni belirleme /
/// artış).
///
/// Legal basis: TMK m.169 (tedbir), m.175 (yoksulluk), m.182 (iştirak), m.197,
/// m.364. The increase path uses the TÜFE 12-month average per the YHGK yerleşik
/// içtihat, reusing <see cref="ITUFEService"/> (the same source as kira artışı).
///
/// IMPORTANT: the output is a "tahmini önerilen nafaka", NOT a court decision.
/// The court exercises its discretion (TMK m.4); see the UI warning.
///
/// İştirak / yoksulluk coefficients are FormulaParameters (slug "nafaka") so an
/// admin can tune them without code changes. The asgari ücret floor reads the
/// global "*"/asgari-ucret-brut parameter.
/// </summary>
public sealed class NafakaCalculator : ICalculator<NafakaInput, NafakaResult>
{
    private const string Slug = "nafaka";

    private readonly IFormulaParameterService _params;
    private readonly ITUFEService _tufe;

    public NafakaCalculator(IFormulaParameterService parameters, ITUFEService tufe)
    {
        _params = parameters ?? throw new ArgumentNullException(nameof(parameters));
        _tufe = tufe ?? throw new ArgumentNullException(nameof(tufe));
    }

    public CalculatorMetadata Metadata { get; } = new()
    {
        Slug = Slug,
        Category = CalculatorCategory.AileMiras,
        Title = "Nafaka Hesaplama",
        ShortDescription = "TMK m.169/175/182 — iştirak, yoksulluk ve tedbir nafakası için tahmini önerilen aylık tutar; artışta TÜFE 12 aylık ortalama. Mahkeme takdir yetkisini kullanır.",
        LegalReference = "TMK m.175/182",
        Status = CalculatorStatus.Active,
        DisplayNumber = "23",
        Keywords = new[] { "nafaka", "iştirak nafakası", "yoksulluk nafakası", "tedbir nafakası", "nafaka artışı", "TÜFE" }
    };

    public async Task<NafakaResult> CalculateAsync(NafakaInput input, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        var result = new NafakaResult();

        if (input.HesapTuru == NafakaHesapTuru.Artis)
            return await ComputeArtisAsync(input, result, cancellationToken);

        return input.NafakaTuru switch
        {
            NafakaTuru.Istirak => await ComputeIstirakAsync(input, result, cancellationToken),
            _ => await ComputeYoksullukTedbirAsync(input, result, cancellationToken) // Yoksulluk + Tedbir
        };
    }

    // ----- İştirak nafakası (m.182) -----
    private async Task<NafakaResult> ComputeIstirakAsync(NafakaInput input, NafakaResult result, CancellationToken ct)
    {
        if (input.YukumluNetGelir is null or <= 0)
            result.ValidationErrors[nameof(input.YukumluNetGelir)] = "Yükümlü net geliri pozitif olmalıdır.";
        if (input.Cocuklar.Count == 0)
            result.ValidationErrors[nameof(input.Cocuklar)] = "En az bir çocuk girilmelidir.";

        if (result.ValidationErrors.Count > 0)
        {
            result.IsValid = false;
            return result;
        }

        var asOf = input.AsOfDate;
        var gelir = input.YukumluNetGelir!.Value;
        var cocukSayisi = input.Cocuklar.Count;

        var bazOran = cocukSayisi switch
        {
            1 => await GetParamAsync(Slug, "istirak.baz-oran.1cocuk", asOf, ct),
            2 => await GetParamAsync(Slug, "istirak.baz-oran.2cocuk", asOf, ct),
            _ => await GetParamAsync(Slug, "istirak.baz-oran.3plus", asOf, ct)
        };

        var sehirKats = input.Sehir == SehirTuru.Buyuksehir
            ? await GetParamAsync(Slug, "istirak.sehir-katsayisi.buyuksehir", asOf, ct)
            : 1.0m;

        var asgariUcret = await GetParamAsync("*", "asgari-ucret-brut", asOf, ct);
        var altSinir = Math.Round(asgariUcret * 0.25m, 2, MidpointRounding.AwayFromZero);

        var tr = new CultureInfo("tr-TR");
        decimal toplam = 0m;
        var i = 0;

        foreach (var cocuk in input.Cocuklar)
        {
            i++;
            var yasKats = cocuk.Yas <= 6
                ? await GetParamAsync(Slug, "istirak.yas-katsayisi.0-6", asOf, ct)
                : cocuk.Yas <= 11
                    ? await GetParamAsync(Slug, "istirak.yas-katsayisi.7-11", asOf, ct)
                    : await GetParamAsync(Slug, "istirak.yas-katsayisi.12-17", asOf, ct);

            var egitimKats = await GetParamAsync(Slug, $"istirak.egitim-katsayisi.{EgitimKey(cocuk.EgitimSeviyesi)}", asOf, ct);

            var ham = Math.Round(gelir * bazOran * yasKats * egitimKats * sehirKats, 2, MidpointRounding.AwayFromZero);
            var cocukNafaka = ham;
            if (cocukNafaka < altSinir)
            {
                cocukNafaka = altSinir;
                result.MinimumUygulandi = true;
            }
            toplam += cocukNafaka;

            result.Rows.Add(new CalculationResultRow
            {
                Key = $"{i}. çocuk ({cocuk.Yas} yaş, {EgitimAdi(cocuk.EgitimSeviyesi)})",
                Value = cocukNafaka.ToString("N2", tr) + " TL" + (cocukNafaka > ham ? " (alt sınır)" : "")
            });
        }

        toplam = Math.Round(toplam, 2, MidpointRounding.AwayFromZero);
        result.OnerilenAylikNafaka = toplam;

        result.Rows.Add(new CalculationResultRow { Key = "Yükümlü Net Gelir", Value = gelir.ToString("N2", tr) + " TL" });
        result.Rows.Add(new CalculationResultRow { Key = "Baz Oran (çocuk başına)", Value = $"%{(bazOran * 100m).ToString("0.##", tr)}" });
        result.Rows.Add(new CalculationResultRow { Key = "Asgari Ücret %25 Alt Sınırı", Value = altSinir.ToString("N2", tr) + " TL" });
        result.Rows.Add(new CalculationResultRow { Key = "Önerilen Toplam Aylık İştirak Nafakası", Value = toplam.ToString("N2", tr) + " TL", IsHighlighted = true });

        if (result.MinimumUygulandi)
            result.Warnings.Add("En az bir çocuk için hesaplanan tutar asgari ücretin %25'inin altında kaldığından alt sınır uygulanmıştır (Yargıtay yerleşik).");

        result.TotalAmount = toplam;
        result.TotalLabel = "Önerilen Aylık İştirak Nafakası";
        result.Unit = "TL";
        result.Note = IstirakNote();
        return result;
    }

    // ----- Yoksulluk (m.175) / Tedbir (m.169) nafakası -----
    private async Task<NafakaResult> ComputeYoksullukTedbirAsync(NafakaInput input, NafakaResult result, CancellationToken ct)
    {
        if (input.YuksekGelirEs is null or < 0)
            result.ValidationErrors[nameof(input.YuksekGelirEs)] = "Yüksek gelir negatif olamaz.";
        if (input.DusukGelirEs is null or < 0)
            result.ValidationErrors[nameof(input.DusukGelirEs)] = "Düşük gelir negatif olamaz.";
        if (input.EvlilikSuresiAy is null or < 0)
            result.ValidationErrors[nameof(input.EvlilikSuresiAy)] = "Evlilik süresi negatif olamaz.";

        if (result.ValidationErrors.Count > 0)
        {
            result.IsValid = false;
            return result;
        }

        var asOf = input.AsOfDate;
        var tedbir = input.NafakaTuru == NafakaTuru.Tedbir;
        var turAdi = tedbir ? "Tedbir" : "Yoksulluk";

        var gelirFarki = Math.Max(0m, input.YuksekGelirEs!.Value - input.DusukGelirEs!.Value);
        var oran = await GetParamAsync(Slug, "yoksulluk.oran", asOf, ct);
        var evlilikAy = input.EvlilikSuresiAy!.Value;

        var (evlilikKey, evlilikEtiket) = evlilikAy switch
        {
            < 36 => ("yoksulluk.evlilik-suresi.0-2", "0-2 yıl"),
            < 72 => ("yoksulluk.evlilik-suresi.3-5", "3-5 yıl"),
            < 132 => ("yoksulluk.evlilik-suresi.6-10", "6-10 yıl"),
            _ => ("yoksulluk.evlilik-suresi.11plus", "11+ yıl")
        };
        var evlilikKats = await GetParamAsync(Slug, evlilikKey, asOf, ct);

        var nafaka = Math.Round(gelirFarki * oran * evlilikKats, 2, MidpointRounding.AwayFromZero);
        result.OnerilenAylikNafaka = nafaka;

        var tr = new CultureInfo("tr-TR");
        result.Rows.Add(new CalculationResultRow { Key = "Gelir Farkı", Value = gelirFarki.ToString("N2", tr) + " TL" });
        result.Rows.Add(new CalculationResultRow { Key = "Temel Oran", Value = $"%{(oran * 100m).ToString("0.##", tr)}" });
        result.Rows.Add(new CalculationResultRow { Key = $"Evlilik Süresi Katsayısı ({evlilikEtiket})", Value = evlilikKats.ToString("0.##", tr) });
        result.Rows.Add(new CalculationResultRow { Key = $"Önerilen Aylık {turAdi} Nafakası", Value = nafaka.ToString("N2", tr) + " TL", IsHighlighted = true });

        if (gelirFarki == 0m)
            result.Warnings.Add("Eşlerin gelirleri arasında fark bulunmadığından (veya düşük gelirli eş daha yüksek olduğundan) yoksulluk nafakası doğmaz.");

        result.TotalAmount = nafaka;
        result.TotalLabel = $"Önerilen Aylık {turAdi} Nafakası";
        result.Unit = "TL";
        result.Note = tedbir ? TedbirNote() : YoksullukNote();
        return result;
    }

    // ----- Artış (TÜFE 12 aylık ortalama) -----
    private async Task<NafakaResult> ComputeArtisAsync(NafakaInput input, NafakaResult result, CancellationToken ct)
    {
        if (input.MevcutAylikNafaka is null or <= 0)
            result.ValidationErrors[nameof(input.MevcutAylikNafaka)] = "Mevcut aylık nafaka pozitif olmalıdır.";
        if (input.HesapTarihi is null)
            result.ValidationErrors[nameof(input.HesapTarihi)] = "Artış hesap tarihi boş olamaz.";

        if (result.ValidationErrors.Count > 0)
        {
            result.IsValid = false;
            return result;
        }

        var mevcut = input.MevcutAylikNafaka!.Value;
        var hesapTarihi = input.HesapTarihi!.Value;

        decimal tufeOrani;
        string tufeKaynagi;
        DateTime kullanilanAy;

        if (input.TufeOverride.HasValue)
        {
            tufeOrani = input.TufeOverride.Value;
            kullanilanAy = new DateTime(hesapTarihi.Year, hesapTarihi.Month, 1).AddMonths(-1);
            tufeKaynagi = "Manuel giriş (Override)";
            result.Warnings.Add("TÜFE oranı manuel olarak girildi. Lütfen TÜİK verisini doğrulayın: data.tuik.gov.tr");
        }
        else
        {
            var (oran, ay, bulundu) = await _tufe.GetKiraArtisOraniAsync(hesapTarihi, ct);
            if (!bulundu)
            {
                result.ValidationErrors[nameof(input.HesapTarihi)] =
                    $"{ay:MMMM yyyy} ayı için sistemde TÜFE verisi yok. " +
                    "Lütfen TÜİK'ten doğrulayıp 'TÜFE Override' alanına manuel girin: data.tuik.gov.tr";
                result.IsValid = false;
                return result;
            }
            tufeOrani = oran!.Value;
            kullanilanAy = ay;
            tufeKaynagi = "Sistem verisi (TÜİK referansı)";
        }

        var artisTutari = Math.Round(mevcut * (tufeOrani / 100m), 2, MidpointRounding.AwayFromZero);
        var guncel = Math.Round(mevcut + artisTutari, 2, MidpointRounding.AwayFromZero);

        result.OnerilenAylikNafaka = guncel;
        result.UygulananTufeOrani = tufeOrani;

        var tr = new CultureInfo("tr-TR");
        result.Rows.Add(new CalculationResultRow { Key = "Mevcut Aylık Nafaka", Value = mevcut.ToString("N2", tr) + " TL" });
        result.Rows.Add(new CalculationResultRow { Key = $"TÜFE 12 Ay Ort. ({kullanilanAy:MMMM yyyy})", Value = $"%{tufeOrani.ToString("0.##", tr)}" });
        result.Rows.Add(new CalculationResultRow { Key = "TÜFE Kaynağı", Value = tufeKaynagi });
        result.Rows.Add(new CalculationResultRow { Key = "Artış Tutarı", Value = artisTutari.ToString("N2", tr) + " TL" });
        result.Rows.Add(new CalculationResultRow { Key = "Güncellenmiş Aylık Nafaka", Value = guncel.ToString("N2", tr) + " TL", IsHighlighted = true });

        result.TotalAmount = guncel;
        result.TotalLabel = "Güncellenmiş Aylık Nafaka";
        result.Unit = "TL";
        result.Note = ArtisNote();
        return result;
    }

    /// <summary>
    /// Reads a coefficient parameter; a missing one is an admin/config error
    /// (the seeder must provide all of them), so we fail loudly — mirrors the
    /// Arsa Payı katsayı contract.
    /// </summary>
    private async Task<decimal> GetParamAsync(string slug, string key, DateTime asOf, CancellationToken ct)
    {
        var value = await _params.GetValueAsync(slug, key, asOf, ct);
        return value ?? throw new InvalidOperationException(
            $"Nafaka parametresi yok: ({slug}, {key}, {asOf:yyyy-MM-dd}). " +
            $"Yönetici '{slug}/{key}' parametresini eklemelidir.");
    }

    private static string EgitimKey(EgitimSeviyesi s) => s switch
    {
        EgitimSeviyesi.Anaokul => "anaokul",
        EgitimSeviyesi.Ilkokul => "ilkokul",
        EgitimSeviyesi.Ortaokul => "ortaokul",
        EgitimSeviyesi.Lise => "lise",
        EgitimSeviyesi.Universite => "universite",
        _ => "anaokul"
    };

    private static string EgitimAdi(EgitimSeviyesi s) => s switch
    {
        EgitimSeviyesi.Anaokul => "anaokul",
        EgitimSeviyesi.Ilkokul => "ilkokul",
        EgitimSeviyesi.Ortaokul => "ortaokul",
        EgitimSeviyesi.Lise => "lise",
        EgitimSeviyesi.Universite => "üniversite",
        _ => s.ToString()
    };

    private const string WarningTail =
        "<strong>Önemli:</strong> Bu hesap referans niteliğindedir. Mahkeme TMK m.4 takdir yetkisini kullanır. " +
        "Somut davada çocuğun özel ihtiyaçları (sağlık, özel eğitim), yükümlünün diğer bakmakla yükümlü olduğu kişiler, " +
        "gelir belgeleme durumu gibi faktörler değerlendirilir. Bu sonuç mahkeme kararı yerine geçmez.";

    private static string IstirakNote() =>
        "<strong>İştirak nafakası (TMK m.182):</strong> Yükümlünün net gelirinden çocuk sayısı, yaş, eğitim ve ikamet katsayılarıyla hesaplanan tahmini tutar; asgari ücretin %25'i alt sınırdır. " + WarningTail;

    private static string YoksullukNote() =>
        "<strong>Yoksulluk nafakası (TMK m.175):</strong> Eşler arasındaki gelir farkına ve evlilik süresine göre hesaplanan tahmini tutar (YHGK yerleşik içtihat). " + WarningTail;

    private static string TedbirNote() =>
        "<strong>Tedbir nafakası (TMK m.169):</strong> Dava süresince geçerli GEÇİCİ nitelikte tutar; yoksulluk nafakası formülüyle hesaplanır, dava sonunda yeniden değerlendirilir. " + WarningTail;

    private static string ArtisNote() =>
        "<strong>Nafaka artışı:</strong> Mevcut nafaka, bir önceki dönemin TÜFE 12 aylık ortalama değişim oranıyla güncellenir (YHGK yerleşik içtihat). " + WarningTail;
}
