using System.ComponentModel.DataAnnotations;

namespace GameDataAggregator.Core.Statistics;

/// <summary>Configuration section "PerformanceMonitor". Controls the background check for slow external APIs.</summary>
public sealed class PerformanceMonitorOptions : IValidatableObject
{
    public const string SectionName = "PerformanceMonitor";

    /// <summary>Turns the background check on or off.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>How often the statistics are checked.</summary>
    [Range(typeof(TimeSpan), "00:00:05", "01:00:00")]
    public TimeSpan CheckInterval { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>The recent period that is compared with everything before it. Whole minutes only.</summary>
    [Range(typeof(TimeSpan), "00:01:00", "01:00:00")]
    public TimeSpan Window { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>How much slower than before, in percent, the recent average must be to count as a slowdown.</summary>
    [Range(1, 1_000)]
    public double SlowdownThresholdPercent { get; set; } = 50;

    /// <summary>
    /// The fewest requests needed both inside the window and before it. Below this, a few slow requests would be
    /// enough to raise a false alarm, so the API is not judged.
    /// </summary>
    [Range(1, 100_000)]
    public int MinimumRequests { get; set; } = 5;

    /// <summary>Checks that the window is a whole number of minutes, as the statistics are kept per minute.</summary>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        //  Reject a window with seconds in it.
        if (Window.Ticks % TimeSpan.TicksPerMinute != 0)
        {
            yield return new ValidationResult(
                $"{nameof(Window)} must be a whole number of minutes.",
                [nameof(Window)]);
        }
    }
}
