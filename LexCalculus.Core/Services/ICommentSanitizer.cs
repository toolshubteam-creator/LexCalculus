namespace LexCalculus.Core.Services;

/// <summary>
/// Yorum body'leri için ayrı sanitizer (post body'den daha sıkı whitelist).
/// İzinli: &lt;a&gt;, &lt;br&gt;. Attribute: href, rel, target. Şema: http,
/// https, mailto. Faz 4.9 P1.
/// </summary>
public interface ICommentSanitizer
{
    string Sanitize(string input);
}
