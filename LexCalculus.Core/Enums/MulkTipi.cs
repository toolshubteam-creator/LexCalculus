using System.ComponentModel.DataAnnotations;

namespace LexCalculus.Core.Enums;

/// <summary>
/// Property type for rent increase calculation.
/// TBK m.344 applies same TÜFE 12-month average ceiling to both,
/// but 7409 sayılı Kanun temporary %25 cap (11.06.2022-01.07.2024)
/// applied ONLY to Konut.
/// </summary>
public enum MulkTipi
{
    [Display(Name = "Konut (mesken, daire, ev)")]
    Konut = 0,

    [Display(Name = "Çatılı İşyeri (dükkan, ofis, mağaza, depo)")]
    CatliIsyeri = 1
}
