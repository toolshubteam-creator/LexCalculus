using System.ComponentModel.DataAnnotations;
using LexCalculus.Core.Services;

namespace LexCalculus.Web.Models.TenantYonetim;

public sealed class TenantYonetimVm
{
    public required TenantDetailDto Tenant { get; init; }
    public required IReadOnlyList<InvitationListItemDto> Invitations { get; init; }
}

public sealed class TenantYonetimDavetVm
{
    [Required(ErrorMessage = "E-posta zorunlu.")]
    [EmailAddress(ErrorMessage = "Geçerli bir e-posta giriniz.")]
    [StringLength(256)]
    [Display(Name = "Davet edilecek e-posta")]
    public string Email { get; set; } = "";
}
