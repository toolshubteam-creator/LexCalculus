using System.ComponentModel.DataAnnotations;

namespace LexCalculus.Core.Enums;

/// <summary>
/// Day count convention for interest calculations.
/// Turkish law (3095 s.K., Yargıtay) generally uses 365.
/// Banking/commercial contexts sometimes use 360 (continental convention).
/// </summary>
public enum GunYiliBazi
{
    [Display(Name = "365 gün (3095 s.K. / Yargıtay)")]
    UcYuzAltmisBes = 365,

    [Display(Name = "360 gün (Bankacılık konvansiyonu)")]
    UcYuzAltmis = 360
}
