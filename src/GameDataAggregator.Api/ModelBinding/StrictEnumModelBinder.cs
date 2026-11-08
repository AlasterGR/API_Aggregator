using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace GameDataAggregator.Api.ModelBinding;

/// <summary>
/// Binds enum query parameters by name only (case-insensitive). The default binder also accepts numbers
/// ("sortBy=1" meant Date) and comma-combined names, which clients should not depend on.
/// </summary>
public sealed class StrictEnumModelBinder(Type enumType) : IModelBinder
{
    /// <summary>Turns the text from the address into a choice, or records why it is not a valid one.</summary>
    public Task BindModelAsync(ModelBindingContext bindingContext)
    {
        //  Refuse to run without the request details it works on.
        ArgumentNullException.ThrowIfNull(bindingContext);

        //  Leave the default choice when nothing was sent.
        var input = bindingContext.ValueProvider.GetValue(bindingContext.ModelName);
        if (input == ValueProviderResult.None || string.IsNullOrWhiteSpace(input.FirstValue))
        {
            return Task.CompletedTask; // not supplied: the property keeps its default
        }

        //  Keep the original text so an error can show what was sent.
        bindingContext.ModelState.SetModelValue(bindingContext.ModelName, input);

        //  Look for a choice with that exact name, ignoring letter case.
        var text = input.FirstValue.Trim();
        var name = Enum.GetNames(enumType).FirstOrDefault(n => n.Equals(text, StringComparison.OrdinalIgnoreCase));

        //  Report an unknown name along with the valid ones.
        if (name is null)
        {
            bindingContext.ModelState.TryAddModelError(
                bindingContext.ModelName,
                $"The value '{text}' is not valid. Valid values: {string.Join(", ", Enum.GetNames(enumType))}.");
            bindingContext.Result = ModelBindingResult.Failed();
            return Task.CompletedTask;
        }

        //  Accept the matching choice.
        bindingContext.Result = ModelBindingResult.Success(Enum.Parse(enumType, name));
        return Task.CompletedTask;
    }
}
