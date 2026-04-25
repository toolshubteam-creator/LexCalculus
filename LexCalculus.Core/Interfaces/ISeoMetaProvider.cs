using LexCalculus.Core.Models.Seo;

namespace LexCalculus.Core.Interfaces;

/// <summary>
/// Produces SeoMeta values, either from defaults (configuration) or by
/// merging a page-specific SeoMeta into the defaults.
/// </summary>
public interface ISeoMetaProvider
{
    /// <summary>Default SeoMeta from configuration (SeoSettings section).</summary>
    SeoMeta GetDefault();

    /// <summary>
    /// Merges a page-specific SeoMeta with defaults. Null/empty fields on
    /// <paramref name="page"/> are filled from defaults. Pass null to get the
    /// pure default.
    /// </summary>
    SeoMeta MergeWithDefault(SeoMeta? page, string? currentPath = null);
}
