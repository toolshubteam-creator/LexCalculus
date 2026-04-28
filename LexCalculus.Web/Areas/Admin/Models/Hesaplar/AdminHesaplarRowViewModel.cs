namespace LexCalculus.Web.Areas.Admin.Models.Hesaplar;

public sealed class AdminHesaplarRowViewModel
{
    public int Id { get; init; }
    public required string ToolSlug { get; init; }
    public required string CategorySlug { get; init; }
    public required string ToolTitle { get; init; }
    public DateTime CreatedAt { get; init; }
    public decimal? TotalAmount { get; init; }
    public string? Unit { get; init; }
    public int UserId { get; init; }
    public required string UserDisplay { get; init; }
}
