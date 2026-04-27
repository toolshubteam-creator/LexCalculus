using LexCalculus.Core.Models.Calculators;

namespace LexCalculus.Core.Calculators.Faiz;

public sealed class MenfiTespitFaizResult : CalculationResult
{
    public decimal HaksizTahsilTutari { get; set; }
    public DateTime FaizBaslangicTarihi { get; set; }
    public string FaizBaslangicAciklama { get; set; } = string.Empty;
    public int ToplamGun { get; set; }

    public decimal FaizTutari { get; set; }
    public decimal IadeEdilecekToplamTutar { get; set; }

    public List<FaizDonemDetay> DonemDetaylar { get; set; } = new();
}
