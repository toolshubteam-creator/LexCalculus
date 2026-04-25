namespace LexCalculus.Core.Models.Seo;

/// <summary>
/// Standard sitemaps.org changefreq values. Hint to crawlers about how
/// often a URL changes — not a directive.
/// </summary>
public enum SitemapChangeFrequency
{
    Always,
    Hourly,
    Daily,
    Weekly,
    Monthly,
    Yearly,
    Never
}
