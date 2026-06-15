namespace LexCalculus.Core.Services;

/// <summary>
/// Türkiye resmi tatil seed + ceza/infaz takvim aritmetiği. Singleton — sabit
/// liste, thread-safe okuma.
///
/// Resmi tatil seed (2020-2030):
///   Sabit miladi tarihler — 2429 s.K.:
///     1 Ocak (Yılbaşı), 23 Nisan, 1 Mayıs, 19 Mayıs, 15 Temmuz, 30 Ağustos,
///     29 Ekim (Cumhuriyet Bayramı, tam gün; 28 Ekim öğleden sonra hesap dışı).
///   Kameri dini bayramlar (Diyanet İşleri Başkanlığı yayınlarına göre, sabit
///   liste — bkz. tech-debt #48 otomatik kameri-miladi dönüşüm için).
///     Ramazan Bayramı: 3 gün, Kurban Bayramı: 4 gün.
/// </summary>
public sealed class CriminalCalendarService : ICriminalCalendarService
{
    private static readonly IReadOnlyDictionary<int, ResmiTatil[]> Tatiller = BuildTatiller();
    private static readonly HashSet<DateOnly> TatilGunSeti = new(
        Tatiller.Values.SelectMany(yil => yil.Select(t => t.Tarih)));

    public DateOnly GunEkle(DateOnly baslangic, int gun, GunHesabiTuru tip = GunHesabiTuru.TumGunler)
    {
        if (gun == 0) return baslangic;
        if (tip == GunHesabiTuru.TumGunler) return baslangic.AddDays(gun);

        var step = gun > 0 ? 1 : -1;
        var hedef = Math.Abs(gun);
        var tarih = baslangic;
        var sayilan = 0;

        while (sayilan < hedef)
        {
            tarih = tarih.AddDays(step);
            if (Sayilir(tarih, tip)) sayilan++;
        }

        return tarih;
    }

    public int GunFarki(DateOnly baslangic, DateOnly bitis, GunHesabiTuru tip = GunHesabiTuru.TumGunler)
    {
        if (tip == GunHesabiTuru.TumGunler) return bitis.DayNumber - baslangic.DayNumber;

        var step = bitis >= baslangic ? 1 : -1;
        var tarih = baslangic;
        var sayilan = 0;

        while (tarih != bitis)
        {
            tarih = tarih.AddDays(step);
            if (Sayilir(tarih, tip)) sayilan += step;
        }

        return sayilan;
    }

    public int InfazSuresi(int mahkumiyetGun, decimal oran)
    {
        if (mahkumiyetGun <= 0) return 0;
        var infaz = (decimal)mahkumiyetGun * oran;
        return (int)Math.Round(infaz, 0, MidpointRounding.AwayFromZero);
    }

    public DateOnly TahliyeTarihi(DateOnly cezaevineGiris, int mahkumiyetGun, decimal oran, int tutuklulukGun = 0)
    {
        var netInfaz = InfazSuresi(mahkumiyetGun, oran) - Math.Max(0, tutuklulukGun);
        if (netInfaz < 0) netInfaz = 0;
        return cezaevineGiris.AddDays(netInfaz);
    }

    public bool ResmiTatilMi(DateOnly tarih) => TatilGunSeti.Contains(tarih);

    public IReadOnlyList<ResmiTatil> YilinTatilleri(int yil) =>
        Tatiller.TryGetValue(yil, out var liste) ? liste : Array.Empty<ResmiTatil>();

    private static bool Sayilir(DateOnly tarih, GunHesabiTuru tip)
    {
        var haftaSonu = tarih.DayOfWeek == DayOfWeek.Saturday || tarih.DayOfWeek == DayOfWeek.Sunday;
        if (tip == GunHesabiTuru.HafataIciGunler) return !haftaSonu;
        return !haftaSonu && !TatilGunSeti.Contains(tarih);
    }

    // ----- Resmi tatil seed (2020-2030) -----
    // Diyanet yayını + 2429 s.K. (sabit miladi) kombinasyonu. Kameri tarihler
    // 1-2 günlük rüyet farkıyla kaymış olabilir; bilimsel tarih sınırı içinde
    // standart Diyanet referans tarihleri kullanıldı.
    private static IReadOnlyDictionary<int, ResmiTatil[]> BuildTatiller()
    {
        var dict = new Dictionary<int, ResmiTatil[]>();
        for (var yil = 2020; yil <= 2030; yil++)
            dict[yil] = SabitMiladi(yil).Concat(DiniBayram(yil)).OrderBy(t => t.Tarih).ToArray();
        return dict;
    }

    private static IEnumerable<ResmiTatil> SabitMiladi(int yil) => new[]
    {
        new ResmiTatil { Tarih = new DateOnly(yil, 1, 1), Adi = "Yılbaşı" },
        new ResmiTatil { Tarih = new DateOnly(yil, 4, 23), Adi = "Ulusal Egemenlik ve Çocuk Bayramı" },
        new ResmiTatil { Tarih = new DateOnly(yil, 5, 1), Adi = "Emek ve Dayanışma Günü" },
        new ResmiTatil { Tarih = new DateOnly(yil, 5, 19), Adi = "Atatürk'ü Anma, Gençlik ve Spor Bayramı" },
        new ResmiTatil { Tarih = new DateOnly(yil, 7, 15), Adi = "Demokrasi ve Milli Birlik Günü" },
        new ResmiTatil { Tarih = new DateOnly(yil, 8, 30), Adi = "Zafer Bayramı" },
        new ResmiTatil { Tarih = new DateOnly(yil, 10, 29), Adi = "Cumhuriyet Bayramı" }
    };

    // Diyanet İşleri Başkanlığı kameri takvim — sabit liste 2020-2030.
    // Ramazan Bayramı 3 gün, Kurban Bayramı 4 gün; arefe yarım gün (özel
    // sektör) bu hesapta dışarıda.
    private static IEnumerable<ResmiTatil> DiniBayram(int yil) => yil switch
    {
        2020 => new[]
        {
            R(2020, 5, 24), R(2020, 5, 25), R(2020, 5, 26),
            K(2020, 7, 31), K(2020, 8, 1),  K(2020, 8, 2),  K(2020, 8, 3)
        },
        2021 => new[]
        {
            R(2021, 5, 13), R(2021, 5, 14), R(2021, 5, 15),
            K(2021, 7, 20), K(2021, 7, 21), K(2021, 7, 22), K(2021, 7, 23)
        },
        2022 => new[]
        {
            R(2022, 5, 2),  R(2022, 5, 3),  R(2022, 5, 4),
            K(2022, 7, 9),  K(2022, 7, 10), K(2022, 7, 11), K(2022, 7, 12)
        },
        2023 => new[]
        {
            R(2023, 4, 21), R(2023, 4, 22), R(2023, 4, 23),
            K(2023, 6, 28), K(2023, 6, 29), K(2023, 6, 30), K(2023, 7, 1)
        },
        2024 => new[]
        {
            R(2024, 4, 10), R(2024, 4, 11), R(2024, 4, 12),
            K(2024, 6, 16), K(2024, 6, 17), K(2024, 6, 18), K(2024, 6, 19)
        },
        2025 => new[]
        {
            R(2025, 3, 30), R(2025, 3, 31), R(2025, 4, 1),
            K(2025, 6, 6),  K(2025, 6, 7),  K(2025, 6, 8),  K(2025, 6, 9)
        },
        2026 => new[]
        {
            R(2026, 3, 20), R(2026, 3, 21), R(2026, 3, 22),
            K(2026, 5, 27), K(2026, 5, 28), K(2026, 5, 29), K(2026, 5, 30)
        },
        2027 => new[]
        {
            R(2027, 3, 9),  R(2027, 3, 10), R(2027, 3, 11),
            K(2027, 5, 16), K(2027, 5, 17), K(2027, 5, 18), K(2027, 5, 19)
        },
        2028 => new[]
        {
            R(2028, 2, 26), R(2028, 2, 27), R(2028, 2, 28),
            K(2028, 5, 5),  K(2028, 5, 6),  K(2028, 5, 7),  K(2028, 5, 8)
        },
        2029 => new[]
        {
            R(2029, 2, 14), R(2029, 2, 15), R(2029, 2, 16),
            K(2029, 4, 24), K(2029, 4, 25), K(2029, 4, 26), K(2029, 4, 27)
        },
        2030 => new[]
        {
            R(2030, 2, 4),  R(2030, 2, 5),  R(2030, 2, 6),
            K(2030, 4, 13), K(2030, 4, 14), K(2030, 4, 15), K(2030, 4, 16)
        },
        _ => Array.Empty<ResmiTatil>()
    };

    private static ResmiTatil R(int y, int m, int d) =>
        new() { Tarih = new DateOnly(y, m, d), Adi = "Ramazan Bayramı", DiniBayramMi = true };

    private static ResmiTatil K(int y, int m, int d) =>
        new() { Tarih = new DateOnly(y, m, d), Adi = "Kurban Bayramı", DiniBayramMi = true };
}
