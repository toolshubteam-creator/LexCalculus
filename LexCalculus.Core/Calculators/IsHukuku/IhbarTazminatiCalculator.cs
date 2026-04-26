using System.Globalization;
using LexCalculus.Core.Calculators.Common;
using LexCalculus.Core.Interfaces;
using LexCalculus.Core.Models.Calculators;

namespace LexCalculus.Core.Calculators.IsHukuku;

/// <summary>
/// Notice pay calculator (İhbar Tazminatı).
///
/// Legal basis: 4857 s.K. m.17. When either party terminates the contract
/// without observing the statutory notice period, the other party is owed
/// notice pay equal to the wage for the unobserved period.
///
/// Notice period table (by tenure):
///   &lt; 6 months         => 2 weeks
///   6 mo - 1.5 yrs     => 4 weeks
///   1.5 - 3 yrs        => 6 weeks
///   3+ yrs             => 8 weeks
///
/// Tax treatment (CRITICAL — differs from kıdem):
///   - Stamp duty (damga vergisi): YES
///   - Income tax (gelir vergisi): YES — notice pay IS taxable
///       (Phase 2 simplification: flat 15% for the first bracket. Production
///        should use the cumulative bracket table from FormulaParameters.)
/// </summary>
public sealed class IhbarTazminatiCalculator : ICalculator<IhbarTazminatiInput, IhbarTazminatiResult>
{
    private const string Slug = "ihbar-tazminati";
    private const string ParamGelirVergisiOrani = "gelir-vergisi-orani-basit";
    private const string ParamDamgaVergisiOrani = "damga-vergisi-orani";

    private readonly IFormulaParameterService _params;

    public IhbarTazminatiCalculator(IFormulaParameterService parameters)
    {
        _params = parameters ?? throw new ArgumentNullException(nameof(parameters));
    }

    public CalculatorMetadata Metadata { get; } = new()
    {
        Slug = Slug,
        Category = CalculatorCategory.IsHukuku,
        Title = "İhbar Tazminatı",
        ShortDescription = "Kıdeme göre 2/4/6/8 hafta ihbar süresi tablosu, brüt ücret üzerinden tazminat hesabı; damga ve gelir vergisi kesintisi.",
        LegalReference = "4857 s.K. m.17",
        Status = CalculatorStatus.Active,
        DisplayNumber = "02",
        Keywords = new[] { "ihbar", "tazminat", "iş hukuku", "fesih", "bildirim süresi" }
    };

    public async Task<IhbarTazminatiResult> CalculateAsync(IhbarTazminatiInput input, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        var result = new IhbarTazminatiResult();

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

        if (result.ValidationErrors.Count > 0)
        {
            result.IsValid = false;
            return result;
        }

        var giris = input.GirisTarihi!.Value;
        var cikis = input.CikisTarihi!.Value;
        var brutAylik = input.BrutAylikUcret!.Value;

        var toplamGun = (int)Math.Floor((cikis - giris).TotalDays);
        var ihbarHaftasi = toplamGun switch
        {
            < 183 => 2,
            < 547 => 4,
            < 1095 => 6,
            _ => 8
        };
        var ihbarGunu = ihbarHaftasi * 7;

        var gunlukUcret = brutAylik / 30m;
        var brutIhbar = Math.Round(gunlukUcret * ihbarGunu, 2, MidpointRounding.AwayFromZero);

        var damgaOrani = await _params.GetValueAsync("*", ParamDamgaVergisiOrani, cikis, cancellationToken)
                      ?? 0.00759m;
        var damga = Math.Round(brutIhbar * damgaOrani, 2, MidpointRounding.AwayFromZero);

        var gelirOrani = await _params.GetValueAsync(Slug, ParamGelirVergisiOrani, cikis, cancellationToken)
                      ?? 0.15m;
        var gelir = Math.Round(brutIhbar * gelirOrani, 2, MidpointRounding.AwayFromZero);

        var net = brutIhbar - damga - gelir;

        result.ToplamGun = toplamGun;
        result.IhbarHaftasi = ihbarHaftasi;
        result.IhbarGunu = ihbarGunu;
        result.GunlukUcret = gunlukUcret;
        result.BrutIhbar = brutIhbar;
        result.DamgaVergisi = damga;
        result.GelirVergisi = gelir;
        result.NetIhbar = net;
        result.HakEden = input.HakEden;

        var tr = new CultureInfo("tr-TR");
        result.TotalAmount = net;
        result.TotalLabel = "Net İhbar Tazminatı";
        result.Unit = "TL";

        var tenureYears = toplamGun / 365.0;
        result.Rows.Add(new CalculationResultRow { Key = "Toplam Çalışma Süresi", Value = $"{toplamGun} gün (~{tenureYears:F1} yıl)" });
        result.Rows.Add(new CalculationResultRow { Key = "İhbar Süresi", Value = $"{ihbarHaftasi} hafta ({ihbarGunu} gün)" });
        result.Rows.Add(new CalculationResultRow { Key = "Günlük Brüt Ücret", Value = gunlukUcret.ToString("N2", tr) + " TL" });
        result.Rows.Add(new CalculationResultRow { Key = "Brüt İhbar Tazminatı", Value = brutIhbar.ToString("N2", tr) + " TL" });
        result.Rows.Add(new CalculationResultRow { Key = "Damga Vergisi", Value = damga.ToString("N2", tr) + " TL" });
        result.Rows.Add(new CalculationResultRow { Key = $"Gelir Vergisi (%{gelirOrani * 100:0.##})", Value = gelir.ToString("N2", tr) + " TL" });

        result.Note = "<strong>Vergi:</strong> İhbar tazminatı kıdem tazminatından farklı olarak gelir vergisine tabidir. " +
                      "<strong>Gelir Vergisi Basitleştirmesi:</strong> Bu hesaplamada %15 sabit oran (ilk dilim) varsayılmıştır; " +
                      "gerçek vergi yıllık kümülatif kazanca bağlıdır ve daha yüksek dilimlere düşebilir. " +
                      "<strong>Mevzuat:</strong> 4857 s.K. m.17.";

        return result;
    }
}
