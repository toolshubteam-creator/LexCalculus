using System.ComponentModel.DataAnnotations;

namespace LexCalculus.Core.Calculators.VergiIdare;

/// <summary>Vergi cezası türü (213 s.K. dispatch için).</summary>
public enum VergiCezaTuru
{
    [Display(Name = "Vergi Ziyaı (m.341) — %50")]
    VergiZiyai = 1,

    [Display(Name = "Kaçakçılık (m.359) — %100")]
    Kacakcilik = 2,

    [Display(Name = "Usulsüzlük (m.352) — maktu (girilen tutar)")]
    Usulsuzluk = 3
}

/// <summary>Faiz/zam türü (213 s.K. m.112 vs 6183 s.K. m.51).</summary>
public enum FaizTuru
{
    [Display(Name = "Gecikme Faizi (213 s.K. m.112)")]
    GecikmeFaizi = 1,

    [Display(Name = "Gecikme Zammı (6183 s.K. m.51)")]
    GecikmeZammi = 2
}

/// <summary>
/// G5 Vergi Cezası ve Gecikme Faizi — 213 s.K. m.341-376 + m.112 (faiz) /
/// 6183 s.K. m.51 (zam). Asıl vergi üzerinden ceza ve vade-ödeme arası
/// dönem için basit aylık faiz hesabı yapılır.
/// </summary>
public sealed class VergiCezasiInput
{
    [Display(Name = "Asıl Vergi (TL)")]
    [Required(ErrorMessage = "Asıl vergi tutarı boş olamaz.")]
    [Range(typeof(decimal), "0.01", "999999999999",
        ErrorMessage = "Asıl vergi pozitif olmalıdır.",
        ParseLimitsInInvariantCulture = true)]
    public decimal? AsilVergi { get; set; }

    [Display(Name = "Vade Tarihi")]
    [Required(ErrorMessage = "Vade tarihi boş olamaz.")]
    [DataType(DataType.Date)]
    public DateTime? VadeTarihi { get; set; }

    [Display(Name = "Ödeme Tarihi")]
    [Required(ErrorMessage = "Ödeme tarihi boş olamaz.")]
    [DataType(DataType.Date)]
    public DateTime? OdemeTarihi { get; set; }

    [Display(Name = "Ceza Türü")]
    public VergiCezaTuru CezaTuru { get; set; } = VergiCezaTuru.VergiZiyai;

    /// <summary>Usulsüzlük cezası için mahkeme/idare takdir tutarı.</summary>
    [Display(Name = "Usulsüzlük Cezası Tutarı (TL) — yalnız usulsüzlük türünde")]
    [Range(typeof(decimal), "0", "9999999",
        ErrorMessage = "Usulsüzlük cezası negatif olamaz.",
        ParseLimitsInInvariantCulture = true)]
    public decimal? UsulsuzlukTutari { get; set; }

    [Display(Name = "Faiz / Zam Türü")]
    public FaizTuru FaizTuru { get; set; } = FaizTuru.GecikmeFaizi;
}
