using LexCalculus.Core.Interfaces;
using LexCalculus.Core.Models.Seo;
using Microsoft.Extensions.Options;

namespace LexCalculus.Infrastructure.Seo;

/// <summary>
/// Default ISeoMetaProvider that reads from SeoSettings configuration.
/// Registered as Singleton — settings are immutable after startup.
/// </summary>
public sealed class DefaultSeoMetaProvider : ISeoMetaProvider
{
    private readonly SeoSettings _settings;

    public DefaultSeoMetaProvider(IOptions<SeoSettings> options)
    {
        _settings = options.Value ?? throw new ArgumentNullException(nameof(options));
    }

    public SeoMeta GetDefault()
    {
        return new SeoMeta
        {
            Title = _settings.DefaultTitle,
            Description = _settings.DefaultDescription,
            Keywords = _settings.DefaultKeywords,
            OgImage = ResolveOgImage(_settings.DefaultOgImage),
            OgType = "website",
            TwitterCard = "summary_large_image"
        };
    }

    public SeoMeta MergeWithDefault(SeoMeta? page, string? currentPath = null)
    {
        var defaults = GetDefault();
        if (page is null)
        {
            // Fill canonical from currentPath even when no page meta supplied
            defaults.CanonicalUrl = ResolveCanonical(currentPath);
            return defaults;
        }

        return new SeoMeta
        {
            Title = string.IsNullOrWhiteSpace(page.Title) ? defaults.Title : page.Title,
            Description = string.IsNullOrWhiteSpace(page.Description) ? defaults.Description : page.Description,
            Keywords = string.IsNullOrWhiteSpace(page.Keywords) ? defaults.Keywords : page.Keywords,
            CanonicalUrl = !string.IsNullOrWhiteSpace(page.CanonicalUrl)
                ? page.CanonicalUrl
                : ResolveCanonical(currentPath),
            OgImage = !string.IsNullOrWhiteSpace(page.OgImage)
                ? ResolveOgImage(page.OgImage)
                : defaults.OgImage,
            OgType = !string.IsNullOrWhiteSpace(page.OgType) ? page.OgType : defaults.OgType,
            TwitterCard = !string.IsNullOrWhiteSpace(page.TwitterCard) ? page.TwitterCard : defaults.TwitterCard,
            JsonLd = page.JsonLd,
            JsonLdBlocks = page.JsonLdBlocks ?? new List<string>()
        };
    }

    private string? ResolveCanonical(string? currentPath)
    {
        if (string.IsNullOrWhiteSpace(currentPath)) return null;

        var siteUrl = _settings.SiteUrl.TrimEnd('/');
        var path = currentPath.StartsWith('/') ? currentPath : "/" + currentPath;
        return $"{siteUrl}{path}";
    }

    private string? ResolveOgImage(string? imagePath)
    {
        if (string.IsNullOrWhiteSpace(imagePath)) return null;

        // Already absolute
        if (Uri.IsWellFormedUriString(imagePath, UriKind.Absolute))
            return imagePath;

        // Relative path — prefix with SiteUrl
        var siteUrl = _settings.SiteUrl.TrimEnd('/');
        var path = imagePath.StartsWith('/') ? imagePath : "/" + imagePath;
        return $"{siteUrl}{path}";
    }
}
