using System.Globalization;

namespace LexCalculus.Core.Services;

/// <summary>
/// Statutory inheritance distribution per the TMK zümre (parentela) system.
///
///   1. zümre (altsoy)        — TMK m.495/498: çocuklar eşit; ölmüş çocuğun payı
///                              torunlarına (halefiyet).
///   2. zümre (ana-baba)      — TMK m.500/501: ana-baba ½/½; ölmüş ana/babanın
///                              payı altsoyuna (murisin kardeşleri → yeğenler);
///                              ölmüş ana/babanın altsoyu yoksa payı sağ kalan
///                              ana/babaya.
///   3. zümre (büyük ana-baba) — TMK m.501: dede/nine eşit (bu Faz'da altsoya
///                              halefiyet yok — basitleştirme).
///
/// Eş payı (TMK m.499): 1. zümre ile ¼, 2. zümre ile ½, 3. zümre ile ¾, zümre
/// mirasçısı yoksa tamamı.
///
/// Pure computation, no I/O — registered Scoped but stateless.
/// </summary>
public sealed class InheritanceDistributionService : IInheritanceDistributionService
{
    public MirasPayDagilimi Dagit(MirasciYapisi yapi, decimal? toplamMalvarligi = null)
    {
        ArgumentNullException.ThrowIfNull(yapi);

        var paylar = new List<MirasciPay>();

        var birinciDerece = yapi.SagCocukSayisi > 0 || yapi.OlmusCocuklar.Any(c => c.TorunSayisi > 0);
        var ikinciDerece = !birinciDerece &&
            (yapi.AnaSag || yapi.BabaSag || yapi.KardesSayisi > 0 || yapi.OlmusKardesler.Any(k => k.YeginSayisi > 0));
        var ucuncuDerece = !birinciDerece && !ikinciDerece && yapi.DedeNineSayisi > 0;

        var aktifDerece = birinciDerece ? 1 : ikinciDerece ? 2 : ucuncuDerece ? 3 : 0;

        // Eş payı (TMK m.499)
        var esPay = yapi.SagKalanEsVar
            ? aktifDerece switch { 1 => 0.25m, 2 => 0.50m, 3 => 0.75m, _ => 1.0m }
            : 0m;

        if (aktifDerece == 0)
        {
            if (yapi.SagKalanEsVar)
            {
                paylar.Add(Pay("Sağ kalan eş", "es", 1.0m, toplamMalvarligi));
                return new MirasPayDagilimi
                {
                    Paylar = paylar,
                    AktifDerece = 0,
                    Aciklama = "Zümre mirasçısı bulunmadığından tüm miras sağ kalan eşe kalır (TMK m.499/son)."
                };
            }
            return new MirasPayDagilimi
            {
                Paylar = paylar,
                AktifDerece = 0,
                Aciklama = "Yasal mirasçı bulunmuyor; miras Devlete (Hazineye) kalır (TMK m.501/son)."
            };
        }

        if (yapi.SagKalanEsVar)
            paylar.Add(Pay("Sağ kalan eş", "es", esPay, toplamMalvarligi));

        var zumrePool = 1m - esPay;

        switch (aktifDerece)
        {
            case 1: DagitAltsoy(yapi, zumrePool, toplamMalvarligi, paylar); break;
            case 2: DagitAnaBaba(yapi, zumrePool, toplamMalvarligi, paylar); break;
            default: DagitDedeNine(yapi, zumrePool, toplamMalvarligi, paylar); break;
        }

        return new MirasPayDagilimi
        {
            Paylar = paylar,
            AktifDerece = aktifDerece,
            Aciklama = DereceAciklama(aktifDerece, yapi.SagKalanEsVar, esPay)
        };
    }

    // ----- 1. zümre: altsoy (TMK m.495/498) -----
    private static void DagitAltsoy(MirasciYapisi yapi, decimal pool, decimal? mv, List<MirasciPay> paylar)
    {
        var olmusKokler = yapi.OlmusCocuklar.Where(c => c.TorunSayisi > 0).ToList();
        var kokSayisi = yapi.SagCocukSayisi + olmusKokler.Count;
        var perKok = pool / kokSayisi;

        for (var i = 0; i < yapi.SagCocukSayisi; i++)
            paylar.Add(Pay($"Çocuk {i + 1}", "cocuk", perKok, mv));

        foreach (var olmus in olmusKokler)
        {
            var perTorun = perKok / olmus.TorunSayisi;
            for (var j = 0; j < olmus.TorunSayisi; j++)
                paylar.Add(Pay($"Torun ({olmus.Tanim}) {j + 1}", "torun", perTorun, mv));
        }
    }

    // ----- 2. zümre: ana-baba ve altsoyu (TMK m.500/501) -----
    private static void DagitAnaBaba(MirasciYapisi yapi, decimal pool, decimal? mv, List<MirasciPay> paylar)
    {
        var kardesKokler = yapi.OlmusKardesler.Where(k => k.YeginSayisi > 0).ToList();
        var kardesSlot = yapi.KardesSayisi + kardesKokler.Count;
        var yarim = pool / 2m;
        var kardesPool = 0m;

        if (yapi.AnaSag && yapi.BabaSag)
        {
            paylar.Add(Pay("Ana", "ana", yarim, mv));
            paylar.Add(Pay("Baba", "baba", yarim, mv));
        }
        else if (yapi.AnaSag) // baba ölmüş
        {
            if (kardesSlot > 0) { paylar.Add(Pay("Ana", "ana", yarim, mv)); kardesPool = yarim; }
            else paylar.Add(Pay("Ana", "ana", pool, mv)); // babanın altsoyu yok → ana'ya
        }
        else if (yapi.BabaSag) // ana ölmüş
        {
            if (kardesSlot > 0) { paylar.Add(Pay("Baba", "baba", yarim, mv)); kardesPool = yarim; }
            else paylar.Add(Pay("Baba", "baba", pool, mv));
        }
        else // ana-baba ölmüş → tüm pay kardeşlere (kardesSlot > 0, çünkü 2. derece aktif)
        {
            kardesPool = pool;
        }

        if (kardesPool > 0m && kardesSlot > 0)
        {
            var perKardes = kardesPool / kardesSlot;
            for (var i = 0; i < yapi.KardesSayisi; i++)
                paylar.Add(Pay($"Kardeş {i + 1}", "kardes", perKardes, mv));

            foreach (var olmus in kardesKokler)
            {
                var perYegen = perKardes / olmus.YeginSayisi;
                for (var j = 0; j < olmus.YeginSayisi; j++)
                    paylar.Add(Pay($"Yeğen ({olmus.Tanim}) {j + 1}", "yegen", perYegen, mv));
            }
        }
    }

    // ----- 3. zümre: büyük ana-baba (TMK m.501) -----
    private static void DagitDedeNine(MirasciYapisi yapi, decimal pool, decimal? mv, List<MirasciPay> paylar)
    {
        var perDede = pool / yapi.DedeNineSayisi;
        for (var i = 0; i < yapi.DedeNineSayisi; i++)
            paylar.Add(Pay($"Büyük ana-baba {i + 1}", "dede-nine", perDede, mv));
    }

    public decimal SakliPayOrani(string mirasciTuru, int aktifDerece = 0) => mirasciTuru switch
    {
        "cocuk" or "torun" => 0.5m,                                  // altsoy — yasal payın yarısı
        "ana" or "baba" => 0.25m,                                    // yasal payın dörtte biri
        "es" => (aktifDerece == 1 || aktifDerece == 2) ? 1.0m : 0.75m, // 1./2. zümre ile tamamı, diğer hâller ¾
        _ => 0m                                                      // kardeş, yeğen, dede-nine saklı paysız
    };

    private static MirasciPay Pay(string tanim, string tur, decimal kesri, decimal? mv) => new()
    {
        Tanim = tanim,
        MirasciTuru = tur,
        PayKesri = kesri,
        PayTutari = mv.HasValue ? Math.Round(kesri * mv.Value, 2, MidpointRounding.AwayFromZero) : null
    };

    private static string DereceAciklama(int derece, bool esVar, decimal esPay)
    {
        var tr = CultureInfo.GetCultureInfo("tr-TR");
        var esKisim = esVar ? $"Sağ kalan eş %{(esPay * 100m).ToString("0.##", tr)} pay alır; kalan " : "";
        return derece switch
        {
            1 => $"{esKisim}1. zümreye (altsoy) dağıtıldı — çocuklar eşit, ölmüş çocuğun payı torunlarına geçer (TMK m.495/498/499).",
            2 => $"{esKisim}2. zümreye (ana-baba ve altsoyu) dağıtıldı (TMK m.500/501/499).",
            _ => $"{esKisim}3. zümreye (büyük ana-baba) dağıtıldı (TMK m.501/499)."
        };
    }
}
