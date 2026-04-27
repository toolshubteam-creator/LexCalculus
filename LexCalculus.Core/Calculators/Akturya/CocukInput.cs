using System.ComponentModel.DataAnnotations;
using LexCalculus.Core.Enums;

namespace LexCalculus.Core.Calculators.Akturya;

/// <summary>
/// Per-child input. Each child has independent birth date, gender, and
/// student status — required for accurate per-child support period calculation
/// (a 5yo and a 15yo cannot be averaged: they receive support for very
/// different durations).
/// </summary>
public sealed class CocukInput
{
    [Display(Name = "Doğum Tarihi")]
    [Required(ErrorMessage = "Çocuk doğum tarihi boş olamaz.")]
    [DataType(DataType.Date)]
    public DateTime? DogumTarihi { get; set; }

    [Display(Name = "Cinsiyet")]
    [Required]
    public Cinsiyet Cinsiyet { get; set; } = Cinsiyet.Erkek;

    [Display(Name = "Eğitimde")]
    public bool Ogrenci { get; set; }
}
