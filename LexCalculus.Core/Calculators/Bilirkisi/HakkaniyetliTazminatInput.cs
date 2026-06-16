using System.ComponentModel.DataAnnotations;

namespace LexCalculus.Core.Calculators.Bilirkisi;

public enum EkonomikDurum
{
    [Display(Name = "Zor (düşük ekonomik durum, çarpan ↑)")]
    Zor = 1,

    [Display(Name = "Normal")]
    Normal = 2,

    [Display(Name = "Refah (yüksek ekonomik durum, çarpan ↓)")]
    Refah = 3
}

public enum OlayAgirligi
{
    [Display(Name = "Hafif")]
    Hafif = 1,

    [Display(Name = "Normal")]
    Normal = 2,

    [Display(Name = "Ağır")]
    Agir = 3
}

public enum YasKategorisi
{
    [Display(Name = "Genç (≤ 30)")]
    Genc = 1,

    [Display(Name = "Orta Yaş (30-60)")]
    OrtaYas = 2,

    [Display(Name = "İleri Yaş (60+)")]
    Ileri = 3
}

/// <summary>
/// I3 Hakkaniyetli Tazminat Simülatörü — TBK m.51 + Yargıtay HGK hakkaniyet
/// ölçütlerini parametrik simülasyon olarak uygular. Tazminat = baz × kusur ×
/// ekonomik × olay × yaş çarpanları. Çarpanlar heuristik referans
/// (FormulaParameter, slug "hakkaniyetli-tazminat") — hukuk profesyoneli
/// incelemesi sonrası kalibrasyon (tech-debt #51).
/// </summary>
public sealed class HakkaniyetliTazminatInput
{
    [Display(Name = "Baz Tazminat (TL)")]
    [Required(ErrorMessage = "Baz tazminat boş olamaz.")]
    [Range(typeof(decimal), "0.01", "999999999999",
        ErrorMessage = "Baz tazminat pozitif olmalıdır.",
        ParseLimitsInInvariantCulture = true)]
    public decimal? BazTazminat { get; set; }

    /// <summary>
    /// Kusur oranı 0-1. 1.0 = davacı tam kusursuz (tam tazminat), 0.0 = tam
    /// kusurlu (tazminat yok). Müterafik kusur indirimi bu çarpan ile uygulanır.
    /// </summary>
    [Display(Name = "Kusur Oranı (0-1, davacının kusursuzluk derecesi)")]
    [Required(ErrorMessage = "Kusur oranı boş olamaz.")]
    [Range(typeof(decimal), "0", "1",
        ErrorMessage = "Kusur oranı 0-1 arası olmalıdır.",
        ParseLimitsInInvariantCulture = true)]
    public decimal? KusurOrani { get; set; }

    [Display(Name = "Ekonomik Durum")]
    public EkonomikDurum EkonomikDurum { get; set; } = EkonomikDurum.Normal;

    [Display(Name = "Olay Ağırlığı")]
    public OlayAgirligi OlayAgirligi { get; set; } = OlayAgirligi.Normal;

    [Display(Name = "Yaş Kategorisi")]
    public YasKategorisi YasKategorisi { get; set; } = YasKategorisi.OrtaYas;

    [Display(Name = "Hesap Tarihi (katsayı referansı)")]
    [DataType(DataType.Date)]
    public DateTime? AsOfDate { get; set; }
}
