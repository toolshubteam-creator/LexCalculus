using System.Globalization;
using LexCalculus.Core.Calculators.Common;
using LexCalculus.Core.Interfaces;
using LexCalculus.Core.Models.Calculators;

namespace LexCalculus.Core.Calculators.IsHukuku;

/// <summary>
/// Reinstatement compensation calculator (İşe İade Tazminatı).
///
/// Legal basis: 4857 s.K. m.18-21.
///
/// Job security (iş güvencesi) requires ALL of:
///   - Workplace with 30+ employees
///   - Worker with 6+ months tenure
///   - Indefinite-term contract
///   - Termination not based on workplace/business/job requirements
///
/// If court rules termination invalid, employer may reinstate OR pay:
///   - Reinstatement compensation: 4-8 months' brut wage (court discretion)
///   - Boş süre ücreti (idle time pay): wage for the period between
///     termination and judgment, capped at 4 months
///
/// Tax: stamp duty + income tax (Phase 2 flat 15%).
///
/// Statute of limitations: 1 month from termination notice (m.20).
/// </summary>
public sealed class IseIadeCalculator : ICalculator<IseIadeInput, IseIadeResult>
{
    private const string Slug = "ise-iade-tazminati";
    private const int BostaSureMaxAy = 4;
    private const int IsGuvencesiMinKidemAyi = 6;
    private const int IsGuvencesiMinCalisan = 30;

    private readonly IFormulaParameterService _params;

    public IseIadeCalculator(IFormulaParameterService parameters)
    {
        _params = parameters ?? throw new ArgumentNullException(nameof(parameters));
    }

    public CalculatorMetadata Metadata { get; } = new()
    {
        Slug = Slug,
        Category = CalculatorCategory.IsHukuku,
        Title = "İşe İade Tazminatı",
        ShortDescription = "Feshin geçersizliği halinde 4-8 aylık brüt ücret tazminatı, boşta geçen süre ücreti (max 4 ay), iş güvencesi şartı kontrolü.",
        LegalReference = "4857 s.K. m.18-21",
        Status = CalculatorStatus.Active,
        DisplayNumber = "05",
        Keywords = new[] { "işe iade", "fesih", "iş güvencesi", "boşta geçen süre" }
    };

    public async Task<IseIadeResult> CalculateAsync(IseIadeInput input, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        var result = new IseIadeResult();

        if (input.GirisTarihi is null) result.ValidationErrors[nameof(input.GirisTarihi)] = "Giriş tarihi boş olamaz.";
        if (input.FesihTarihi is null) result.ValidationErrors[nameof(input.FesihTarihi)] = "Fesih tarihi boş olamaz.";
        if (input.KararTarihi is null) result.ValidationErrors[nameof(input.KararTarihi)] = "Karar tarihi boş olamaz.";
        if (input.BrutAylikUcret is null or <= 0)
            result.ValidationErrors[nameof(input.BrutAylikUcret)] = "Brüt ücret pozitif olmalıdır.";

        if (input.GirisTarihi is not null && input.FesihTarihi is not null
            && input.FesihTarihi <= input.GirisTarihi)
        {
            result.ValidationErrors[nameof(input.FesihTarihi)] = "Fesih tarihi giriş tarihinden sonra olmalıdır.";
        }
        if (input.FesihTarihi is not null && input.KararTarihi is not null
            && input.KararTarihi < input.FesihTarihi)
        {
            result.ValidationErrors[nameof(input.KararTarihi)] = "Karar tarihi fesih tarihinden sonra olmalıdır.";
        }

        if (input.IadeAyiSayisi is < 4 or > 8)
        {
            result.ValidationErrors[nameof(input.IadeAyiSayisi)] = "İade tazminatı 4-8 ay arasında olmalıdır.";
        }

        if (result.ValidationErrors.Count > 0)
        {
            result.IsValid = false;
            return result;
        }

        var giris = input.GirisTarihi!.Value;
        var fesih = input.FesihTarihi!.Value;
        var karar = input.KararTarihi!.Value;
        var brutAylik = input.BrutAylikUcret!.Value;

        var kidemAyi = ((fesih.Year - giris.Year) * 12) + (fesih.Month - giris.Month);
        if (fesih.Day < giris.Day) kidemAyi--;
        kidemAyi = Math.Max(0, kidemAyi);

        var isGuvencesinde = kidemAyi >= IsGuvencesiMinKidemAyi
                           && input.IsyeriCalisanSayisi >= IsGuvencesiMinCalisan;

        if (!isGuvencesinde)
        {
            if (kidemAyi < IsGuvencesiMinKidemAyi)
                result.Warnings.Add($"İş güvencesi için en az {IsGuvencesiMinKidemAyi} ay kıdem gerekir; bu işçinin kıdemi {kidemAyi} ay. Hesap bilgilendirme amaçlıdır.");
            if (input.IsyeriCalisanSayisi < IsGuvencesiMinCalisan)
                result.Warnings.Add($"İş güvencesi için işyerinde en az {IsGuvencesiMinCalisan} işçi çalıştırılması gerekir; girilen sayı {input.IsyeriCalisanSayisi}. Hesap bilgilendirme amaçlıdır.");
        }

        var bostaGecenAy = ((karar.Year - fesih.Year) * 12) + (karar.Month - fesih.Month);
        if (karar.Day < fesih.Day) bostaGecenAy--;
        bostaGecenAy = Math.Max(0, bostaGecenAy);

        var bostaSinirli = Math.Min(bostaGecenAy, BostaSureMaxAy);

        var iadeTazminati = brutAylik * input.IadeAyiSayisi;
        var bostaUcret = brutAylik * bostaSinirli;
        var brutToplam = iadeTazminati + bostaUcret;

        var damgaOrani = await _params.GetValueAsync("*", "damga-vergisi-orani", karar, cancellationToken)
                      ?? 0.00759m;
        var damga = Math.Round(brutToplam * damgaOrani, 2, MidpointRounding.AwayFromZero);

        var gelirOrani = await _params.GetValueAsync(Slug, "gelir-vergisi-orani-basit", karar, cancellationToken)
                      ?? await _params.GetValueAsync("ihbar-tazminati", "gelir-vergisi-orani-basit", karar, cancellationToken)
                      ?? 0.15m;
        var gelir = Math.Round(brutToplam * gelirOrani, 2, MidpointRounding.AwayFromZero);

        var net = brutToplam - damga - gelir;

        result.IsGuvencesindeMi = isGuvencesinde;
        result.KidemAyi = kidemAyi;
        result.BostaGecenAy = bostaGecenAy;
        result.BostaSinirliAy = bostaSinirli;
        result.IadeAyiSayisi = input.IadeAyiSayisi;
        result.IadeTazminati = iadeTazminati;
        result.BostaGecenSureUcreti = bostaUcret;
        result.BrutToplam = brutToplam;
        result.DamgaVergisi = damga;
        result.GelirVergisi = gelir;
        result.NetTutar = net;

        var tr = new CultureInfo("tr-TR");
        result.TotalAmount = net;
        result.TotalLabel = "Net Toplam Alacak";
        result.Unit = "TL";

        result.Rows.Add(new CalculationResultRow {
            Key = "İş Güvencesi Kapsamı",
            Value = isGuvencesinde ? "Kapsamda ✓" : "Kapsam dışı (uyarıya bakınız)"
        });
        result.Rows.Add(new CalculationResultRow { Key = "Kıdem", Value = $"{kidemAyi} ay" });
        result.Rows.Add(new CalculationResultRow { Key = "İade Tazminatı Süresi", Value = $"{input.IadeAyiSayisi} ay" });
        result.Rows.Add(new CalculationResultRow { Key = $"İade Tazminatı ({input.IadeAyiSayisi} ay × brüt)", Value = iadeTazminati.ToString("N2", tr) + " TL" });
        result.Rows.Add(new CalculationResultRow { Key = "Boşta Geçen Süre", Value = $"{bostaGecenAy} ay (sınır: {bostaSinirli} ay)" });
        result.Rows.Add(new CalculationResultRow { Key = $"Boşta Geçen Süre Ücreti ({bostaSinirli} ay × brüt)", Value = bostaUcret.ToString("N2", tr) + " TL" });
        result.Rows.Add(new CalculationResultRow { Key = "Brüt Toplam", Value = brutToplam.ToString("N2", tr) + " TL", IsHighlighted = true });
        result.Rows.Add(new CalculationResultRow { Key = "Damga Vergisi", Value = damga.ToString("N2", tr) + " TL" });
        result.Rows.Add(new CalculationResultRow { Key = $"Gelir Vergisi (%{gelirOrani * 100:0.##})", Value = gelir.ToString("N2", tr) + " TL" });

        result.Note = "<strong>İş Güvencesi Şartları:</strong> 30+ işçili işyeri, 6+ ay kıdem, belirsiz süreli sözleşme, işletme dışı sebep. " +
                      "<strong>İade Tazminatı:</strong> Mahkeme 4-8 ay arasında takdir eder; ortalama 4-5 ay. " +
                      "<strong>Boşta Geçen Süre:</strong> Maksimum 4 ay ücret ödenir (m.21/3). " +
                      "<strong>Süre:</strong> Fesih bildiriminden 1 ay içinde dava açılmalıdır (m.20).";

        return result;
    }
}
