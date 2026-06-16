using System.Globalization;
using LexCalculus.Core.Calculators.Common;
using LexCalculus.Core.Interfaces;

namespace LexCalculus.Core.Calculators.Bilirkisi;

/// <summary>
/// I3 Hakkaniyetli Tazminat Simülatörü — TBK m.51 + Yargıtay HGK hakkaniyet
/// ölçütlerinin parametrik simülasyonu. Tazminat = baz × kusur × ekonomik ×
/// olay × yaş çarpanları.
///
/// Çarpanlar FormulaParameter (slug "hakkaniyetli-tazminat"):
///   ekonomik.{zor|normal|refah}, olay.{hafif|normal|agir}, yas.{genc|orta|ileri}
///
/// Saf hesap — parametre okuma için <see cref="IFormulaParameterService"/>
/// inject. Çarpanlar heuristik referansdır (bkz. tech-debt #51, nafaka #45
/// pattern); hukuk profesyoneli incelemesi sonrası kalibrasyon yapılır.
/// </summary>
public sealed class HakkaniyetliTazminatCalculator : ICalculator<HakkaniyetliTazminatInput, HakkaniyetliTazminatResult>
{
    private const string Slug = "hakkaniyetli-tazminat";

    private readonly IFormulaParameterService _params;

    public HakkaniyetliTazminatCalculator(IFormulaParameterService parameters)
    {
        _params = parameters ?? throw new ArgumentNullException(nameof(parameters));
    }

    public CalculatorMetadata Metadata { get; } = new()
    {
        Slug = Slug,
        Category = CalculatorCategory.Bilirkisi,
        Title = "Hakkaniyetli Tazminat Simülatörü",
        ShortDescription = "TBK m.51 + Yargıtay HGK hakkaniyet ölçütleri — baz tazminat × kusur oranı × ekonomik/olay/yaş çarpanları ile parametrik simülasyon. Hâkim takdir yetkisi için referans.",
        LegalReference = "TBK m.51",
        Status = CalculatorStatus.Active,
        DisplayNumber = "42",
        Keywords = new[] { "hakkaniyetli tazminat", "TBK 51", "manevi tazminat", "hakkaniyet", "kusur oranı", "müterafik kusur" }
    };

    public async Task<HakkaniyetliTazminatResult> CalculateAsync(HakkaniyetliTazminatInput input, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        var result = new HakkaniyetliTazminatResult();

        if (input.BazTazminat is null or <= 0)
            result.ValidationErrors[nameof(input.BazTazminat)] = "Baz tazminat pozitif olmalıdır.";
        if (input.KusurOrani is null or < 0 or > 1)
            result.ValidationErrors[nameof(input.KusurOrani)] = "Kusur oranı 0-1 arası olmalıdır.";

        if (result.ValidationErrors.Count > 0)
        {
            result.IsValid = false;
            return result;
        }

        var asOf = input.AsOfDate ?? DateTime.UtcNow.Date;

        var ekonomikKats = await GetParamAsync("ekonomik." + EkonomikKey(input.EkonomikDurum), asOf, cancellationToken);
        var olayKats = await GetParamAsync("olay." + OlayKey(input.OlayAgirligi), asOf, cancellationToken);
        var yasKats = await GetParamAsync("yas." + YasKey(input.YasKategorisi), asOf, cancellationToken);

        var baz = input.BazTazminat!.Value;
        var kusur = input.KusurOrani!.Value;

        var hesaplanan = Math.Round(baz * kusur * ekonomikKats * olayKats * yasKats, 2, MidpointRounding.AwayFromZero);

        result.BazTazminat = baz;
        result.KusurOrani = kusur;
        result.EkonomikDurumKats = ekonomikKats;
        result.OlayAgirligiKats = olayKats;
        result.YasKats = yasKats;
        result.HesaplananTazminat = hesaplanan;
        result.UyariMesaji = "Çarpan değerleri heuristik referansdır; somut davada hâkim TBK m.4 + m.51 takdir yetkisini kullanır.";

        var tr = new CultureInfo("tr-TR");

        result.Rows.Add(new() { Key = "Baz Tazminat", Value = baz.ToString("N2", tr) + " TL" });
        result.Rows.Add(new() { Key = "Kusur Oranı (davacı kusursuzluk)", Value = kusur.ToString("0.##", tr) });
        result.Rows.Add(new() { Key = $"Ekonomik Durum ({EkonomikAdi(input.EkonomikDurum)})", Value = "× " + ekonomikKats.ToString("0.##", tr) });
        result.Rows.Add(new() { Key = $"Olay Ağırlığı ({OlayAdi(input.OlayAgirligi)})", Value = "× " + olayKats.ToString("0.##", tr) });
        result.Rows.Add(new() { Key = $"Yaş Kategorisi ({YasAdi(input.YasKategorisi)})", Value = "× " + yasKats.ToString("0.##", tr) });
        result.Rows.Add(new() { Key = "Hesaplanan Tazminat (referans)", Value = hesaplanan.ToString("N2", tr) + " TL", IsHighlighted = true });

        if (kusur == 0m)
            result.Warnings.Add("Kusur oranı sıfır; tam müterafik kusur durumunda tazminat doğmaz.");

        result.TotalAmount = hesaplanan;
        result.TotalLabel = "Hesaplanan Tazminat (Referans)";
        result.Unit = "TL";
        result.Note = SonucNote();
        return result;
    }

    private async Task<decimal> GetParamAsync(string key, DateTime asOf, CancellationToken ct)
    {
        var value = await _params.GetValueAsync(Slug, key, asOf, ct);
        return value ?? throw new InvalidOperationException(
            $"Hakkaniyetli tazminat parametresi yok: ({Slug}, {key}, {asOf:yyyy-MM-dd}). " +
            $"Yönetici '{Slug}/{key}' parametresini eklemelidir.");
    }

    private static string EkonomikKey(EkonomikDurum d) => d switch
    {
        EkonomikDurum.Zor => "zor",
        EkonomikDurum.Refah => "refah",
        _ => "normal"
    };

    private static string OlayKey(OlayAgirligi o) => o switch
    {
        OlayAgirligi.Hafif => "hafif",
        OlayAgirligi.Agir => "agir",
        _ => "normal"
    };

    private static string YasKey(YasKategorisi y) => y switch
    {
        YasKategorisi.Genc => "genc",
        YasKategorisi.Ileri => "ileri",
        _ => "orta"
    };

    private static string EkonomikAdi(EkonomikDurum d) => d switch
    {
        EkonomikDurum.Zor => "Zor",
        EkonomikDurum.Refah => "Refah",
        _ => "Normal"
    };

    private static string OlayAdi(OlayAgirligi o) => o switch
    {
        OlayAgirligi.Hafif => "Hafif",
        OlayAgirligi.Agir => "Ağır",
        _ => "Normal"
    };

    private static string YasAdi(YasKategorisi y) => y switch
    {
        YasKategorisi.Genc => "Genç",
        YasKategorisi.Ileri => "İleri Yaş",
        _ => "Orta Yaş"
    };

    private static string SonucNote() =>
        "<strong>Yöntem:</strong> TBK m.51 hakkaniyet ölçütlerini parametrik simülasyon olarak uygular: " +
        "Tazminat = baz × kusur oranı × ekonomik durum × olay ağırlığı × yaş çarpanları. " +
        "<strong>Önemli:</strong> Çarpan değerleri heuristik referansdır (hukuk profesyoneli incelemesi sonrası " +
        "kalibre edilecek). Somut davada mahkeme TBK m.4 + m.51 takdir yetkisini kullanır; tarafların ekonomik durumu, " +
        "olayın özel koşulları, eylemin niteliği gibi faktörler ayrıca değerlendirilir. " +
        "<strong>Bu sonuç mahkeme kararı veya bilirkişi raporu yerine geçmez.</strong>";
}
