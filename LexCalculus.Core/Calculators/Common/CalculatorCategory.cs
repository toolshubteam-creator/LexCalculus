namespace LexCalculus.Core.Calculators.Common;

/// <summary>
/// High-level category for calculation tools. Used for grouping in the UI
/// (sidebar, tabs) and for category landing pages with their own SEO.
/// Slug values are used in URLs, e.g. /hesapla/is-hukuku/...
/// </summary>
public enum CalculatorCategory
{
    /// <summary>İş Hukuku — kıdem, ihbar, yıllık izin, fazla mesai, işe iade...</summary>
    IsHukuku = 1,

    /// <summary>Aktüerya — destekten yoksun kalma, maluliyet, iş göremezlik...</summary>
    Akturya = 2,

    /// <summary>Faiz ve Alacak — yasal/ticari/temerrüt faizi, kira artışı...</summary>
    Faiz = 3,

    /// <summary>Gayrimenkul ve Kat Mülkiyeti — Faz 7</summary>
    Gayrimenkul = 4,

    /// <summary>Aile ve Miras — Faz 7</summary>
    AileMiras = 5,

    /// <summary>Ceza Hukuku ve İnfaz — Faz 7</summary>
    Ceza = 6,

    /// <summary>Vergi ve İdare — Faz 7</summary>
    VergiIdare = 7,

    /// <summary>Ticaret Hukuku — Faz 7</summary>
    Ticaret = 8,

    /// <summary>Bilirkişilik Özel Araçları — Faz 7</summary>
    Bilirkisi = 9
}
