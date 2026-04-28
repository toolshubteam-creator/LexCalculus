namespace LexCalculus.Web.Areas.Admin.Models.Hesaplar;

public sealed class AdminHesaplarFilterViewModel
{
    public string? ToolSlug { get; set; }
    public int? UserId { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }

    public bool HasAnyFilter =>
        !string.IsNullOrWhiteSpace(ToolSlug)
        || UserId.HasValue
        || StartDate.HasValue
        || EndDate.HasValue;
}
