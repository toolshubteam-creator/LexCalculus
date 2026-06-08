using System.Globalization;
using LexCalculus.Core.Calculators.Common;
using LexCalculus.Core.Models.Calculators;

namespace LexCalculus.Core.Calculators.AileMiras;

/// <summary>
/// Marital property regime liquidation calculator (Edinilmiş Mallara Katılma
/// Rejimi Tasfiyesi).
///
/// Legal basis: TMK m.218-241. The "edinilmiş mallara katılma" regime is the
/// default for marriages from 01.01.2002 onward. On dissolution each spouse is
/// entitled to ONE-HALF of the OTHER spouse's artık değer (surplus value):
///
///   Artık Değer (eş)        = max(0, evlilik içi edinilen mal - ilgili borç)
///   Katılma Alacağı (Eş 1)  = Eş 2 Artık Değer × ½
///   Katılma Alacağı (Eş 2)  = Eş 1 Artık Değer × ½
///   Net Alacak              = Eş 1 Katılma Alacağı - Eş 2 Katılma Alacağı
///
/// Net Alacak &gt; 0 → Eş 1 alacaklı (= Eş 1'in artık değeri daha DÜŞÜK; düşük
/// olan eş net alacaklıdır). Personal property (kişisel mal: evlilik öncesi mal +
/// miras/bağış, TMK m.220) is EXCLUDED from the artık değer.
///
/// No FormulaParameters — every figure is user input.
/// </summary>
public sealed class MalRejimiTasfiyesiCalculator : ICalculator<MalRejimiTasfiyesiInput, MalRejimiTasfiyesiResult>
{
    public CalculatorMetadata Metadata { get; } = new()
    {
        Slug = "mal-rejimi-tasfiyesi",
        Category = CalculatorCategory.AileMiras,
        Title = "Mal Rejimi Tasfiyesi",
        ShortDescription = "TMK m.218-241 — edinilmiş mallara katılma rejiminde artık değer ve katılma alacağı; kişisel mal (evlilik öncesi + miras/bağış) tasfiye dışı.",
        LegalReference = "TMK m.218-241",
        Status = CalculatorStatus.Active,
        DisplayNumber = "24",
        Keywords = new[] { "mal rejimi", "tasfiye", "edinilmiş mal", "katılma alacağı", "artık değer", "boşanma" }
    };

    public Task<MalRejimiTasfiyesiResult> CalculateAsync(MalRejimiTasfiyesiInput input, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        var result = new MalRejimiTasfiyesiResult();

        ValidateNonNegative(result, nameof(input.Es1EdinilenMal), input.Es1EdinilenMal);
        ValidateNonNegative(result, nameof(input.Es1Borc), input.Es1Borc);
        ValidateNonNegative(result, nameof(input.Es2EdinilenMal), input.Es2EdinilenMal);
        ValidateNonNegative(result, nameof(input.Es2Borc), input.Es2Borc);

        if (result.ValidationErrors.Count > 0)
        {
            result.IsValid = false;
            return Task.FromResult(result);
        }

        // Kişisel mal (evlilik öncesi + miras/bağış) tasfiye DIŞI — sadece raporlanır.
        var es1Kisisel = (input.Es1EvlilikOncesiMal ?? 0m) + (input.Es1MirasBagis ?? 0m);
        var es2Kisisel = (input.Es2EvlilikOncesiMal ?? 0m) + (input.Es2MirasBagis ?? 0m);

        // Artık değer = max(0, edinilmiş mal - borç). Borç fazlaysa artık değer 0 (negatif olmaz).
        var es1Artik = Math.Max(0m, (input.Es1EdinilenMal ?? 0m) - (input.Es1Borc ?? 0m));
        var es2Artik = Math.Max(0m, (input.Es2EdinilenMal ?? 0m) - (input.Es2Borc ?? 0m));

        var es1Katilma = Math.Round(es2Artik * 0.5m, 2, MidpointRounding.AwayFromZero);
        var es2Katilma = Math.Round(es1Artik * 0.5m, 2, MidpointRounding.AwayFromZero);

        var net = es1Katilma - es2Katilma;

        result.Es1KisiselMal = es1Kisisel;
        result.Es2KisiselMal = es2Kisisel;
        result.Es1ArtikDeger = es1Artik;
        result.Es2ArtikDeger = es2Artik;
        result.Es1KatilmaAlacagi = es1Katilma;
        result.Es2KatilmaAlacagi = es2Katilma;
        result.NetAlacak = net;

        string netAciklama;
        if (net > 0m)
        {
            result.AlacakliEs = AlacakliEs.Es1;
            netAciklama = $"Eş 1, Eş 2'den net {net.ToString("N2", new CultureInfo("tr-TR"))} TL katılma alacağı talep edebilir.";
        }
        else if (net < 0m)
        {
            result.AlacakliEs = AlacakliEs.Es2;
            netAciklama = $"Eş 2, Eş 1'den net {Math.Abs(net).ToString("N2", new CultureInfo("tr-TR"))} TL katılma alacağı talep edebilir.";
        }
        else
        {
            result.AlacakliEs = AlacakliEs.Esit;
            netAciklama = "Karşılıklı katılma alacakları eşit; net alacak doğmaz (tasfiye dengelidir).";
        }

        var tr = new CultureInfo("tr-TR");
        result.Rows.Add(new CalculationResultRow { Key = "Eş 1 Artık Değer", Value = es1Artik.ToString("N2", tr) + " TL" });
        result.Rows.Add(new CalculationResultRow { Key = "Eş 2 Artık Değer", Value = es2Artik.ToString("N2", tr) + " TL" });
        result.Rows.Add(new CalculationResultRow { Key = "Eş 1 Katılma Alacağı (Eş 2 artık × ½)", Value = es1Katilma.ToString("N2", tr) + " TL" });
        result.Rows.Add(new CalculationResultRow { Key = "Eş 2 Katılma Alacağı (Eş 1 artık × ½)", Value = es2Katilma.ToString("N2", tr) + " TL" });
        if (es1Kisisel > 0m)
            result.Rows.Add(new CalculationResultRow { Key = "Eş 1 Kişisel Mal (tasfiye dışı)", Value = es1Kisisel.ToString("N2", tr) + " TL" });
        if (es2Kisisel > 0m)
            result.Rows.Add(new CalculationResultRow { Key = "Eş 2 Kişisel Mal (tasfiye dışı)", Value = es2Kisisel.ToString("N2", tr) + " TL" });
        result.Rows.Add(new CalculationResultRow { Key = "Net Sonuç", Value = netAciklama, IsHighlighted = true });

        result.TotalAmount = Math.Abs(net);
        result.TotalLabel = result.AlacakliEs switch
        {
            AlacakliEs.Es1 => "Eş 1 Net Katılma Alacağı",
            AlacakliEs.Es2 => "Eş 2 Net Katılma Alacağı",
            _ => "Net Katılma Alacağı"
        };
        result.Unit = "TL";

        result.Note = "<strong>Yöntem:</strong> Her eş, diğerinin artık değerinin yarısı oranında katılma alacağına hak kazanır (TMK m.236). " +
                      "Artık değer = evlilik içinde edinilen mal − ilgili borç; negatif çıkması hâlinde sıfır kabul edilir. " +
                      "<strong>Mevzuat:</strong> TMK m.218-241 (edinilmiş mallara katılma — 01.01.2002 sonrası yasal rejim). " +
                      "<strong>Önemli:</strong> Bu hesap mal rejimi tasfiyesi için temel formülü uygular. Somut davada malların değer takdiri uzman bilirkişi raporuyla yapılır, kişisel mal-edinilmiş mal ayrımı dava sürecinde tespit edilir. Bu sonuç bilirkişi raporu yerine geçmez.";

        return Task.FromResult(result);
    }

    private static void ValidateNonNegative(MalRejimiTasfiyesiResult result, string field, decimal? value)
    {
        if (value is < 0m)
            result.ValidationErrors[field] = "Değer negatif olamaz.";
    }
}
