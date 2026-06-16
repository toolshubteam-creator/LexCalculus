using System.ComponentModel.DataAnnotations;

namespace LexCalculus.Core.Calculators.Ticaret;

/// <summary>Ceza şartı şekli (TBK m.179).</summary>
public enum CezaSekli
{
    [Display(Name = "Sabit Tutar")]
    SabitTutar = 1,

    [Display(Name = "Yüzde Oran (asıl borç üzerinden)")]
    YuzdeOran = 2
}

/// <summary>İhlal türü (TBK m.180 bilgilendirme — formüle etki etmez).</summary>
public enum IhlalTuru
{
    [Display(Name = "Temerrütten Dolayı (ifa + ceza)")]
    TemerruttenDolayi = 1,

    [Display(Name = "İfadan Geçiş (ceza yerine kabul)")]
    IfaDanGecisi = 2
}

/// <summary>
/// H3 Sözleşme Cezası (Ceza Şartı) — TBK m.179-182. Hesap, belirlenen cezayı
/// uygular ve asıl borca oranını "fahiş mi?" değerlendirmesi olarak raporlar
/// (m.182 hâkim takdir referansı için).
/// </summary>
public sealed class SozlesmeCezasiInput
{
    [Display(Name = "Asıl Borç (TL)")]
    [Required(ErrorMessage = "Asıl borç boş olamaz.")]
    [Range(typeof(decimal), "0.01", "999999999999",
        ErrorMessage = "Asıl borç pozitif olmalıdır.",
        ParseLimitsInInvariantCulture = true)]
    public decimal? AsilBorc { get; set; }

    [Display(Name = "Ceza Şekli")]
    public CezaSekli CezaSekli { get; set; } = CezaSekli.SabitTutar;

    [Display(Name = "Belirlenen Ceza (TL) — sabit tutar şekli")]
    [Range(typeof(decimal), "0", "999999999999",
        ErrorMessage = "Belirlenen ceza negatif olamaz.",
        ParseLimitsInInvariantCulture = true)]
    public decimal? BelirlenenCeza { get; set; }

    [Display(Name = "Belirlenen Oran (%, asıl borç üzerinden) — yüzde şekli")]
    [Range(typeof(decimal), "0", "10000",
        ErrorMessage = "Belirlenen oran 0-10000 arası olmalıdır.",
        ParseLimitsInInvariantCulture = true)]
    public decimal? BelirlenenOran { get; set; }

    [Display(Name = "İhlal Türü")]
    public IhlalTuru IhlalTuru { get; set; } = IhlalTuru.TemerruttenDolayi;
}
