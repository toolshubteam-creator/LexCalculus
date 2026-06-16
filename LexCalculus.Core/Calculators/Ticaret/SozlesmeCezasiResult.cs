using LexCalculus.Core.Models.Calculators;

namespace LexCalculus.Core.Calculators.Ticaret;

/// <summary>Cezanın asıl borca oranına göre fahiş değerlendirme bandı (m.182 referans).</summary>
public enum FahisDegerlendirmesi
{
    /// <summary>Asıl borcun üstünde değil (oran ≤ 1.0).</summary>
    Standart = 1,

    /// <summary>Asıl borcu aşıyor (1.0 &lt; oran ≤ 2.0) — dikkat edici.</summary>
    DikkatEdici = 2,

    /// <summary>Asıl borcun 2 katından fazla — fahiş olabilir (m.182 hâkim takdiri).</summary>
    Fahis = 3
}

/// <summary>
/// H3 Sözleşme Cezası sonucu. Hesaplanan ceza, asıl borca oranı ve fahiş
/// değerlendirmesi raporlanır; sonuç her durumda "hâkim m.182 takdir
/// kullanabilir" uyarısı içerir.
/// </summary>
public sealed class SozlesmeCezasiResult : CalculationResult
{
    public decimal HesaplananCeza { get; set; }
    public decimal AsilBorc { get; set; }
    public decimal AsilBorcKati { get; set; }
    public FahisDegerlendirmesi FahisDegerlendirmesi { get; set; }
}
