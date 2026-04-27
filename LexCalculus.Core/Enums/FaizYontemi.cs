using System.ComponentModel.DataAnnotations;

namespace LexCalculus.Core.Enums;

/// <summary>
/// Interest calculation method.
/// Simple: principal × rate × time (3095 default)
/// Compound: (1 + rate/n)^(nt) — only allowed under TTK for merchants per 3095 m.3 exception
/// </summary>
public enum FaizYontemi
{
    [Display(Name = "Basit Faiz (3095 standart)")]
    Basit = 0,

    [Display(Name = "Bileşik Faiz (TTK — sadece tacirler arası yazılı sözleşme)")]
    Bilesik = 1
}
