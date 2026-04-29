using LexCalculus.Core.Services;

namespace LexCalculus.Web.Areas.Admin.Models.Tenants;

public sealed class TenantListVm
{
    public required IReadOnlyList<TenantListItemDto> Items { get; init; }
    public string? Search { get; init; }
    public bool IncludeDeleted { get; init; }
}
