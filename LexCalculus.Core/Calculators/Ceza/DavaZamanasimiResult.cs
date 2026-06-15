using LexCalculus.Core.Models.Calculators;

namespace LexCalculus.Core.Calculators.Ceza;

/// <summary>
/// F3 Dava Zamanaşımı sonucu. Asli süre (m.66), mutlak süre (m.67/4 = asli ×
/// 1.5), son kesintiden hesaplanan asli bitiş ile sabit mutlak bitiş ayrı ayrı
/// raporlanır; mutlak sınır asli'yi aşarsa cap uygulanır.
/// </summary>
public sealed class DavaZamanasimiResult : CalculationResult
{
    public int AsliZamanasimiSuresiYil { get; set; }
    public decimal MutlakZamanasimiSuresiYil { get; set; }

    /// <summary>Asli zamanaşımının başlangıç noktası — kesinti yoksa suç işleme tarihi.</summary>
    public DateOnly SonBaslangicTarihi { get; set; }

    public DateOnly AsliZamanasimiBitis { get; set; }
    public DateOnly MutlakZamanasimiBitis { get; set; }

    /// <summary>m.67/4 cap uygulandı mı (asli > mutlak'a vardığı için kesildi).</summary>
    public bool MutlakSinirUygulandi { get; set; }

    /// <summary>AsOfDate'e göre dava zamanaşımına uğradı mı.</summary>
    public bool ZamanasimineUgradiMi { get; set; }

    /// <summary>Kalan gün (eksi = geçmiş). Etkin bitiş ile AsOfDate farkı.</summary>
    public int KalanGun { get; set; }
}
