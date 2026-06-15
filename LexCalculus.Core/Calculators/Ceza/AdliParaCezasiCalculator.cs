using System.Globalization;
using LexCalculus.Core.Calculators.Common;

namespace LexCalculus.Core.Calculators.Ceza;

/// <summary>
/// F4 Adli Para Cezası — TCK m.52. Toplam ceza = gün sayısı × günlük miktar.
/// Hapis çevrim modunda mahkumiyet günü × çevrim miktarı uygulanır. Günlük
/// miktar 20-100 TL bandında olmalıdır (TCK m.52/2 sınırı, mahkeme takdir).
///
/// Saf hesap — DB bağımlılığı yok, parametresiz; günlük miktar mahkeme
/// takdiriyle girdi olarak alınır.
/// </summary>
public sealed class AdliParaCezasiCalculator : ICalculator<AdliParaCezasiInput, AdliParaCezasiResult>
{
    public CalculatorMetadata Metadata { get; } = new()
    {
        Slug = "adli-para-cezasi",
        Category = CalculatorCategory.Ceza,
        Title = "Adli Para Cezası",
        ShortDescription = "TCK m.52 — gün sayısı × günlük miktar formülüyle adli para cezası ya da hapis cezasının paraya çevrimi (günlük miktar 20-100 TL bandında, mahkeme takdir).",
        LegalReference = "TCK m.52",
        Status = CalculatorStatus.Active,
        DisplayNumber = "30",
        Keywords = new[] { "adli para cezası", "TCK 52", "hapis çevrim", "para cezası", "gün karşılığı" }
    };

    public Task<AdliParaCezasiResult> CalculateAsync(AdliParaCezasiInput input, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        var result = new AdliParaCezasiResult();

        if (input.HesapTuru == AdliParaHesapTuru.Direkt)
        {
            if (input.GunSayisi is null or <= 0)
                result.ValidationErrors[nameof(input.GunSayisi)] = "Gün sayısı pozitif olmalıdır.";
            if (input.GunlukMiktar is null or <= 0)
                result.ValidationErrors[nameof(input.GunlukMiktar)] = "Günlük miktar pozitif olmalıdır.";
        }
        else
        {
            if (input.HapisGun is null or <= 0)
                result.ValidationErrors[nameof(input.HapisGun)] = "Hapis günü pozitif olmalıdır.";
            if (input.CevrimGunlukMiktar is null or <= 0)
                result.ValidationErrors[nameof(input.CevrimGunlukMiktar)] = "Çevrim günlük miktarı pozitif olmalıdır.";
        }

        if (result.ValidationErrors.Count > 0)
        {
            result.IsValid = false;
            return Task.FromResult(result);
        }

        var (gun, miktar, etiket) = input.HesapTuru == AdliParaHesapTuru.Direkt
            ? (input.GunSayisi!.Value, input.GunlukMiktar!.Value, "Direkt")
            : (input.HapisGun!.Value, input.CevrimGunlukMiktar!.Value, "Hapis Çevrim");

        var toplam = Math.Round(gun * miktar, 2, MidpointRounding.AwayFromZero);

        result.EtkinGunSayisi = gun;
        result.UygulananGunlukMiktar = miktar;
        result.ToplamCeza = toplam;
        result.UyariMesaji = "Günlük miktar (TCK m.52/2: 20-100 TL bandı) mahkeme takdiridir; sanığın ekonomik durumu ve suçun ağırlığı dikkate alınır.";

        var tr = new CultureInfo("tr-TR");

        result.Rows.Add(new() { Key = "Hesap Türü", Value = etiket });
        result.Rows.Add(new() { Key = input.HesapTuru == AdliParaHesapTuru.Direkt ? "Gün Sayısı" : "Hapis Cezası (gün)", Value = $"{gun} gün" });
        result.Rows.Add(new() { Key = "Günlük Miktar", Value = miktar.ToString("N2", tr) + " TL" });
        result.Rows.Add(new() { Key = "Toplam Adli Para Cezası", Value = toplam.ToString("N2", tr) + " TL", IsHighlighted = true });

        result.TotalAmount = toplam;
        result.TotalLabel = "Toplam Adli Para Cezası";
        result.Unit = "TL";
        result.Note = SonucNote();

        return Task.FromResult(result);
    }

    private static string SonucNote() =>
        "<strong>Yöntem:</strong> Toplam adli para cezası = gün sayısı × günlük miktar (TCK m.52). " +
        "Hapis çevrim modunda mahkumiyet günü × günlük çevrim miktarı uygulanır. " +
        "<strong>Önemli:</strong> Günlük miktar (TCK m.52/2: 20-100 TL bandı) mahkeme takdiridir, " +
        "sanığın ekonomik durumu, suçun ağırlığı gibi faktörler değerlendirilir. " +
        "<strong>Bu sonuç mahkeme kararı yerine geçmez.</strong>";
}
