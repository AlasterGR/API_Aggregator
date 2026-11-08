using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace GameDataAggregator.Api.ModelBinding;

/// <summary>Uses <see cref="UtcDateTimeOffsetModelBinder"/> for <see cref="DateTimeOffset"/> and its nullable form.</summary>
public sealed class UtcDateTimeOffsetModelBinderProvider : IModelBinderProvider
{
    /// <summary>Returns the UTC date reader for date values, and nothing for anything else.</summary>
    public IModelBinder? GetBinder(ModelBinderProviderContext context)
    {
        //  Refuse to run without the parameter details.
        ArgumentNullException.ThrowIfNull(context);

        //  Handle the value only when it is a date, optional or not.
        var type = Nullable.GetUnderlyingType(context.Metadata.ModelType) ?? context.Metadata.ModelType;
        return type == typeof(DateTimeOffset) ? new UtcDateTimeOffsetModelBinder() : null;
    }
}
