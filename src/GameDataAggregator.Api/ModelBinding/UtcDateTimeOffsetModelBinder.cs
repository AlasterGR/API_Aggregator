using System.Globalization;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace GameDataAggregator.Api.ModelBinding;

/// <summary>
/// Binds <see cref="DateTimeOffset"/> query parameters, treating a value without an offset ("2015-05-18") as UTC.
/// The default binder uses the server's time zone.
/// </summary>
public sealed class UtcDateTimeOffsetModelBinder : IModelBinder
{
    /// <summary>Turns the date text from the address into a date, or records why it is not one.</summary>
    public Task BindModelAsync(ModelBindingContext bindingContext)
    {
        //  Refuse to run without the request details it works on.
        ArgumentNullException.ThrowIfNull(bindingContext);

        //  Leave the date unset when it was not sent or is blank.
        var input = bindingContext.ValueProvider.GetValue(bindingContext.ModelName);
        if (input == ValueProviderResult.None || string.IsNullOrWhiteSpace(input.FirstValue))
        {
            return Task.CompletedTask;
        }

        //  Keep the original text so an error can show what was sent.
        bindingContext.ModelState.SetModelValue(bindingContext.ModelName, input);

        //  Accept the text as a date in UTC when it reads as one, otherwise report it as invalid.
        if (DateTimeOffset.TryParse(input.FirstValue, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var value))
        {
            bindingContext.Result = ModelBindingResult.Success(value);
        }
        else
        {
            bindingContext.ModelState.TryAddModelError(
                bindingContext.ModelName,
                $"The value '{input.FirstValue}' is not a valid date. Use ISO 8601, e.g. 2015-05-18 or 2015-05-18T10:00:00Z.");
            bindingContext.Result = ModelBindingResult.Failed();
        }

        return Task.CompletedTask;
    }
}
