namespace LexCalculus.Web.Models.Hesaplarim;

public sealed class HesaplarimFilterViewModel
{
    public string? ToolSlug { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }

    /// <summary>
    /// Paylaşım kapsamı: "all" | "mine" | "shared-by-me" | "team" (default "all").
    /// Sadece tenant üyesi kullanıcılar için anlamlı; bireysel kullanıcılarda UI gizli.
    /// </summary>
    public string? Scope { get; set; }

    public bool HasAnyFilter =>
        !string.IsNullOrWhiteSpace(ToolSlug)
        || StartDate.HasValue
        || EndDate.HasValue
        || (!string.IsNullOrWhiteSpace(Scope) && Scope != "all");
}
