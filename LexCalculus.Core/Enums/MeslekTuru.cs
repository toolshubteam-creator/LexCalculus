namespace LexCalculus.Core.Enums;

/// <summary>
/// Kullanıcı meslek türü. Profil property'si — yetki belirlemez,
/// sadece istatistik ve hedefli iletişim için.
/// Diger seçilirse ApplicationUser.MeslekTuruDiger string alanı doldurulur.
/// </summary>
public enum MeslekTuru
{
    Avukat = 1,
    Hakim = 2,
    Savci = 3,
    Bilirkisi = 4,
    MaliMusavir = 5,
    Diger = 99
}
