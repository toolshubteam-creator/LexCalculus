using LexCalculus.Core.Calculators.Common;
using LexCalculus.Core.Interfaces;
using LexCalculus.Core.Models.Seo;
using LexCalculus.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LexCalculus.Infrastructure.Seo;

/// <summary>
/// Phase 1 sitemap: static list of public pages. Phase 4.1 P3/3'te public
/// profile URL'leri DB-driven eklendi (IsPublicProfile=true + IsActive=true).
/// </summary>
public sealed class DefaultSitemapBuilder : ISitemapBuilder
{
    private readonly SeoSettings _settings;
    private readonly ICalculatorRegistry _registry;
    private readonly ApplicationDbContext _ctx;

    public DefaultSitemapBuilder(
        IOptions<SeoSettings> options,
        ICalculatorRegistry registry,
        ApplicationDbContext ctx)
    {
        _settings = options.Value ?? throw new ArgumentNullException(nameof(options));
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _ctx = ctx ?? throw new ArgumentNullException(nameof(ctx));
    }

    public async Task<IReadOnlyList<SitemapNode>> BuildAsync(CancellationToken cancellationToken = default)
    {
        var siteUrl = _settings.SiteUrl.TrimEnd('/');

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
                Url = $"{siteUrl}/hesapla",
                LastModified = DateTime.UtcNow,
                ChangeFrequency = SitemapChangeFrequency.Weekly,
                Priority = 0.9m
            },
            new()
            {
                Url = $"{siteUrl}/Home/Privacy",
                LastModified = DateTime.UtcNow,
                ChangeFrequency = SitemapChangeFrequency.Yearly,
                Priority = 0.3m
            }
        };

        foreach (var category in _registry.GetActiveCategories())
        {
            nodes.Add(new SitemapNode
            {
                Url = $"{siteUrl}/hesapla/{category.ToSlug()}",
                LastModified = DateTime.UtcNow,
                ChangeFrequency = SitemapChangeFrequency.Weekly,
                Priority = 0.7m
            });
        }

        // Individual calculator pages — only Active tools (not ComingSoon/Deprecated)
        foreach (var tool in _registry.GetAll().Where(t => t.Status == CalculatorStatus.Active))
        {
            nodes.Add(new SitemapNode
            {
                Url = $"{siteUrl}{tool.UrlPath}",
                LastModified = DateTime.UtcNow,
                ChangeFrequency = SitemapChangeFrequency.Weekly,
                Priority = 0.6m
            });
        }

        // Faz 4.1 P3/3 — public profile URL'leri (IsPublicProfile + IsActive)
        var publicProfiles = await _ctx.UserProfiles
            .AsNoTracking()
            .Include(p => p.User)
            .Where(p => p.IsPublicProfile
                     && p.PublicSlug != null
                     && p.User != null
                     && p.User.IsActive)
            .Select(p => new
            {
                p.PublicSlug,
                LastMod = p.UpdatedAt ?? p.CreatedAt
            })
            .ToListAsync(cancellationToken);

        foreach (var profile in publicProfiles)
        {
            nodes.Add(new SitemapNode
            {
                Url = $"{siteUrl}/uye/{profile.PublicSlug}",
                LastModified = profile.LastMod == default ? DateTime.UtcNow : profile.LastMod,
                ChangeFrequency = SitemapChangeFrequency.Weekly,
                Priority = 0.5m
            });
        }

        // Faz 4.7 — public makale URL'leri (yayında + yazar aktif + yazar profili public)
        // Yazar profili gizliyse (IsPublicProfile=false) makale URL'i erişilebilir
        // ama sitemap dışı (charter §G-a).
        var publishedPosts = await _ctx.UserPosts
            .AsNoTracking()
            .Include(p => p.User).ThenInclude(u => u!.Profile)
            .Where(p => p.IsPublished
                     && p.User != null
                     && p.User.IsActive
                     && p.User.Profile != null
                     && p.User.Profile.IsPublicProfile
                     && p.User.Profile.PublicSlug != null)
            .Select(p => new
            {
                UserSlug = p.User!.Profile!.PublicSlug!,
                PostSlug = p.Slug,
                LastMod = p.UpdatedAt
            })
            .ToListAsync(cancellationToken);

        foreach (var post in publishedPosts)
        {
            nodes.Add(new SitemapNode
            {
                Url = $"{siteUrl}/uye/{post.UserSlug}/makale/{post.PostSlug}",
                LastModified = post.LastMod,
                ChangeFrequency = SitemapChangeFrequency.Weekly,
                Priority = 0.6m
            });
        }

        return nodes;
    }
}
