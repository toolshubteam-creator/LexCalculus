using Hangfire;
using Hangfire.Storage;
using LexCalculus.Core.Admin.Dashboard;
using LexCalculus.Core.Calculators.Common;
using LexCalculus.Core.Entities;
using LexCalculus.Core.Entities.Calculators;
using LexCalculus.Core.Entities.Identity;
using LexCalculus.Core.Interfaces;
using LexCalculus.Core.Notifications;
using LexCalculus.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LexCalculus.Infrastructure.Admin.Dashboard;

public sealed class DashboardSummaryService : IDashboardSummaryService
{
    private readonly ApplicationDbContext _ctx;
    private readonly IFormulaFreshnessChecker _freshness;
    private readonly INotificationService _notifications;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ICalculatorRegistry _registry;
    private readonly ILogger<DashboardSummaryService> _logger;

    public DashboardSummaryService(
        ApplicationDbContext ctx,
        IFormulaFreshnessChecker freshness,
        INotificationService notifications,
        UserManager<ApplicationUser> userManager,
        ICalculatorRegistry registry,
        ILogger<DashboardSummaryService> logger)
    {
        _ctx = ctx;
        _freshness = freshness;
        _notifications = notifications;
        _userManager = userManager;
        _registry = registry;
        _logger = logger;
    }

    public async Task<DashboardSummary> GetSummaryAsync(int currentAdminUserId, CancellationToken ct = default)
    {
        return new DashboardSummary
        {
            Freshness = await SafeRunAsync(() => GetFreshnessAsync(ct), nameof(DataFreshnessSummary)),
            Activity = await SafeRunAsync(() => GetActivityAsync(ct), nameof(CalculationActivitySummary)),
            Users = await SafeRunAsync(() => GetUsersAsync(ct), nameof(UserSummary)),
            Jobs = SafeRun(() => GetJobs(), nameof(HangfireSummary)),
            Notifications = await SafeRunAsync(() => GetNotificationsAsync(currentAdminUserId, ct), nameof(NotificationSummary)),
            GeneratedAt = DateTime.UtcNow
        };
    }

    private async Task<T?> SafeRunAsync<T>(Func<Task<T>> fn, string widgetName) where T : class
    {
        try { return await fn(); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Dashboard widget '{Widget}' yüklenemedi.", widgetName);
            return null;
        }
    }

    private T? SafeRun<T>(Func<T> fn, string widgetName) where T : class
    {
        try { return fn(); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Dashboard widget '{Widget}' yüklenemedi.", widgetName);
            return null;
        }
    }

    private async Task<DataFreshnessSummary> GetFreshnessAsync(CancellationToken ct)
    {
        var allParameters = await _ctx.Set<FormulaParameter>().ToListAsync(ct);
        var now = DateTime.UtcNow;

        var staleCount = allParameters
            .GroupBy(p => (p.ToolSlug, p.Key))
            .Select(g => g.OrderByDescending(p => p.EffectiveDate).First())
            .Count(latest => _freshness.IsStale(latest, now));

        return new DataFreshnessSummary(
            TotalParameters: allParameters.Count,
            StaleCount: staleCount,
            DashboardLink: "/admin/parametreler?OnlyStale=true");
    }

    private async Task<CalculationActivitySummary> GetActivityAsync(CancellationToken ct)
    {
        var since = DateTime.UtcNow.AddDays(-7);
        var query = _ctx.Set<CalculationHistory>().Where(h => h.CreatedAt > since);

        var totalCount = await query.CountAsync(ct);

        var topGroups = await query
            .GroupBy(h => h.ToolSlug)
            .Select(g => new { Slug = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Take(3)
            .ToListAsync(ct);

        var titleMap = _registry.GetAll()
            .ToDictionary(m => m.Slug, m => m.Title, StringComparer.OrdinalIgnoreCase);

        var topTools = topGroups
            .Select(g => new TopToolUsage(
                ToolSlug: g.Slug,
                ToolTitle: titleMap.TryGetValue(g.Slug, out var t) ? t : g.Slug,
                UsageCount: g.Count))
            .ToList();

        return new CalculationActivitySummary(
            TotalLast7Days: totalCount,
            TopTools: topTools,
            DashboardLink: "/admin/hesaplar");
    }

    private async Task<UserSummary> GetUsersAsync(CancellationToken ct)
    {
        var totalActive = await _userManager.Users.CountAsync(u => u.IsActive, ct);
        var since = DateTime.UtcNow.AddDays(-30);
        var loggedInRecently = await _userManager.Users
            .CountAsync(u => u.IsActive && u.LastLoginAt > since, ct);

        return new UserSummary(
            TotalActiveUsers: totalActive,
            ActiveLast30Days: loggedInRecently,
            DashboardLink: "/admin/kullanicilar");
    }

    private HangfireSummary GetJobs()
    {
        using var connection = JobStorage.Current.GetConnection();
        var recurringJobs = connection.GetRecurringJobs();
        var monitoringApi = JobStorage.Current.GetMonitoringApi();
        var servers = monitoringApi.Servers();

        return new HangfireSummary(
            RecurringJobCount: recurringJobs.Count,
            ServerCount: servers.Count,
            DashboardLink: "/admin/hangfire");
    }

    private async Task<NotificationSummary> GetNotificationsAsync(int currentAdminUserId, CancellationToken ct)
    {
        var unread = await _notifications.GetUnreadCountAsync(currentAdminUserId, ct);
        return new NotificationSummary(
            UnreadForCurrentAdmin: unread,
            DashboardLink: "/bildirimler");
    }
}
