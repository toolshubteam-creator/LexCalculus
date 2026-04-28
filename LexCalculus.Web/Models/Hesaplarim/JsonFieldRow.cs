namespace LexCalculus.Web.Models.Hesaplarim;

public sealed class JsonFieldRow
{
    public required string Key { get; init; }
    public required string DisplayKey { get; init; }
    public required string DisplayValue { get; init; }
    public required string Type { get; init; }   // "decimal", "integer", "date", "string", "bool", "complex"
    public bool IsComplex => Type == "complex";
}
