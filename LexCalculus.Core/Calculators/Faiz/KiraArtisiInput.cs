using System.ComponentModel.DataAnnotations;
using LexCalculus.Core.Enums;

namespace LexCalculus.Core.Calculators.Faiz;

public sealed class KiraArtisiInput
{
    [Display(Name = "Mevcut Kira Bedeli (TL)")]
    [Required(ErrorMessage = "Mevcut kira bedeli boş olamaz.")]
    [Range(typeof(decimal), "0.01", "999999999",
        ErrorMessage = "Mevcut kira pozitif olmalıdır.",
        ParseLimitsInInvariantCulture = true)]
    public decimal? MevcutKira { get; set; }

    [Display(Name = "Yenileme Tarihi")]
    [Required(ErrorMessage = "Yenileme tarihi boş olamaz.")]
    [DataType(DataType.Date)]
    public DateTime? YenilenmeTarihi { get; set; }

    [Display(Name = "Mülk Tipi")]
    public MulkTipi MulkTipi { get; set; } = MulkTipi.Konut;

    /// <summary>
    /// Sözleşmede taraflarca belirlenen artış oranı. Boşsa TÜFE üst sınırı uygulanır.
    /// Doluysa min(sözleşme, TÜFE) kuralı çalışır.
    /// </summary>
    [Display(Name = "Sözleşme Artış Oranı (% — opsiyonel)")]
    [Range(typeof(decimal), "0", "1000",
        ErrorMessage = "Sözleşme oranı 0 - 1000 arası olmalıdır.",
        ParseLimitsInInvariantCulture = true)]
    public decimal? SozlesmeOrani { get; set; }

    /// <summary>
    /// Sistemde olmayan eski/yeni ay için manuel TÜFE girişi.
    /// Doluysa veritabanı sorgusu atlanır.
    /// </summary>
    [Display(Name = "TÜFE Override (% — opsiyonel, sistemde olmayan aylar için)")]
    [Range(typeof(decimal), "0", "1000",
        ErrorMessage = "TÜFE oranı 0 - 1000 arası olmalıdır.",
        ParseLimitsInInvariantCulture = true)]
    public decimal? TUFEOverride { get; set; }
}
