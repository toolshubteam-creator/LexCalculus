using System.Net;
using System.Text.RegularExpressions;
using LexCalculus.Core.Services;

namespace LexCalculus.Infrastructure.Services;

/// <summary>
/// Yorum body işleme: HtmlEncode → satır sonu &lt;br&gt; → URL auto-link →
/// sanitize (ekstra savunma). Faz 4.9 P1.
/// </summary>
public static class CommentBodyProcessor
{
    private static readonly Regex UrlRegex = new(
        @"\b(https?://[^\s<]+)",
        RegexOptions.Compiled);

    /// <summary>
    /// Kullanıcının raw text yorumunu güvenli HTML'e dönüştürür.
    /// 1. HtmlEncode — &lt;script&gt;, &lt;b&gt; vb. text olarak kalır
    /// 2. \n → &lt;br&gt; (paragraf yapısı)
    /// 3. http(s):// URL'leri &lt;a href ... rel="nofollow noopener"&gt; ile sarar
    /// 4. CommentSanitizer ile son kontrol (beklenmeyen tag'leri temizle)
    /// </summary>
    public static string Process(string raw, ICommentSanitizer sanitizer)
    {
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;

        var encoded = WebUtility.HtmlEncode(raw.Trim());
        var withBr = encoded.Replace("\n", "<br>");

        var linked = UrlRegex.Replace(withBr, m =>
        {
            var url = m.Value;
            // HtmlEncode sonrası URL içindeki & karakteri &amp; olabilir;
            // href ve görüntü için aynı kullan, browser doğru parse eder.
            return $"<a href=\"{url}\" rel=\"nofollow noopener\" target=\"_blank\">{url}</a>";
        });

        return sanitizer.Sanitize(linked);
    }
}
