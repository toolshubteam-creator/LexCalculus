using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;

namespace LexCalculus.Web.Infrastructure.Rendering;

/// <summary>
/// <see cref="IImageVariantEnricher"/> regex tabanlı implementasyon. Gövde
/// sunucuda Ganss.Xss ile sanitize edilmiş, dar whitelist'li HTML olduğundan
/// img etiketleri iyi biçimli → hedefli regex güvenli. Yalnızca
/// <c>uploads/posts/{id}/inline/{guid}.webp</c> deseni işlenir; featured/harici
/// görseller (variant'ı yok) dokunulmaz.
/// </summary>
public sealed class ImageVariantEnricher : IImageVariantEnricher
{
    private static readonly int[] VariantWidths = { 480, 800 };
    private const string Sizes = "(max-width: 768px) 100vw, 760px";

    // <img ...> etiketleri (self-closing veya değil).
    private static readonly Regex ImgTagRegex = new(
        @"<img\b[^>]*>",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // src değeri (tek veya çift tırnak).
    private static readonly Regex SrcRegex = new(
        "src\\s*=\\s*(?:\"([^\"]*)\"|'([^']*)')",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // İnline görsel webp src'i (root-relative veya absolute fark etmez —
    // "uploads/posts/.../inline/{guid}.webp" alt-dizgisi aranır).
    private static readonly Regex InlineSrcRegex = new(
        @"uploads/posts/\d+/inline/[^""'/?#]+\.webp$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly IWebHostEnvironment _env;

    public ImageVariantEnricher(IWebHostEnvironment env) => _env = env;

    public string Enrich(string? bodyHtml)
    {
        if (string.IsNullOrEmpty(bodyHtml)) return bodyHtml ?? string.Empty;
        // Hızlı çıkış: hiç inline görsel yoksa regex'e girme.
        if (!bodyHtml.Contains("/inline/", StringComparison.OrdinalIgnoreCase))
            return bodyHtml;

        return ImgTagRegex.Replace(bodyHtml, m => EnrichTag(m.Value));
    }

    private string EnrichTag(string tag)
    {
        // İdempotent: zaten srcset varsa dokunma.
        if (tag.Contains("srcset", StringComparison.OrdinalIgnoreCase))
            return tag;

        var srcMatch = SrcRegex.Match(tag);
        if (!srcMatch.Success) return tag;

        var src = srcMatch.Groups[1].Success ? srcMatch.Groups[1].Value : srcMatch.Groups[2].Value;
        if (string.IsNullOrEmpty(src) || !InlineSrcRegex.IsMatch(src))
            return tag;

        // WebRoot-relative yol: "uploads/posts/..." başlangıcından itibaren.
        var idx = src.IndexOf("uploads/posts/", StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return tag;
        var relative = src[idx..];
        var baseSrc = src[..^".webp".Length];           // src ".webp" ile biter (regex garanti)
        var relativeBase = relative[..^".webp".Length];

        var srcsetParts = new List<string>();
        foreach (var w in VariantWidths)
        {
            var variantRelative = $"{relativeBase}_{w}.webp";
            var diskPath = Path.Combine(
                _env.WebRootPath,
                variantRelative.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(diskPath))
                srcsetParts.Add($"{baseSrc}_{w}.webp {w}w");
        }

        if (srcsetParts.Count == 0) return tag;          // variant yok → dokunma

        var attrs = new StringBuilder();
        attrs.Append(" srcset=\"").Append(string.Join(", ", srcsetParts)).Append('"');
        attrs.Append(" sizes=\"").Append(Sizes).Append('"');
        if (!tag.Contains("loading", StringComparison.OrdinalIgnoreCase))
            attrs.Append(" loading=\"lazy\"");

        // Attribute'ları kapanıştan ('/>' veya '>') hemen önce ekle.
        if (tag.EndsWith("/>", StringComparison.Ordinal))
            return string.Concat(tag.AsSpan(0, tag.Length - 2), attrs.ToString(), "/>");
        return string.Concat(tag.AsSpan(0, tag.Length - 1), attrs.ToString(), ">");
    }
}
