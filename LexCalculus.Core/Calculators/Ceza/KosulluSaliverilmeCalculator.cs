using System.Globalization;
using LexCalculus.Core.Calculators.Common;
using LexCalculus.Core.Services;

namespace LexCalculus.Core.Calculators.Ceza;

/// <summary>
/// F2 Koşullu Salıverilme — 5275 s.K. m.107. Suç tipine göre yatılması gereken
/// oran ile şartlı tahliye tarihi hesaplanır. Tutukluluk mahsubu net infaz
/// süresinden düşülür.
///
/// Oranlar (5275 s.K. m.107):
///   Genel suçlar       → 2/3
///   Terör suçları      → 3/4
///   Cinsel suçlar      → 3/4 (TCK m.102/103/104/105)
///   Örgütlü suçlar     → 3/4
///   Diğer ağır suçlar  → 3/4 (kasten öldürme vb.)
///
/// Hesap "iyi hal varsayımıyla" yapılır (m.107/8 — iyi hal koşulu). Eksik veya
/// kötü hal durumunda gerçek tahliye tarihi infaz hâkiminin takdirine bağlıdır;
/// hesap sonucu mahkeme/infaz hâkimi kararı yerine geçmez.
///
/// Saf hesap — DB bağımlılığı yok, mevzuat sabitleri ile çalışır.
/// </summary>
public sealed class KosulluSaliverilmeCalculator : ICalculator<KosulluSaliverilmeInput, KosulluSaliverilmeResult>
{
    private readonly ICriminalCalendarService _takvim;

    public KosulluSaliverilmeCalculator(ICriminalCalendarService takvim)
    {
        _takvim = takvim ?? throw new ArgumentNullException(nameof(takvim));
    }

    public CalculatorMetadata Metadata { get; } = new()
    {
        Slug = "kosullu-saliverilme",
        Category = CalculatorCategory.Ceza,
        Title = "Koşullu Salıverilme",
        ShortDescription = "5275 s.K. m.107 — mahkumiyet süresi ve suç tipine (genel 2/3, terör/cinsel/örgütlü/ağır 3/4) göre şartlı tahliye tarihi; tutukluluk mahsubu.",
        LegalReference = "5275 s.K. m.107",
        Status = CalculatorStatus.Active,
        DisplayNumber = "28",
        Keywords = new[] { "koşullu salıverilme", "5275 sayılı kanun", "infaz", "şartlı tahliye", "tutukluluk mahsubu", "denetimli serbestlik" }
    };

    public Task<KosulluSaliverilmeResult> CalculateAsync(KosulluSaliverilmeInput input, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        var result = new KosulluSaliverilmeResult();

        if (input.MahkumiyetGun is null or <= 0)
            result.ValidationErrors[nameof(input.MahkumiyetGun)] = "Mahkumiyet süresi pozitif olmalıdır.";
        if (input.CezaevineGirisTarihi is null)
            result.ValidationErrors[nameof(input.CezaevineGirisTarihi)] = "Cezaevine giriş tarihi boş olamaz.";
        if (input.TutuklulukGun is < 0)
            result.ValidationErrors[nameof(input.TutuklulukGun)] = "Tutukluluk süresi negatif olamaz.";

        if (result.ValidationErrors.Count > 0)
        {
            result.IsValid = false;
            return Task.FromResult(result);
        }

        var mahkumiyet = input.MahkumiyetGun!.Value;
        var tutukluluk = input.TutuklulukGun ?? 0;
        var oran = OranIcin(input.SucTipi);
        var infazSuresi = _takvim.InfazSuresi(mahkumiyet, oran);

        if (tutukluluk > infazSuresi)
        {
            result.ValidationErrors[nameof(input.TutuklulukGun)] =
                $"Tutukluluk ({tutukluluk} gün) net infaz süresinden ({infazSuresi} gün) fazla olamaz.";
            result.IsValid = false;
            return Task.FromResult(result);
        }

        var giris = DateOnly.FromDateTime(input.CezaevineGirisTarihi!.Value);
        var sartliTahliye = _takvim.TahliyeTarihi(giris, mahkumiyet, oran, tutukluluk);
        var netInfaz = infazSuresi - tutukluluk;

        var asOf = input.AsOfDate.HasValue
            ? DateOnly.FromDateTime(input.AsOfDate.Value)
            : DateOnly.FromDateTime(DateTime.UtcNow.Date);
        var kalan = sartliTahliye.DayNumber - asOf.DayNumber;

        result.SartliTahliyeTarihi = sartliTahliye;
        result.KalanGunSayisi = kalan;
        result.HesaplananOran = oran;
        result.NetInfazSuresi = netInfaz;
        result.UyariMesaji = "İyi hal varsayımıyla hesaplanmıştır; gerçek tahliye tarihi infaz hâkimi takdirine bağlıdır.";

        var tr = new CultureInfo("tr-TR");

        result.Rows.Add(new() { Key = "Mahkumiyet Süresi", Value = $"{mahkumiyet} gün" });
        result.Rows.Add(new() { Key = "Suç Tipi", Value = SucTipiAdi(input.SucTipi) });
        result.Rows.Add(new() { Key = "Uygulanan Oran", Value = OranAdi(oran) });
        result.Rows.Add(new() { Key = "İnfaz Süresi (mahkumiyet × oran)", Value = $"{infazSuresi} gün" });
        if (tutukluluk > 0)
            result.Rows.Add(new() { Key = "Tutukluluk Mahsubu", Value = $"-{tutukluluk} gün" });
        result.Rows.Add(new() { Key = "Net Yatılacak Süre", Value = $"{netInfaz} gün" });
        result.Rows.Add(new() { Key = "Cezaevine Giriş", Value = giris.ToString("dd MMMM yyyy", tr) });
        result.Rows.Add(new() { Key = "Şartlı Tahliye Tarihi", Value = sartliTahliye.ToString("dd MMMM yyyy", tr), IsHighlighted = true });
        result.Rows.Add(new()
        {
            Key = "Kalan Gün (referans " + asOf.ToString("dd.MM.yyyy", tr) + ")",
            Value = kalan >= 0 ? $"{kalan} gün" : $"{Math.Abs(kalan)} gün önce geçmiş"
        });

        result.TotalAmount = netInfaz;
        result.TotalLabel = "Net Yatılacak Süre";
        result.Unit = "gün";
        result.Note = SonucNote();

        return Task.FromResult(result);
    }

    private static decimal OranIcin(SucTipi sucTipi) => sucTipi switch
    {
        SucTipi.Genel => 2m / 3m,
        SucTipi.Teror or SucTipi.CinselSuc or SucTipi.OrgutluSuc or SucTipi.DigerAgirSuc => 0.75m,
        _ => 2m / 3m
    };

    private static string OranAdi(decimal oran) =>
        oran == 0.75m ? "3/4 (%75)" : "2/3 (%66,67)";

    private static string SucTipiAdi(SucTipi tip) => tip switch
    {
        SucTipi.Genel => "Genel Suçlar",
        SucTipi.Teror => "Terör Suçları",
        SucTipi.CinselSuc => "Cinsel Suçlar (TCK m.102/103/104/105)",
        SucTipi.OrgutluSuc => "Örgütlü Suçlar",
        SucTipi.DigerAgirSuc => "Diğer Ağır Suçlar",
        _ => tip.ToString()
    };

    private static string SonucNote() =>
        "<strong>Yöntem:</strong> Mahkumiyet süresi suç tipine ait koşullu salıverilme oranı (5275 s.K. m.107) ile çarpılır, " +
        "varsa tutukluluk süresi mahsup edilir; sonuç cezaevine giriş tarihine eklenerek şartlı tahliye tarihi bulunur. " +
        "<strong>Önemli:</strong> Bu hesap iyi hal varsayımıyla yapılmıştır. İnfaz hâkimi 5275 s.K. m.107/8 kapsamında " +
        "iyi hal takdir yetkisi kullanır; disiplin cezası halinde tahliye tarihi ertelenebilir, denetim raporları sonuca " +
        "etki edebilir. <strong>Bu sonuç infaz hâkimi kararı veya cezaevi kayıtları yerine geçmez.</strong>";
}
