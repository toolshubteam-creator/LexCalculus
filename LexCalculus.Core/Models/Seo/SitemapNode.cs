namespace LexCalculus.Core.Models.Seo;

/// <summary>
/// One &lt;url&gt; entry in a sitemap.xml. Url is absolute (https://...).
/// </summary>
public sealed class SitemapNode
{
    public string Url { get; set; } = string.Empty;
    public DateTime LastModified { get; set; } = DateTime.UtcNow;
    public SitemapChangeFrequency ChangeFrequency { get; set; } = SitemapChangeFrequency.Weekly;
    public decimal Priority { get; set; } = 0.5m;
}
