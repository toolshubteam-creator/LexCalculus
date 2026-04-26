using LexCalculus.Core.Calculators.Common;

namespace LexCalculus.Infrastructure.Calculators;

/// <summary>
/// Eager singleton registry. All ICalculator instances are resolved from DI
/// at construction; the metadata snapshot is then immutable for the app lifetime.
///
/// Display ordering: by category (enum order) then by DisplayNumber if set,
/// else by Title. Calculators register their own DisplayNumber via metadata,
/// or it can be assigned by registration order at startup.
/// </summary>
public sealed class CalculatorRegistry : ICalculatorRegistry
{
    private readonly IReadOnlyList<CalculatorMetadata> _all;
    private readonly Dictionary<string, CalculatorMetadata> _bySlug;
    private readonly Dictionary<(CalculatorCategory, string), CalculatorMetadata> _byCategorySlug;

    public CalculatorRegistry(IEnumerable<ICalculator> calculators)
    {
        ArgumentNullException.ThrowIfNull(calculators);

        _all = calculators
            .Select(c => c.Metadata)
            .OrderBy(m => (int)m.Category)
            .ThenBy(m => m.DisplayNumber ?? m.Title)
            .ToList();

        _bySlug = new Dictionary<string, CalculatorMetadata>(StringComparer.OrdinalIgnoreCase);
        _byCategorySlug = new Dictionary<(CalculatorCategory, string), CalculatorMetadata>();

        foreach (var meta in _all)
        {
            if (!_bySlug.TryAdd(meta.Slug, meta))
            {
                throw new InvalidOperationException(
                    $"Duplicate calculator slug detected: '{meta.Slug}'. Each calculator must have a unique slug.");
            }
            _byCategorySlug[(meta.Category, meta.Slug)] = meta;
        }
    }

    public IReadOnlyList<CalculatorMetadata> GetAll() => _all;

    public IReadOnlyList<CalculatorMetadata> GetByCategory(CalculatorCategory category) =>
        _all.Where(m => m.Category == category).ToList();

    public CalculatorMetadata? FindBySlug(string slug)
    {
        if (string.IsNullOrWhiteSpace(slug)) return null;
        return _bySlug.TryGetValue(slug, out var meta) ? meta : null;
    }

    public CalculatorMetadata? Find(CalculatorCategory category, string slug)
    {
        if (string.IsNullOrWhiteSpace(slug)) return null;
        return _byCategorySlug.TryGetValue((category, slug), out var meta) ? meta : null;
    }

    public IReadOnlyList<CalculatorCategory> GetActiveCategories() =>
        _all.Select(m => m.Category).Distinct().OrderBy(c => (int)c).ToList();

    public bool HasActiveTools(CalculatorCategory category) =>
        _all.Any(m => m.Category == category && m.Status == CalculatorStatus.Active);
}
