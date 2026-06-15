using System.Globalization;
using LexCalculus.Core.Calculators.Common;
using LexCalculus.Core.Services;

namespace LexCalculus.Core.Calculators.Ceza;

/// <summary>
/// F3 Dava Zamanaşımı — TCK m.66 (asli süreler) + m.67 (kesinti) + m.67/4
/// (mutlak sınır = asli × 1.5).
///
/// Asli süreler (m.66):
///   Kısa (&lt; 5 yıl hapis / adli para)  → 8 yıl
///   Orta (5-20 yıl hapis)              → 15 yıl
///   Uzun (20+ yıl hapis)               → 20 yıl
///   Müebbet                            → 25 yıl
///   Ağırlaştırılmış müebbet            → 30 yıl
///
/// Algoritma:
///   1. Suç ağırlığından asli süre belirlenir; mutlak süre = asli × 1.5.
///   2. Mutlak bitiş = suç işleme tarihi + mutlak süre (HİÇ değişmez).
///   3. Kesinti varsa son kesinti tarihinden asli süre eklenir → asli bitiş;
///      kesinti yoksa suç işleme tarihinden asli eklenir.
///   4. Asli bitiş mutlak bitişi aşıyorsa cap uygulanır (m.67/4).
///   5. Etkin bitiş = min(asli, mutlak); AsOfDate ile karşılaştırılır.
///
/// Saf hesap — DB bağımlılığı yok. ICriminalCalendarService sadece tarih
/// aritmetiği (yıl ekleme) ve referans tarih için kullanılır.
/// </summary>
public sealed class DavaZamanasimiCalculator : ICalculator<DavaZamanasimiInput, DavaZamanasimiResult>
{
    private readonly ICriminalCalendarService _takvim;

    public DavaZamanasimiCalculator(ICriminalCalendarService takvim)
    {
        _takvim = takvim ?? throw new ArgumentNullException(nameof(takvim));
    }

    public CalculatorMetadata Metadata { get; } = new()
    {
        Slug = "dava-zamanasimi",
        Category = CalculatorCategory.Ceza,
        Title = "Dava Zamanaşımı",
        ShortDescription = "TCK m.66-67 — suç ağırlığına göre asli zamanaşımı (8/15/20/25/30 yıl), m.67 kesinti uygulaması ve m.67/4 mutlak sınır (asli × 1.5).",
        LegalReference = "TCK m.66-67",
        Status = CalculatorStatus.Active,
        DisplayNumber = "29",
        Keywords = new[] { "dava zamanaşımı", "TCK 66", "TCK 67", "zamanaşımı kesintisi", "mutlak zamanaşımı" }
    };

    public Task<DavaZamanasimiResult> CalculateAsync(DavaZamanasimiInput input, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        var result = new DavaZamanasimiResult();

        if (input.SucIslemeTarihi is null)
            result.ValidationErrors[nameof(input.SucIslemeTarihi)] = "Suç işleme tarihi boş olamaz.";

        if (result.ValidationErrors.Count > 0)
        {
            result.IsValid = false;
            return Task.FromResult(result);
        }

        var sucTarihi = DateOnly.FromDateTime(input.SucIslemeTarihi!.Value);

        // Kesinti tarihleri validation: tarih dolu, suç işleme tarihinden sonra.
        var kesintiler = new List<(DateOnly Tarih, string? Tur)>();
        foreach (var k in input.Kesintiler ?? new List<KesintiGirdi>())
        {
            if (k.Tarih is null) continue;
            var t = DateOnly.FromDateTime(k.Tarih.Value);
            if (t < sucTarihi)
            {
                result.ValidationErrors[nameof(input.Kesintiler)] =
                    $"Kesinti tarihi ({t:dd.MM.yyyy}) suç işleme tarihinden ({sucTarihi:dd.MM.yyyy}) önce olamaz.";
                result.IsValid = false;
                return Task.FromResult(result);
            }
            kesintiler.Add((t, string.IsNullOrWhiteSpace(k.IslemTuru) ? null : k.IslemTuru));
        }
        kesintiler.Sort((a, b) => a.Tarih.CompareTo(b.Tarih));

        var asliYil = AsliSureYil(input.SucAgirligi);
        var mutlakYil = asliYil * 1.5m;

        var mutlakBitis = sucTarihi.AddYears((int)Math.Floor(mutlakYil))
            .AddDays((int)Math.Round((mutlakYil - (int)Math.Floor(mutlakYil)) * 365m, 0, MidpointRounding.AwayFromZero));

        var sonBaslangic = kesintiler.Count > 0 ? kesintiler[^1].Tarih : sucTarihi;
        var asliBitisHam = sonBaslangic.AddYears(asliYil);

        var mutlakSinirAktif = asliBitisHam > mutlakBitis;
        var etkinBitis = mutlakSinirAktif ? mutlakBitis : asliBitisHam;

        var asOf = input.AsOfDate.HasValue
            ? DateOnly.FromDateTime(input.AsOfDate.Value)
            : DateOnly.FromDateTime(DateTime.UtcNow.Date);

        var kalan = etkinBitis.DayNumber - asOf.DayNumber;

        result.AsliZamanasimiSuresiYil = asliYil;
        result.MutlakZamanasimiSuresiYil = mutlakYil;
        result.SonBaslangicTarihi = sonBaslangic;
        result.AsliZamanasimiBitis = mutlakSinirAktif ? mutlakBitis : asliBitisHam;
        result.MutlakZamanasimiBitis = mutlakBitis;
        result.MutlakSinirUygulandi = mutlakSinirAktif;
        result.ZamanasimineUgradiMi = etkinBitis <= asOf;
        result.KalanGun = kalan;

        var tr = new CultureInfo("tr-TR");

        result.Rows.Add(new() { Key = "Suç İşleme Tarihi", Value = sucTarihi.ToString("dd MMMM yyyy", tr) });
        result.Rows.Add(new() { Key = "Suç Ağırlığı", Value = SucAgirligiAdi(input.SucAgirligi) });
        result.Rows.Add(new() { Key = "Asli Zamanaşımı Süresi (m.66)", Value = $"{asliYil} yıl" });
        result.Rows.Add(new() { Key = "Mutlak Zamanaşımı Süresi (m.67/4)", Value = $"{mutlakYil.ToString("0.#", tr)} yıl (asli × 1.5)" });

        if (kesintiler.Count > 0)
        {
            result.Rows.Add(new() { Key = "Kesinti Sayısı (m.67)", Value = $"{kesintiler.Count}" });
            result.Rows.Add(new() { Key = "Son Kesinti Tarihi", Value = sonBaslangic.ToString("dd MMMM yyyy", tr) });
        }
        else
        {
            result.Rows.Add(new() { Key = "Kesinti", Value = "Yok (asli süre suç tarihinden başlar)" });
        }

        result.Rows.Add(new() { Key = "Mutlak Zamanaşımı Bitişi", Value = mutlakBitis.ToString("dd MMMM yyyy", tr) });
        result.Rows.Add(new() { Key = "Etkin Zamanaşımı Bitişi", Value = etkinBitis.ToString("dd MMMM yyyy", tr), IsHighlighted = true });

        if (mutlakSinirAktif)
            result.Warnings.Add("Asli zamanaşımı süresi mutlak zamanaşımı sınırını (asli × 1.5) aştığı için TCK m.67/4 uyarınca mutlak bitiş tarihi uygulanmıştır.");

        result.Rows.Add(new()
        {
            Key = "Kalan Gün (referans " + asOf.ToString("dd.MM.yyyy", tr) + ")",
            Value = kalan >= 0 ? $"{kalan} gün" : $"{Math.Abs(kalan)} gün önce dolmuş"
        });
        result.Rows.Add(new()
        {
            Key = "Durum",
            Value = result.ZamanasimineUgradiMi ? "Dava zamanaşımına uğramıştır" : "Henüz zamanaşımına uğramamıştır",
            IsHighlighted = true
        });

        result.TotalAmount = kalan;
        result.TotalLabel = result.ZamanasimineUgradiMi ? "Zamanaşımı (geçmiş)" : "Kalan Süre";
        result.Unit = "gün";
        result.Note = SonucNote();

        return Task.FromResult(result);
    }

    private static int AsliSureYil(SucAgirligi a) => a switch
    {
        SucAgirligi.Kisa => 8,
        SucAgirligi.Orta => 15,
        SucAgirligi.Uzun => 20,
        SucAgirligi.Muebbet => 25,
        SucAgirligi.AgirlastirilmisMuebbet => 30,
        _ => 8
    };

    private static string SucAgirligiAdi(SucAgirligi a) => a switch
    {
        SucAgirligi.Kisa => "5 yıldan az hapis / adli para",
        SucAgirligi.Orta => "5-20 yıl arası hapis",
        SucAgirligi.Uzun => "20+ yıl hapis",
        SucAgirligi.Muebbet => "Müebbet hapis",
        SucAgirligi.AgirlastirilmisMuebbet => "Ağırlaştırılmış müebbet",
        _ => a.ToString()
    };

    private static string SonucNote() =>
        "<strong>Yöntem:</strong> Asli zamanaşımı süresi TCK m.66 tablosundan suç ağırlığına göre belirlenir; " +
        "TCK m.67 kesintisi her kesinti sonrası süreyi sıfırdan başlatır, ancak m.67/4 uyarınca asli × 1.5 mutlak " +
        "sınırını aşamaz. <strong>Önemli:</strong> Suçun nitelendirilmesi (basit/nitelikli), kesintinin niteliği " +
        "(savcılık ifadesi, iddianame, yakalama emri vb.) yargılama makamı takdirindedir. <strong>Bu sonuç " +
        "mahkeme kararı yerine geçmez.</strong>";
}
