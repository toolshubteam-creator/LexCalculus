using LexCalculus.Core.Services;

namespace LexCalculus.Web.Areas.Admin.Models.Tenants;

public sealed class TenantDetailVm
{
    public required TenantDetailDto Detail { get; init; }

    /// <summary>
    /// Üye olarak eklenebilecek kullanıcılar (TenantId == null).
    /// </summary>
    public required IReadOnlyList<UserOptionDto> AvailableUsers { get; init; }

    /// <summary>
    /// Bu tenant için tüm geçmiş davetler (Faz 3.7 P3/5).
    /// </summary>
    public IReadOnlyList<InvitationListItemDto> Invitations { get; init; }
        = Array.Empty<InvitationListItemDto>();
}
