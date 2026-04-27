using System.Globalization;
using LexCalculus.Core.Calculators.Common;
using LexCalculus.Core.Interfaces;
using LexCalculus.Core.Models.Calculators;

namespace LexCalculus.Core.Calculators.IsHukuku;

/// <summary>
/// Mobbing / non-pecuniary damages calculator (Mobbing / Manevi Tazminat).
///
/// Legal basis: TBK m.58, 4857 s.K. m.5/77, Yargıtay 9. HD case law.
///
/// CRITICAL: Unlike severance/notice/leave, mobbing damages have NO statutory
/// formula. Courts decide based on case-by-case discretion. This calculator
/// produces a SUGGESTED RANGE based on case law averages — actual award may
/// differ significantly.
///
/// Methodology (built from Yargıtay 9. HD aggregate):
///
///   Base months by severity × duration:
///     Hafif:    1-3 ay × brüt
///     Orta:     3-6 ay × brüt
///     Agir:     6-12 ay × brüt
///     CokAgir:  12-24 ay × brüt
///
///   Adjustments (additive to base months):
///     +2 ay if süre ≥ 24 ay (sustained mobbing, 9. HD pattern)
///     +3 ay if sağlık raporu var (concrete medical proof)
///     +2 ay if mobbing sebepli istifa
///     +1 ay if büyük holding/çok uluslu (deeper pockets, larger awards)
///     +1 ay if kamu kurumu (public accountability)
///
/// Mobbing formal requirements (court will verify):
///   - 6+ ay süre (some courts strict, others flexible)
///   - Systematic, targeted, repeated
///   - Demonstrable harm
///
/// Statute of limitations: 1 year (TBK m.72).
/// </summary>
public sealed class MobbingCalculator : ICalculator<MobbingInput, MobbingResult>
{
    private const string Slug = "mobbing-tazminati";
    private const int MinMobbingSureAy = 6;

    private readonly IFormulaParameterService _params;

    public MobbingCalculator(IFormulaParameterService parameters)
    {
        _params = parameters ?? throw new ArgumentNullException(nameof(parameters));
    }

    public CalculatorMetadata Metadata { get; } = new()
    {
        Slug = Slug,
        Category = CalculatorCategory.IsHukuku,
        Title = "Mobbing / Manevi Tazminat",
        ShortDescription = "Yargıtay emsal kararları tabanlı tahmin: süre, şiddet, sağlık raporu ve işveren niteliğine göre tazminat aralığı.",
        LegalReference = "TBK m.58 / 4857 s.K. m.5,77",
        Status = CalculatorStatus.Active,
        DisplayNumber = "07",
        Keywords = new[] { "mobbing", "manevi tazminat", "iş hukuku", "psikolojik taciz" }
    };

    public Task<MobbingResult> CalculateAsync(MobbingInput input, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        var result = new MobbingResult();

        if (input.BrutAylikUcret is null or <= 0)
            result.ValidationErrors[nameof(input.BrutAylikUcret)] = "Brüt ücret pozitif olmalıdır.";
        if (input.SureAy is null or <= 0)
            result.ValidationErrors[nameof(input.SureAy)] = "Süre pozitif olmalıdır.";

        if (result.ValidationErrors.Count > 0)
        {
            result.IsValid = false;
            return Task.FromResult(result);
        }

        var brut = input.BrutAylikUcret!.Value;
        var sure = input.SureAy!.Value;

        var (tabanAy, ustAy) = input.Siddet switch
        {
            MobbingSiddeti.Hafif    => (1, 3),
            MobbingSiddeti.Orta     => (3, 6),
            MobbingSiddeti.Agir     => (6, 12),
            MobbingSiddeti.CokAgir  => (12, 24),
            _ => (3, 6)
        };

        int ekUst = 0;
        var faktorler = new List<string>();

        if (sure >= 24)
        {
            ekUst += 2;
            faktorler.Add("Mobbing süresi 2+ yıl (sürekli mobbing): +2 ay üst sınıra");
        }
        if (input.SaglikRaporu)
        {
            ekUst += 3;
            faktorler.Add("Sağlık raporu / tıbbi belge: +3 ay (somut zarar kanıtı)");
        }
        if (input.IstifaSebebi)
        {
            ekUst += 2;
            faktorler.Add("Mobbing sebepli istifa: +2 ay");
        }
        if (input.IsverenTipi == IsverenKonumu.BuyukHolding)
        {
            ekUst += 1;
            faktorler.Add("Büyük holding / çok uluslu işveren: +1 ay");
        }
        if (input.IsverenTipi == IsverenKonumu.Kamu)
        {
            ekUst += 1;
            faktorler.Add("Kamu kurumu işveren: +1 ay");
        }

        var nihaiUstAy = ustAy + ekUst;

        var altSinir = brut * tabanAy;
        var ustSinir = brut * nihaiUstAy;
        var onerilen = Math.Round((altSinir + ustSinir) / 2m + (ustSinir - altSinir) * 0.2m, 2, MidpointRounding.AwayFromZero);

        var mahkemeyeUygun = sure >= MinMobbingSureAy;

        result.TabanAyKatsayisi = tabanAy;
        result.UstAyKatsayisi = nihaiUstAy;
        result.AltSinirTutar = altSinir;
        result.UstSinirTutar = ustSinir;
        result.OnerilenTutar = onerilen;
        result.MahkemeyeUygunDelilVarMi = mahkemeyeUygun;
        result.EmsalKarakteristik = faktorler;

        var tr = new CultureInfo("tr-TR");
        result.TotalAmount = onerilen;
        result.TotalLabel = "Önerilen Talep Tutarı";
        result.Unit = "TL";

        result.Rows.Add(new CalculationResultRow { Key = "Mobbing Şiddeti", Value = input.Siddet.ToString() });
        result.Rows.Add(new CalculationResultRow { Key = "Süre", Value = $"{sure} ay" });
        result.Rows.Add(new CalculationResultRow { Key = "Brüt Aylık Ücret", Value = brut.ToString("N2", tr) + " TL" });
        result.Rows.Add(new CalculationResultRow { Key = "Taban Katsayı", Value = $"{tabanAy} ay × brüt" });
        result.Rows.Add(new CalculationResultRow { Key = "Üst Katsayı (düzeltmelerle)", Value = $"{nihaiUstAy} ay × brüt" });
        result.Rows.Add(new CalculationResultRow { Key = "Alt Sınır Tutar", Value = altSinir.ToString("N2", tr) + " TL" });
        result.Rows.Add(new CalculationResultRow { Key = "Üst Sınır Tutar", Value = ustSinir.ToString("N2", tr) + " TL" });
        result.Rows.Add(new CalculationResultRow { Key = "Önerilen Talep", Value = onerilen.ToString("N2", tr) + " TL", IsHighlighted = true });

        if (!mahkemeyeUygun)
        {
            result.Warnings.Add($"Mobbing süresi {sure} ay; Yargıtay içtihatlarında genellikle minimum {MinMobbingSureAy} ay aranır. Davanın mobbing değil ayrı bir hukuki kavram (örn. haksız fiil) olarak değerlendirilmesi gerekebilir.");
        }

        if (!input.SaglikRaporu && (input.Siddet == MobbingSiddeti.Agir || input.Siddet == MobbingSiddeti.CokAgir))
        {
            result.Warnings.Add("Ağır/çok ağır mobbing iddiasında sağlık raporu (psikiyatrist, depresyon, anksiyete vb.) ispat açısından kritiktir. Eksiklik tutarı düşürebilir.");
        }

        result.Note = "<strong>UYARI:</strong> Manevi tazminatın <em>yasal formülü yoktur</em>. Bu hesap Yargıtay 9. HD emsal kararlarından çıkarılmış <em>tahmindir</em>; mahkeme takdir hakkına göre çok farklı bir rakama karar verebilir. " +
                      "<strong>Mobbing Unsurları:</strong> Sistematiklik, süreklilik (en az 6 ay), hedeflilik, zarar — dördü birlikte ispatlanmalı. " +
                      "<strong>İspat Yükü:</strong> İşçide. Email/mesaj/tanık/sağlık raporu kritik. " +
                      "<strong>Zamanaşımı:</strong> 1 yıl (TBK m.72) — öğrenme tarihinden itibaren.";

        return Task.FromResult(result);
    }
}
