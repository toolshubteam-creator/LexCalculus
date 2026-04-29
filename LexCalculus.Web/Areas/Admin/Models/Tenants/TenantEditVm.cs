using System.ComponentModel.DataAnnotations;
using LexCalculus.Core.Services;

namespace LexCalculus.Web.Areas.Admin.Models.Tenants;

public sealed class TenantEditVm
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Tenant adı zorunlu.")]
    [StringLength(200)]
    [Display(Name = "Ad")]
    public string Name { get; set; } = "";

    [Required(ErrorMessage = "Slug zorunlu.")]
    [StringLength(100)]
    [RegularExpression(@"^[a-z0-9-]+$",
        ErrorMessage = "Slug sadece küçük harf, rakam ve tire içerebilir.")]
    [Display(Name = "Slug")]
    public string Slug { get; set; } = "";

    [Required(ErrorMessage = "Owner kullanıcı seçimi zorunlu.")]
    [Display(Name = "Owner")]
    public int OwnerUserId { get; set; }

    /// <summary>
    /// Mevcut owner + TenantId == null kullanıcılar.
    /// </summary>
    public IReadOnlyList<UserOptionDto> OwnerCandidates { get; set; } = Array.Empty<UserOptionDto>();
}
