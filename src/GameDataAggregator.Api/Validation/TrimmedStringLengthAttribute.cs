using System.ComponentModel.DataAnnotations;

namespace GameDataAggregator.Api.Validation;

/// <summary>
/// <see cref="StringLengthAttribute"/> measured after trimming. The value is trimmed before use, so
/// "  a  " must count as one character, not five.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
public sealed class TrimmedStringLengthAttribute(int maximumLength) : StringLengthAttribute(maximumLength)
{
    /// <summary>Checks the length of the text without its outer spaces.</summary>
    public override bool IsValid(object? value) => base.IsValid(value is string text ? text.Trim() : value);
}
