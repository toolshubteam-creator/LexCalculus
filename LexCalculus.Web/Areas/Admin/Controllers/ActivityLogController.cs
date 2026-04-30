using LexCalculus.Core.Services;
using LexCalculus.Infrastructure.Data;
using LexCalculus.Infrastructure.Tenancy;
using LexCalculus.Web.Areas.Admin.Models.ActivityLog;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LexCalculus.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = "AdminOnly")]
[Route("admin/activity-log")]
public sealed class ActivityLogController : Controller
{
    private const int DefaultPageSize = 50;

    private readonly IActivityLogService _activityLog;
    private readonly ApplicationDbContext _ctx;

    public ActivityLogController(IActivityLogService activityLog, ApplicationDbContext ctx)
    {
        _activityLog = activityLog;
        _ctx = ctx;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(
        [FromQuery] ActivityLogFilterVm filter,
        [FromQuery] int page = 1,
        CancellationToken ct = default)
    {
        filter ??= new ActivityLogFilterVm();

        // UserSearch → UserId çözümü (email/username substring)
        if (!filter.UserId.HasValue && !string.IsNullOrWhiteSpace(filter.UserSearch))
        {
            var term = filter.UserSearch.Trim().ToLowerInvariant();
            var match = await _ctx.Users
                .AsAdminQuery()
                .Where(u => (u.Email ?? "").ToLower().Contains(term)
                         || (u.UserName ?? "").ToLower().Contains(term))
                .Select(u => (int?)u.Id)
                .FirstOrDefaultAsync(ct);
            filter.UserId = match;
        }

        var serviceFilter = new ActivityLogFilter
        {
            FromDate = filter.FromDate,
            ToDate = filter.ToDate,
            UserId = filter.UserId,
            Action = string.IsNullOrWhiteSpace(filter.Action) ? null : filter.Action,
            TenantId = filter.TenantId
        };

        var result = await _activityLog.GetPaginatedAsync(serviceFilter, page, DefaultPageSize, ct);
        var actions = await _activityLog.GetDistinctActionsAsync(ct);
        var tenants = await _ctx.Tenants
            .AsAdminQuery()
            .Where(t => !t.IsDeleted)
            .OrderBy(t => t.Name)
            .Select(t => new TenantOptionVm(t.Id, t.Name))
            .ToListAsync(ct);

        var vm = new ActivityLogListVm
        {
            Result = result,
            Filter = filter,
            AvailableActions = actions,
            AvailableTenants = tenants
        };

        ViewData["Title"] = "Activity Log";
        ViewData["Breadcrumb"] = new List<(string Label, string? Url)>
        {
            ("Dashboard", Url.Action("Index", "Home", new { area = "Admin" })),
            ("Activity Log", null)
        };
        return View(vm);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Detay(int id, CancellationToken ct = default)
    {
        var detail = await _activityLog.GetByIdAsync(id, ct);
        if (detail == null) return NotFound();

        ViewData["Title"] = $"Activity Log #{id}";
        ViewData["Breadcrumb"] = new List<(string Label, string? Url)>
        {
            ("Dashboard", Url.Action("Index", "Home", new { area = "Admin" })),
            ("Activity Log", Url.Action(nameof(Index))),
            ($"#{id}", null)
        };
        return View(detail);
    }
}
