namespace LexCalculus.Core.Calculators;

public static class FrequencyLabels
{
    public static string ToTurkish(string? freq) => freq switch
    {
        "Monthly"      => "Aylık",
        "Quarterly"    => "Üç Aylık",
        "Biannual"     => "Altı Aylık",
        "Yearly"       => "Yıllık",
        "OnLawChange"  => "Olay Bazlı (Mevzuat)",
        "Static"       => "Sabit (Kanun)",
        "Event"        => "Olay Bazlı",
        null or ""     => "—",
        _              => freq
    };

    /// <summary>Dropdown options for forms. Value = backend string, Label = TR.</summary>
    public static IReadOnlyList<(string Value, string Label)> All { get; } = new[]
    {
        ("Monthly",     "Aylık"),
        ("Quarterly",   "Üç Aylık"),
        ("Biannual",    "Altı Aylık"),
        ("Yearly",      "Yıllık"),
        ("OnLawChange", "Olay Bazlı (Mevzuat)"),
        ("Static",      "Sabit (Kanun)"),
        ("Event",       "Olay Bazlı"),
    };
}
