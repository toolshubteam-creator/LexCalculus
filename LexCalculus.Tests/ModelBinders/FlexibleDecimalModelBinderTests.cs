using FluentAssertions;
using LexCalculus.Web.ModelBinders;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Xunit;

namespace LexCalculus.Tests.ModelBinders;

public class FlexibleDecimalModelBinderTests
{
    private readonly FlexibleDecimalModelBinder _binder = new();

    private async Task<(decimal? value, bool hasErrors)> Bind(string? raw, Type modelType)
    {
        var bindingContext = new DefaultModelBindingContext
        {
            ModelMetadata = new EmptyModelMetadataProvider().GetMetadataForType(modelType),
            ModelName = "field",
            ValueProvider = raw is null
                ? new SimpleValueProvider()
                : new SimpleValueProvider { ["field"] = raw },
            ModelState = new ModelStateDictionary()
        };

        await _binder.BindModelAsync(bindingContext);

        var hasErrors = bindingContext.ModelState["field"]?.Errors.Count > 0;
        var value = bindingContext.Result.IsModelSet ? (decimal?)bindingContext.Result.Model : null;
        return (value, hasErrors);
    }

    [Theory]
    [InlineData("3.5", 3.5)]
    [InlineData("3,5", 3.5)]
    [InlineData("0.0", 0.0)]
    [InlineData("0,0", 0.0)]
    [InlineData("100", 100)]
    [InlineData("66.67", 66.67)]
    [InlineData("66,67", 66.67)]
    [InlineData("1234.56", 1234.56)]
    [InlineData("1234,56", 1234.56)]
    public async Task Tek_Decimal_Format_Cesitleri_Hepsi_Parse_Olur(string input, double expected)
    {
        var (value, hasErrors) = await Bind(input, typeof(decimal));

        hasErrors.Should().BeFalse($"'{input}' parse edilebilmeli");
        value.Should().Be((decimal)expected);
    }

    [Theory]
    [InlineData("not a number")]
    [InlineData("abc")]
    [InlineData("3..5")]
    public async Task Gecersiz_Sayi_Hata_Verir(string input)
    {
        var (_, hasErrors) = await Bind(input, typeof(decimal));

        hasErrors.Should().BeTrue();
    }

    [Fact]
    public async Task Bos_Deger_Nullable_Null_Doner()
    {
        var (value, hasErrors) = await Bind("", typeof(decimal?));

        hasErrors.Should().BeFalse();
        value.Should().BeNull();
    }

    [Fact]
    public async Task Whitespace_Trim_Edilir()
    {
        var (value, hasErrors) = await Bind("  3.5  ", typeof(decimal));

        hasErrors.Should().BeFalse();
        value.Should().Be(3.5m);
    }

    private sealed class SimpleValueProvider : Dictionary<string, string>, IValueProvider
    {
        public bool ContainsPrefix(string prefix) => ContainsKey(prefix);

        public ValueProviderResult GetValue(string key)
        {
            return TryGetValue(key, out var v)
                ? new ValueProviderResult(new[] { v })
                : ValueProviderResult.None;
        }
    }
}
