using System.Globalization;
using LexCalculus.Core.Calculators.Common;
using LexCalculus.Core.Interfaces;
using LexCalculus.Core.Models.Calculators;

namespace LexCalculus.Core.Calculators.IsHukuku;

/// <summary>
/// Unused annual leave pay calculator (Yıllık İzin Ücreti).
///
/// Legal basis: 4857 s.K. m.53.
///
/// Annual leave entitlement (per full working year):
///   1 - 5 yrs:    14 days
///   5 - 15 yrs:   20 days
///   15+ yrs:      26 days
///
/// Special rules (m.53/4):
///   - Workers under 18 OR over 50 years old: minimum 20 days regardless of tenure
///   - Less than 1 year of service: NO leave entitlement
///   - Partial years beyond first year: round DOWN (m.53 — only full years count)
///
/// Tax treatment (matches ihbar — DIFFERENT from kıdem):
///   - Stamp duty: yes (binde 7,59)
///   - Income tax: yes (Phase 2: flat 15%)
///
/// Statute of limitations (m.59): 5 years from termination.
/// </summary>
public sealed class YillikIzinCalculator : ICalculator<YillikIzinInput, YillikIzinResult>
{
    private const string Slug = "yillik-izin-ucreti";
    private const string ParamGelirVergisiOrani = "gelir-vergisi-orani-basit";
    private const string ParamDamgaVergisiOrani = "damga-vergisi-orani";

    private readonly IFormulaParameterService _params;

    public YillikIzinCalculator(IFormulaParameterService parameters)
    {
        _params = parameters ?? throw new ArgumentNullException(nameof(parameters));
    }

    public CalculatorMetadata Metadata { get; } = new()
    {
        Slug = Slug,
        Category = CalculatorCategory.IsHukuku,
        Title = "Yıllık İzin Ücreti",
        ShortDescription = "Kullanılmayan izin günlerinin günlük brüt ücret ile çarpımı, yaş özel hükmü ve 5 yıllık zamanaşımı uyarısı.",
        LegalReference = "4857 s.K. m.53",
        Status = CalculatorStatus.Active,
        DisplayNumber = "03",
        Keywords = new[] { "yıllık izin", "izin ücreti", "iş hukuku", "kullanılmayan izin" }
    };

    public async Task<YillikIzinResult> CalculateAsync(YillikIzinInput input, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        var result = new YillikIzinResult();

        if (input.GirisTarihi is null)
            result.ValidationErrors[nameof(input.GirisTarihi)] = "Giriş tarihi boş olamaz.";
        if (input.CikisTarihi is null)
            result.ValidationErrors[nameof(input.CikisTarihi)] = "Çıkış tarihi boş olamaz.";
        if (input.BrutAylikUcret is null or <= 0)
            result.ValidationErrors[nameof(input.BrutAylikUcret)] = "Brüt ücret pozitif olmalıdır.";

        if (input.GirisTarihi is not null && input.CikisTarihi is not null
            && input.CikisTarihi <= input.GirisTarihi)
        {
            result.ValidationErrors[nameof(input.CikisTarihi)] = "Çıkış tarihi giriş tarihinden sonra olmalıdır.";
        }

        if (input.GirisTarihi is not null && input.CikisTarihi is not null)
        {
            var sureDays = (input.CikisTarihi.Value - input.GirisTarihi.Value).TotalDays;
            if (sureDays < 365)
            {
                result.ValidationErrors[nameof(input.CikisTarihi)] = "Yıllık izin için en az 1 yıl çalışma şartı vardır (m.53).";
            }
        }

        if (result.ValidationErrors.Count > 0)
        {
            result.IsValid = false;
            return result;
        }

        var giris = input.GirisTarihi!.Value;
        var cikis = input.CikisTarihi!.Value;
        var brutAylik = input.BrutAylikUcret!.Value;

        var toplamGun = (int)Math.Floor((cikis - giris).TotalDays);
        var tamYil = toplamGun / 365;

        int yillikIzinGunHakki = tamYil switch
        {
            < 5 => 14,
            < 15 => 20,
            _ => 26
        };

        bool yasOzelHukum = false;
        if (input.DogumTarihi is not null)
        {
            var yasCikistaki = cikis.Year - input.DogumTarihi.Value.Year;
            if (cikis.DayOfYear < input.DogumTarihi.Value.DayOfYear) yasCikistaki--;

            if ((yasCikistaki < 18 || yasCikistaki > 50) && yillikIzinGunHakki < 20)
            {
                yillikIzinGunHakki = 20;
                yasOzelHukum = true;
            }
        }

        var toplamHakEdilen = yillikIzinGunHakki * tamYil;
        var kullanilmayan = Math.Max(0, toplamHakEdilen - input.KullanilanIzinGunu);

        var gunlukUcret = brutAylik / 30m;
        var brutIzin = Math.Round(gunlukUcret * kullanilmayan, 2, MidpointRounding.AwayFromZero);

        var damgaOrani = await _params.GetValueAsync("*", ParamDamgaVergisiOrani, cikis, cancellationToken)
                      ?? 0.00759m;
        var damga = Math.Round(brutIzin * damgaOrani, 2, MidpointRounding.AwayFromZero);

        var gelirOrani = await _params.GetValueAsync(Slug, ParamGelirVergisiOrani, cikis, cancellationToken)
                      ?? await _params.GetValueAsync("ihbar-tazminati", ParamGelirVergisiOrani, cikis, cancellationToken)
                      ?? 0.15m;
        var gelir = Math.Round(brutIzin * gelirOrani, 2, MidpointRounding.AwayFromZero);

        var net = brutIzin - damga - gelir;

        result.ToplamGun = toplamGun;
        result.TamYil = tamYil;
        result.YillikIzinGunHakki = yillikIzinGunHakki;
        result.ToplamHakEdilenIzin = toplamHakEdilen;
        result.KullanilanIzin = input.KullanilanIzinGunu;
        result.KullanilmayanIzin = kullanilmayan;
        result.GunlukUcret = gunlukUcret;
        result.BrutIzinUcreti = brutIzin;
        result.DamgaVergisi = damga;
        result.GelirVergisi = gelir;
        result.NetIzinUcreti = net;
        result.YasOzelHukum = yasOzelHukum;

        var tr = new CultureInfo("tr-TR");
        result.TotalAmount = net;
        result.TotalLabel = "Net Yıllık İzin Ücreti";
        result.Unit = "TL";

        result.Rows.Add(new CalculationResultRow { Key = "Toplam Çalışma Süresi", Value = $"{tamYil} yıl ({toplamGun} gün)" });
        result.Rows.Add(new CalculationResultRow { Key = "Yıllık İzin Hakkı", Value = $"{yillikIzinGunHakki} gün/yıl" + (yasOzelHukum ? " (yaş özel hükmü)" : "") });
        result.Rows.Add(new CalculationResultRow { Key = "Toplam Hak Edilen İzin", Value = $"{toplamHakEdilen} gün" });
        result.Rows.Add(new CalculationResultRow { Key = "Kullanılan İzin", Value = $"{input.KullanilanIzinGunu} gün" });
        result.Rows.Add(new CalculationResultRow { Key = "Kullanılmayan (Ödenecek) İzin", Value = $"{kullanilmayan} gün", IsHighlighted = true });
        result.Rows.Add(new CalculationResultRow { Key = "Günlük Brüt Ücret", Value = gunlukUcret.ToString("N2", tr) + " TL" });
        result.Rows.Add(new CalculationResultRow { Key = "Brüt İzin Ücreti", Value = brutIzin.ToString("N2", tr) + " TL" });
        result.Rows.Add(new CalculationResultRow { Key = "Damga Vergisi", Value = damga.ToString("N2", tr) + " TL" });
        result.Rows.Add(new CalculationResultRow { Key = $"Gelir Vergisi (%{gelirOrani * 100:0.##})", Value = gelir.ToString("N2", tr) + " TL" });

        if (kullanilmayan == 0)
        {
            result.Warnings.Add("Kullanılan izin hak edilenden fazla veya eşit; ödenecek izin ücreti yoktur.");
        }
        if (yasOzelHukum)
        {
            result.Warnings.Add("18 yaş altı veya 50 yaş üstü işçiler için yıllık izin minimum 20 gündür (m.53/4).");
        }

        result.Note = "<strong>Zamanaşımı:</strong> Yıllık izin ücreti alacağı, iş sözleşmesinin sona erdiği tarihten itibaren <em>5 yıl</em> içinde talep edilmelidir (m.59). " +
                      "<strong>Vergi:</strong> Yıllık izin ücreti gelir vergisine tabidir (Phase 2: %15 sabit oran varsayılmıştır). " +
                      "<strong>Mevzuat:</strong> 4857 s.K. m.53.";

        return result;
    }
}
