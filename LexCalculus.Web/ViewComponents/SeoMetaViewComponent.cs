using LexCalculus.Core.Interfaces;
using LexCalculus.Core.Models.Seo;
using Microsoft.AspNetCore.Mvc;

namespace LexCalculus.Web.ViewComponents;

/// <summary>
/// Renders all SEO meta tags (title, description, canonical, Open Graph,
/// Twitter Card, JSON-LD) inside the document head. Reads page-specific
/// SeoMeta from ViewData["PageMeta"] when available, otherwise falls back
/// to defaults. Always emits a canonical URL based on the current request.
/// </summary>
public sealed class SeoMetaViewComponent : ViewComponent
{
    private readonly ISeoMetaProvider _provider;

    public SeoMetaViewComponent(ISeoMetaProvider provider)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
    }

    public IViewComponentResult Invoke()
    {
        // Pull page-specific meta from ViewData if controller set it
        var pageMeta = ViewContext.ViewData["PageMeta"] as SeoMeta;

        // Build current path including query string
        var path = ViewContext.HttpContext.Request.Path.Value ?? "/";
        var queryString = ViewContext.HttpContext.Request.QueryString.Value;
        var fullPath = string.IsNullOrEmpty(queryString) ? path : path + queryString;

        var merged = _provider.MergeWithDefault(pageMeta, fullPath);
        return View(merged);
    }
}
