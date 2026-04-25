namespace LexCalculus.Core.Models.Seo;

/// <summary>
/// Plain data carrier for per-page SEO metadata. Controllers populate the
/// nullable fields they care about; ISeoMetaProvider.MergeWithDefault fills
/// the rest from configuration. Rendered by SeoMetaViewComponent.
/// </summary>
public sealed class SeoMeta
{
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? Keywords { get; set; }
    public string? CanonicalUrl { get; set; }
    public string? OgImage { get; set; }
    public string OgType { get; set; } = "website";
    public string TwitterCard { get; set; } = "summary_large_image";

    /// <summary>
    /// Pre-serialized JSON-LD structured data. Caller is responsible for
    /// producing valid JSON; the renderer wraps it in a script tag verbatim.
    /// Multiple JSON-LD blocks per page are supported via the JsonLdBlocks list.
    /// </summary>
    public string? JsonLd { get; set; }

    /// <summary>
    /// When more than one JSON-LD block is needed (Article + BreadcrumbList +
    /// FAQPage etc.), use this list. JsonLd above is rendered first if set.
    /// </summary>
    public List<string> JsonLdBlocks { get; set; } = new();
}
