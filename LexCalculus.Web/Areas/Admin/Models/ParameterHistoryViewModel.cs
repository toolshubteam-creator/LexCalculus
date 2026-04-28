namespace LexCalculus.Web.Areas.Admin.Models;

public sealed class ParameterHistoryViewModel
{
    public required string ToolSlug { get; init; }
    public required string Key { get; init; }
    public IReadOnlyList<ParameterListRowViewModel> Versions { get; init; }
        = Array.Empty<ParameterListRowViewModel>();
}
