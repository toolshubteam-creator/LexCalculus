using System.ComponentModel.DataAnnotations;

namespace LexCalculus.Core.Calculators.IsHukuku;

/// <summary>
/// Inputs for the notice pay (ihbar tazminatı) calculator. Unlike severance,
/// notice pay is computed on the BASE brüt monthly wage only (not "dressed"
/// wage), and is subject to BOTH stamp duty AND income tax.
/// </summary>
public sealed class IhbarTazminatiInput
{
    [Display(Name = "İşe Giriş Tarihi")]
    [Required(ErrorMessage = "Giriş tarihi boş olamaz.")]
    [DataType(DataType.Date)]
    public DateTime? GirisTarihi { get; set; }

    [Display(Name = "İşten Ayrılış Tarihi")]
    [Required(ErrorMessage = "Çıkış tarihi boş olamaz.")]
    [DataType(DataType.Date)]
    public DateTime? CikisTarihi { get; set; }

    [Display(Name = "Brüt Aylık Ücret (TL)")]
    [Required(ErrorMessage = "Brüt ücret boş olamaz.")]
    [Range(typeof(decimal), "0.01", "999999999",
        ErrorMessage = "Brüt ücret pozitif bir sayı olmalıdır.",
        ParseLimitsInInvariantCulture = true)]
    public decimal? BrutAylikUcret { get; set; }

    [Display(Name = "Tazminatı Hak Eden Taraf")]
    [Required]
    public IhbarHakEden HakEden { get; set; } = IhbarHakEden.Isci;
}

/// <summary>
/// Notice pay can be owed by either party — employer if firing without notice,
/// or employee if quitting without notice. Algorithm and amount are the same.
/// Field exists for the audit trail and tax-treatment decisions in subclasses.
/// </summary>
public enum IhbarHakEden
{
    [Display(Name = "İşçi (işveren ihbar süresine uymadı)")]
    Isci = 1,

    [Display(Name = "İşveren (işçi ihbar süresine uymadı)")]
    Isveren = 2
}
