using System.ComponentModel.DataAnnotations;

namespace GameDataAggregator.Api.RateLimiting;

/// <summary>Configuration section "RateLimiting": how many searches one client may make per time window.</summary>
public sealed class SearchRateLimitOptions
{
    public const string SectionName = "RateLimiting";

    /// <summary>Searches allowed per client per <see cref="Window"/>.</summary>
    [Range(1, 10_000)]
    public int PermitLimit { get; set; } = 60;

    [Range(typeof(TimeSpan), "00:00:01", "01:00:00")]
    public TimeSpan Window { get; set; } = TimeSpan.FromMinutes(1);
}
