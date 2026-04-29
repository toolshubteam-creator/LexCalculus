using System.ComponentModel.DataAnnotations;

namespace LexCalculus.Web.Models.TenantTalep;

public sealed class TenantTalepCreateVm
{
    [Required(ErrorMessage = "Hukuk bürosu adı zorunlu.")]
    [StringLength(200)]
    [Display(Name = "Hukuk bürosu adı")]
    public string ProposedName { get; set; } = "";

    [StringLength(100)]
    [RegularExpression(@"^[a-z0-9-]*$",
        ErrorMessage = "Slug sadece küçük harf, rakam ve tire içerebilir.")]
    [Display(Name = "Slug (opsiyonel)")]
    public string? ProposedSlug { get; set; }

    [Required(ErrorMessage = "Baro/Sicil No zorunlu.")]
    [StringLength(50)]
    [Display(Name = "Baro/Sicil No")]
    public string BarSicilNo { get; set; } = "";

    [StringLength(1000)]
    [Display(Name = "Açıklama (opsiyonel)")]
    public string? Description { get; set; }
}
