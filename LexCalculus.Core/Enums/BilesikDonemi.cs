using System.ComponentModel.DataAnnotations;

namespace LexCalculus.Core.Enums;

/// <summary>
/// Compounding frequency. Banking convention: monthly. TTK silent on default.
/// </summary>
public enum BilesikDonemi
{
    [Display(Name = "Aylık (12/yıl — bankacılık standardı)")]
    Aylik = 12,

    [Display(Name = "Üç Aylık (4/yıl)")]
    UcAylik = 4,

    [Display(Name = "Altı Aylık (2/yıl)")]
    AltiAylik = 2,

    [Display(Name = "Yıllık (1/yıl)")]
    Yillik = 1
}
