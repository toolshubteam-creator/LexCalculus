namespace LexCalculus.Web.Models.Hesaplarim;

public sealed class HesaplarimRowViewModel
{
    public int Id { get; init; }
    public required string ToolSlug { get; init; }
    public required string CategorySlug { get; init; }
    public required string ToolTitle { get; init; }
    public DateTime CreatedAt { get; init; }
    public decimal? TotalAmount { get; init; }
    public string? Unit { get; init; }
}
