namespace GameDataAggregator.Core.Statistics;

/// <summary>
/// One external API's average response time in a recent time window, next to its average from all the time before
/// that window. The two never overlap, so a slowdown in the window does not dilute the figure it is compared with.
/// </summary>
public sealed record ApiPerformanceWindow
{
    public required string Api { get; init; }

    /// <summary>Requests made inside the window.</summary>
    public required long RecentRequests { get; init; }

    /// <summary>Average response time inside the window, or 0 when there were no requests.</summary>
    public required double RecentAverageResponseTimeMs { get; init; }

    /// <summary>Requests made since start-up but before the window.</summary>
    public required long BaselineRequests { get; init; }

    /// <summary>Average response time before the window, or 0 when there were no requests.</summary>
    public required double BaselineAverageResponseTimeMs { get; init; }
}
