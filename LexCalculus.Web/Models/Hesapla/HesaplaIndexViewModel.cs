using LexCalculus.Core.Calculators.Common;

namespace LexCalculus.Web.Models.Hesapla;

public sealed class HesaplaIndexViewModel
{
    public required IReadOnlyList<CategorySection> Sections { get; init; }
}

public sealed class CategorySection
{
    public required CalculatorCategory Category { get; init; }
    public required IReadOnlyList<CalculatorMetadata> Tools { get; init; }
}
