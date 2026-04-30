namespace LexCalculus.Core.Services;

public sealed class ActivityLogFilter
{
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public int? UserId { get; set; }
    public string? Action { get; set; }
    public int? TenantId { get; set; }
}

public sealed class ActivityLogListItemDto
{
    public int Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public int? UserId { get; set; }
    public string? UserName { get; set; }
    public string Action { get; set; } = default!;
    public string? Description { get; set; }
    public int? TenantId { get; set; }
    public string? TenantName { get; set; }
}

public sealed class ActivityLogPagedResult
{
    public List<ActivityLogListItemDto> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => Math.Max(1, (int)Math.Ceiling((double)TotalCount / PageSize));
    public bool HasPrevious => Page > 1;
    public bool HasNext => Page < TotalPages;
}

public sealed class ActivityLogDetailDto
{
    public int Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public int? UserId { get; set; }
    public string? UserName { get; set; }
    public string Action { get; set; } = default!;
    public string? EntityType { get; set; }
    public int? EntityId { get; set; }
    public string? Description { get; set; }
    public string? MetadataJson { get; set; }
    public int? TenantId { get; set; }
    public string? TenantName { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
}

/// <summary>
/// Sistem genelinde denetim olaylarını yazar ve admin sorgularına döner.
/// Faz 3.8 P1/2'de eklendi.
///
/// LogAsync defansif: try/catch içinde — log fail ederse asıl işlemi bozmaz.
/// MetadataJson 10KB üzerine kırpılır. UserName denormalize.
/// </summary>
public interface IActivityLogService
{
    Task LogAsync(
        string action,
        string? entityType = null,
        int? entityId = null,
        string? description = null,
        object? metadata = null,
        int? tenantId = null,
        CancellationToken ct = default);

    Task<ActivityLogPagedResult> GetPaginatedAsync(
        ActivityLogFilter filter,
        int page,
        int pageSize,
        CancellationToken ct = default);

    Task<ActivityLogDetailDto?> GetByIdAsync(int id, CancellationToken ct = default);

    /// <summary>Filter dropdown'ı için DB'deki distinct Action listesi.</summary>
    Task<IReadOnlyList<string>> GetDistinctActionsAsync(CancellationToken ct = default);
}
