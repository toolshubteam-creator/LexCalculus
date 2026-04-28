using System.ComponentModel.DataAnnotations;

namespace LexCalculus.Web.Areas.Admin.Models.LifeTable;

public sealed class LifeTableCreateViewModel
{
    [Required(ErrorMessage = "Kod zorunlu.")]
    [RegularExpression(@"^[A-Z0-9-]+$",
        ErrorMessage = "Kod sadece büyük harf, rakam ve tire içerebilir (örn. TRH-2020).")]
    [StringLength(50)]
    public string Code { get; set; } = "";

    [Required(ErrorMessage = "İsim zorunlu.")]
    [StringLength(200)]
    public string Name { get; set; } = "";

    [Required(ErrorMessage = "Yürürlük tarihi zorunlu.")]
    [DataType(DataType.Date)]
    public DateTime EffectiveDate { get; set; } = DateTime.UtcNow.Date;

    [StringLength(500)]
    public string? Source { get; set; }

    [StringLength(2000)]
    public string? Note { get; set; }

    [Required(ErrorMessage = "CSV dosyası zorunlu.")]
    public IFormFile? CsvFile { get; set; }
}
