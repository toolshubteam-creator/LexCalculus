using System.Globalization;
using LexCalculus.Core.Calculators.Common;

namespace LexCalculus.Core.Calculators.Ticaret;

/// <summary>
/// H3 Sözleşme Cezası (Ceza Şartı) — TBK m.179-182.
///
/// Şekil:
///   SabitTutar → ceza doğrudan girilen tutar
///   YuzdeOran  → ceza = asıl borç × belirlenen oran
///
/// Fahiş değerlendirme bantları (asıl borca oranı):
///   ≤ 1.0          → Standart
///   1.0 &lt; oran ≤ 2.0 → DikkatEdici (asıl borcu aşıyor)
///   &gt; 2.0          → Fahis (m.182 hâkim takdiri için aday)
///
/// Saf hesap — DB bağımlılığı yok, parametresiz. m.182 hâkim takdiri her
/// durumda uyarı olarak gösterilir; bu hesap "fahiş mi?" sonucu vermez,
/// yargı pratiği için indikatör sunar.
/// </summary>
public sealed class SozlesmeCezasiCalculator : ICalculator<SozlesmeCezasiInput, SozlesmeCezasiResult>
{
    public CalculatorMetadata Metadata { get; } = new()
    {
        Slug = "sozlesme-cezasi",
        Category = CalculatorCategory.Ticaret,
        Title = "Sözleşme Cezası (Ceza Şartı)",
        ShortDescription = "TBK m.179-182 — sabit tutar veya yüzde oran şeklinde ceza şartı hesabı; cezanın asıl borca oranına göre fahiş değerlendirme bandı (m.182 hâkim takdir referansı).",
        LegalReference = "TBK m.179-182",
        Status = CalculatorStatus.Active,
        DisplayNumber = "39",
        Keywords = new[] { "ceza şartı", "sözleşme cezası", "TBK 179", "TBK 182", "hâkim indirimi", "fahiş ceza", "cezai şart" }
    };

    public Task<SozlesmeCezasiResult> CalculateAsync(SozlesmeCezasiInput input, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        var result = new SozlesmeCezasiResult();

        if (input.AsilBorc is null or <= 0)
            result.ValidationErrors[nameof(input.AsilBorc)] = "Asıl borç pozitif olmalıdır.";

        if (input.CezaSekli == CezaSekli.SabitTutar)
        {
            if (input.BelirlenenCeza is null or < 0)
                result.ValidationErrors[nameof(input.BelirlenenCeza)] = "Belirlenen ceza tutarı negatif olamaz.";
        }
        else
        {
            if (input.BelirlenenOran is null or < 0)
                result.ValidationErrors[nameof(input.BelirlenenOran)] = "Belirlenen oran negatif olamaz.";
        }

        if (result.ValidationErrors.Count > 0)
        {
            result.IsValid = false;
            return Task.FromResult(result);
        }

        var asil = input.AsilBorc!.Value;
        var ceza = input.CezaSekli == CezaSekli.SabitTutar
            ? Math.Round(input.BelirlenenCeza!.Value, 2, MidpointRounding.AwayFromZero)
            : Math.Round(asil * (input.BelirlenenOran!.Value / 100m), 2, MidpointRounding.AwayFromZero);

        var kat = asil > 0m ? Math.Round(ceza / asil, 4, MidpointRounding.AwayFromZero) : 0m;

        var degerlendirme = kat switch
        {
            > 2.0m => FahisDegerlendirmesi.Fahis,
            > 1.0m => FahisDegerlendirmesi.DikkatEdici,
            _ => FahisDegerlendirmesi.Standart
        };

        result.AsilBorc = asil;
        result.HesaplananCeza = ceza;
        result.AsilBorcKati = kat;
        result.FahisDegerlendirmesi = degerlendirme;

        var tr = new CultureInfo("tr-TR");

        result.Rows.Add(new() { Key = "Asıl Borç", Value = asil.ToString("N2", tr) + " TL" });
        result.Rows.Add(new() { Key = "Ceza Şekli", Value = input.CezaSekli == CezaSekli.SabitTutar ? "Sabit Tutar" : "Yüzde Oran" });
        if (input.CezaSekli == CezaSekli.YuzdeOran)
            result.Rows.Add(new() { Key = "Belirlenen Oran", Value = $"%{input.BelirlenenOran!.Value.ToString("0.##", tr)}" });
        result.Rows.Add(new() { Key = "İhlal Türü", Value = IhlalAdi(input.IhlalTuru) });
        result.Rows.Add(new() { Key = "Hesaplanan Ceza", Value = ceza.ToString("N2", tr) + " TL", IsHighlighted = true });
        result.Rows.Add(new() { Key = "Asıl Borç Katı (ceza ÷ asıl)", Value = kat.ToString("0.###", tr) });
        result.Rows.Add(new() { Key = "Değerlendirme", Value = DegerlendirmeAdi(degerlendirme) });

        // Uyarılar — her durumda hâkim takdir uyarısı eklenir.
        switch (degerlendirme)
        {
            case FahisDegerlendirmesi.Fahis:
                result.Warnings.Add($"Ceza asıl borcun {kat.ToString("0.##", tr)} katıdır; FAHİŞ değerlendirmesi yapılabilir (TBK m.182 hâkim takdiri ile indirim olasılığı yüksek).");
                break;
            case FahisDegerlendirmesi.DikkatEdici:
                result.Warnings.Add($"Ceza asıl borcun {kat.ToString("0.##", tr)} katıdır (asıl borcu aşıyor); hâkim TBK m.182 ile ölçülülük denetimi yapabilir.");
                break;
        }
        result.Warnings.Add("Hâkim TBK m.182 uyarınca cezayı hakkaniyete uygun olarak azaltabilir; bu değerlendirme yargı pratiği için indikatördür.");

        result.TotalAmount = ceza;
        result.TotalLabel = "Hesaplanan Ceza";
        result.Unit = "TL";
        result.Note = SonucNote();
        return Task.FromResult(result);
    }

    private static string IhlalAdi(IhlalTuru t) => t switch
    {
        IhlalTuru.TemerruttenDolayi => "Temerrütten dolayı (TBK m.180/1 — ifa + ceza)",
        IhlalTuru.IfaDanGecisi => "İfadan geçiş (TBK m.180/2 — ceza yerine kabul)",
        _ => t.ToString()
    };

    private static string DegerlendirmeAdi(FahisDegerlendirmesi d) => d switch
    {
        FahisDegerlendirmesi.Standart => "Standart (asıl borcun üstünde değil)",
        FahisDegerlendirmesi.DikkatEdici => "Dikkat edici (asıl borcu aşıyor)",
        FahisDegerlendirmesi.Fahis => "Fahiş olabilir (asıl borcun 2 katından fazla)",
        _ => d.ToString()
    };

    private static string SonucNote() =>
        "<strong>Yöntem:</strong> TBK m.179-180 — Sabit tutar şeklinde ceza doğrudan, yüzde şekilde asıl borç × oran " +
        "olarak hesaplanır. Cezanın asıl borca oranı m.182 hâkim takdiri için indikatör değerlendirme bandında raporlanır. " +
        "<strong>Önemli:</strong> Hâkim TBK m.182 uyarınca cezayı hakkaniyete uygun olarak azaltabilir. İfadan geçiş ile " +
        "temerrütten dolayı talep (m.180) ayrı değerlendirilir; tacirler için m.182 indirimi farklı yorumlanır. " +
        "<strong>Bu sonuç mahkeme kararı yerine geçmez.</strong>";
}
