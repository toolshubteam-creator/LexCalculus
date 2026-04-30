using System.Security.Claims;
using System.Text.Json;
using LexCalculus.Core.Entities;
using LexCalculus.Core.Entities.Identity;
using LexCalculus.Core.Services;
using LexCalculus.Infrastructure.Data;
using LexCalculus.Infrastructure.Tenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LexCalculus.Infrastructure.Services;

public sealed class ActivityLogService : IActivityLogService
{
    private const int MetadataMaxLength = 10_000;
    private const string MetadataTruncationSuffix = "...[truncated]";

    private readonly ApplicationDbContext _ctx;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IHttpContextAccessor _httpAccessor;
    private readonly ILogger<ActivityLogService> _logger;

    public ActivityLogService(
        ApplicationDbContext ctx,
        UserManager<ApplicationUser> userManager,
        IHttpContextAccessor httpAccessor,
        ILogger<ActivityLogService> logger)
    {
        _ctx = ctx;
        _userManager = userManager;
        _httpAccessor = httpAccessor;
        _logger = logger;
    }

    public async Task LogAsync(
        string action,
        string? entityType = null,
        int? entityId = null,
        string? description = null,
        object? metadata = null,
        int? tenantId = null,
        CancellationToken ct = default)
    {
        try
        {
            var (userId, userName, ip, ua) = await ResolveContextAsync();

            string? metadataJson = null;
            if (metadata is not null)
            {
                metadataJson = JsonSerializer.Serialize(metadata);
                if (metadataJson.Length > MetadataMaxLength)
                {
                    metadataJson = metadataJson[..MetadataMaxLength] + MetadataTruncationSuffix;
                }
            }

            var entry = new ActivityLog
            {
                CreatedAt = DateTime.UtcNow,
                UserId = userId,
                UserName = userName,
                Action = action,
                EntityType = entityType,
                EntityId = entityId,
                Description = description,
                MetadataJson = metadataJson,
                TenantId = tenantId,
                IpAddress = ip,
                UserAgent = ua
            };

            _ctx.ActivityLogs.Add(entry);
            await _ctx.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ActivityLog yazılamadı (action={Action}). Asıl işlem etkilenmiyor.", action);
        }
    }

    public async Task<ActivityLogPagedResult> GetPaginatedAsync(
        ActivityLogFilter filter,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 200) pageSize = 50;

        var q = _ctx.ActivityLogs.AsAdminQuery();

        if (filter.FromDate.HasValue)
            q = q.Where(a => a.CreatedAt >= filter.FromDate.Value);
        if (filter.ToDate.HasValue)
            q = q.Where(a => a.CreatedAt <= filter.ToDate.Value);
        if (filter.UserId.HasValue)
            q = q.Where(a => a.UserId == filter.UserId.Value);
        if (!string.IsNullOrWhiteSpace(filter.Action))
            q = q.Where(a => a.Action == filter.Action);
        if (filter.TenantId.HasValue)
            q = q.Where(a => a.TenantId == filter.TenantId.Value);

        var totalCount = await q.CountAsync(ct);

        var rows = await q
            .OrderByDescending(a => a.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new ActivityLogListItemDto
            {
                Id = a.Id,
                CreatedAt = a.CreatedAt,
                UserId = a.UserId,
                UserName = a.UserName,
                Action = a.Action,
                Description = a.Description,
                TenantId = a.TenantId,
                TenantName = a.Tenant != null ? a.Tenant.Name : null
            })
            .ToListAsync(ct);

        return new ActivityLogPagedResult
        {
            Items = rows,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<ActivityLogDetailDto?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var a = await _ctx.ActivityLogs
            .AsAdminQuery()
            .Include(x => x.User)
            .Include(x => x.Tenant)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        if (a == null) return null;

        return new ActivityLogDetailDto
        {
            Id = a.Id,
            CreatedAt = a.CreatedAt,
            UserId = a.UserId,
            UserName = a.UserName,
            Action = a.Action,
            EntityType = a.EntityType,
            EntityId = a.EntityId,
            Description = a.Description,
            MetadataJson = a.MetadataJson,
            TenantId = a.TenantId,
            TenantName = a.Tenant?.Name,
            IpAddress = a.IpAddress,
            UserAgent = a.UserAgent
        };
    }

    public async Task<IReadOnlyList<string>> GetDistinctActionsAsync(CancellationToken ct = default)
    {
        return await _ctx.ActivityLogs
            .AsAdminQuery()
            .Select(a => a.Action)
            .Distinct()
            .OrderBy(s => s)
            .ToListAsync(ct);
    }

    private async Task<(int? userId, string? userName, string? ip, string? ua)> ResolveContextAsync()
    {
        var http = _httpAccessor.HttpContext;
        if (http is null)
            return (null, null, null, null);

        int? userId = null;
        string? userName = null;
        var raw = http.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (int.TryParse(raw, out var parsed))
        {
            userId = parsed;
            try
            {
                var user = await _userManager.FindByIdAsync(parsed.ToString());
                userName = user?.Email ?? user?.UserName;
            }
            catch
            {
                userName = http.User.Identity?.Name;
            }
        }

        var ip = http.Connection.RemoteIpAddress?.ToString();
        var ua = http.Request.Headers.UserAgent.ToString();
        if (ua.Length > 500) ua = ua[..500];
        if (string.IsNullOrWhiteSpace(ua)) ua = null;

        return (userId, userName, ip, ua);
    }
}
