using System.ComponentModel.DataAnnotations;
using LexCalculus.Core.Enums;

namespace LexCalculus.Core.Calculators.Bilirkisi;

/// <summary>Yaşam tablosu sorgu modu (tek bir yaş veya yaş aralığı).</summary>
public enum YasamSorguTipi
{
    [Display(Name = "Tek Kişi (yaş + cinsiyet)")]
    TekKisi = 1,

    [Display(Name = "Yaş Aralığı (başlangıç-bitiş)")]
    YasAraligi = 2
}

/// <summary>
/// I1 PMF Yaşam Tablosu Sorgulama — TRH 2010 (Türkiye Hayat Tablosu). Bilirkişi
/// raporlarında destekten yoksun kalma, maluliyet, anüite hesapları için temel
/// kalan yaşam umudu (eX) sorgu aracı. ILifeTableService reuse.
/// </summary>
public sealed class YasamTablosuSorguInput
{
    [Display(Name = "Sorgu Tipi")]
    public YasamSorguTipi SorguTipi { get; set; } = YasamSorguTipi.TekKisi;

    [Display(Name = "Cinsiyet")]
    public Cinsiyet Cinsiyet { get; set; } = Cinsiyet.Erkek;

    // ----- Tek Kişi -----
    [Display(Name = "Yaş")]
    [Range(0, 105, ErrorMessage = "Yaş 0-105 arası olmalıdır.")]
    public int? Yas { get; set; }

    // ----- Yaş Aralığı -----
    [Display(Name = "Başlangıç Yaşı")]
    [Range(0, 105, ErrorMessage = "Başlangıç yaşı 0-105 arası olmalıdır.")]
    public int? BaslangicYas { get; set; }

    [Display(Name = "Bitiş Yaşı")]
    [Range(0, 105, ErrorMessage = "Bitiş yaşı 0-105 arası olmalıdır.")]
    public int? BitisYas { get; set; }
}
