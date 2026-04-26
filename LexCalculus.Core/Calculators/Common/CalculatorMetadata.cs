namespace LexCalculus.Core.Calculators.Common;

/// <summary>
/// Static metadata describing a calculator tool. Each ICalculator implementation
/// exposes its own Metadata via a static property. Used by the registry, the
/// catalog page, and the per-tool page header.
/// </summary>
public sealed class CalculatorMetadata
{
    public required string Slug { get; init; }
    public required CalculatorCategory Category { get; init; }
    public required string Title { get; init; }
    public required string ShortDescription { get; init; }
    public required string LegalReference { get; init; }
    public CalculatorStatus Status { get; init; } = CalculatorStatus.Active;
    public IReadOnlyList<string> Keywords { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Full URL path including category, e.g. "/hesapla/is-hukuku/kidem-tazminati".
    /// Composed from category slug and tool slug.
    /// </summary>
    public string UrlPath => $"/hesapla/{Category.ToSlug()}/{Slug}";

    /// <summary>
    /// Display number for the catalog (e.g. "01", "02"). Set by registry on registration.
    /// </summary>
    public string? DisplayNumber { get; init; }
}
