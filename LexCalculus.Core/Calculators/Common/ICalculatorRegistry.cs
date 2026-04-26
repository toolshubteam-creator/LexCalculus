namespace LexCalculus.Core.Calculators.Common;

/// <summary>
/// Single source of truth for all calculator metadata. Reads from registered
/// ICalculator instances at startup; results cached in memory.
///
/// In Phase 2 we register calculators directly via AddCalculator&lt;T&gt;().
/// In Phase 3 we may add a database-backed registration layer for admin
/// toggling of tools without redeploys.
/// </summary>
public interface ICalculatorRegistry
{
    /// <summary>All registered calculators in display order (category, then DisplayNumber).</summary>
    IReadOnlyList<CalculatorMetadata> GetAll();

    /// <summary>All calculators in a given category, ordered by DisplayNumber.</summary>
    IReadOnlyList<CalculatorMetadata> GetByCategory(CalculatorCategory category);

    /// <summary>Lookup by slug (without category prefix). Returns null if not found.</summary>
    CalculatorMetadata? FindBySlug(string slug);

    /// <summary>Lookup by category + slug. Returns null if either doesn't match.</summary>
    CalculatorMetadata? Find(CalculatorCategory category, string slug);

    /// <summary>All categories that have at least one registered calculator.</summary>
    IReadOnlyList<CalculatorCategory> GetActiveCategories();

    /// <summary>True if the category has any non-ComingSoon, non-Deprecated calculator.</summary>
    bool HasActiveTools(CalculatorCategory category);
}
