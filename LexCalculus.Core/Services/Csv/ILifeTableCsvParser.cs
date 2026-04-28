namespace LexCalculus.Core.Services.Csv;

public interface ILifeTableCsvParser
{
    /// <summary>
    /// CSV stream'inden 200 LifeTableRow parse eder.
    /// Tüm validation hatalarını toplar (ilkinde durmaz).
    /// Beklenen format: header "Yas,Cinsiyet,BekledigiYasam" + 200 veri satırı,
    /// her yaş 0-99 için Erkek + Kadin.
    /// </summary>
    Task<CsvParseResult> ParseAsync(Stream csvStream, CancellationToken ct = default);
}
