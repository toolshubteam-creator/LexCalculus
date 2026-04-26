namespace LexCalculus.Core.Calculators.Common;

/// <summary>
/// Helpers to convert CalculatorCategory to/from URL slugs and display names.
/// Centralized here so route handling, UI labels, and SEO use the same source.
/// </summary>
public static class CalculatorCategoryExtensions
{
    public static string ToSlug(this CalculatorCategory category) => category switch
    {
        CalculatorCategory.IsHukuku     => "is-hukuku",
        CalculatorCategory.Akturya      => "akturya",
        CalculatorCategory.Faiz         => "faiz",
        CalculatorCategory.Gayrimenkul  => "gayrimenkul",
        CalculatorCategory.AileMiras    => "aile-miras",
        CalculatorCategory.Ceza         => "ceza",
        CalculatorCategory.VergiIdare   => "vergi-idare",
        CalculatorCategory.Ticaret      => "ticaret",
        CalculatorCategory.Bilirkisi    => "bilirkisi",
        _ => throw new ArgumentOutOfRangeException(nameof(category), category, null)
    };

    public static string ToDisplayName(this CalculatorCategory category) => category switch
    {
        CalculatorCategory.IsHukuku     => "İş Hukuku",
        CalculatorCategory.Akturya      => "Aktüerya ve Tazminat",
        CalculatorCategory.Faiz         => "Faiz ve Alacak",
        CalculatorCategory.Gayrimenkul  => "Gayrimenkul ve Kat Mülkiyeti",
        CalculatorCategory.AileMiras    => "Aile ve Miras Hukuku",
        CalculatorCategory.Ceza         => "Ceza Hukuku ve İnfaz",
        CalculatorCategory.VergiIdare   => "Vergi ve İdare",
        CalculatorCategory.Ticaret      => "Ticaret Hukuku",
        CalculatorCategory.Bilirkisi    => "Bilirkişilik Özel Araçları",
        _ => throw new ArgumentOutOfRangeException(nameof(category), category, null)
    };

    public static string ToShortName(this CalculatorCategory category) => category switch
    {
        CalculatorCategory.IsHukuku     => "İş Hukuku",
        CalculatorCategory.Akturya      => "Aktüerya",
        CalculatorCategory.Faiz         => "Faiz",
        CalculatorCategory.Gayrimenkul  => "Gayrimenkul",
        CalculatorCategory.AileMiras    => "Aile/Miras",
        CalculatorCategory.Ceza         => "Ceza",
        CalculatorCategory.VergiIdare   => "Vergi",
        CalculatorCategory.Ticaret      => "Ticaret",
        CalculatorCategory.Bilirkisi    => "Bilirkişi",
        _ => throw new ArgumentOutOfRangeException(nameof(category), category, null)
    };

    /// <summary>Inverse of ToSlug. Returns null if no category matches.</summary>
    public static CalculatorCategory? FromSlug(string slug)
    {
        if (string.IsNullOrWhiteSpace(slug)) return null;

        return slug.ToLowerInvariant() switch
        {
            "is-hukuku"     => CalculatorCategory.IsHukuku,
            "akturya"       => CalculatorCategory.Akturya,
            "faiz"          => CalculatorCategory.Faiz,
            "gayrimenkul"   => CalculatorCategory.Gayrimenkul,
            "aile-miras"    => CalculatorCategory.AileMiras,
            "ceza"          => CalculatorCategory.Ceza,
            "vergi-idare"   => CalculatorCategory.VergiIdare,
            "ticaret"       => CalculatorCategory.Ticaret,
            "bilirkisi"     => CalculatorCategory.Bilirkisi,
            _ => null
        };
    }
}
