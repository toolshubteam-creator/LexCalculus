namespace LexCalculus.Core.Services;

/// <summary>
/// Gün hesabı modu — ileri/geri günleme + iki tarih arası gün farkı için.
///   TumGunler         — her gün dahil (takvim günü).
///   HafataIciGunler   — hafta sonu (cumartesi, pazar) hariç.
///   IsGunleri         — hafta sonu + resmi tatil hariç.
/// </summary>
public enum GunHesabiTuru
{
    TumGunler = 1,
    HafataIciGunler = 2,
    IsGunleri = 3
}

/// <summary>
/// Türkiye'de bir resmi tatili tanımlar (2429 s.K. resmi tatil + Diyanet
/// dini bayram). Kameri takvime bağlı dini bayramlar 2020-2030 için sabit
/// listede tutulur (bkz. tech-debt #48).
/// </summary>
public sealed class ResmiTatil
{
    public required DateOnly Tarih { get; init; }
    public required string Adi { get; init; }
    public bool DiniBayramMi { get; init; }
}

/// <summary>
/// Ceza hukuku ve infaz hesapları için ortak takvim servisi (Faz 7 Charter
/// Karar 2). F1 Erteleme, F2 Koşullu Salıverilme, F3 Zamanaşımı ve F5
/// Tutukluluk Mahsup tarafından paylaşılır.
///
/// Saf hesap — DB bağımlılığı yok, Singleton (sabit liste, thread-safe okuma).
///
/// Hukuki dayanak (tatil seed): 2429 sayılı Ulusal Bayram ve Genel Tatiller
/// Hakkında Kanun + Diyanet İşleri Başkanlığı kameri takvim verileri.
/// </summary>
public interface ICriminalCalendarService
{
    /// <summary>Başlangıç tarihine gün ekleyerek yeni tarih döner.</summary>
    DateOnly GunEkle(DateOnly baslangic, int gun, GunHesabiTuru tip = GunHesabiTuru.TumGunler);

    /// <summary>İki tarih arası gün sayısı (bitis - baslangic, mod dahil).</summary>
    int GunFarki(DateOnly baslangic, DateOnly bitis, GunHesabiTuru tip = GunHesabiTuru.TumGunler);

    /// <summary>
    /// İnfaz süresi (gün): mahkumiyet süresi × oran, yarım gün AwayFromZero
    /// yuvarlama. Negatif veya sıfır mahkumiyet için 0 döner.
    /// </summary>
    int InfazSuresi(int mahkumiyetGun, decimal oran);

    /// <summary>
    /// Şartlı/koşullu tahliye tarihi: cezaevineGiris + (mahkumiyetGun × oran)
    /// - tutuklulukGun (mahsup). Tutukluluk mahsubu net infaz süresinden
    /// düşülür (5275 s.K. m.107/9).
    /// </summary>
    DateOnly TahliyeTarihi(DateOnly cezaevineGiris, int mahkumiyetGun, decimal oran, int tutuklulukGun = 0);

    /// <summary>Belirli bir tarih Türkiye resmi tatili mi (seed listesine göre).</summary>
    bool ResmiTatilMi(DateOnly tarih);

    /// <summary>Bir yıldaki resmi tatiller (sorgu için, tarih sırasında).</summary>
    IReadOnlyList<ResmiTatil> YilinTatilleri(int yil);
}
