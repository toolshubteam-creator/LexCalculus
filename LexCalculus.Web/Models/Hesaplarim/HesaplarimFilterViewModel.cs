namespace LexCalculus.Web.Models.Hesaplarim;

public sealed class HesaplarimFilterViewModel
{
    public string? ToolSlug { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }

    public bool HasAnyFilter =>
        !string.IsNullOrWhiteSpace(ToolSlug)
        || StartDate.HasValue
        || EndDate.HasValue;
}
