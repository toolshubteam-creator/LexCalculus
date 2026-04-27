using LexCalculus.Core.Enums;
using LexCalculus.Core.Models.Calculators;

namespace LexCalculus.Core.Calculators.Akturya;

public sealed class DesteKtenYoksunKalmaResult : CalculationResult
{
    public int OlenYas { get; set; }
    public decimal OlenBekledigiYasam { get; set; }
    public int AktifYil { get; set; }
    public int PasifYil { get; set; }

    public decimal EsPay { get; set; }
    public decimal EsPayTutarAktif { get; set; }
    public decimal EsPayTutarPasif { get; set; }
    public decimal EsToplamTutar { get; set; }

    public int CocukSayisi { get; set; }
    public decimal CocukPayPerCocuk { get; set; }
    public List<CocukDetay> CocukDetaylar { get; set; } = new();
    public decimal CocuklarToplamTutar { get; set; }

    public decimal ToplamTazminat { get; set; }
}

public sealed class CocukDetay
{
    public required Cinsiyet Cinsiyet { get; init; }
    public required int OlayAnindakiYas { get; init; }
    public required int BagimsizlikYasi { get; init; }
    public required int DestekSuresi { get; init; }
    public required decimal Tutar { get; init; }
    public required string Aciklama { get; init; }
}
