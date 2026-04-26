using LexCalculus.Core.Calculators.Common;

namespace LexCalculus.Web.Models.Hesapla;

public sealed class HesaplaCategoryViewModel
{
    public required CalculatorCategory Category { get; init; }
    public required string DisplayName { get; init; }
    public required IReadOnlyList<CalculatorMetadata> Tools { get; init; }
}
