using System.Globalization;
using LexCalculus.Core.Calculators.Common;
using LexCalculus.Core.Models.Calculators;
using LexCalculus.Core.Services;

namespace LexCalculus.Core.Calculators.AileMiras;

/// <summary>
/// Legal inheritance share calculator (Yasal Miras Payı). Wraps
/// <see cref="IInheritanceDistributionService"/> (TMK m.495-501 zümre system +
/// m.498 halefiyet) and maps the distribution to the result envelope.
///
/// This is statutory distribution only — vasiyet, bağış, ıskat, yoksunluk are
/// out of scope (see the UI warning; tenkis has its own tool).
/// </summary>
public sealed class MirasPayiCalculator : ICalculator<MirasPayiInput, MirasPayiResult>
{
    private readonly IInheritanceDistributionService _dagitim;

    public MirasPayiCalculator(IInheritanceDistributionService dagitim)
    {
        _dagitim = dagitim ?? throw new ArgumentNullException(nameof(dagitim));
    }

    public CalculatorMetadata Metadata { get; } = new()
    {
        Slug = "miras-payi",
        Category = CalculatorCategory.AileMiras,
        Title = "Yasal Miras Payı",
        ShortDescription = "TMK m.495-501 — yasal mirasçılık zümre sistemine göre sağ kalan eş ve altsoy/ana-baba/büyük ana-baba paylarının dağıtımı; ölmüş mirasçıda halefiyet (m.498).",
        LegalReference = "TMK m.495-501",
        Status = CalculatorStatus.Active,
        DisplayNumber = "25",
        Keywords = new[] { "miras payı", "yasal mirasçı", "zümre", "halefiyet", "saklı pay", "TMK 495" }
    };

    public Task<MirasPayiResult> CalculateAsync(MirasPayiInput input, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        var result = new MirasPayiResult();

        if (input.ToplamMalvarligi is null or <= 0)
            result.ValidationErrors[nameof(input.ToplamMalvarligi)] = "Toplam malvarlığı pozitif olmalıdır.";

        var yapi = input.Yapi.ToYapi();
        var dagilim = _dagitim.Dagit(yapi, input.ToplamMalvarligi);

        if (dagilim.Paylar.Count == 0)
            result.ValidationErrors[nameof(input.Yapi)] =
                "Hiç yasal mirasçı girilmedi. En az bir mirasçı (eş, çocuk, ana-baba, kardeş veya dede-nine) belirtilmelidir.";

        if (result.ValidationErrors.Count > 0)
        {
            result.IsValid = false;
            return Task.FromResult(result);
        }

        var tr = new CultureInfo("tr-TR");
        result.AktifDerece = dagilim.AktifDerece;

        foreach (var p in dagilim.Paylar)
        {
            result.PayListesi.Add(new MirasPayiSatiri
            {
                Tanim = p.Tanim,
                MirasciTuru = p.MirasciTuru,
                PayKesri = p.PayKesri,
                PayTutari = p.PayTutari
            });

            var yuzde = (p.PayKesri * 100m).ToString("0.####", tr);
            var tutar = p.PayTutari.HasValue ? $" — {p.PayTutari.Value.ToString("N2", tr)} TL" : "";
            result.Rows.Add(new CalculationResultRow
            {
                Key = p.Tanim,
                Value = $"%{yuzde}{tutar}"
            });
        }

        result.Rows.Add(new CalculationResultRow
        {
            Key = "Aktif Zümre (Derece)",
            Value = dagilim.AktifDerece == 0 ? "Zümre mirasçısı yok" : $"{dagilim.AktifDerece}. derece",
            IsHighlighted = true
        });

        result.TotalAmount = input.ToplamMalvarligi;
        result.TotalLabel = "Toplam Malvarlığı (dağıtılan)";
        result.Unit = "TL";

        result.Note = "<strong>Yöntem:</strong> " + dagilim.Aciklama + " " +
                      "<strong>Mevzuat:</strong> TMK m.495-501 (zümre sistemi + eş payı), m.498 (halefiyet). " +
                      "<strong>Önemli:</strong> Bu hesap yalnızca yasal mirasçılık oranlarını gösterir. Vasiyet, sağlar arası bağış, mirastan ıskat veya mirastan yoksunluk gibi durumlar bu hesabın dışındadır; saklı pay/tenkis için Tenkis Hesaplama aracına bakınız. Bu sonuç mahkeme kararı veya uzman bilirkişi raporu yerine geçmez.";

        return Task.FromResult(result);
    }
}
