using LexCalculus.Core.Models.Calculators;

namespace LexCalculus.Core.Calculators.Faiz;

public sealed class AkdiTemerrutFaizResult : CalculationResult
{
    public decimal AnaPara { get; set; }
    public int ToplamGun { get; set; }
    public string FaizYontemiAciklama { get; set; } = string.Empty;

    public decimal AkdiFaizTutari { get; set; }
    public decimal AkdiToplamTutar { get; set; }
    public List<FaizDonemDetay> AkdiDonemDetaylar { get; set; } = new();

    public decimal Yasal3095FaizTutari { get; set; }
    public decimal Yasal3095ToplamTutar { get; set; }
    public List<FaizDonemDetay> Yasal3095DonemDetaylar { get; set; } = new();

    public string UygulanacakSecim { get; set; } = string.Empty;
    public decimal UygulanacakTutar { get; set; }
    public bool UcMaddesi120AltSiniri { get; set; }

    public decimal OrtalamaYillikOran { get; set; }
}
