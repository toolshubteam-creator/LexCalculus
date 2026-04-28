namespace LexCalculus.Web.Areas.Admin.Models.Hesaplar;

public sealed class AdminHesaplarListPageViewModel
{
    public required IReadOnlyList<AdminHesaplarRowViewModel> Items { get; init; }
    public required AdminHesaplarFilterViewModel Filter { get; init; }
    public IReadOnlyList<(string Slug, string Title)> ToolOptions { get; init; }
        = Array.Empty<(string, string)>();
    public IReadOnlyList<(int Id, string Display)> UserOptions { get; init; }
        = Array.Empty<(int, string)>();
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }

    public int TotalPages => Math.Max(1, (int)Math.Ceiling((double)TotalCount / PageSize));
    public bool HasPrevious => Page > 1;
    public bool HasNext => Page < TotalPages;
}
