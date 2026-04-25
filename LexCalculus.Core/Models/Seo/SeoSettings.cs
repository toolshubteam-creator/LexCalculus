namespace LexCalculus.Core.Models.Seo;

/// <summary>
/// Strongly-typed binding for the SeoSettings configuration section.
/// Populated via IConfiguration.Bind() in DefaultSeoMetaProvider.
/// </summary>
public sealed class SeoSettings
{
    public string SiteName { get; set; } = "Lex Calculus";
    public string SiteUrl { get; set; } = "https://lexcalculus.com";
    public string DefaultTitle { get; set; } = "Lex Calculus";
    public string DefaultDescription { get; set; } = "";
    public string? DefaultKeywords { get; set; }
    public string? DefaultOgImage { get; set; }
    public string? TwitterHandle { get; set; }
}
