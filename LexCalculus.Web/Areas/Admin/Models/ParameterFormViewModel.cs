using System.ComponentModel.DataAnnotations;

namespace LexCalculus.Web.Areas.Admin.Models;

public enum ParameterFormMode
{
    /// <summary>Tamamen yeni (slug, key) çifti ekleme.</summary>
    New,
    /// <summary>Mevcut (slug, key) için yeni effective-dated versiyon.</summary>
    NewVersion,
    /// <summary>Yapısal düzeltme: mevcut row'u IN PLACE güncelleme. UYARI ile.</summary>
    Edit
}

public sealed class ParameterFormViewModel : IValidatableObject
{
    public ParameterFormMode Mode { get; set; } = ParameterFormMode.New;

    /// <summary>Edit modunda hangi row mutate edilecek. New/NewVersion'da null.</summary>
    public int? Id { get; set; }

    [Required(ErrorMessage = "Araç seçimi zorunludur.")]
    public string ToolSlug { get; set; } = "";

    [Required(ErrorMessage = "Anahtar zorunludur.")]
    [RegularExpression(@"^[a-z0-9]+(-[a-z0-9]+)*$",
        ErrorMessage = "Anahtar kebab-case olmalı (örn. kidem-tavan, asgari-ucret-net).")]
    [StringLength(80)]
    public string Key { get; set; } = "";

    [Required(ErrorMessage = "Değer zorunludur.")]
    public decimal Value { get; set; }

    [Required(ErrorMessage = "Yürürlük tarihi zorunludur.")]
    [DataType(DataType.Date)]
    public DateTime EffectiveDate { get; set; } = DateTime.UtcNow.Date;

    [Required(ErrorMessage = "Güncelleme sıklığı seçimi zorunludur.")]
    public string ExpectedUpdateFrequency { get; set; } = "Yearly";

    [DataType(DataType.Date)]
    public DateTime? LastUpdatedDate { get; set; } = DateTime.UtcNow.Date;

    [StringLength(200)]
    public string? Source { get; set; }

    [StringLength(500)]
    public string? Note { get; set; }

    [StringLength(2000)]
    public string? Notes { get; set; }

    public IReadOnlyList<(string Value, string Label)> ToolSlugOptions { get; set; }
        = Array.Empty<(string, string)>();
    public IReadOnlyList<(string Value, string Label)> FrequencyOptions { get; set; }
        = Array.Empty<(string, string)>();

    public IEnumerable<ValidationResult> Validate(ValidationContext ctx)
    {
        if (EffectiveDate < new DateTime(2000, 1, 1) || EffectiveDate > new DateTime(2050, 12, 31))
            yield return new ValidationResult("Yürürlük tarihi 2000-2050 aralığında olmalı.",
                new[] { nameof(EffectiveDate) });

        if (LastUpdatedDate.HasValue && LastUpdatedDate.Value > DateTime.UtcNow.Date)
            yield return new ValidationResult("Son güncelleme tarihi gelecekte olamaz.",
                new[] { nameof(LastUpdatedDate) });

        if (Value < 0)
            yield return new ValidationResult("Değer negatif olamaz (eğer kasıtlıysa Notes alanına açıkla).",
                new[] { nameof(Value) });
    }
}
