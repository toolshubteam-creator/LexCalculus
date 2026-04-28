namespace LexCalculus.Core.Email.Models;

public sealed class UserFreshnessDigestRow
{
    public required string ToolSlug { get; init; }
    public required string ToolTitle { get; init; }
    public required string ParameterKey { get; init; }
    public DateTime? LastUpdatedDate { get; init; }
    public int DaysOverdue { get; init; }
    public string? FrequencyLabelTr { get; init; }
    public required string CalculatorUrl { get; init; }
    public DateTime LastUserCalculation { get; init; }
}

public sealed class UserFreshnessDigestModel
{
    public required string UserName { get; init; }
    public required IReadOnlyList<UserFreshnessDigestRow> AffectedTools { get; init; }
    public required string SettingsUrl { get; init; }
    public DateTime GeneratedAt { get; init; } = DateTime.UtcNow;
}
