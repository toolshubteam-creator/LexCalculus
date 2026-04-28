using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using LexCalculus.Web.Models.Hesaplarim;

namespace LexCalculus.Web.Infrastructure.Hesaplarim;

public static class JsonViewerHelper
{
    private static readonly Regex IsoDateRegex =
        new(@"^\d{4}-\d{2}-\d{2}(T\d{2}:\d{2}:\d{2}(\.\d+)?(Z|[+-]\d{2}:\d{2})?)?$",
            RegexOptions.Compiled);

    private static readonly CultureInfo TrCulture = new("tr-TR");

    public static IReadOnlyList<JsonFieldRow> ParseTopLevel(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return Array.Empty<JsonFieldRow>();

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                return Array.Empty<JsonFieldRow>();

            var rows = new List<JsonFieldRow>();
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                rows.Add(MapProperty(prop));
            }
            return rows;
        }
        catch (JsonException)
        {
            return Array.Empty<JsonFieldRow>();
        }
    }

    public static string PrettyPrint(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return string.Empty;
        try
        {
            using var doc = JsonDocument.Parse(json);
            return JsonSerializer.Serialize(doc.RootElement,
                new JsonSerializerOptions { WriteIndented = true });
        }
        catch (JsonException)
        {
            return json;
        }
    }

    private static JsonFieldRow MapProperty(JsonProperty prop)
    {
        var displayKey = CamelCaseToTitle(prop.Name);

        return prop.Value.ValueKind switch
        {
            JsonValueKind.Number => MapNumber(prop.Name, displayKey, prop.Value),
            JsonValueKind.String => MapString(prop.Name, displayKey, prop.Value),
            JsonValueKind.True => new JsonFieldRow { Key = prop.Name, DisplayKey = displayKey, DisplayValue = "Evet", Type = "bool" },
            JsonValueKind.False => new JsonFieldRow { Key = prop.Name, DisplayKey = displayKey, DisplayValue = "Hayır", Type = "bool" },
            JsonValueKind.Null => new JsonFieldRow { Key = prop.Name, DisplayKey = displayKey, DisplayValue = "—", Type = "string" },
            JsonValueKind.Object or JsonValueKind.Array => new JsonFieldRow
            {
                Key = prop.Name,
                DisplayKey = displayKey,
                DisplayValue = $"({prop.Value.ValueKind})",
                Type = "complex"
            },
            _ => new JsonFieldRow { Key = prop.Name, DisplayKey = displayKey, DisplayValue = prop.Value.ToString(), Type = "string" }
        };
    }

    private static JsonFieldRow MapNumber(string key, string displayKey, JsonElement value)
    {
        if (value.TryGetInt64(out var integer))
        {
            return new JsonFieldRow
            {
                Key = key,
                DisplayKey = displayKey,
                DisplayValue = integer.ToString("N0", TrCulture),
                Type = "integer"
            };
        }

        if (value.TryGetDecimal(out var dec))
        {
            return new JsonFieldRow
            {
                Key = key,
                DisplayKey = displayKey,
                DisplayValue = dec.ToString("N2", TrCulture),
                Type = "decimal"
            };
        }

        return new JsonFieldRow
        {
            Key = key,
            DisplayKey = displayKey,
            DisplayValue = value.ToString(),
            Type = "decimal"
        };
    }

    private static JsonFieldRow MapString(string key, string displayKey, JsonElement value)
    {
        var raw = value.GetString() ?? "";

        if (IsoDateRegex.IsMatch(raw)
            && DateTime.TryParse(raw, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var dt))
        {
            return new JsonFieldRow
            {
                Key = key,
                DisplayKey = displayKey,
                DisplayValue = dt.ToLocalTime().ToString("dd.MM.yyyy"),
                Type = "date"
            };
        }

        return new JsonFieldRow
        {
            Key = key,
            DisplayKey = displayKey,
            DisplayValue = raw,
            Type = "string"
        };
    }

    private static string CamelCaseToTitle(string camel)
    {
        if (string.IsNullOrEmpty(camel)) return camel;

        var sb = new System.Text.StringBuilder();
        sb.Append(char.ToUpperInvariant(camel[0]));

        for (var i = 1; i < camel.Length; i++)
        {
            if (char.IsUpper(camel[i]))
                sb.Append(' ');
            sb.Append(camel[i]);
        }

        return sb.ToString();
    }
}
