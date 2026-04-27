using System.ComponentModel.DataAnnotations;

namespace LexCalculus.Core.Enums;

/// <summary>
/// Gender used in actuarial calculations. Türkiye life tables (TRH 2010+)
/// publish separate values for male and female.
/// </summary>
public enum Cinsiyet
{
    [Display(Name = "Erkek")]
    Erkek = 1,

    [Display(Name = "Kadın")]
    Kadin = 2
}
