using System.ComponentModel.DataAnnotations;
using LexCalculus.Core.Services;

namespace LexCalculus.Core.Calculators.AileMiras;

/// <summary>MVC-bindable heir structure shared by E2 Miras Payı and E3 Tenkis.
/// Maps to the service-layer <see cref="MirasciYapisi"/> via <see cref="ToYapi"/>.</summary>
public sealed class MirasciYapisiInput
{
    [Display(Name = "Sağ Kalan Eş Var")]
    public bool SagKalanEsVar { get; set; }

    [Display(Name = "Sağ Çocuk Sayısı")]
    [Range(0, 50, ErrorMessage = "Çocuk sayısı 0-50 arası olmalıdır.")]
    public int SagCocukSayisi { get; set; }

    public List<OlmusCocukGirdi> OlmusCocuklar { get; set; } = new();

    [Display(Name = "Ana Sağ")]
    public bool AnaSag { get; set; }

    [Display(Name = "Baba Sağ")]
    public bool BabaSag { get; set; }

    [Display(Name = "Sağ Kardeş Sayısı")]
    [Range(0, 50, ErrorMessage = "Kardeş sayısı 0-50 arası olmalıdır.")]
    public int KardesSayisi { get; set; }

    public List<OlmusKardesGirdi> OlmusKardesler { get; set; } = new();

    [Display(Name = "Büyük Ana-Baba (Dede/Nine) Sayısı")]
    [Range(0, 4, ErrorMessage = "Dede/nine sayısı 0-4 arası olmalıdır.")]
    public int DedeNineSayisi { get; set; }

    public MirasciYapisi ToYapi() => new()
    {
        SagKalanEsVar = SagKalanEsVar,
        SagCocukSayisi = SagCocukSayisi,
        OlmusCocuklar = OlmusCocuklar
            .Where(c => c.TorunSayisi > 0)
            .Select((c, i) => new OlmusCocuk { Tanim = string.IsNullOrWhiteSpace(c.Tanim) ? $"ölmüş çocuk {i + 1}" : c.Tanim!, TorunSayisi = c.TorunSayisi })
            .ToList(),
        AnaSag = AnaSag,
        BabaSag = BabaSag,
        KardesSayisi = KardesSayisi,
        OlmusKardesler = OlmusKardesler
            .Where(k => k.YeginSayisi > 0)
            .Select((k, i) => new OlmusKardes { Tanim = string.IsNullOrWhiteSpace(k.Tanim) ? $"ölmüş kardeş {i + 1}" : k.Tanim!, YeginSayisi = k.YeginSayisi })
            .ToList(),
        DedeNineSayisi = DedeNineSayisi
    };
}

public sealed class OlmusCocukGirdi
{
    [Display(Name = "Tanım")]
    public string? Tanim { get; set; }

    [Display(Name = "Torun Sayısı")]
    [Range(0, 50, ErrorMessage = "Torun sayısı 0-50 arası olmalıdır.")]
    public int TorunSayisi { get; set; }
}

public sealed class OlmusKardesGirdi
{
    [Display(Name = "Tanım")]
    public string? Tanim { get; set; }

    [Display(Name = "Yeğen Sayısı")]
    [Range(0, 50, ErrorMessage = "Yeğen sayısı 0-50 arası olmalıdır.")]
    public int YeginSayisi { get; set; }
}
