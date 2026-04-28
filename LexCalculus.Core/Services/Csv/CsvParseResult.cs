using LexCalculus.Core.Entities.Calculators;

namespace LexCalculus.Core.Services.Csv;

public sealed record CsvParseError(
    int LineNumber,
    string Field,
    string Message);

public sealed record CsvParseResult(
    bool Success,
    IReadOnlyList<LifeTableRow> Rows,
    IReadOnlyList<CsvParseError> Errors)
{
    public static CsvParseResult Successful(IReadOnlyList<LifeTableRow> rows)
        => new(true, rows, Array.Empty<CsvParseError>());

    public static CsvParseResult Failed(IReadOnlyList<CsvParseError> errors)
        => new(false, Array.Empty<LifeTableRow>(), errors);
}
