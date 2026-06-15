using System.Globalization;
using LexCalculus.Core.Calculators.Common;
using LexCalculus.Core.Services;

namespace LexCalculus.Core.Calculators.Ceza;

/// <summary>
/// F1 Ceza Erteleme — TCK m.51. Verilen hapis cezasının ertelemeye uygunluğunu
/// (üst sınır + adli sicil temiz + ceza türü) kontrol eder ve mahkemenin takdir
/// ettiği erteleme süresinin bitiş tarihini hesaplar.
///
/// Üst sınır (TCK m.51/1): yetişkin 2 yıl (730 gün), 18 yaş altı çocuk 3 yıl
/// (1095 gün). Adli para cezası erteleme dışı (TCK m.51/1 yalnız hapis cezası).
/// Sanığın adli sicili: önceki kasıtlı suçtan 3 ay+ ceza almamış olması
/// (m.51/1/b).
///
/// Saf hesap — DB bağımlılığı yok, mevzuat sabitleri ile çalışır. Mahkemenin
/// takdir yetkisini (m.51/1 son cümle "sanığın yeniden suç işlemeyeceği
/// kanaati") hesap kapsamaz; sadece formel koşulları test eder.
/// </summary>
public sealed class CezaErtelemeCalculator : ICalculator<CezaErtelemeInput, CezaErtelemeResult>
{
    private const int YetiskinUstSinirGun = 730;   // TCK m.51/1
    private const int CocukUstSinirGun = 1095;     // TCK m.51/2 (18 yaş altı)

    private readonly ICriminalCalendarService _takvim;

    public CezaErtelemeCalculator(ICriminalCalendarService takvim)
    {
        _takvim = takvim ?? throw new ArgumentNullException(nameof(takvim));
    }

    public CalculatorMetadata Metadata { get; } = new()
    {
        Slug = "ceza-erteleme",
        Category = CalculatorCategory.Ceza,
        Title = "Ceza Erteleme",
        ShortDescription = "TCK m.51 — verilen hapis cezasının ertelemeye uygunluğu (yetişkin 2 yıl / çocuk 3 yıl üst sınırı, adli sicil temiz, ceza türü) ve erteleme süresi bitiş tarihi.",
        LegalReference = "TCK m.51",
        Status = CalculatorStatus.Active,
        DisplayNumber = "27",
        Keywords = new[] { "ceza erteleme", "TCK 51", "denetimli serbestlik", "hapis erteleme", "ertelenmiş ceza" }
    };

    public Task<CezaErtelemeResult> CalculateAsync(CezaErtelemeInput input, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        var result = new CezaErtelemeResult();

        if (input.VerilenCezaGun is null or <= 0)
            result.ValidationErrors[nameof(input.VerilenCezaGun)] = "Ceza gün sayısı pozitif olmalıdır.";
        if (input.KararTarihi is null)
            result.ValidationErrors[nameof(input.KararTarihi)] = "Karar tarihi boş olamaz.";

        if (result.ValidationErrors.Count > 0)
        {
            result.IsValid = false;
            return Task.FromResult(result);
        }

        var ceza = input.VerilenCezaGun!.Value;
        var ustSinir = input.CezaTuru == CezaTuru.HapisCocuk ? CocukUstSinirGun : YetiskinUstSinirGun;
        result.UygulananUstSinirGun = ustSinir;

        var tr = new CultureInfo("tr-TR");

        // Koşul 1: ceza türü — adli para erteleme dışı (TCK m.51/1).
        if (input.CezaTuru == CezaTuru.AdliPara)
        {
            result.ErtelemeyeUygunMu = false;
            result.UygunsuzlukSebebi = "Adli para cezası TCK m.51 erteleme kapsamı dışındadır; erteleme yalnızca hapis cezasına özgüdür.";
            return Task.FromResult(FillUygunsuz(input, result, ceza, ustSinir, tr));
        }

        // Koşul 2: üst sınır (yetişkin 2 yıl / çocuk 3 yıl).
        if (ceza > ustSinir)
        {
            result.ErtelemeyeUygunMu = false;
            var sinirYil = ustSinir == YetiskinUstSinirGun ? "2 yıl" : "3 yıl";
            result.UygunsuzlukSebebi = $"Verilen ceza ({ceza} gün) erteleme üst sınırını ({sinirYil} = {ustSinir} gün) aşmaktadır (TCK m.51/1).";
            return Task.FromResult(FillUygunsuz(input, result, ceza, ustSinir, tr));
        }

        // Koşul 3: adli sicil temiz (m.51/1/b).
        if (!input.AdliSicilTemiz)
        {
            result.ErtelemeyeUygunMu = false;
            result.UygunsuzlukSebebi = "Sanığın önceki kasıtlı suçtan 3 ay veya daha fazla ceza alması ertelemeyi engeller (TCK m.51/1/b).";
            return Task.FromResult(FillUygunsuz(input, result, ceza, ustSinir, tr));
        }

        // Tüm formel koşullar sağlandı.
        result.ErtelemeyeUygunMu = true;

        var kararDateOnly = DateOnly.FromDateTime(input.KararTarihi!.Value);
        var ertelemeAy = (int)input.ErtelemeSuresi;
        var ertelemeBitis = kararDateOnly.AddMonths(ertelemeAy);
        result.ErtelemeBitisTarihi = ertelemeBitis;

        DateOnly? denetimBitis = null;
        if (input.DenetimliSerbestlikAy is > 0)
        {
            denetimBitis = kararDateOnly.AddMonths(input.DenetimliSerbestlikAy.Value);
            result.DenetimliSerbestlikBitisTarihi = denetimBitis;
        }

        result.Rows.Add(new() { Key = "Ceza Türü", Value = CezaTuruAdi(input.CezaTuru) });
        result.Rows.Add(new() { Key = "Verilen Ceza", Value = $"{ceza} gün" });
        result.Rows.Add(new() { Key = "Erteleme Üst Sınırı", Value = $"{ustSinir} gün ({(ustSinir == YetiskinUstSinirGun ? "yetişkin 2 yıl" : "çocuk 3 yıl")})" });
        result.Rows.Add(new() { Key = "Adli Sicil", Value = "Temiz (uygun)" });
        result.Rows.Add(new() { Key = "Karar Tarihi", Value = kararDateOnly.ToString("dd MMMM yyyy", tr) });
        result.Rows.Add(new() { Key = "Erteleme Süresi", Value = $"{ertelemeAy} ay" });
        result.Rows.Add(new() { Key = "Erteleme Bitiş Tarihi", Value = ertelemeBitis.ToString("dd MMMM yyyy", tr), IsHighlighted = true });
        if (denetimBitis.HasValue)
            result.Rows.Add(new() { Key = "Denetimli Serbestlik Bitiş", Value = denetimBitis.Value.ToString("dd MMMM yyyy", tr) });

        result.TotalAmount = ertelemeAy;
        result.TotalLabel = "Erteleme Süresi";
        result.Unit = "ay";
        result.Note = UygunNote();

        return Task.FromResult(result);
    }

    private static CezaErtelemeResult FillUygunsuz(CezaErtelemeInput input, CezaErtelemeResult result, int ceza, int ustSinir, CultureInfo tr)
    {
        result.Rows.Add(new() { Key = "Ceza Türü", Value = CezaTuruAdi(input.CezaTuru) });
        result.Rows.Add(new() { Key = "Verilen Ceza", Value = $"{ceza} gün" });
        result.Rows.Add(new() { Key = "Erteleme Üst Sınırı", Value = $"{ustSinir} gün" });
        result.Rows.Add(new() { Key = "Adli Sicil Temiz", Value = input.AdliSicilTemiz ? "Evet" : "Hayır" });
        result.Rows.Add(new() { Key = "Uygunluk", Value = "UYGUN DEĞİL", IsHighlighted = true });
        result.Warnings.Add(result.UygunsuzlukSebebi ?? "Erteleme koşulları sağlanmıyor.");

        result.TotalAmount = 0;
        result.TotalLabel = "Erteleme Süresi (uygunsuz)";
        result.Unit = "ay";
        result.Note = UygunsuzNote();
        return result;
    }

    private static string CezaTuruAdi(CezaTuru tip) => tip switch
    {
        CezaTuru.HapisYetiskin => "Hapis Cezası (yetişkin)",
        CezaTuru.HapisCocuk => "Hapis Cezası (18 yaş altı çocuk)",
        CezaTuru.AdliPara => "Adli Para Cezası",
        _ => tip.ToString()
    };

    private const string WarningTail =
        "<strong>Önemli:</strong> Bu hesap TCK m.51 formel koşullarını kontrol eder; mahkeme " +
        "'sanığın yeniden suç işlemeyeceği kanaati' (m.51/1 son cümle) gibi takdir yetkisini de " +
        "kullanır. Bu sonuç mahkeme kararı yerine geçmez.";

    private static string UygunNote() =>
        "<strong>Sonuç:</strong> Verilen ceza TCK m.51 formel koşullarını (üst sınır + ceza türü + adli sicil) sağlamaktadır. " +
        "Mahkeme takdir yetkisini kullanarak ertelemeye hükmederse, erteleme süresinin bitiş tarihi yukarıdaki tarihtir. " + WarningTail;

    private static string UygunsuzNote() =>
        "<strong>Sonuç:</strong> Verilen ceza TCK m.51 erteleme koşullarını sağlamamaktadır. " +
        "Erteleme kararı verilemez. " + WarningTail;
}
