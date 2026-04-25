using LexCalculus.Core.Interfaces;
using LexCalculus.Core.Models.Seo;
using Microsoft.Extensions.Options;

namespace LexCalculus.Infrastructure.Seo;

/// <summary>
/// Phase 1 sitemap: static list of public pages. Future phases extend this
/// to query the database for published blog posts, calculation tools, and
/// public user profiles.
/// </summary>
public sealed class DefaultSitemapBuilder : ISitemapBuilder
{
    private readonly SeoSettings _settings;

    public DefaultSitemapBuilder(IOptions<SeoSettings> options)
    {
        _settings = options.Value ?? throw new ArgumentNullException(nameof(options));
    }

    public Task<IReadOnlyList<SitemapNode>> BuildAsync(CancellationToken cancellationToken = default)
    {
        var siteUrl = _settings.SiteUrl.TrimEnd('/');

        // Static entries for Phase 1 — replace with DB-backed queries in later phases
        var nodes = new List<SitemapNode>
        {
            new()
            {
                Url = $"{siteUrl}/",
                LastModified = DateTime.UtcNow,
                ChangeFrequency = SitemapChangeFrequency.Daily,
                Priority = 1.0m
            },
            new()
            {
                Url = $"{siteUrl}/Home/Privacy",
                LastModified = DateTime.UtcNow,
                ChangeFrequency = SitemapChangeFrequency.Yearly,
                Priority = 0.3m
            }
        };

        return Task.FromResult<IReadOnlyList<SitemapNode>>(nodes);
    }
}
