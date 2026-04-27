using LexCalculus.Core.Interfaces;

namespace LexCalculus.Infrastructure.Calculators;

public sealed class ActuarialService : IActuarialService
{
    public int HesaplaYas(DateTime dogumTarihi, DateTime referansTarihi)
    {
        if (referansTarihi < dogumTarihi)
            throw new ArgumentException("Referans tarihi doğum tarihinden önce olamaz.", nameof(referansTarihi));

        var yas = referansTarihi.Year - dogumTarihi.Year;
        if (referansTarihi.Month < dogumTarihi.Month
            || (referansTarihi.Month == dogumTarihi.Month && referansTarihi.Day < dogumTarihi.Day))
        {
            yas--;
        }
        return Math.Max(0, yas);
    }

    public decimal AnnuityPresentValue(decimal yillikTutar, int yilSayisi, decimal yillikIskontoOrani)
    {
        if (yilSayisi <= 0) return 0m;
        if (yillikIskontoOrani == 0m) return yillikTutar * yilSayisi;
        if (yillikIskontoOrani < 0m)
            throw new ArgumentOutOfRangeException(nameof(yillikIskontoOrani), "İskonto oranı negatif olamaz.");

        var r = (double)yillikIskontoOrani;
        var n = yilSayisi;
        var faktor = (1d - Math.Pow(1d + r, -n)) / r;
        return Math.Round(yillikTutar * (decimal)faktor, 2, MidpointRounding.AwayFromZero);
    }

    public int AktifDonemYili(int simdikiYas, int emeklilikYasi = 65)
    {
        return Math.Max(0, emeklilikYasi - simdikiYas);
    }

    public int PasifDonemYili(int simdikiYas, decimal bekledigiYasamSuresi, int emeklilikYasi = 65)
    {
        var olumYasi = simdikiYas + (int)Math.Floor((double)bekledigiYasamSuresi);
        var pasifBaslangic = Math.Max(simdikiYas, emeklilikYasi);
        return Math.Max(0, olumYasi - pasifBaslangic);
    }
}
