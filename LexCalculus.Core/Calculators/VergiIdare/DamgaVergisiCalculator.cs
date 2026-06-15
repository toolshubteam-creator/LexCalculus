using System.Globalization;
using LexCalculus.Core.Calculators.Common;
using LexCalculus.Core.Interfaces;

namespace LexCalculus.Core.Calculators.VergiIdare;

/// <summary>
/// G3 Damga Vergisi — 488 s.K. Belge başına: değer × oran; sonuç azami sınır
/// (2026: 5.281.302,40 TL — 86 Seri No'lu Damga Vergisi Genel Tebliği) ile
/// cap'lenir; toplam belge adedi ile çarpılır.
///
/// Oranlar ve azami sınır FormulaParameter zaman-versiyonlu olarak okunur
/// (slug "damga-vergisi", key "oran.{belge}" ve "azami-sinir"). Yıllık tebliğ
/// güncellemesi admin paneliyle yapılır.
/// </summary>
public sealed class DamgaVergisiCalculator : ICalculator<DamgaVergisiInput, DamgaVergisiResult>
{
    private const string Slug = "damga-vergisi";

    private readonly IFormulaParameterService _params;

    public DamgaVergisiCalculator(IFormulaParameterService parameters)
    {
        _params = parameters ?? throw new ArgumentNullException(nameof(parameters));
    }

    public CalculatorMetadata Metadata { get; } = new()
    {
        Slug = Slug,
        Category = CalculatorCategory.VergiIdare,
        Title = "Damga Vergisi",
        ShortDescription = "488 s.K. — belge türüne göre oran (binde 1,89-9,48) ile damga vergisi; 2026 azami sınırı 5.281.302,40 TL (86 Seri No'lu Damga Vergisi Genel Tebliği).",
        LegalReference = "488 s.K.",
        Status = CalculatorStatus.Active,
        DisplayNumber = "34",
        Keywords = new[] { "damga vergisi", "488 sayılı kanun", "binde 9,48", "sözleşme damga", "kira damga", "azami damga" }
    };

    public async Task<DamgaVergisiResult> CalculateAsync(DamgaVergisiInput input, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        var result = new DamgaVergisiResult();

        if (input.DegerTutari is null or <= 0)
            result.ValidationErrors[nameof(input.DegerTutari)] = "Belge değeri pozitif olmalıdır.";
        if (input.BelgeAdedi <= 0)
            result.ValidationErrors[nameof(input.BelgeAdedi)] = "Belge adedi pozitif olmalıdır.";

        if (result.ValidationErrors.Count > 0)
        {
            result.IsValid = false;
            return result;
        }

        var asOf = input.AsOfDate ?? DateTime.UtcNow.Date;
        var oranKey = "oran." + BelgeKey(input.BelgeTuru);
        var oran = await GetParamAsync(oranKey, asOf, cancellationToken);
        var azamiSinir = await GetParamAsync("azami-sinir", asOf, cancellationToken);

        var deger = input.DegerTutari!.Value;
        var belgeBasinaHam = Math.Round(deger * oran, 2, MidpointRounding.AwayFromZero);
        var belgeBasinaUygulanan = Math.Min(belgeBasinaHam, azamiSinir);
        var cap = belgeBasinaUygulanan < belgeBasinaHam;

        var odenecek = belgeBasinaUygulanan * input.BelgeAdedi;
        var hesaplananToplam = belgeBasinaHam * input.BelgeAdedi;

        result.BelgeTuru = input.BelgeTuru;
        result.OranYuzde = oran;
        result.HesaplananVergi = hesaplananToplam;
        result.AzamiSinir = azamiSinir;
        result.OdenecekVergi = odenecek;
        result.AzamiSinirUygulandi = cap;

        var tr = new CultureInfo("tr-TR");

        result.Rows.Add(new() { Key = "Belge Türü", Value = BelgeAdi(input.BelgeTuru) });
        result.Rows.Add(new() { Key = "Belge Değeri", Value = deger.ToString("N2", tr) + " TL" });
        result.Rows.Add(new() { Key = "Uygulanan Oran", Value = $"‰{(oran * 1000m).ToString("0.##", tr)}" });
        result.Rows.Add(new() { Key = "Belge Başına Hesaplanan", Value = belgeBasinaHam.ToString("N2", tr) + " TL" });
        if (cap)
            result.Rows.Add(new() { Key = "Azami Sınır (cap)", Value = azamiSinir.ToString("N2", tr) + " TL" });
        if (input.BelgeAdedi > 1)
            result.Rows.Add(new() { Key = $"Belge Adedi (× {input.BelgeAdedi})", Value = belgeBasinaUygulanan.ToString("N2", tr) + " TL × " + input.BelgeAdedi });
        result.Rows.Add(new() { Key = "Ödenecek Toplam Damga Vergisi", Value = odenecek.ToString("N2", tr) + " TL", IsHighlighted = true });

        if (cap)
            result.Warnings.Add($"Belge başına hesaplanan vergi azami sınırı ({azamiSinir.ToString("N2", tr)} TL) aştığı için cap uygulanmıştır.");

        result.TotalAmount = odenecek;
        result.TotalLabel = "Ödenecek Damga Vergisi";
        result.Unit = "TL";
        result.Note = SonucNote();
        return result;
    }

    private async Task<decimal> GetParamAsync(string key, DateTime asOf, CancellationToken ct)
    {
        var value = await _params.GetValueAsync(Slug, key, asOf, ct);
        return value ?? throw new InvalidOperationException(
            $"Damga vergisi parametresi yok: ({Slug}, {key}, {asOf:yyyy-MM-dd}). " +
            $"Yönetici '{Slug}/{key}' parametresini eklemelidir.");
    }

    private static string BelgeKey(DamgaBelgeTuru t) => t switch
    {
        DamgaBelgeTuru.GenelSozlesme => "genel-sozlesme",
        DamgaBelgeTuru.KiraMukavelesi => "kira-mukavelesi",
        DamgaBelgeTuru.IhaleKarari => "ihale-karari",
        DamgaBelgeTuru.Makbuz => "makbuz",
        _ => "diger"
    };

    private static string BelgeAdi(DamgaBelgeTuru t) => t switch
    {
        DamgaBelgeTuru.GenelSozlesme => "Genel Sözleşme",
        DamgaBelgeTuru.KiraMukavelesi => "Kira Mukavelesi",
        DamgaBelgeTuru.IhaleKarari => "İhale Kararı",
        DamgaBelgeTuru.Makbuz => "Makbuz",
        _ => "Diğer"
    };

    private static string SonucNote() =>
        "<strong>Yöntem:</strong> 488 s.K. Damga Vergisi Tarifesine göre belge değeri × belge türü oranı; sonuç " +
        "belge başına azami sınırla cap'lenir; toplam belge adedi ile çarpılır. " +
        "<strong>Önemli:</strong> Bazı belgeler maktu damga vergisine tabidir (bu hesabın dışında), indirimli oranlar " +
        "(mali müşavir hizmetleri, kamu istisnaları vb.) ayrıca değerlendirilir. " +
        "<strong>Bu sonuç vergi dairesi tarafından kesin tarh edilmiş tutar yerine geçmez.</strong>";
}
