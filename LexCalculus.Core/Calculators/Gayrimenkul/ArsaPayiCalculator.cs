using System.Globalization;
using LexCalculus.Core.Calculators.Common;
using LexCalculus.Core.Interfaces;
using LexCalculus.Core.Models.Calculators;

namespace LexCalculus.Core.Calculators.Gayrimenkul;

/// <summary>
/// Land share calculator (Arsa Payı).
///
/// Legal basis: 634 s.K. (Kat Mülkiyeti Kanunu) m.3. Arsa payları, bağımsız
/// bölümlerin <em>değerleri</em> ile oranlı olarak bölüştürülür. This tool uses
/// the value-weighted floor area method commonly applied by court experts:
/// each unit's weight = floor area × usage coefficient × floor coefficient, and
/// the unit's share is its weight over the total, expressed out of 1000.
///
/// Coefficients are stored as FormulaParameters so an admin can tune them
/// without code changes; they default to neutral (mesken/zemin = 1.0). This is
/// a simplified model — the actual değer takdiri may weigh location, view, and
/// rayiç further (see the warning note in the UI).
/// </summary>
public sealed class ArsaPayiCalculator : ICalculator<ArsaPayiInput, ArsaPayiResult>
{
    private const string Slug = "arsa-payi";
    private const string ParamMesken = "katsayi.mesken";
    private const string ParamDukkan = "katsayi.dukkan";
    private const string ParamBodrum = "katsayi.bodrum";
    private const string ParamCatiKati = "katsayi.cati-kati";
    private const string ParamKatZemin = "katsayi.kat.zemin";
    private const string ParamKatUstArtis = "katsayi.kat.ust-artis-orani";

    private readonly IFormulaParameterService _params;

    public ArsaPayiCalculator(IFormulaParameterService parameters)
    {
        _params = parameters ?? throw new ArgumentNullException(nameof(parameters));
    }

    public CalculatorMetadata Metadata { get; } = new()
    {
        Slug = Slug,
        Category = CalculatorCategory.Gayrimenkul,
        Title = "Arsa Payı Hesaplama",
        ShortDescription = "634 s.K. Kat Mülkiyeti Kanunu m.3 — değer ağırlıklı yüzölçümü yöntemiyle bağımsız bölümlerin arsa paylarının 1000 üzerinden dağıtımı.",
        LegalReference = "634 s.K. m.3",
        Status = CalculatorStatus.Active,
        DisplayNumber = "18",
        Keywords = new[] { "arsa payı", "kat mülkiyeti", "634", "bağımsız bölüm", "kat irtifakı" }
    };

    public async Task<ArsaPayiResult> CalculateAsync(ArsaPayiInput input, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        var result = new ArsaPayiResult();

        var bolumler = input.BagimsizBolumler ?? new List<BagimsizBolumGirdi>();

        if (bolumler.Count == 0)
        {
            result.ValidationErrors[nameof(input.BagimsizBolumler)] = "En az bir bağımsız bölüm girilmelidir.";
        }

        for (var i = 0; i < bolumler.Count; i++)
        {
            var bb = bolumler[i];
            if (bb.Yuzolcumu is null or <= 0)
            {
                result.ValidationErrors[$"{nameof(input.BagimsizBolumler)}[{i}].{nameof(BagimsizBolumGirdi.Yuzolcumu)}"] =
                    $"{i + 1}. bağımsız bölümün yüzölçümü pozitif olmalıdır.";
            }
        }

        if (result.ValidationErrors.Count > 0)
        {
            result.IsValid = false;
            return result;
        }

        var asOf = input.AsOfDate;
        var meskenKats = await GetCoefficientAsync(ParamMesken, asOf, cancellationToken);
        var dukkanKats = await GetCoefficientAsync(ParamDukkan, asOf, cancellationToken);
        var bodrumKats = await GetCoefficientAsync(ParamBodrum, asOf, cancellationToken);
        var catiKats = await GetCoefficientAsync(ParamCatiKati, asOf, cancellationToken);
        var katZeminKats = await GetCoefficientAsync(ParamKatZemin, asOf, cancellationToken);
        var katUstArtisOrani = await GetCoefficientAsync(ParamKatUstArtis, asOf, cancellationToken);

        var araSonuc = new List<(BagimsizBolumGirdi Girdi, decimal Kullanim, decimal Kat, decimal Agirlik)>();
        foreach (var bb in bolumler)
        {
            var kullanimKats = bb.KullanimTuru switch
            {
                KullanimTuru.Mesken => meskenKats,
                KullanimTuru.Dukkan => dukkanKats,
                KullanimTuru.Bodrum => bodrumKats,
                KullanimTuru.CatiKati => catiKats,
                _ => 1.0m
            };

            // Zemin ve altı (bodrum) zemin katsayısını alır; her üst kat artış oranı kadar eklenir.
            var katKats = bb.KatNumarasi <= 0
                ? katZeminKats
                : katZeminKats + (bb.KatNumarasi * katUstArtisOrani);

            var agirlik = bb.Yuzolcumu!.Value * kullanimKats * katKats;
            araSonuc.Add((bb, kullanimKats, katKats, agirlik));
        }

        var toplamAgirlik = araSonuc.Sum(x => x.Agirlik);

        var paylar = araSonuc.Select(x => new BagimsizBolumPay
        {
            Tanim = x.Girdi.Tanim ?? string.Empty,
            Yuzolcumu = x.Girdi.Yuzolcumu!.Value,
            KullanimTuru = x.Girdi.KullanimTuru,
            KatNumarasi = x.Girdi.KatNumarasi,
            AgirliklikDeger = Math.Round(x.Agirlik, 4, MidpointRounding.AwayFromZero),
            Pay1000 = Math.Round(x.Agirlik / toplamAgirlik * 1000m, 2, MidpointRounding.AwayFromZero)
        }).ToList();

        result.Paylar = paylar;
        result.ToplamAgirliklikDeger = Math.Round(toplamAgirlik, 4, MidpointRounding.AwayFromZero);
        result.ToplamPay = paylar.Sum(p => p.Pay1000);

        var tr = new CultureInfo("tr-TR");
        result.TotalAmount = result.ToplamPay;
        result.TotalLabel = "Toplam Arsa Payı";
        result.Unit = "/1000";

        foreach (var p in paylar)
        {
            result.Rows.Add(new CalculationResultRow
            {
                Key = $"{p.Tanim} ({KullanimAdi(p.KullanimTuru)}, {KatAdi(p.KatNumarasi)})",
                Value = $"{p.Pay1000.ToString("N2", tr)} / 1000"
            });
        }

        result.Rows.Add(new CalculationResultRow
        {
            Key = "Toplam Ağırlıklı Değer",
            Value = result.ToplamAgirliklikDeger.ToString("N4", tr),
            IsHighlighted = true
        });

        // Yuvarlama nedeniyle toplam 1000'den ±0.5 sapabilir; net sapmada uyar.
        var sapma = Math.Abs(result.ToplamPay - 1000m);
        if (sapma > 0.5m)
        {
            result.Warnings.Add($"Yuvarlama nedeniyle payların toplamı {result.ToplamPay.ToString("N2", tr)} / 1000 oldu. " +
                                "Tapu tescilinde toplam tam 1000 olacak şekilde en büyük paya küsurat eklenebilir.");
        }

        result.Note = "<strong>Yöntem:</strong> Değer ağırlıklı yüzölçümü " +
                      "(yüzölçümü × kullanım türü katsayısı × kat etkisi katsayısı). " +
                      "<strong>Mevzuat:</strong> 634 s.K. (Kat Mülkiyeti Kanunu) m.3 — arsa payları bağımsız bölümlerin değerleriyle oranlı dağıtılır. " +
                      "Bu hesap standart katsayılarla üretilen bir ön değerlendirmedir; somut olayda konum, manzara ve rayiç değer ek katsayılarla değerlendirilebilir.";

        return result;
    }

    /// <summary>
    /// Reads a coefficient parameter. A missing coefficient is an admin/config
    /// error (the seeder must provide all six), so we fail loudly — mirrors the
    /// Kıdem "tavan" parameter contract.
    /// </summary>
    private async Task<decimal> GetCoefficientAsync(string key, DateTime asOf, CancellationToken ct)
    {
        var value = await _params.GetValueAsync(Slug, key, asOf, ct);
        return value ?? throw new InvalidOperationException(
            $"Arsa payı katsayı parametresi yok: ({Slug}, {key}, {asOf:yyyy-MM-dd}). " +
            $"Yönetici '{Slug}/{key}' parametresini eklemelidir.");
    }

    private static string KullanimAdi(KullanimTuru t) => t switch
    {
        KullanimTuru.Mesken => "Mesken",
        KullanimTuru.Dukkan => "Dükkan",
        KullanimTuru.Bodrum => "Bodrum",
        KullanimTuru.CatiKati => "Çatı Katı",
        _ => t.ToString()
    };

    private static string KatAdi(int kat) => kat switch
    {
        < 0 => $"{-kat}. bodrum",
        0 => "zemin",
        _ => $"{kat}. kat"
    };
}
