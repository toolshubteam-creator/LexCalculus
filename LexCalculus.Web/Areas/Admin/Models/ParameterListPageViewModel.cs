namespace LexCalculus.Web.Areas.Admin.Models;

public sealed class ParameterListPageViewModel
{
    public ParameterListFilterViewModel Filter { get; init; } = new();
    public IReadOnlyList<ParameterListRowViewModel> Rows { get; init; } = Array.Empty<ParameterListRowViewModel>();
    public int TotalCount { get; init; }
    public int FilteredCount { get; init; }
    public int StaleCount { get; init; }
    public IReadOnlyList<(string Value, string Label)> ToolSlugOptions { get; init; }
        = Array.Empty<(string, string)>();
}
