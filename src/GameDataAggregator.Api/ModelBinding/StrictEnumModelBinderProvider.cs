using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace GameDataAggregator.Api.ModelBinding;

/// <summary>Uses <see cref="StrictEnumModelBinder"/> for every enum and nullable enum parameter.</summary>
public sealed class StrictEnumModelBinderProvider : IModelBinderProvider
{
    /// <summary>Returns the strict reader for enum values, and nothing for anything else.</summary>
    public IModelBinder? GetBinder(ModelBinderProviderContext context)
    {
        //  Refuse to run without the parameter details.
        ArgumentNullException.ThrowIfNull(context);

        //  Handle the value only when it is an enum, optional or not.
        var type = Nullable.GetUnderlyingType(context.Metadata.ModelType) ?? context.Metadata.ModelType;
        return type.IsEnum ? new StrictEnumModelBinder(type) : null;
    }
}
