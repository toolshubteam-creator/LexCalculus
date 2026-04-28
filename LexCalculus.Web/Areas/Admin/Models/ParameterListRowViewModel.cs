namespace LexCalculus.Web.Areas.Admin.Models;

public sealed class ParameterListRowViewModel
{
    public int Id { get; init; }
    public required string ToolSlug { get; init; }
    public required string Key { get; init; }
    public decimal Value { get; init; }
    public DateTime EffectiveDate { get; init; }
    public string? Frequency { get; init; }
    public string FrequencyLabelTr { get; init; } = "—";
    public DateTime? LastUpdatedDate { get; init; }
    public string? Source { get; init; }
    public string? Notes { get; init; }

    public bool IsStale { get; init; }
    public int? DaysUntilStale { get; init; }
    public bool IsGlobal => ToolSlug == "*";
    public bool IsCurrentVersion { get; init; }
}
