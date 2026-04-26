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

public sealed class FazlaMesaiPlaceholder : PlaceholderCalculator
{
    public override CalculatorMetadata Metadata { get; } = new()
    {
        Slug = "fazla-mesai",
        Category = CalculatorCategory.IsHukuku,
        Title = "Fazla Mesai Alacağı",
        ShortDescription = "45 saati aşan mesai %50 zamlı, hafta tatili %100, ulusal bayram %100 — kümülatif hesap.",
        LegalReference = "4857 s.K. m.41",
        Status = CalculatorStatus.ComingSoon,
        DisplayNumber = "04"
    };
}

public sealed class IseIadeTazminatiPlaceholder : PlaceholderCalculator
{
    public override CalculatorMetadata Metadata { get; } = new()
    {
        Slug = "ise-iade-tazminati",
        Category = CalculatorCategory.IsHukuku,
        Title = "İşe İade Tazminatı",
        ShortDescription = "Feshin geçersizliği halinde 4-8 aylık brüt ücret tazminatı, boşta geçen süre ücreti.",
        LegalReference = "4857 s.K. m.21",
        Status = CalculatorStatus.ComingSoon,
        DisplayNumber = "05"
    };
}

public sealed class AsgariUcretKontrolPlaceholder : PlaceholderCalculator
{
    public override CalculatorMetadata Metadata { get; } = new()
    {
        Slug = "asgari-ucret-kontrol",
        Category = CalculatorCategory.IsHukuku,
        Title = "Asgari Ücret Uyumluluk Kontrolü",
        ShortDescription = "Girilen ücretin dönemsel asgari ücretle karşılaştırması, eksik ödeme tespiti ve toplam alacak.",
        LegalReference = "Çalışma Bakanlığı",
        Status = CalculatorStatus.ComingSoon,
        DisplayNumber = "06"
    };
}

public sealed class MobbingTazminatiPlaceholder : PlaceholderCalculator
{
    public override CalculatorMetadata Metadata { get; } = new()
    {
        Slug = "mobbing-tazminati",
        Category = CalculatorCategory.IsHukuku,
        Title = "Mobbing / Manevi Tazminat",
        ShortDescription = "Emsal kararlar tabanlı tahmini hesap; çalışma süresi ve şiddet düzeyine göre.",
        LegalReference = "Yargıtay emsal kararları",
        Status = CalculatorStatus.ComingSoon,
        DisplayNumber = "07"
    };
}

// === KATEGORİ B — AKTÜERYA ===

public sealed class DesteKtenYoksunKalmaPlaceholder : PlaceholderCalculator
{
    public override CalculatorMetadata Metadata { get; } = new()
    {
        Slug = "destekten-yoksun-kalma",
        Category = CalculatorCategory.Akturya,
        Title = "Destekten Yoksun Kalma Tazminatı",
        ShortDescription = "TRH 2010 PMF tablosu, aktif/pasif dönem progresif rant, eş ve çocuk paylarıyla.",
        LegalReference = "TRH 2010 / Yargıtay içtihatları",
        Status = CalculatorStatus.ComingSoon,
        DisplayNumber = "08"
    };
}

public sealed class MaluliyetTazminatiPlaceholder : PlaceholderCalculator
{
    public override CalculatorMetadata Metadata { get; } = new()
    {
        Slug = "maluliyet-tazminati",
        Category = CalculatorCategory.Akturya,
        Title = "Maluliyet Tazminatı",
        ShortDescription = "Maluliyet oranı × gelir × kalan aktif süre; mesleki ve genel maluliyet ayrımı.",
        LegalReference = "2918 s.K. / Borçlar Kanunu",
        Status = CalculatorStatus.ComingSoon,
        DisplayNumber = "09"
    };
}

public sealed class GeciciIsGoremezlikPlaceholder : PlaceholderCalculator
{
    public override CalculatorMetadata Metadata { get; } = new()
    {
        Slug = "gecici-is-goremezlik",
        Category = CalculatorCategory.Akturya,
        Title = "Geçici İş Göremezlik Tazminatı",
        ShortDescription = "Tedavi süresi × günlük net gelir, SGK ödemesi mahsubu.",
        LegalReference = "5510 s.K.",
        Status = CalculatorStatus.ComingSoon,
        DisplayNumber = "10"
    };
}

public sealed class BakiciGideriPlaceholder : PlaceholderCalculator
{
    public override CalculatorMetadata Metadata { get; } = new()
    {
        Slug = "bakici-gideri",
        Category = CalculatorCategory.Akturya,
        Title = "Bakıcı Gideri Tazminatı",
        ShortDescription = "Günlük bakıcı ücreti × bakım süresi, asgari ücret referanslı taban.",
        LegalReference = "Yargıtay içtihatları",
        Status = CalculatorStatus.ComingSoon,
        DisplayNumber = "11"
    };
}

public sealed class AracDegerKaybiPlaceholder : PlaceholderCalculator
{
    public override CalculatorMetadata Metadata { get; } = new()
    {
        Slug = "arac-deger-kaybi",
        Category = CalculatorCategory.Akturya,
        Title = "Araç Değer Kaybı",
        ShortDescription = "Hasar oranı × piyasa değeri, tamir öncesi/sonrası değer farkı.",
        LegalReference = "2918 s.K.",
        Status = CalculatorStatus.ComingSoon,
        DisplayNumber = "12"
    };
}

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
