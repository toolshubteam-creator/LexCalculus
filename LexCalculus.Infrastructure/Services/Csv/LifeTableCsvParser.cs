using System.Globalization;
using System.Text;
using LexCalculus.Core.Entities.Calculators;
using LexCalculus.Core.Enums;
using LexCalculus.Core.Services.Csv;

namespace LexCalculus.Infrastructure.Services.Csv;

public sealed class LifeTableCsvParser : ILifeTableCsvParser
{
    private const string ExpectedHeader = "Yas,Cinsiyet,BekledigiYasam";
    private const int ExpectedRowCount = 200;

    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public async Task<CsvParseResult> ParseAsync(Stream csvStream, CancellationToken ct = default)
    {
        var errors = new List<CsvParseError>();
        var rows = new List<LifeTableRow>();
        var seenKeys = new HashSet<(int yas, Cinsiyet cinsiyet)>();

        using var reader = new StreamReader(csvStream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);

        // 1. Header
        var header = (await reader.ReadLineAsync(ct))?.Trim();
        if (header == null)
        {
            errors.Add(new CsvParseError(1, "header", "Dosya boş."));
            return CsvParseResult.Failed(errors);
        }

        header = header.TrimStart('﻿');

        if (!string.Equals(header, ExpectedHeader, StringComparison.OrdinalIgnoreCase))
        {
            errors.Add(new CsvParseError(1, "header",
                $"İlk satır '{ExpectedHeader}' olmalı. Bulunan: '{header}'."));
            return CsvParseResult.Failed(errors);
        }

        // 2. Veri satırları
        // CA2024: async akışta senkron EndOfStream yerine ReadLineAsync döngüsü.
        // Davranış aynı — ReadLineAsync EOF'ta null, boş satırda "" döner.
        var lineNumber = 1;
        string? rawLine;
        while ((rawLine = await reader.ReadLineAsync(ct)) != null)
        {
            lineNumber++;
            var line = rawLine.Trim();

            if (string.IsNullOrWhiteSpace(line))
                continue;

            var parts = line.Split(',');
            if (parts.Length != 3)
            {
                errors.Add(new CsvParseError(lineNumber, "format",
                    $"3 sütun bekleniyor (Yas,Cinsiyet,BekledigiYasam), {parts.Length} bulundu."));
                continue;
            }

            // Yas
            if (!int.TryParse(parts[0].Trim(), NumberStyles.Integer, Inv, out var yas))
            {
                errors.Add(new CsvParseError(lineNumber, "Yas",
                    $"Yas tam sayı olmalı, bulunan: '{parts[0]}'."));
                continue;
            }
            if (yas < 0 || yas > 99)
            {
                errors.Add(new CsvParseError(lineNumber, "Yas",
                    $"Yas 0-99 aralığında olmalı, bulunan: {yas}."));
                continue;
            }

            // Cinsiyet (esnek: Erkek/Kadın/Kadin/case-insensitive)
            var cinsiyetRaw = parts[1].Trim();
            var cinsiyetNormalized = cinsiyetRaw
                .Replace("ı", "i", StringComparison.Ordinal)
                .Replace("İ", "I", StringComparison.Ordinal);

            Cinsiyet cinsiyet;
            if (cinsiyetNormalized.Equals("Erkek", StringComparison.OrdinalIgnoreCase))
                cinsiyet = Cinsiyet.Erkek;
            else if (cinsiyetNormalized.Equals("Kadin", StringComparison.OrdinalIgnoreCase))
                cinsiyet = Cinsiyet.Kadin;
            else
            {
                errors.Add(new CsvParseError(lineNumber, "Cinsiyet",
                    $"Cinsiyet 'Erkek' veya 'Kadın' olmalı, bulunan: '{cinsiyetRaw}'."));
                continue;
            }

            // BekledigiYasam (decimal, hem nokta hem virgül kabul)
            var yasamRaw = parts[2].Trim().Replace(',', '.');
            if (!decimal.TryParse(yasamRaw, NumberStyles.Float, Inv, out var yasam))
            {
                errors.Add(new CsvParseError(lineNumber, "BekledigiYasam",
                    $"Sayısal değer olmalı, bulunan: '{parts[2]}'."));
                continue;
            }
            if (yasam <= 0)
            {
                errors.Add(new CsvParseError(lineNumber, "BekledigiYasam",
                    $"Pozitif olmalı, bulunan: {yasam}."));
                continue;
            }

            // Duplicate kontrol
            var key = (yas, cinsiyet);
            if (!seenKeys.Add(key))
            {
                errors.Add(new CsvParseError(lineNumber, "Yas+Cinsiyet",
                    $"Aynı (Yas={yas}, Cinsiyet={cinsiyet}) zaten önceki satırda var."));
                continue;
            }

            rows.Add(new LifeTableRow
            {
                Yas = yas,
                Cinsiyet = cinsiyet,
                BekledigiYasam = yasam
            });
        }

        // 3. Toplam satır kontrolü
        if (rows.Count != ExpectedRowCount)
        {
            errors.Add(new CsvParseError(0, "total",
                $"Tam {ExpectedRowCount} satır bekleniyor (100 yaş × 2 cinsiyet), {rows.Count} bulundu."));
        }

        // 4. Her yaş için iki cinsiyet de var mı?
        if (rows.Count == ExpectedRowCount)
        {
            var grouped = rows.GroupBy(r => r.Yas).ToDictionary(g => g.Key, g => g.Count());
            for (int y = 0; y <= 99; y++)
            {
                if (!grouped.TryGetValue(y, out var count))
                {
                    errors.Add(new CsvParseError(0, $"Yas={y}", $"Yas {y} için satır eksik."));
                }
                else if (count != 2)
                {
                    errors.Add(new CsvParseError(0, $"Yas={y}",
                        $"Yas {y} için 2 satır bekleniyor (Erkek + Kadin), {count} bulundu."));
                }
            }
        }

        return errors.Count == 0
            ? CsvParseResult.Successful(rows)
            : CsvParseResult.Failed(errors);
    }
}
