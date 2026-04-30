using LexCalculus.Core.Services;

namespace LexCalculus.Tests.TestHelpers;

/// <summary>
/// Test-only no-op IActivityLogService — testin asıl ilgilendiği davranış
/// audit log değilse bu kullanılır. Audit davranışını assert eden testler
/// in-memory ApplicationDbContext üzerinden gerçek ActivityLogService kullanmalı.
/// </summary>
internal sealed class NullActivityLogService : IActivityLogService
{
    public Task LogAsync(
        string action,
        string? entityType = null,
        int? entityId = null,
        string? description = null,
        object? metadata = null,
        int? tenantId = null,
        CancellationToken ct = default) => Task.CompletedTask;

    public Task<ActivityLogPagedResult> GetPaginatedAsync(
        ActivityLogFilter filter,
        int page,
        int pageSize,
        CancellationToken ct = default)
        => Task.FromResult(new ActivityLogPagedResult { Page = page, PageSize = pageSize });

    public Task<ActivityLogDetailDto?> GetByIdAsync(int id, CancellationToken ct = default)
        => Task.FromResult<ActivityLogDetailDto?>(null);

    public Task<IReadOnlyList<string>> GetDistinctActionsAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
}
