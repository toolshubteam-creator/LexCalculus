namespace LexCalculus.Core.Admin.Dashboard;

/// <summary>
/// Admin dashboard 5 widget özeti. Alt-record'lar nullable: bir widget
/// query'si fail olursa null döner, view "Yüklenemedi" gösterir; diğer
/// widget'lar etkilenmez.
/// </summary>
public sealed class DashboardSummary
{
    public DataFreshnessSummary? Freshness { get; init; }
    public CalculationActivitySummary? Activity { get; init; }
    public UserSummary? Users { get; init; }
    public HangfireSummary? Jobs { get; init; }
    public NotificationSummary? Notifications { get; init; }
    public DateTime GeneratedAt { get; init; } = DateTime.UtcNow;
}

public sealed record DataFreshnessSummary(
    int TotalParameters,
    int StaleCount,
    string DashboardLink);

public sealed record CalculationActivitySummary(
    int TotalLast7Days,
    IReadOnlyList<TopToolUsage> TopTools,
    string DashboardLink);

public sealed record TopToolUsage(string ToolSlug, string ToolTitle, int UsageCount);

public sealed record UserSummary(
    int TotalActiveUsers,
    int ActiveLast30Days,
    string DashboardLink);

public sealed record HangfireSummary(
    int RecurringJobCount,
    int ServerCount,
    string DashboardLink);

public sealed record NotificationSummary(
    int UnreadForCurrentAdmin,
    string DashboardLink);
