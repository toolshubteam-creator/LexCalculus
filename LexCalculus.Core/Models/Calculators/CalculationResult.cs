namespace LexCalculus.Core.Models.Calculators;

/// <summary>
/// Standard envelope for all calculator outputs. Mirrors the result-panel
/// component in the design system: total amount on top, breakdown rows
/// below, optional disclaimer note, and any validation errors.
///
/// Calculators that need structured outputs beyond text rows should return
/// a richer subclass (e.g. KidemTazminatiResult : CalculationResult).
/// </summary>
public class CalculationResult
{
    /// <summary>If false, IsValid validation errors are populated and other fields are not meaningful.</summary>
    public bool IsValid { get; set; } = true;

    /// <summary>
    /// Validation messages keyed by input field name (e.g. "GirisTarihi" => "Tarih boş olamaz").
    /// Populated when IsValid == false.
    /// </summary>
    public Dictionary<string, string> ValidationErrors { get; set; } = new();

    /// <summary>
    /// Top-line amount. Format determines unit (TL, gün, %, etc.) — set by calculator.
    /// </summary>
    public decimal? TotalAmount { get; set; }

    /// <summary>Label rendered above TotalAmount in the result panel, e.g. "Toplam Tazminat".</summary>
    public string? TotalLabel { get; set; }

    /// <summary>
    /// Currency or unit symbol shown next to TotalAmount, e.g. "TL", "%", "gün".
    /// Empty string for unitless ratios.
    /// </summary>
    public string? Unit { get; set; } = "TL";

    /// <summary>
    /// Breakdown rows shown below the total. Each is a key/value pair like
    /// "Toplam Çalışma Süresi" => "5 yıl 3 ay".
    /// </summary>
    public List<CalculationResultRow> Rows { get; set; } = new();

    /// <summary>
    /// Disclaimer / explanatory note shown at the bottom of the result panel.
    /// May contain limited inline HTML (strong, em) — caller renders trusted.
    /// </summary>
    public string? Note { get; set; }

    /// <summary>
    /// Warnings that don't invalidate the result but the user should see
    /// (e.g. "Ücret tavanı aşıldı, tavan değer kullanıldı").
    /// </summary>
    public List<string> Warnings { get; set; } = new();
}

public sealed class CalculationResultRow
{
    public required string Key { get; init; }
    public required string Value { get; init; }

    /// <summary>If true, render this row with bold/highlighted styling.</summary>
    public bool IsHighlighted { get; init; }
}
