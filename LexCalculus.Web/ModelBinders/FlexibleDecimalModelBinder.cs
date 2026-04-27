using System.Globalization;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace LexCalculus.Web.ModelBinders;

/// <summary>
/// Decimal model binder that accepts both invariant (3.5) and Turkish (3,5)
/// decimal formats, plus mixed Turkish (1.234,56) and US (1,234.56) thousand
/// separators when both separators are present.
///
/// Why this exists: HTML5 input type="number" sends decimals as "3.0" in
/// some browsers/contexts and "3,0" in others depending on locale. The
/// default ASP.NET binder only respects current request culture, causing
/// "must be a number" validation failures.
///
/// Strategy:
///   - Mixed separators (both . and ,): the LAST separator is decimal,
///     the other is thousands; remove thousands then parse invariant.
///   - Comma only: treat as decimal, convert to dot, parse invariant.
///   - Dot only or no separator: parse invariant directly.
///   - AllowThousands is NOT used in TryParse — too easily abused
///     (e.g. "3..5" would otherwise parse as 35 in Turkish culture).
/// </summary>
public sealed class FlexibleDecimalModelBinder : IModelBinder
{
    private const NumberStyles AllowedStyles =
        NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint;

    public Task BindModelAsync(ModelBindingContext bindingContext)
    {
        ArgumentNullException.ThrowIfNull(bindingContext);

        var modelName = bindingContext.ModelName;
        var valueProviderResult = bindingContext.ValueProvider.GetValue(modelName);

        if (valueProviderResult == ValueProviderResult.None)
        {
            return Task.CompletedTask;
        }

        bindingContext.ModelState.SetModelValue(modelName, valueProviderResult);

        var raw = valueProviderResult.FirstValue;

        if (string.IsNullOrWhiteSpace(raw))
        {
            if (bindingContext.ModelType == typeof(decimal?))
            {
                bindingContext.Result = ModelBindingResult.Success(null);
            }
            return Task.CompletedTask;
        }

        var trimmed = raw.Trim();
        var normalized = NormalizeForInvariantParsing(trimmed);

        if (decimal.TryParse(normalized, AllowedStyles, CultureInfo.InvariantCulture, out var parsed))
        {
            bindingContext.Result = ModelBindingResult.Success(parsed);
            return Task.CompletedTask;
        }

        bindingContext.ModelState.TryAddModelError(
            modelName,
            $"'{raw}' geçerli bir sayı değildir. Sayıyı 3.5 veya 3,5 formatında girebilirsiniz.");

        return Task.CompletedTask;
    }

    private static string NormalizeForInvariantParsing(string s)
    {
        var hasDot = s.Contains('.');
        var hasComma = s.Contains(',');

        if (hasDot && hasComma)
        {
            var lastDot = s.LastIndexOf('.');
            var lastComma = s.LastIndexOf(',');

            if (lastComma > lastDot)
            {
                // Turkish: dot=thousands, comma=decimal — "1.234,56" → "1234.56"
                return s.Replace(".", string.Empty).Replace(',', '.');
            }
            // US: comma=thousands, dot=decimal — "1,234.56" → "1234.56"
            return s.Replace(",", string.Empty);
        }

        if (hasComma)
        {
            return s.Replace(',', '.');
        }

        return s;
    }
}
