using System.Globalization;
using LexCalculus.Core.Calculators.Common;

namespace LexCalculus.Core.Calculators.Bilirkisi;

/// <summary>
/// I4 Çevresel Zarar Tazminatı — 2872 s.K. Çevre Kanunu m.28.
///   ToplamZarar = DogrudanZarar + RestorasyonMaliyeti + EkosistemKaybi
///   BilirkisiMaliyet = ToplamZarar × (oran / 100) — opsiyonel
///   ToplamTazminat = ToplamZarar + BilirkisiMaliyet
///
/// Saf hesap — DB bağımlılığı yok, parametresiz. Manevi tazminat ve 2872 s.K.
/// m.20 idari para cezası bu hesabın dışındadır.
/// </summary>
public sealed class CevreselZararCalculator : ICalculator<CevreselZararInput, CevreselZararResult>
{
    public CalculatorMetadata Metadata { get; } = new()
    {
        Slug = "cevresel-zarar",
        Category = CalculatorCategory.Bilirkisi,
        Title = "Çevresel Zarar Tazminatı",
        ShortDescription = "2872 s.K. m.28 — doğrudan zarar + restorasyon maliyeti + ekosistem kaybı kalem toplamı; opsiyonel bilirkişi maliyet oranı.",
        LegalReference = "2872 s.K. m.28",
        Status = CalculatorStatus.Active,
        DisplayNumber = "43",
        Keywords = new[] { "çevresel zarar", "çevre kanunu", "2872 sayılı kanun", "restorasyon", "ekosistem", "bilirkişi" }
    };

    public Task<CevreselZararResult> CalculateAsync(CevreselZararInput input, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        var result = new CevreselZararResult();

        if (input.DogrudanZarar is null or < 0)
            result.ValidationErrors[nameof(input.DogrudanZarar)] = "Doğrudan zarar negatif olamaz.";
        if (input.RestorasyonMaliyeti is null or < 0)
            result.ValidationErrors[nameof(input.RestorasyonMaliyeti)] = "Restorasyon maliyeti negatif olamaz.";
        if (input.EkosistemKaybi is null or < 0)
            result.ValidationErrors[nameof(input.EkosistemKaybi)] = "Ekosistem kaybı negatif olamaz.";
        if (input.BilirkisiMaliyetOraniYuzde is < 0 or > 15)
            result.ValidationErrors[nameof(input.BilirkisiMaliyetOraniYuzde)] = "Bilirkişi maliyet oranı 0-15 arası olmalıdır.";

        if (result.ValidationErrors.Count > 0)
        {
            result.IsValid = false;
            return Task.FromResult(result);
        }

        var dogrudan = input.DogrudanZarar!.Value;
        var restorasyon = input.RestorasyonMaliyeti!.Value;
        var ekosistem = input.EkosistemKaybi!.Value;
        var oran = input.BilirkisiMaliyetOraniYuzde ?? 0m;

        var toplamZarar = dogrudan + restorasyon + ekosistem;
        var bilirkisiMaliyet = Math.Round(toplamZarar * (oran / 100m), 2, MidpointRounding.AwayFromZero);
        var toplamTazminat = toplamZarar + bilirkisiMaliyet;

        result.DogrudanZarar = dogrudan;
        result.RestorasyonMaliyeti = restorasyon;
        result.EkosistemKaybi = ekosistem;
        result.ToplamZarar = toplamZarar;
        result.BilirkisiMaliyet = bilirkisiMaliyet;
        result.ToplamTazminat = toplamTazminat;

        var tr = new CultureInfo("tr-TR");

        result.Rows.Add(new() { Key = "Doğrudan Zarar", Value = dogrudan.ToString("N2", tr) + " TL" });
        result.Rows.Add(new() { Key = "Restorasyon Maliyeti", Value = restorasyon.ToString("N2", tr) + " TL" });
        result.Rows.Add(new() { Key = "Ekosistem Kaybı", Value = ekosistem.ToString("N2", tr) + " TL" });
        result.Rows.Add(new() { Key = "Toplam Zarar (3 kalem)", Value = toplamZarar.ToString("N2", tr) + " TL", IsHighlighted = true });

        if (oran > 0m)
        {
            result.Rows.Add(new() { Key = $"Bilirkişi Maliyet Oranı (%{oran.ToString("0.##", tr)})", Value = bilirkisiMaliyet.ToString("N2", tr) + " TL" });
            result.Rows.Add(new() { Key = "Toplam Tazminat", Value = toplamTazminat.ToString("N2", tr) + " TL", IsHighlighted = true });
        }

        if (toplamZarar == 0m)
            result.Warnings.Add("Üç kalem de sıfır girildi; toplam zarar tutarı bulunmamaktadır.");

        result.TotalAmount = toplamTazminat;
        result.TotalLabel = "Toplam Tazminat";
        result.Unit = "TL";
        result.Note = SonucNote();
        return Task.FromResult(result);
    }

    private static string SonucNote() =>
        "<strong>Yöntem:</strong> 2872 s.K. m.28 — Çevre Kanunu kapsamında doğrudan zarar (varlık değer kaybı), " +
        "restorasyon maliyeti (alan onarımı) ve ekosistem kaybı (uzun vadeli hizmet kaybı tahmini) kalem bazında " +
        "toplanır; opsiyonel bilirkişi maliyet oranı eklenir. " +
        "<strong>Önemli:</strong> Restorasyon maliyeti uzman çevre mühendisi raporuna, ekosistem kaybı uzun vadeli " +
        "ekolojik analize dayanır. Manevi tazminat ve 2872 s.K. m.20 idari para cezası bu hesabın dışındadır. " +
        "<strong>Bu sonuç bilirkişi raporu yerine geçmez.</strong>";
}
