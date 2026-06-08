using System.ComponentModel.DataAnnotations;

namespace LexCalculus.Core.Calculators.AileMiras;

/// <summary>
/// Inputs for the legal inheritance share calculator (Yasal Miras Payı,
/// TMK m.495-501). The heir structure is the shared <see cref="MirasciYapisiInput"/>;
/// the total estate is optional (when given, TL amounts are computed).
/// </summary>
public sealed class MirasPayiInput
{
    [Display(Name = "Toplam Malvarlığı (TL)")]
    [Required(ErrorMessage = "Toplam malvarlığı boş olamaz.")]
    [Range(typeof(decimal), "0.01", "999999999999",
        ErrorMessage = "Toplam malvarlığı pozitif olmalıdır.",
        ParseLimitsInInvariantCulture = true)]
    public decimal? ToplamMalvarligi { get; set; }

    public MirasciYapisiInput Yapi { get; set; } = new();

    public DateTime AsOfDate { get; set; } = DateTime.UtcNow;
}
