namespace LexCalculus.Core.Email.Models;

public sealed class AdminFreshnessDigestRow
{
    public required string ToolSlug { get; init; }
    public required string Key { get; init; }
    public DateTime? LastUpdatedDate { get; init; }
    public int DaysOverdue { get; init; }
    public string? Source { get; init; }
    public string? FrequencyLabelTr { get; init; }
}

public sealed class AdminFreshnessDigestModel
{
    public required string AdminName { get; init; }
    public required IReadOnlyList<AdminFreshnessDigestRow> StaleRows { get; init; }
    public required string DashboardUrl { get; init; }
    public DateTime GeneratedAt { get; init; } = DateTime.UtcNow;
    public int TotalParameterCount { get; init; }
}
