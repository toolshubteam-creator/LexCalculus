namespace LexCalculus.Web.Models.Hesaplarim;

public sealed class HesaplarimListPageViewModel
{
    public required IReadOnlyList<HesaplarimRowViewModel> Items { get; init; }
    public required HesaplarimFilterViewModel Filter { get; init; }
    public IReadOnlyList<(string Slug, string Title)> ToolOptions { get; init; }
        = Array.Empty<(string, string)>();
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }

    public int TotalPages => Math.Max(1, (int)Math.Ceiling((double)TotalCount / PageSize));
    public bool HasPrevious => Page > 1;
    public bool HasNext => Page < TotalPages;
}
