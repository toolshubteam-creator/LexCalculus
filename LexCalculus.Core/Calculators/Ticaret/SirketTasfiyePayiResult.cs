using LexCalculus.Core.Models.Calculators;

namespace LexCalculus.Core.Calculators.Ticaret;

public sealed class ImtiyazliPayDagitimSatiri
{
    public required string OrtakAdi { get; init; }
    public required int PayAdedi { get; init; }
    public required decimal ImtiyazOraniYuzde { get; init; }
    public required decimal AlacagiTutar { get; init; }
}

/// <summary>
/// H1 Tasfiye Payı sonucu. Net varlık, imtiyazlı blok dağılımı ve standart
/// ortak başına düşen pay ayrı raporlanır.
/// </summary>
public sealed class SirketTasfiyePayiResult : CalculationResult
{
    public decimal NetTasfiyeVarliği { get; set; }
    public IReadOnlyList<ImtiyazliPayDagitimSatiri> ImtiyazliPayDagilimi { get; set; }
        = Array.Empty<ImtiyazliPayDagitimSatiri>();
    public decimal ImtiyazliBlokToplam { get; set; }
    public decimal StandartPayKisiBasi { get; set; }
    public decimal ToplamPayDagitimi { get; set; }
}
