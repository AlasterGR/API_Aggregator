using System.ComponentModel.DataAnnotations;
using GameDataAggregator.Api.Contracts;

namespace GameDataAggregator.Api.Validation;

/// <summary>
/// Validates a comma-separated list of <typeparamref name="TEnum"/> names (case-insensitive).
/// Numbers are rejected: "1" is not a meaningful category to a client and would silently change meaning if the
/// enum were reordered.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class EnumNameListAttribute<TEnum> : ValidationAttribute
    where TEnum : struct, Enum
{
    public override bool RequiresValidationContext => true;

    /// <summary>Accepts the list when every entry is a known name, otherwise lists the unknown ones.</summary>
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        //  Find the entries that match no known name.
        var names = Enum.GetNames<TEnum>();
        var unknown = QueryList.Split(value as string)
            .Where(item => !names.Contains(item, StringComparer.OrdinalIgnoreCase))
            .ToList();

        //  Pass, or name the unknown entries alongside the valid ones.
        return unknown.Count == 0
            ? ValidationResult.Success
            : new ValidationResult(
                $"Unknown {validationContext.DisplayName.ToLowerInvariant()}: {string.Join(", ", unknown)}. " +
                $"Valid values: {string.Join(", ", names)}.",
                MemberNames(validationContext));
    }

    /// <summary>Names the field the error belongs to, when known.</summary>
    private static string[] MemberNames(ValidationContext context) =>
        context.MemberName is { } member ? [member] : [];
}
