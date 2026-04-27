using System.ComponentModel.DataAnnotations;

namespace LexCalculus.Core.Calculators.IsHukuku;

public sealed class MobbingInput
{
    [Display(Name = "Brüt Aylık Ücret (TL)")]
    [Required(ErrorMessage = "Brüt ücret boş olamaz.")]
    [Range(typeof(decimal), "0.01", "999999999",
        ErrorMessage = "Brüt ücret pozitif olmalıdır.",
        ParseLimitsInInvariantCulture = true)]
    public decimal? BrutAylikUcret { get; set; }

    [Display(Name = "Mobbing Süresi (ay)")]
    [Required(ErrorMessage = "Süre boş olamaz.")]
    [Range(1, 600, ErrorMessage = "Süre 1-600 ay arası olmalı.")]
    public int? SureAy { get; set; }

    [Display(Name = "Mobbing Şiddeti")]
    [Required]
    public MobbingSiddeti Siddet { get; set; } = MobbingSiddeti.Orta;

    [Display(Name = "Sağlık Raporu / Tıbbi Belge Var Mı")]
    public bool SaglikRaporu { get; set; }

    [Display(Name = "Mobbing Sebebiyle İstifa Var Mı")]
    public bool IstifaSebebi { get; set; }

    [Display(Name = "İşveren Konumu")]
    [Required]
    public IsverenKonumu IsverenTipi { get; set; } = IsverenKonumu.OzelSektor;
}

public enum MobbingSiddeti
{
    [Display(Name = "Hafif (verbal taciz, izolasyon girişimleri)")]
    Hafif = 1,

    [Display(Name = "Orta (sistemli aşağılama, görev kısıtlama, küçük düşürme)")]
    Orta = 2,

    [Display(Name = "Ağır (kamuya açık aşağılama, terfi engelleme, yıldırma)")]
    Agir = 3,

    [Display(Name = "Çok Ağır (taciz, fiziksel/psikolojik zarar, uzun süreli)")]
    CokAgir = 4
}

public enum IsverenKonumu
{
    [Display(Name = "Özel Sektör")]
    OzelSektor = 1,

    [Display(Name = "Kamu Kurumu")]
    Kamu = 2,

    [Display(Name = "Büyük Holding / Çok Uluslu Şirket")]
    BuyukHolding = 3
}
