using LexCalculus.Core.Services;

namespace LexCalculus.Web.Areas.Admin.Models.ActivityLog;

public sealed class ActivityLogListVm
{
    public required ActivityLogPagedResult Result { get; init; }
    public required ActivityLogFilterVm Filter { get; init; }
    public required IReadOnlyList<string> AvailableActions { get; init; }
    public required IReadOnlyList<TenantOptionVm> AvailableTenants { get; init; }
}

public sealed class ActivityLogFilterVm
{
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public int? UserId { get; set; }
    public string? UserSearch { get; set; }
    public string? Action { get; set; }
    public int? TenantId { get; set; }
}

public sealed record TenantOptionVm(int Id, string Name);
