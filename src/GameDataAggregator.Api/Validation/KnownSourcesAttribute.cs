using System.ComponentModel.DataAnnotations;
using GameDataAggregator.Api.Contracts;
using GameDataAggregator.Core.Aggregation;

namespace GameDataAggregator.Api.Validation;

/// <summary>
/// Validates a comma-separated list of source names against the providers registered at run time.
/// As a property attribute it runs together with every other field check, so a client sees all of its
/// mistakes in one 400 response instead of fixing them one round trip at a time.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class KnownSourcesAttribute : ValidationAttribute
{
    public override bool RequiresValidationContext => true;

    /// <summary>Accepts the list when every entry is a known source, otherwise lists the unknown ones.</summary>
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        //  Get the list of sources from the request's services.
        var aggregation = (IAggregationService?)validationContext.GetService(typeof(IAggregationService))
            ?? throw new InvalidOperationException($"{nameof(IAggregationService)} is not registered.");

        //  Find the entries that match no known source.
        var unknown = QueryList.Split(value as string)
            .Except(aggregation.SourceNames, StringComparer.OrdinalIgnoreCase)
            .ToList();

        //  Pass, or name the unknown entries alongside the valid ones.
        return unknown.Count == 0
            ? ValidationResult.Success
            : new ValidationResult(
                $"Unknown sources: {string.Join(", ", unknown)}. Valid values: {string.Join(", ", aggregation.SourceNames)}.",
                validationContext.MemberName is { } member ? [member] : []);
    }
}
