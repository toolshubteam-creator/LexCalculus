using LexCalculus.Core.Models.Calculators;

namespace LexCalculus.Core.Calculators.IsHukuku;

public sealed class MobbingResult : CalculationResult
{
    public int TabanAyKatsayisi { get; set; }
    public int UstAyKatsayisi { get; set; }
    public decimal AltSinirTutar { get; set; }
    public decimal UstSinirTutar { get; set; }
    public decimal OnerilenTutar { get; set; }
    public bool MahkemeyeUygunDelilVarMi { get; set; }
    public List<string> EmsalKarakteristik { get; set; } = new();
}
