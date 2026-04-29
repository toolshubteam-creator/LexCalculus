using System.ComponentModel.DataAnnotations;
using LexCalculus.Core.Services;

namespace LexCalculus.Web.Areas.Admin.Models.Tenants;

public sealed class TenantCreateVm
{
    [Required(ErrorMessage = "Tenant adı zorunlu.")]
    [StringLength(200)]
    [Display(Name = "Ad")]
    public string Name { get; set; } = "";

    [StringLength(100)]
    [RegularExpression(@"^[a-z0-9-]*$",
        ErrorMessage = "Slug sadece küçük harf, rakam ve tire içerebilir.")]
    [Display(Name = "Slug")]
    public string? Slug { get; set; }

    [Required(ErrorMessage = "Owner kullanıcı seçimi zorunlu.")]
    [Display(Name = "Owner")]
    public int? OwnerUserId { get; set; }

    public IReadOnlyList<UserOptionDto> AvailableOwners { get; set; } = Array.Empty<UserOptionDto>();
}
