using LexCalculus.Core.Enums;

namespace LexCalculus.Core.Common;

/// <summary>
/// MeslekTuru enum üyelerinin Türkçe etiketleri için merkezi yardımcı.
/// /profil, /uye/{slug}, /baglantilarim sayfalarında aynı format kullanılır.
/// Faz 4.2 P3a/3 — UyeModel + BaglantilarimModel duplikasyonunu kaldırır.
/// </summary>
public static class MeslekTuruExtensions
{
    public static string? ToTurkish(this MeslekTuru? tur, string? digerOverride = null)
        => tur is null ? null : tur.Value.ToTurkish(digerOverride);

    public static string ToTurkish(this MeslekTuru tur, string? digerOverride = null)
    {
        if (tur == MeslekTuru.Diger && !string.IsNullOrWhiteSpace(digerOverride))
            return digerOverride;

        return tur switch
        {
            MeslekTuru.Avukat => "Avukat",
            MeslekTuru.Hakim => "Hâkim",
            MeslekTuru.Savci => "Savcı",
            MeslekTuru.Bilirkisi => "Bilirkişi",
            MeslekTuru.MaliMusavir => "Mali Müşavir",
            MeslekTuru.Diger => "Diğer",
            _ => tur.ToString()
        };
    }
}
