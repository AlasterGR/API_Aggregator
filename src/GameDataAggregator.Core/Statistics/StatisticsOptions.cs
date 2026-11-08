using System.ComponentModel.DataAnnotations;

namespace GameDataAggregator.Core.Statistics;

/// <summary>Configuration section "Statistics". Defines the performance bucket boundaries.</summary>
public sealed class StatisticsOptions : IValidatableObject
{
    public const string SectionName = "Statistics";

    /// <summary>Requests faster than this (in ms) are "Fast".</summary>
    [Range(1, 60_000)]
    public int FastThresholdMs { get; set; } = 100;

    /// <summary>Requests slower than this (in ms) are "Slow". Anything in between is "Average".</summary>
    [Range(1, 60_000)]
    public int SlowThresholdMs { get; set; } = 200;

    /// <summary>Checks that the slow limit is above the fast limit.</summary>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        //  Reject settings where the slow limit is not above the fast one.
        if (SlowThresholdMs <= FastThresholdMs)
        {
            yield return new ValidationResult(
                $"{nameof(SlowThresholdMs)} must be greater than {nameof(FastThresholdMs)}.",
                [nameof(SlowThresholdMs)]);
        }
    }
}
