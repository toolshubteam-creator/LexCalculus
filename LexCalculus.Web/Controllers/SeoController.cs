using System.Text;
using System.Xml;
using LexCalculus.Core.Interfaces;
using LexCalculus.Core.Models.Seo;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace LexCalculus.Web.Controllers;

/// <summary>
/// Crawler-facing endpoints: sitemap.xml and robots.txt.
/// Output is cached briefly to avoid rebuilding on every crawler hit.
/// </summary>
[Route("")]
public class SeoController : Controller
{
    private readonly ISitemapBuilder _sitemapBuilder;
    private readonly SeoSettings _seoSettings;

    public SeoController(ISitemapBuilder sitemapBuilder, IOptions<SeoSettings> seoOptions)
    {
        _sitemapBuilder = sitemapBuilder ?? throw new ArgumentNullException(nameof(sitemapBuilder));
        _seoSettings = seoOptions.Value ?? throw new ArgumentNullException(nameof(seoOptions));
    }

    [HttpGet("sitemap.xml")]
    [ResponseCache(Duration = 3600, Location = ResponseCacheLocation.Any)]
    public async Task<IActionResult> Sitemap(CancellationToken cancellationToken)
    {
        var nodes = await _sitemapBuilder.BuildAsync(cancellationToken);

        var xml = BuildSitemapXml(nodes);
        return Content(xml, "application/xml; charset=utf-8");
    }

    [HttpGet("robots.txt")]
    [ResponseCache(Duration = 86400, Location = ResponseCacheLocation.Any)]
    public IActionResult Robots()
    {
        var siteUrl = _seoSettings.SiteUrl.TrimEnd('/');

        var sb = new StringBuilder();
        sb.AppendLine("User-agent: *");
        sb.AppendLine("Allow: /");
        sb.AppendLine("Disallow: /Identity/");
        sb.AppendLine("Disallow: /Admin/");
        sb.AppendLine("Disallow: /api/");
        sb.AppendLine();
        sb.AppendLine($"Sitemap: {siteUrl}/sitemap.xml");

        return Content(sb.ToString(), "text/plain; charset=utf-8");
    }

    private static string BuildSitemapXml(IReadOnlyList<SitemapNode> nodes)
    {
        var settings = new XmlWriterSettings
        {
            Indent = true,
            Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            Async = false
        };

        using var sw = new Utf8StringWriter();
        using (var writer = XmlWriter.Create(sw, settings))
        {
            writer.WriteStartDocument();
            writer.WriteStartElement("urlset", "http://www.sitemaps.org/schemas/sitemap/0.9");

            foreach (var node in nodes)
            {
                writer.WriteStartElement("url");

                writer.WriteElementString("loc", node.Url);
                writer.WriteElementString("lastmod", node.LastModified.ToString("yyyy-MM-ddTHH:mm:ssZ"));
                writer.WriteElementString("changefreq", node.ChangeFrequency.ToString().ToLowerInvariant());
                writer.WriteElementString("priority", node.Priority.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture));

                writer.WriteEndElement(); // </url>
            }

            writer.WriteEndElement(); // </urlset>
            writer.WriteEndDocument();
        }

        return sw.ToString();
    }

    /// <summary>
    /// StringWriter that reports UTF-8 (the default StringWriter reports UTF-16,
    /// which would make XmlWriter emit &lt;?xml encoding="utf-16"?&gt;).
    /// </summary>
    private sealed class Utf8StringWriter : StringWriter
    {
        public override Encoding Encoding => Encoding.UTF8;
    }
}
