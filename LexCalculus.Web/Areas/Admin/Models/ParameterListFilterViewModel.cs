namespace LexCalculus.Web.Areas.Admin.Models;

public sealed class ParameterListFilterViewModel
{
    public string? ToolSlug { get; set; }
    public string? Key { get; set; }
    public bool OnlyStale { get; set; }
    public bool OnlyCurrent { get; set; }

    public bool HasAnyFilter =>
        !string.IsNullOrWhiteSpace(ToolSlug)
        || !string.IsNullOrWhiteSpace(Key)
        || OnlyStale || OnlyCurrent;
}
