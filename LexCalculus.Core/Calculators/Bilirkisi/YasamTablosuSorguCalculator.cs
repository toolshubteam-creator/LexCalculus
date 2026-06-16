using System.Globalization;
using LexCalculus.Core.Calculators.Common;
using LexCalculus.Core.Enums;
using LexCalculus.Core.Interfaces;

namespace LexCalculus.Core.Calculators.Bilirkisi;

/// <summary>
/// I1 PMF Yaşam Tablosu Sorgulama — TRH 2010 (Türkiye Hayat Tablosu) reuse.
/// Bilirkişi raporlarında destekten yoksun kalma, maluliyet, anüite hesapları
/// için temel kalan yaşam umudu (eX) sorgu aracı. Faz 2 <see cref="ILifeTableService"/>
/// üzerinden aktif tabloyu kullanır.
/// </summary>
public sealed class YasamTablosuSorguCalculator : ICalculator<YasamTablosuSorguInput, YasamTablosuSorguResult>
{
    private readonly ILifeTableService _lifeTable;

    public YasamTablosuSorguCalculator(ILifeTableService lifeTable)
    {
        _lifeTable = lifeTable ?? throw new ArgumentNullException(nameof(lifeTable));
    }

    public CalculatorMetadata Metadata { get; } = new()
    {
        Slug = "yasam-tablosu-sorgu",
        Category = CalculatorCategory.Bilirkisi,
        Title = "PMF Yaşam Tablosu Sorgulama",
        ShortDescription = "TRH 2010 (Türkiye Hayat Tablosu) — yaş ve cinsiyete göre kalan yaşam umudu (eX) sorgusu. Bilirkişi raporlarında destekten yoksun kalma ve anüite hesapları için temel girdi.",
        LegalReference = "TRH 2010 / Yargıtay HGK",
        Status = CalculatorStatus.Active,
        DisplayNumber = "40",
        Keywords = new[] { "yaşam tablosu", "TRH 2010", "PMF", "kalan ömür", "bekleneni yaşam", "bilirkişi" }
    };

    public async Task<YasamTablosuSorguResult> CalculateAsync(YasamTablosuSorguInput input, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        var result = new YasamTablosuSorguResult
        {
            SorguTipi = input.SorguTipi,
            Cinsiyet = input.Cinsiyet
        };

        if (input.SorguTipi == YasamSorguTipi.TekKisi)
        {
            if (input.Yas is null or < 0 or > 105)
                result.ValidationErrors[nameof(input.Yas)] = "Yaş 0-105 arası olmalıdır.";
        }
        else
        {
            if (input.BaslangicYas is null or < 0 or > 105)
                result.ValidationErrors[nameof(input.BaslangicYas)] = "Başlangıç yaşı 0-105 arası olmalıdır.";
            if (input.BitisYas is null or < 0 or > 105)
                result.ValidationErrors[nameof(input.BitisYas)] = "Bitiş yaşı 0-105 arası olmalıdır.";
            if (input.BaslangicYas is not null && input.BitisYas is not null && input.BitisYas < input.BaslangicYas)
                result.ValidationErrors[nameof(input.BitisYas)] = "Bitiş yaşı başlangıçtan küçük olamaz.";
        }

        if (result.ValidationErrors.Count > 0)
        {
            result.IsValid = false;
            return result;
        }

        var tr = new CultureInfo("tr-TR");

        if (input.SorguTipi == YasamSorguTipi.TekKisi)
        {
            var eX = await _lifeTable.GetBekledigiYasamAsync(input.Yas!.Value, input.Cinsiyet, ct: cancellationToken);
            if (eX is null)
            {
                result.ValidationErrors[nameof(input.Yas)] =
                    $"TRH tablosunda {input.Yas.Value} yaş / {input.Cinsiyet} için kayıt bulunamadı.";
                result.IsValid = false;
                return result;
            }

            result.TekSonuc = new YasamTablosuSatiri { Yas = input.Yas.Value, KalanYasamUmidi = eX.Value };
            result.Rows.Add(new() { Key = "Cinsiyet", Value = input.Cinsiyet == Cinsiyet.Erkek ? "Erkek" : "Kadın" });
            result.Rows.Add(new() { Key = "Yaş", Value = input.Yas.Value.ToString() });
            result.Rows.Add(new() { Key = "Kalan Yaşam Umudi (eX)", Value = eX.Value.ToString("0.##", tr) + " yıl", IsHighlighted = true });

            result.TotalAmount = eX.Value;
            result.TotalLabel = "Kalan Yaşam Umudu";
            result.Unit = "yıl";
            result.Note = SonucNote();
            return result;
        }

        // Yaş aralığı modu.
        var liste = new List<YasamTablosuSatiri>();
        for (var y = input.BaslangicYas!.Value; y <= input.BitisYas!.Value; y++)
        {
            var eX = await _lifeTable.GetBekledigiYasamAsync(y, input.Cinsiyet, ct: cancellationToken);
            if (eX.HasValue)
                liste.Add(new YasamTablosuSatiri { Yas = y, KalanYasamUmidi = eX.Value });
        }

        if (liste.Count == 0)
        {
            result.ValidationErrors[nameof(input.BaslangicYas)] =
                $"TRH tablosunda {input.BaslangicYas}-{input.BitisYas} aralığında kayıt bulunamadı.";
            result.IsValid = false;
            return result;
        }

        result.AralikSonuclari = liste;
        result.Rows.Add(new() { Key = "Cinsiyet", Value = input.Cinsiyet == Cinsiyet.Erkek ? "Erkek" : "Kadın" });
        result.Rows.Add(new() { Key = "Yaş Aralığı", Value = $"{input.BaslangicYas} - {input.BitisYas} ({liste.Count} satır)" });

        foreach (var s in liste)
            result.Rows.Add(new() { Key = $"Yaş {s.Yas}", Value = s.KalanYasamUmidi.ToString("0.##", tr) + " yıl" });

        result.TotalAmount = liste.Count;
        result.TotalLabel = "Satır Sayısı";
        result.Unit = "satır";
        result.Note = SonucNote();
        return result;
    }

    private static string SonucNote() =>
        "<strong>Yöntem:</strong> Sorgu, sistemde aktif olan TRH 2010 (veya admin tarafından aktif edilen güncel) " +
        "Türkiye Hayat Tablosundaki <em>eX</em> (kalan yaşam umudu) değerlerini döner. " +
        "<strong>Önemli:</strong> Tablo değerleri TÜİK güncellemelerine tabidir. Destekten yoksun kalma, maluliyet, " +
        "anüite gibi tazminat hesaplarında bu değerler temel girdidir ancak hesabın tamamı değildir. " +
        "<strong>Bu sorgu sonucu kesin tazminat hesabı yerine geçmez.</strong>";
}
