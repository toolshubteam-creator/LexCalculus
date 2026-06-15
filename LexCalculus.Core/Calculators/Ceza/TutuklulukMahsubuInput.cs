using System.ComponentModel.DataAnnotations;

namespace LexCalculus.Core.Calculators.Ceza;

/// <summary>
/// F5 Tutukluluk Mahsup — TCK m.63 + 5275 s.K. m.108. Tutukluluk gün sayısı =
/// (bitis - baslangic) + 1 (inclusive). Adli para mahsubu istenirse günlük
/// miktar ile çarpılır (TCK m.63 son fıkra atfı).
/// </summary>
public sealed class TutuklulukMahsubuInput
{
    [Display(Name = "Tutukluluk Başlangıç Tarihi")]
    [Required(ErrorMessage = "Tutukluluk başlangıç tarihi boş olamaz.")]
    [DataType(DataType.Date)]
    public DateTime? TutuklulukBaslangic { get; set; }

    [Display(Name = "Tutukluluk Bitiş Tarihi")]
    [Required(ErrorMessage = "Tutukluluk bitiş tarihi boş olamaz.")]
    [DataType(DataType.Date)]
    public DateTime? TutuklulukBitis { get; set; }

    [Display(Name = "Adli Para Cezasından Mahsup Et")]
    public bool AdliParaMahsubu { get; set; }

    [Display(Name = "Günlük Miktar (TL) — adli para mahsubu için")]
    [Range(typeof(decimal), "20", "100",
        ErrorMessage = "Günlük miktar 20-100 TL arası olmalıdır.",
        ParseLimitsInInvariantCulture = true)]
    public decimal? GunlukMiktar { get; set; }
}
