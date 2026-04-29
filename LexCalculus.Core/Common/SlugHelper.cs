using System.Text;
using System.Text.RegularExpressions;

namespace LexCalculus.Core.Common;

/// <summary>
/// URL-safe slug üretici. Türkçe karakterler ASCII'ye normalize edilir,
/// alfanumerik dışı karakterler (özel karakter, sembol) atılır,
/// boşluk/altçizgi tireye çevrilir, ardışık tireler tek tireye indirilir.
/// </summary>
public static class SlugHelper
{
    private static readonly Dictionary<char, char> TurkishMap = new()
    {
        ['ş'] = 's', ['Ş'] = 's',
        ['ğ'] = 'g', ['Ğ'] = 'g',
        ['ı'] = 'i', ['İ'] = 'i',
        ['ç'] = 'c', ['Ç'] = 'c',
        ['ö'] = 'o', ['Ö'] = 'o',
        ['ü'] = 'u', ['Ü'] = 'u'
    };

    public static string Generate(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;

        var sb = new StringBuilder(input.Length);
        foreach (var ch in input.Trim().ToLowerInvariant())
        {
            if (TurkishMap.TryGetValue(ch, out var mapped))
                sb.Append(mapped);
            else if (char.IsLetterOrDigit(ch) && ch < 128)
                sb.Append(ch);
            else if (char.IsWhiteSpace(ch) || ch == '-' || ch == '_')
                sb.Append('-');
            // diğer karakterler atılır
        }

        var slug = Regex.Replace(sb.ToString(), "-+", "-");
        return slug.Trim('-');
    }
}
