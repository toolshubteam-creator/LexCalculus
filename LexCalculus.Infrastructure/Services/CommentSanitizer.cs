using Ganss.Xss;
using LexCalculus.Core.Services;

namespace LexCalculus.Infrastructure.Services;

/// <summary>
/// Yorum sanitizer — sıkı whitelist. Singleton (thread-safe).
/// Post body sanitizer'dan ayrı (post: img/h2/blockquote dahil; comment: sadece
/// link + line break). Faz 4.9 P1.
/// </summary>
public sealed class CommentSanitizer : ICommentSanitizer
{
    private readonly HtmlSanitizer _sanitizer;

    public CommentSanitizer()
    {
        _sanitizer = new HtmlSanitizer();
        _sanitizer.AllowedTags.Clear();
        _sanitizer.AllowedTags.UnionWith(new[] { "a", "br" });
        _sanitizer.AllowedAttributes.Clear();
        _sanitizer.AllowedAttributes.UnionWith(new[] { "href", "rel", "target" });
        _sanitizer.AllowedSchemes.Clear();
        _sanitizer.AllowedSchemes.UnionWith(new[] { "http", "https", "mailto" });
        _sanitizer.AllowedCssProperties.Clear();
        _sanitizer.AllowedAtRules.Clear();
    }

    public string Sanitize(string input) => _sanitizer.Sanitize(input ?? string.Empty);
}
