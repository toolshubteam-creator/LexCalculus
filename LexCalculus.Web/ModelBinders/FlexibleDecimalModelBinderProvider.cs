using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace LexCalculus.Web.ModelBinders;

/// <summary>
/// Provider that registers FlexibleDecimalModelBinder for decimal and decimal? types.
/// </summary>
public sealed class FlexibleDecimalModelBinderProvider : IModelBinderProvider
{
    public IModelBinder? GetBinder(ModelBinderProviderContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Metadata.ModelType == typeof(decimal)
            || context.Metadata.ModelType == typeof(decimal?))
        {
            return new FlexibleDecimalModelBinder();
        }

        return null;
    }
}
