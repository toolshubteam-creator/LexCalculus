using System.Globalization;
using LexCalculus.Core.Calculators.Common;

namespace LexCalculus.Core.Calculators.Ceza;

/// <summary>
/// F5 Tutukluluk Mahsup — TCK m.63 + 5275 s.K. m.108. Tutukluluk gün sayısı
/// inclusive sayılır: (bitis - baslangic) + 1. Adli para mahsubu seçildiyse
/// gün sayısı × günlük miktar TL tutarı verilir (TCK m.63 son fıkra).
///
/// Saf hesap — DB bağımlılığı yok, parametresiz, servis injection yok.
/// </summary>
public sealed class TutuklulukMahsubuCalculator : ICalculator<TutuklulukMahsubuInput, TutuklulukMahsubuResult>
{
    public CalculatorMetadata Metadata { get; } = new()
    {
        Slug = "tutukluluk-mahsubu",
        Category = CalculatorCategory.Ceza,
        Title = "Tutukluluk Mahsup",
        ShortDescription = "TCK m.63 + 5275 s.K. m.108 — tutukluluk başlangıç/bitiş tarihinden gün sayısı; opsiyonel adli para mahsubu (gün × günlük miktar).",
        LegalReference = "TCK m.63 / 5275 s.K. m.108",
        Status = CalculatorStatus.Active,
        DisplayNumber = "31",
        Keywords = new[] { "tutukluluk mahsubu", "TCK 63", "5275 sayılı kanun 108", "infaz mahsubu", "tutuklulukta geçen süre" }
    };

    public Task<TutuklulukMahsubuResult> CalculateAsync(TutuklulukMahsubuInput input, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        var result = new TutuklulukMahsubuResult();

        if (input.TutuklulukBaslangic is null)
            result.ValidationErrors[nameof(input.TutuklulukBaslangic)] = "Başlangıç tarihi boş olamaz.";
        if (input.TutuklulukBitis is null)
            result.ValidationErrors[nameof(input.TutuklulukBitis)] = "Bitiş tarihi boş olamaz.";

        if (result.ValidationErrors.Count > 0)
        {
            result.IsValid = false;
            return Task.FromResult(result);
        }

        var basla = DateOnly.FromDateTime(input.TutuklulukBaslangic!.Value);
        var bitis = DateOnly.FromDateTime(input.TutuklulukBitis!.Value);

        if (bitis < basla)
        {
            result.ValidationErrors[nameof(input.TutuklulukBitis)] =
                "Bitiş tarihi başlangıçtan önce olamaz.";
            result.IsValid = false;
            return Task.FromResult(result);
        }

        if (input.AdliParaMahsubu && (input.GunlukMiktar is null or <= 0))
        {
            result.ValidationErrors[nameof(input.GunlukMiktar)] =
                "Adli para mahsubu seçildiğinde günlük miktar girilmelidir.";
            result.IsValid = false;
            return Task.FromResult(result);
        }

        var gun = (bitis.DayNumber - basla.DayNumber) + 1; // inclusive
        result.TutuklulukGunleri = gun;

        var tr = new CultureInfo("tr-TR");

        result.Rows.Add(new() { Key = "Tutukluluk Başlangıç", Value = basla.ToString("dd MMMM yyyy", tr) });
        result.Rows.Add(new() { Key = "Tutukluluk Bitiş", Value = bitis.ToString("dd MMMM yyyy", tr) });
        result.Rows.Add(new() { Key = "Tutuklulukta Geçen Süre", Value = $"{gun} gün", IsHighlighted = true });

        if (input.AdliParaMahsubu)
        {
            var miktar = input.GunlukMiktar!.Value;
            var mahsup = Math.Round(gun * miktar, 2, MidpointRounding.AwayFromZero);
            result.MahsupTutari = mahsup;

            result.Rows.Add(new() { Key = "Günlük Miktar", Value = miktar.ToString("N2", tr) + " TL" });
            result.Rows.Add(new() { Key = "Adli Paradan Mahsup", Value = mahsup.ToString("N2", tr) + " TL", IsHighlighted = true });

            result.TotalAmount = mahsup;
            result.TotalLabel = "Adli Paradan Mahsup Tutarı";
            result.Unit = "TL";
        }
        else
        {
            result.TotalAmount = gun;
            result.TotalLabel = "Tutuklulukta Geçen Süre";
            result.Unit = "gün";
        }

        result.Note = SonucNote();
        return Task.FromResult(result);
    }

    private static string SonucNote() =>
        "<strong>Yöntem:</strong> Tutuklulukta geçen süre, başlangıç ve bitiş tarihleri dahil edilerek " +
        "(bitis - baslangic + 1) gün olarak hesaplanır (TCK m.63). Adli para cezasından mahsup edilecekse " +
        "gün sayısı günlük miktarla çarpılır (TCK m.63 son fıkra, 5275 s.K. m.108 atfı). " +
        "<strong>Önemli:</strong> Gerçek tutukluluk süresi cezaevi kayıtlarına göre belirlenir; gözaltı " +
        "süresi de TCK m.63 son fıkra kapsamında mahsuba dahil olabilir. " +
        "<strong>Bu sonuç infaz hâkimi kararı yerine geçmez.</strong>";
}
