using LexCalculus.Core.Models.Calculators;

namespace LexCalculus.Core.Calculators.Faiz;

public sealed class KiraArtisiResult : CalculationResult
{
    public decimal MevcutKira { get; set; }
    public decimal YeniKira { get; set; }
    public decimal ArtisTutari { get; set; }
    public decimal UygulanacakOran { get; set; }

    public decimal? TUFEOrani { get; set; }
    public DateTime? KullanilanTUFEAyi { get; set; }
    public string TUFEKaynagi { get; set; } = string.Empty;

    public decimal? SozlesmeOrani { get; set; }
    public bool SozlesmeOraniUygulandi { get; set; }

    public bool YuzdeYirmiBesSiniriUygulandi { get; set; }
    public string UygulanacakOranAciklama { get; set; } = string.Empty;
}
