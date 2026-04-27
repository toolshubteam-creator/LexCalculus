using LexCalculus.Core.Calculators.Common;

namespace LexCalculus.Core.Calculators;

/// <summary>
/// Placeholder calculators for Phase 2 scaffolding. Each implements ICalculator
/// only to expose Metadata to the registry; CalculateAsync isn't implemented
/// because the real logic isn't ready yet. Status = ComingSoon so the UI shows
/// them as inactive cards.
///
/// As real calculators are built (Phase 2.5+), each placeholder is REPLACED by
/// its real counterpart with Status = Active.
/// </summary>
public abstract class PlaceholderCalculator : ICalculator
{
    public abstract CalculatorMetadata Metadata { get; }
}

// === KATEGORİ A — İŞ HUKUKU ===
// Note: KidemTazminati moved to real implementation in IsHukuku/KidemTazminatiCalculator.cs

// Note: IhbarTazminati moved to real implementation in IsHukuku/IhbarTazminatiCalculator.cs

// Note: YillikIzin moved to real implementation in IsHukuku/YillikIzinCalculator.cs

// Note: FazlaMesai moved to real implementation in IsHukuku/FazlaMesaiCalculator.cs

// Note: IseIade moved to real implementation in IsHukuku/IseIadeCalculator.cs

// Note: AsgariUcret moved to real implementation in IsHukuku/AsgariUcretCalculator.cs

// Note: Mobbing moved to real implementation in IsHukuku/MobbingCalculator.cs

// === KATEGORİ B — AKTÜERYA ===

// Note: DestekTenYoksunKalma moved to real implementation in Akturya/DesteKtenYoksunKalmaCalculator.cs

// Note: Maluliyet moved to real implementation in Akturya/MaluliyetCalculator.cs

// Note: GeciciIsGoremezlik moved to real implementation in Akturya/GeciciIsGoremezlikCalculator.cs

// Note: BakiciGideri moved to real implementation in Akturya/BakiciGideriCalculator.cs

// Note: AracDegerKaybi moved to real implementation in Akturya/AracDegerKaybiCalculator.cs

// === KATEGORİ C — FAİZ ===

public sealed class YasalFaizPlaceholder : PlaceholderCalculator
{
    public override CalculatorMetadata Metadata { get; } = new()
    {
        Slug = "yasal-faiz",
        Category = CalculatorCategory.Faiz,
        Title = "Yasal Faiz Hesabı",
        ShortDescription = "3095 sayılı Kanun dönemsel oranları, basit ve bileşik faiz seçeneği, çok dönemli hesap.",
        LegalReference = "3095 s.K.",
        Status = CalculatorStatus.ComingSoon,
        DisplayNumber = "13"
    };
}

public sealed class TicariFaizPlaceholder : PlaceholderCalculator
{
    public override CalculatorMetadata Metadata { get; } = new()
    {
        Slug = "ticari-faiz",
        Category = CalculatorCategory.Faiz,
        Title = "Ticari Faiz",
        ShortDescription = "TCMB avans faizi referanslı, yıllık ilan tablosu ile otomatik oran.",
        LegalReference = "TCMB / TTK",
        Status = CalculatorStatus.ComingSoon,
        DisplayNumber = "14"
    };
}

public sealed class TemerrutFaiziPlaceholder : PlaceholderCalculator
{
    public override CalculatorMetadata Metadata { get; } = new()
    {
        Slug = "temerrut-faizi",
        Category = CalculatorCategory.Faiz,
        Title = "Temerrüt Faizi",
        ShortDescription = "BK m.117 — temerrüt tarihi ve faiz başlangıcı, ihtar koşulları kontrolü.",
        LegalReference = "Borçlar Kanunu m.117",
        Status = CalculatorStatus.ComingSoon,
        DisplayNumber = "15"
    };
}

public sealed class KiraArtisiPlaceholder : PlaceholderCalculator
{
    public override CalculatorMetadata Metadata { get; } = new()
    {
        Slug = "kira-artisi",
        Category = CalculatorCategory.Faiz,
        Title = "Kira Alacağı ve Artış Hesabı",
        ShortDescription = "6098 s.K. m.344 — TÜFE oranı sınırı, birikmiş kira alacağı ve faiz.",
        LegalReference = "6098 s.K. m.344",
        Status = CalculatorStatus.ComingSoon,
        DisplayNumber = "16"
    };
}

public sealed class MenfiTespitPlaceholder : PlaceholderCalculator
{
    public override CalculatorMetadata Metadata { get; } = new()
    {
        Slug = "menfi-tespit",
        Category = CalculatorCategory.Faiz,
        Title = "Menfi Tespit / İstirdat Alacağı",
        ShortDescription = "Haksız ödenen tutar + yasal faiz, ödeme tarihinden itibaren hesap.",
        LegalReference = "Borçlar Kanunu",
        Status = CalculatorStatus.ComingSoon,
        DisplayNumber = "17"
    };
}
