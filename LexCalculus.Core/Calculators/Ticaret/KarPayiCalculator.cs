using System.Globalization;
using LexCalculus.Core.Calculators.Common;

namespace LexCalculus.Core.Calculators.Ticaret;

/// <summary>
/// H2 Anonim Şirket Kâr Payı — TTK m.508 (kâr dağıtımı) + m.519 (yedek akçe).
///
/// Akış:
///   1. Yasal yedek (m.519/1): net kârın %5'i, ancak yasal yedek toplamı
///      kanuni sermayenin %20'sini aşamaz (cap). Cap dolduysa daha ayrılmaz.
///   2. Dağıtılabilir = net kâr - yasal yedek.
///   3. Birinci temettü (m.508/1): kanuni sermayenin %5'i asgari. Dağıtılabilir
///      bu tutara yetmezse mevcut tutarla sınırlı.
///   4. Opsiyonel özel yedek (m.519/2): birinci temettü sonrası kalanın %10'u.
///      Esas sözleşme şartına bağlıdır.
///   5. İkinci temettü (m.508/2): kalan.
///   6. Toplam temettü = birinci + ikinci.
///
/// Saf hesap — DB bağımlılığı yok, parametresiz (oranlar TTK sabit).
/// </summary>
public sealed class KarPayiCalculator : ICalculator<KarPayiInput, KarPayiResult>
{
    private const decimal YasalYedekOran = 0.05m;          // m.519/1
    private const decimal YasalYedekCapOran = 0.20m;       // m.519/1 cap (sermayenin %20'si)
    private const decimal BirinciTemettuOran = 0.05m;      // m.508/1
    private const decimal OzelYedekOran = 0.10m;           // m.519/2

    public CalculatorMetadata Metadata { get; } = new()
    {
        Slug = "kar-payi",
        Category = CalculatorCategory.Ticaret,
        Title = "Anonim Şirket Kâr Payı",
        ShortDescription = "TTK m.508 + m.519 — net kârdan yasal yedek (%5, sermaye %20 cap), birinci temettü (sermaye %5), opsiyonel özel yedek (%10) ve ikinci temettü dağıtımı.",
        LegalReference = "TTK m.508 / m.519",
        Status = CalculatorStatus.Active,
        DisplayNumber = "38",
        Keywords = new[] { "kâr payı", "temettü", "TTK 508", "TTK 519", "yasal yedek", "birinci temettü", "ikinci temettü" }
    };

    public Task<KarPayiResult> CalculateAsync(KarPayiInput input, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        var result = new KarPayiResult();

        if (input.NetKar is null or <= 0)
            result.ValidationErrors[nameof(input.NetKar)] = "Net kâr pozitif olmalıdır.";
        if (input.KanuniSermaye is null or <= 0)
            result.ValidationErrors[nameof(input.KanuniSermaye)] = "Kanuni sermaye pozitif olmalıdır.";
        if (input.MevcutYasalYedek is < 0)
            result.ValidationErrors[nameof(input.MevcutYasalYedek)] = "Mevcut yasal yedek negatif olamaz.";

        if (result.ValidationErrors.Count > 0)
        {
            result.IsValid = false;
            return Task.FromResult(result);
        }

        var netKar = input.NetKar!.Value;
        var sermaye = input.KanuniSermaye!.Value;
        var mevcutYedek = input.MevcutYasalYedek ?? 0m;

        // 1. Yasal yedek (m.519/1) — sermaye %20 cap.
        var yasalYedekLimit = Math.Round(sermaye * YasalYedekCapOran, 2, MidpointRounding.AwayFromZero);
        var yedekKalanLimit = Math.Max(0m, yasalYedekLimit - mevcutYedek);
        var hesaplananYedek = Math.Round(netKar * YasalYedekOran, 2, MidpointRounding.AwayFromZero);
        var yasalYedekAyrilan = Math.Min(hesaplananYedek, yedekKalanLimit);
        var capDoldu = yedekKalanLimit == 0m;

        var dagitilabilir = netKar - yasalYedekAyrilan;

        // 2. Birinci temettü (m.508/1) — sermaye %5 asgari.
        var birinciTemettuAsgari = Math.Round(sermaye * BirinciTemettuOran, 2, MidpointRounding.AwayFromZero);
        var birinciTemettu = Math.Min(birinciTemettuAsgari, dagitilabilir);

        var kalanDagitilabilir = dagitilabilir - birinciTemettu;

        // 3. Özel yedek (m.519/2, opsiyonel) — kalanın %10'u.
        var ozelYedek = input.OzelYedekUygulanir
            ? Math.Round(kalanDagitilabilir * OzelYedekOran, 2, MidpointRounding.AwayFromZero)
            : 0m;

        // 4. İkinci temettü (m.508/2).
        var ikinciTemettu = kalanDagitilabilir - ozelYedek;
        if (ikinciTemettu < 0m) ikinciTemettu = 0m;

        var toplamTemettu = birinciTemettu + ikinciTemettu;

        result.YasalYedekAyrilan = yasalYedekAyrilan;
        result.YasalYedekLimit = yasalYedekLimit;
        result.YasalYedekLimitDoldu = capDoldu;
        result.BirinciTemettu = birinciTemettu;
        result.OzelYedekAyrilan = ozelYedek;
        result.IkinciTemettu = ikinciTemettu;
        result.ToplamTemettu = toplamTemettu;
        result.KarDagitilabilir = dagitilabilir;

        var tr = new CultureInfo("tr-TR");

        result.Rows.Add(new() { Key = "Net Kâr", Value = netKar.ToString("N2", tr) + " TL" });
        result.Rows.Add(new() { Key = "Kanuni Sermaye", Value = sermaye.ToString("N2", tr) + " TL" });
        if (mevcutYedek > 0m)
            result.Rows.Add(new() { Key = "Mevcut Yasal Yedek", Value = mevcutYedek.ToString("N2", tr) + " TL" });
        result.Rows.Add(new() { Key = $"Yasal Yedek Limiti (sermaye × %20)", Value = yasalYedekLimit.ToString("N2", tr) + " TL" });

        if (capDoldu)
            result.Rows.Add(new() { Key = "Yasal Yedek Ayrılan", Value = "0,00 TL (limit doldu)" });
        else
            result.Rows.Add(new() { Key = "Yasal Yedek Ayrılan (m.519/1)", Value = yasalYedekAyrilan.ToString("N2", tr) + " TL" });

        result.Rows.Add(new() { Key = "Dağıtılabilir Kâr", Value = dagitilabilir.ToString("N2", tr) + " TL" });
        result.Rows.Add(new() { Key = $"Birinci Temettü (sermaye × %5 = {birinciTemettuAsgari.ToString("N2", tr)})", Value = birinciTemettu.ToString("N2", tr) + " TL" });

        if (input.OzelYedekUygulanir)
            result.Rows.Add(new() { Key = "Özel Yedek (m.519/2 — kalan × %10)", Value = ozelYedek.ToString("N2", tr) + " TL" });
        else
            result.Rows.Add(new() { Key = "Özel Yedek", Value = "Uygulanmıyor (esas sözleşme şartı yok)" });

        result.Rows.Add(new() { Key = "İkinci Temettü", Value = ikinciTemettu.ToString("N2", tr) + " TL" });
        result.Rows.Add(new() { Key = "Toplam Temettü (1. + 2.)", Value = toplamTemettu.ToString("N2", tr) + " TL", IsHighlighted = true });

        if (capDoldu)
            result.Warnings.Add("Yasal yedek (m.519/1) sermaye %20 sınırına ulaştığından bu yıl için yedek ayrılmadı.");
        if (birinciTemettu < birinciTemettuAsgari)
            result.Warnings.Add("Dağıtılabilir kâr birinci temettü asgarisinin altında kaldığı için birinci temettü mevcut tutarla sınırlandırıldı.");

        result.TotalAmount = toplamTemettu;
        result.TotalLabel = "Toplam Temettü";
        result.Unit = "TL";
        result.Note = SonucNote();
        return Task.FromResult(result);
    }

    private static string SonucNote() =>
        "<strong>Yöntem:</strong> TTK m.508 + m.519 — Net kârdan sırayla yasal yedek (%5, sermaye %20 cap), " +
        "birinci temettü (sermaye %5 asgari), opsiyonel özel yedek (kalanın %10'u, esas sözleşme şartı), " +
        "ve ikinci temettü ayrılır. <strong>Önemli:</strong> Genel kurul kararı, özel yedek akçe (m.521), " +
        "kâr dağıtımına katılma esasları (m.503), imtiyazlı pay sahiplerinin hakları (m.478) ayrıca değerlendirilir. " +
        "<strong>Bu sonuç genel kurul kararı yerine geçmez.</strong>";
}
