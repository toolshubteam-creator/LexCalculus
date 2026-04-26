using System.Globalization;
using LexCalculus.Core.Calculators.Common;
using LexCalculus.Core.Interfaces;
using LexCalculus.Core.Models.Calculators;

namespace LexCalculus.Core.Calculators.IsHukuku;

/// <summary>
/// Severance pay calculator (Kıdem Tazminatı).
///
/// Legal basis: 4857 s.K. (formerly 1475 s.K. m.14). For each full year of
/// service, the worker is entitled to 30 days of "dressed wage" (giydirilmiş
/// ücret = base brüt + monthly equivalent of side benefits). Partial years
/// pay proportionally on a daily basis.
///
/// Tax treatment: Severance pay is exempt from income tax. Only stamp duty
/// (damga vergisi) is withheld at the rate stored in FormulaParameters.
///
/// Ceiling: When the dressed wage exceeds the period's statutory ceiling, the
/// ceiling value is used as the basis. The ceiling changes ~twice a year and
/// is read from FormulaParameters with EffectiveDate &lt;= cikisTarihi.
/// </summary>
public sealed class KidemTazminatiCalculator : ICalculator<KidemTazminatiInput, KidemTazminatiResult>
{
    private const string Slug = "kidem-tazminati";
    private const string ParamTavan = "tavan";
    private const string ParamDamgaVergisiOrani = "damga-vergisi-orani";

    private readonly IFormulaParameterService _params;

    public KidemTazminatiCalculator(IFormulaParameterService parameters)
    {
        _params = parameters ?? throw new ArgumentNullException(nameof(parameters));
    }

    public CalculatorMetadata Metadata { get; } = new()
    {
        Slug = Slug,
        Category = CalculatorCategory.IsHukuku,
        Title = "Kıdem Tazminatı",
        ShortDescription = "İş Kanunu m.14 (mülga 1475 s.K.) kapsamında giydirilmiş ücret üzerinden, dönem tavanı ve damga vergisi kesintisi ile.",
        LegalReference = "4857 s.K. / 1475 s.K. m.14",
        Status = CalculatorStatus.Active,
        DisplayNumber = "01",
        Keywords = new[] { "kıdem", "tazminat", "iş hukuku", "tavan", "giydirilmiş ücret" }
    };

    public async Task<KidemTazminatiResult> CalculateAsync(KidemTazminatiInput input, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        var result = new KidemTazminatiResult();

        if (input.GirisTarihi is null)
            result.ValidationErrors[nameof(input.GirisTarihi)] = "Giriş tarihi boş olamaz.";
        if (input.CikisTarihi is null)
            result.ValidationErrors[nameof(input.CikisTarihi)] = "Çıkış tarihi boş olamaz.";
        if (input.BrutAylikUcret is null or <= 0)
            result.ValidationErrors[nameof(input.BrutAylikUcret)] = "Brüt ücret pozitif olmalıdır.";

        if (input.GirisTarihi is not null && input.CikisTarihi is not null
            && input.CikisTarihi <= input.GirisTarihi)
        {
            result.ValidationErrors[nameof(input.CikisTarihi)] = "Çıkış tarihi giriş tarihinden sonra olmalıdır.";
        }

        if (input.GirisTarihi is not null && input.CikisTarihi is not null)
        {
            var sureDays = (input.CikisTarihi.Value - input.GirisTarihi.Value).TotalDays;
            if (sureDays < 365)
            {
                result.ValidationErrors[nameof(input.CikisTarihi)] = "Kıdem tazminatı için en az 1 yıl (365 gün) çalışma gerekir.";
            }
        }

        if (result.ValidationErrors.Count > 0)
        {
            result.IsValid = false;
            return result;
        }

        var giris = input.GirisTarihi!.Value;
        var cikis = input.CikisTarihi!.Value;
        var brutAylik = input.BrutAylikUcret!.Value;
        var yanOdeme = input.YanOdemelerAylik;

        var fark = cikis - giris;
        var toplamGun = (int)Math.Floor(fark.TotalDays);
        var tamYil = toplamGun / 365;
        var kalanGun = toplamGun % 365;

        var giydirilmis = brutAylik + yanOdeme;

        var tavan = await _params.GetValueAsync(Slug, ParamTavan, cikis, cancellationToken)
                  ?? throw new InvalidOperationException(
                       $"Kıdem tavanı parametresi yok: ({Slug}, {ParamTavan}, {cikis:yyyy-MM-dd}). " +
                       "Yönetici 'kidem-tazminati/tavan' parametresini eklemelidir.");

        var tavanAsildi = giydirilmis > tavan;
        var hesaplamaTabani = tavanAsildi ? tavan : giydirilmis;

        var brutKidem = hesaplamaTabani * ((decimal)toplamGun / 365m);
        brutKidem = Math.Round(brutKidem, 2, MidpointRounding.AwayFromZero);

        var damgaOrani = await _params.GetValueAsync("*", ParamDamgaVergisiOrani, cikis, cancellationToken)
                      ?? 0.00759m;

        var damga = Math.Round(brutKidem * damgaOrani, 2, MidpointRounding.AwayFromZero);
        var netKidem = brutKidem - damga;

        decimal? ihbar = null;
        int? ihbarHaftasi = null;
        if (input.IhbarDahil)
        {
            ihbarHaftasi = toplamGun switch
            {
                < 183 => 2,
                < 547 => 4,
                < 1095 => 6,
                _ => 8
            };
            var gunlukUcret = brutAylik / 30m;
            ihbar = Math.Round(gunlukUcret * (ihbarHaftasi.Value * 7), 2, MidpointRounding.AwayFromZero);
        }

        result.ToplamGun = toplamGun;
        result.TamYil = tamYil;
        result.KalanGun = kalanGun;
        result.GiydirilmisUcret = giydirilmis;
        result.KullanilanTavan = tavan;
        result.TavanAsildi = tavanAsildi;
        result.BrutKidem = brutKidem;
        result.DamgaVergisi = damga;
        result.NetKidem = netKidem;
        result.IhbarTazminati = ihbar;
        result.IhbarHaftasi = ihbarHaftasi;

        var tr = new CultureInfo("tr-TR");
        result.TotalAmount = netKidem;
        result.TotalLabel = "Net Kıdem Tazminatı";
        result.Unit = "TL";

        result.Rows.Add(new CalculationResultRow { Key = "Toplam Çalışma Süresi", Value = $"{tamYil} yıl {kalanGun} gün ({toplamGun} gün)" });
        result.Rows.Add(new CalculationResultRow { Key = "Giydirilmiş Aylık Ücret", Value = giydirilmis.ToString("N2", tr) + " TL" });
        result.Rows.Add(new CalculationResultRow { Key = "Dönem Tavanı (Çıkış Tarihi)", Value = tavan.ToString("N2", tr) + " TL" });
        result.Rows.Add(new CalculationResultRow { Key = "Hesaplama Tabanı", Value = hesaplamaTabani.ToString("N2", tr) + " TL" });
        result.Rows.Add(new CalculationResultRow { Key = "Brüt Kıdem Tazminatı", Value = brutKidem.ToString("N2", tr) + " TL" });
        result.Rows.Add(new CalculationResultRow { Key = "Damga Vergisi Kesintisi", Value = damga.ToString("N2", tr) + " TL" });

        if (input.IhbarDahil && ihbar.HasValue)
        {
            result.Rows.Add(new CalculationResultRow { Key = "İhbar Süresi", Value = $"{ihbarHaftasi} hafta" });
            result.Rows.Add(new CalculationResultRow { Key = "İhbar Tazminatı (Brüt)", Value = ihbar.Value.ToString("N2", tr) + " TL" });
        }

        if (tavanAsildi)
        {
            result.Warnings.Add($"Giydirilmiş ücret tavanı aştığı için tavan değeri ({tavan.ToString("N2", tr)} TL) hesaplama tabanı olarak kullanıldı.");
        }

        result.Note = "<strong>Vergi:</strong> Kıdem tazminatı gelir vergisinden muaftır; yalnızca damga vergisi (binde 7,59) kesilir. " +
                      "<strong>Mevzuat:</strong> 4857 s.K. (mülga 1475 s.K. m.14). " +
                      "Hesaplama Çalışma Bakanlığı tarafından yayımlanan dönemsel kıdem tavanı parametresine dayanır.";

        return result;
    }
}
