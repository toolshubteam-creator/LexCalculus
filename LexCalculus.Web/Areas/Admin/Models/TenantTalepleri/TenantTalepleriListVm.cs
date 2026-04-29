using LexCalculus.Core.Entities.Identity;
using LexCalculus.Core.Services;

namespace LexCalculus.Web.Areas.Admin.Models.TenantTalepleri;

public sealed class TenantTalepleriListVm
{
    public required IReadOnlyList<TenantRequestListItemDto> Items { get; init; }
    public TenantRequestStatus? StatusFilter { get; init; }
}
