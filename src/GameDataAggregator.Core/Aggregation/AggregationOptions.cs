using System.ComponentModel.DataAnnotations;

namespace GameDataAggregator.Core.Aggregation;

/// <summary>Configuration section "Aggregation".</summary>
public sealed class AggregationOptions
{
    public const string SectionName = "Aggregation";

    /// <summary>How long a provider's response is reused before the external API is called again.</summary>
    [Range(typeof(TimeSpan), "00:00:01", "1.00:00:00")]
    public TimeSpan CacheDuration { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// How long the last successful response is kept as a fallback for when the external API fails.
    /// Must be longer than <see cref="CacheDuration"/> to be useful.
    /// </summary>
    [Range(typeof(TimeSpan), "00:00:01", "7.00:00:00")]
    public TimeSpan FallbackDuration { get; set; } = TimeSpan.FromHours(6);
}
