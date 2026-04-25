using LexCalculus.Core.Models.Seo;

namespace LexCalculus.Core.Interfaces;

/// <summary>
/// Produces the list of SitemapNode entries for /sitemap.xml.
/// In Phase 1 this returns a static list; later phases will append blog
/// posts, calculation tools, public profiles, etc., from the database.
/// </summary>
public interface ISitemapBuilder
{
    Task<IReadOnlyList<SitemapNode>> BuildAsync(CancellationToken cancellationToken = default);
}
