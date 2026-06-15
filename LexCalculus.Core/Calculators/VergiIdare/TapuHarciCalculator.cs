using System.Globalization;
using LexCalculus.Core.Calculators.Common;
using LexCalculus.Core.Interfaces;

namespace LexCalculus.Core.Calculators.VergiIdare;

/// <summary>
/// G2 Tapu Harcı — 492 s.K. Tapu ve Kadastro Harçları Tarifesi. Alıcı + satıcı
/// her biri %2 (toplam %4). Oran <c>FormulaParameter</c> üzerinden
/// (<c>tapu-harci/oran</c>) okunur; yıllık güncelleme admin paneliyle yapılır.
/// </summary>
public sealed class TapuHarciCalculator : ICalculator<TapuHarciInput, TapuHarciResult>
{
    private const string Slug = "tapu-harci";

    private readonly IFormulaParameterService _params;

    public TapuHarciCalculator(IFormulaParameterService parameters)
    {
        _params = parameters ?? throw new ArgumentNullException(nameof(parameters));
    }

    public CalculatorMetadata Metadata { get; } = new()
    {
        Slug = Slug,
        Category = CalculatorCategory.VergiIdare,
        Title = "Tapu Harcı",
        ShortDescription = "492 s.K. Harçlar Kanunu Tapu ve Kadastro Tarifesi — alıcı ve satıcı için ayrı ayrı %2 (toplam %4) tapu harcı hesabı.",
        LegalReference = "492 s.K.",
        Status = CalculatorStatus.Active,
        DisplayNumber = "33",
        Keywords = new[] { "tapu harcı", "492 sayılı kanun", "harçlar kanunu", "gayrimenkul devir harcı", "alıcı satıcı harç" }
    };

    public async Task<TapuHarciResult> CalculateAsync(TapuHarciInput input, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        var result = new TapuHarciResult();

        if (input.SatisDegeri is null or <= 0)
        {
            result.ValidationErrors[nameof(input.SatisDegeri)] = "Satış değeri pozitif olmalıdır.";
            result.IsValid = false;
            return result;
        }

        var asOf = input.AsOfDate ?? DateTime.UtcNow.Date;
        var oran = await _params.GetValueAsync(Slug, "oran", asOf, cancellationToken)
            ?? throw new InvalidOperationException(
                $"Tapu harç oranı parametresi yok: ({Slug}, oran, {asOf:yyyy-MM-dd}). " +
                "Yönetici 'tapu-harci/oran' parametresini eklemelidir.");

        var deger = input.SatisDegeri!.Value;
        var aliciHarc = Math.Round(deger * oran, 2, MidpointRounding.AwayFromZero);
        var saticiHarc = Math.Round(deger * oran, 2, MidpointRounding.AwayFromZero);

        var toplam = input.KisiBasi switch
        {
            TapuHarcKisi.Alici => aliciHarc,
            TapuHarcKisi.Satici => saticiHarc,
            _ => aliciHarc + saticiHarc
        };

        result.HarcOrani = oran;
        result.SatisDegeri = deger;
        result.AliciHarc = aliciHarc;
        result.SaticiHarc = saticiHarc;
        result.ToplamHarc = toplam;

        var tr = new CultureInfo("tr-TR");

        result.Rows.Add(new() { Key = "Satış Değeri", Value = deger.ToString("N2", tr) + " TL" });
        result.Rows.Add(new() { Key = "Harç Oranı (taraf başına)", Value = $"%{(oran * 100m).ToString("0.##", tr)}" });

        if (input.KisiBasi != TapuHarcKisi.Satici)
            result.Rows.Add(new() { Key = "Alıcı Harcı", Value = aliciHarc.ToString("N2", tr) + " TL" });
        if (input.KisiBasi != TapuHarcKisi.Alici)
            result.Rows.Add(new() { Key = "Satıcı Harcı", Value = saticiHarc.ToString("N2", tr) + " TL" });

        result.Rows.Add(new() { Key = "Toplam Harç", Value = toplam.ToString("N2", tr) + " TL", IsHighlighted = true });

        result.TotalAmount = toplam;
        result.TotalLabel = "Toplam Tapu Harcı";
        result.Unit = "TL";
        result.Note = SonucNote();
        return result;
    }

    private static string SonucNote() =>
        "<strong>Yöntem:</strong> 492 s.K. Tapu ve Kadastro Harçları Tarifesine göre alıcı ve satıcının her biri " +
        "satış değeri üzerinden ayrı ayrı %2 oranında harç öder (toplam %4). " +
        "<strong>Önemli:</strong> İndirimli oranlar (kentsel dönüşüm, sosyal konut, ilk konut alımı vb.) ayrıca " +
        "değerlendirilir. KDV (taşınmaz türüne göre %1 veya %10) ve döner sermaye ücreti bu hesabın dışındadır. " +
        "<strong>Bu sonuç tapu müdürlüğünce tahakkuk ettirilen kesin tutar yerine geçmez.</strong>";
}
