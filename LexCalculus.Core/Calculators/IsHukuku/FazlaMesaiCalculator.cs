using System.Globalization;
using LexCalculus.Core.Calculators.Common;
using LexCalculus.Core.Interfaces;
using LexCalculus.Core.Models.Calculators;

namespace LexCalculus.Core.Calculators.IsHukuku;

/// <summary>
/// Overtime pay calculator (Fazla Mesai Alacağı).
///
/// Legal basis: 4857 s.K. m.41.
///
/// Pay multipliers:
///   - Regular overtime (over 45 weekly hours): 1.5×
///   - Weekly rest day work: 2.0×
///   - National holiday / public holiday work: 2.0×
///
/// Hourly wage formula (Yargıtay-accepted):
///   hourlyWage = monthlyBrut / (weeklyHours × 4.33)
///
/// 4.33 ≈ average weeks per month (52/12).
///
/// Tax treatment: stamp duty + income tax (Phase 2 flat 15%).
///
/// Statute of limitations (m.32): 5 years for overtime claims.
/// </summary>
public sealed class FazlaMesaiCalculator : ICalculator<FazlaMesaiInput, FazlaMesaiResult>
{
    private const string Slug = "fazla-mesai";
    private const decimal HaftaBasinaAyOranı = 4.33m;
    private const decimal FazlaMesaiZamOrani = 1.5m;
    private const decimal HaftaTatiliZamOrani = 2.0m;
    private const decimal BayramZamOrani = 2.0m;

    private readonly IFormulaParameterService _params;

    public FazlaMesaiCalculator(IFormulaParameterService parameters)
    {
        _params = parameters ?? throw new ArgumentNullException(nameof(parameters));
    }

    public CalculatorMetadata Metadata { get; } = new()
    {
        Slug = Slug,
        Category = CalculatorCategory.IsHukuku,
        Title = "Fazla Mesai Alacağı",
        ShortDescription = "45 saati aşan mesai %50 zamlı, hafta tatili %100, ulusal bayram %100 — kümülatif hesap.",
        LegalReference = "4857 s.K. m.41",
        Status = CalculatorStatus.Active,
        DisplayNumber = "04",
        Keywords = new[] { "fazla mesai", "mesai", "iş hukuku", "hafta tatili", "bayram" }
    };

    public async Task<FazlaMesaiResult> CalculateAsync(FazlaMesaiInput input, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        var result = new FazlaMesaiResult();

        if (input.BrutAylikUcret is null or <= 0)
            result.ValidationErrors[nameof(input.BrutAylikUcret)] = "Brüt ücret pozitif olmalıdır.";
        if (input.HesapTarihi is null)
            result.ValidationErrors[nameof(input.HesapTarihi)] = "Hesap tarihi boş olamaz.";
        if (input.HaftalikNormalSaat is < 1 or > 60)
            result.ValidationErrors[nameof(input.HaftalikNormalSaat)] = "Haftalık çalışma süresi 1-60 saat arası olmalıdır.";

        if (input.FazlaMesaiSaati == 0 && input.HaftaTatiliSaati == 0 && input.BayramSaati == 0)
        {
            result.ValidationErrors[nameof(input.FazlaMesaiSaati)] = "En az bir kategoride fazla mesai saati girilmelidir.";
        }

        if (result.ValidationErrors.Count > 0)
        {
            result.IsValid = false;
            return result;
        }

        var brutAylik = input.BrutAylikUcret!.Value;
        var hesapTarihi = input.HesapTarihi!.Value;
        var haftalikSure = input.HaftalikNormalSaat;

        var saatlikUcret = brutAylik / (haftalikSure * HaftaBasinaAyOranı);
        saatlikUcret = Math.Round(saatlikUcret, 4, MidpointRounding.AwayFromZero);

        var fazlaMesai = Math.Round(saatlikUcret * input.FazlaMesaiSaati * FazlaMesaiZamOrani, 2, MidpointRounding.AwayFromZero);
        var haftaTatili = Math.Round(saatlikUcret * input.HaftaTatiliSaati * HaftaTatiliZamOrani, 2, MidpointRounding.AwayFromZero);
        var bayram = Math.Round(saatlikUcret * input.BayramSaati * BayramZamOrani, 2, MidpointRounding.AwayFromZero);

        var brutToplam = fazlaMesai + haftaTatili + bayram;

        var damgaOrani = await _params.GetValueAsync("*", "damga-vergisi-orani", hesapTarihi, cancellationToken)
                      ?? 0.00759m;
        var damga = Math.Round(brutToplam * damgaOrani, 2, MidpointRounding.AwayFromZero);

        var gelirOrani = await _params.GetValueAsync(Slug, "gelir-vergisi-orani-basit", hesapTarihi, cancellationToken)
                      ?? await _params.GetValueAsync("ihbar-tazminati", "gelir-vergisi-orani-basit", hesapTarihi, cancellationToken)
                      ?? 0.15m;
        var gelir = Math.Round(brutToplam * gelirOrani, 2, MidpointRounding.AwayFromZero);

        var net = brutToplam - damga - gelir;

        result.SaatlikUcret = saatlikUcret;
        result.FazlaMesaiSaati = input.FazlaMesaiSaati;
        result.HaftaTatiliSaati = input.HaftaTatiliSaati;
        result.BayramSaati = input.BayramSaati;
        result.FazlaMesaiTutari = fazlaMesai;
        result.HaftaTatiliTutari = haftaTatili;
        result.BayramTutari = bayram;
        result.BrutToplam = brutToplam;
        result.DamgaVergisi = damga;
        result.GelirVergisi = gelir;
        result.NetTutar = net;

        var tr = new CultureInfo("tr-TR");
        result.TotalAmount = net;
        result.TotalLabel = "Net Fazla Mesai Alacağı";
        result.Unit = "TL";

        result.Rows.Add(new CalculationResultRow { Key = "Saatlik Brüt Ücret", Value = saatlikUcret.ToString("N4", tr) + " TL" });

        if (input.FazlaMesaiSaati > 0)
        {
            result.Rows.Add(new CalculationResultRow {
                Key = $"Fazla Mesai ({input.FazlaMesaiSaati} sa × ×1,5)",
                Value = fazlaMesai.ToString("N2", tr) + " TL"
            });
        }
        if (input.HaftaTatiliSaati > 0)
        {
            result.Rows.Add(new CalculationResultRow {
                Key = $"Hafta Tatili ({input.HaftaTatiliSaati} sa × ×2,0)",
                Value = haftaTatili.ToString("N2", tr) + " TL"
            });
        }
        if (input.BayramSaati > 0)
        {
            result.Rows.Add(new CalculationResultRow {
                Key = $"Bayram / Tatil ({input.BayramSaati} sa × ×2,0)",
                Value = bayram.ToString("N2", tr) + " TL"
            });
        }
        result.Rows.Add(new CalculationResultRow { Key = "Brüt Toplam", Value = brutToplam.ToString("N2", tr) + " TL", IsHighlighted = true });
        result.Rows.Add(new CalculationResultRow { Key = "Damga Vergisi", Value = damga.ToString("N2", tr) + " TL" });
        result.Rows.Add(new CalculationResultRow { Key = $"Gelir Vergisi (%{gelirOrani * 100:0.##})", Value = gelir.ToString("N2", tr) + " TL" });

        result.Note = "<strong>Saatlik ücret formülü:</strong> Aylık brüt / (haftalık saat × 4,33). " +
                      "<strong>Zam oranları:</strong> Fazla mesai %50, hafta tatili %100, bayram/genel tatil %100. " +
                      "<strong>Zamanaşımı:</strong> 5 yıl (m.32). " +
                      "<strong>Mevzuat:</strong> 4857 s.K. m.41.";

        return result;
    }
}
