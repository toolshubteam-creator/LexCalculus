using System.Globalization;
using LexCalculus.Core.Calculators.Common;
using LexCalculus.Core.Interfaces;
using LexCalculus.Core.Models.Calculators;

namespace LexCalculus.Core.Calculators.Gayrimenkul;

/// <summary>
/// Ecrimisil (unjust occupation compensation) calculator.
///
/// Legal basis: TMK m.995 + Yargıtay 1. HD yerleşik içtihadı (E. 2013/16267
/// K. 2014/4059). The expert determines the first-period rayiç kira; for each
/// subsequent year the rent is escalated by that year's ÜFE increase, and the
/// compensation "cannot be less than" the full ÜFE-escalated amount.
///
/// IMPORTANT: Yargıtay uses ÜFE (üretici fiyat endeksi), NOT TÜFE. The ÜFE
/// rates are stored as global FormulaParameters ("*", "ufe.yillik") so other
/// D-I tools can reuse them.
///
/// This is a simplified, principle-level estimate — it does not replace a court
/// expert report (see the UI warning).
/// </summary>
public sealed class EcrimisilCalculator : ICalculator<EcrimisilInput, EcrimisilResult>
{
    private const string UfeSlug = "*";
    private const string UfeKey = "ufe.yillik";

    private readonly IFormulaParameterService _params;

    public EcrimisilCalculator(IFormulaParameterService parameters)
    {
        _params = parameters ?? throw new ArgumentNullException(nameof(parameters));
    }

    public CalculatorMetadata Metadata { get; } = new()
    {
        Slug = "ecrimisil",
        Category = CalculatorCategory.Gayrimenkul,
        Title = "Ecrimisil Hesaplama",
        ShortDescription = "TMK m.995 + Yargıtay 1. HD içtihadı — ilk dönem rayiç kira üzerinden ÜFE birikimli artışıyla haksız işgal tazminatı (ecrimisil).",
        LegalReference = "TMK m.995",
        Status = CalculatorStatus.Active,
        DisplayNumber = "20",
        Keywords = new[] { "ecrimisil", "haksız işgal", "TMK 995", "rayiç kira", "ÜFE" }
    };

    public async Task<EcrimisilResult> CalculateAsync(EcrimisilInput input, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        var result = new EcrimisilResult();

        if (input.IsgalBaslangic is null)
            result.ValidationErrors[nameof(input.IsgalBaslangic)] = "İşgal başlangıç tarihi boş olamaz.";
        if (input.IsgalBitis is null)
            result.ValidationErrors[nameof(input.IsgalBitis)] = "İşgal bitiş tarihi boş olamaz.";
        if (input.IlkDonemRayicKira is null or <= 0)
            result.ValidationErrors[nameof(input.IlkDonemRayicKira)] = "İlk dönem rayiç kira pozitif olmalıdır.";

        if (input.IsgalBaslangic is not null && input.IsgalBitis is not null
            && input.IsgalBitis <= input.IsgalBaslangic)
        {
            result.ValidationErrors[nameof(input.IsgalBitis)] = "İşgal bitiş tarihi başlangıç tarihinden sonra olmalıdır.";
        }

        if (result.ValidationErrors.Count > 0)
        {
            result.IsValid = false;
            return result;
        }

        var baslangic = input.IsgalBaslangic!.Value;
        var bitis = input.IsgalBitis!.Value;
        var tr = new CultureInfo("tr-TR");

        // İlk dönem aylık rayiç kira (yıllık girildiyse 12'ye böl).
        var ilkAylikKira = input.DonemTuru == EcrimisilDonemTuru.Yillik
            ? input.IlkDonemRayicKira!.Value / 12m
            : input.IlkDonemRayicKira!.Value;

        // Her yıl için güncel aylık kirayı ÜFE ile birikimli hesapla.
        var baslangicYil = baslangic.Year;
        var bitisYil = bitis.Year;

        var kiraByYil = new Dictionary<int, decimal>();
        var ufeByYil = new Dictionary<int, decimal>();
        kiraByYil[baslangicYil] = ilkAylikKira;
        ufeByYil[baslangicYil] = 0m; // başlangıç yılı baz; artış yok
        for (var y = baslangicYil + 1; y <= bitisYil; y++)
        {
            // Exact-year match: GetValueAsync'in "<= tarih" fallback'i, eksik bir
            // yıl için önceki yılın ÜFE'sini döndürürdü (stale). O yüzden satırın
            // EffectiveDate yılı tam o yıla eşit değilse "eksik" sayıp uyarırız.
            var param = await _params.GetParameterAsync(UfeSlug, UfeKey, new DateTime(y, 12, 31), cancellationToken);
            if (param is null || param.EffectiveDate.Year != y)
            {
                ufeByYil[y] = 0m;
                kiraByYil[y] = kiraByYil[y - 1];
                result.Warnings.Add($"{y} yılı için ÜFE oranı parametresi bulunamadı; bu yıl için artış uygulanmadı. Yönetici '{UfeSlug}/{UfeKey}' ({y}) değerini eklemelidir.");
            }
            else
            {
                ufeByYil[y] = param.Value;
                kiraByYil[y] = kiraByYil[y - 1] * (1m + param.Value / 100m);
            }
        }

        // İşgal süresini ay ay dolaş, yıllara grupla.
        var donemler = new List<EcrimisilDonem>();
        var cursor = new DateTime(baslangic.Year, baslangic.Month, 1);
        var son = new DateTime(bitis.Year, bitis.Month, 1);
        var currentYil = cursor.Year;
        var ayCount = 0;
        DateTime segStart = cursor;

        void Flush(int yil, DateTime start, int aySayisi)
        {
            if (aySayisi <= 0) return;
            var aylik = Math.Round(kiraByYil[yil], 2, MidpointRounding.AwayFromZero);
            var bedel = Math.Round(aylik * aySayisi, 2, MidpointRounding.AwayFromZero);
            donemler.Add(new EcrimisilDonem
            {
                Yil = yil,
                BaslangicTarih = start,
                BitisTarih = start.AddMonths(aySayisi).AddDays(-1),
                AySayisi = aySayisi,
                UfeOrani = ufeByYil[yil],
                GuncelAylikKira = aylik,
                DonemBedeli = bedel
            });
        }

        while (cursor < son)
        {
            if (cursor.Year != currentYil)
            {
                Flush(currentYil, segStart, ayCount);
                currentYil = cursor.Year;
                segStart = cursor;
                ayCount = 0;
            }
            ayCount++;
            cursor = cursor.AddMonths(1);
        }
        Flush(currentYil, segStart, ayCount);

        var toplam = donemler.Sum(d => d.DonemBedeli);

        result.DonemListesi = donemler;
        result.ToplamEcrimisil = toplam;

        result.TotalAmount = toplam;
        result.TotalLabel = "Toplam Ecrimisil";
        result.Unit = "TL";

        foreach (var d in donemler)
        {
            var ufeText = d.UfeOrani > 0 ? $", ÜFE %{d.UfeOrani.ToString("0.##", tr)}" : "";
            result.Rows.Add(new CalculationResultRow
            {
                Key = $"{d.Yil} ({d.AySayisi} ay, aylık {d.GuncelAylikKira.ToString("N2", tr)} TL{ufeText})",
                Value = d.DonemBedeli.ToString("N2", tr) + " TL"
            });
        }

        result.Note = "<strong>Yöntem:</strong> İlk dönem rayiç kira, sonraki her yıl için yıllık ÜFE (üretici fiyat endeksi) artış oranıyla birikimli güncellenir. " +
                      "<strong>Mevzuat:</strong> TMK m.995 + Yargıtay 1. HD yerleşik içtihadı (E. 2013/16267 K. 2014/4059) — ecrimisil, ÜFE artış oranının tamamı yansıtılarak bulunan miktardan az olamaz. " +
                      "Yargıtay TÜFE değil <strong>ÜFE</strong> kullanır. " +
                      "<strong>Önemli:</strong> Bu hesaplama prensip seviyesinde bir ön değerlendirmedir; somut davada mahkeme/bilirkişi raporu rayiç kira, konum ve çevresel faktörleri ayrıca değerlendirir. Bu sonuç bilirkişi incelemesi yerine geçmez.";

        return result;
    }
}
