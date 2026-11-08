namespace GameDataAggregator.Core.Statistics;

/// <summary>Speed group a request falls into.</summary>
public enum PerformanceBucket
{
    Fast,
    Average,
    Slow,
}

/// <summary>Request count and average response time for one performance bucket.</summary>
public sealed record BucketStatistics
{
    public required PerformanceBucket Bucket { get; init; }

    /// <summary>Human-readable range, e.g. "&lt; 100 ms".</summary>
    public required string Range { get; init; }

    public required long Requests { get; init; }

    public required double AverageResponseTimeMs { get; init; }
}

/// <summary>Aggregated request statistics for one external API.</summary>
public sealed record ApiStatistics
{
    public required string Api { get; init; }

    public required long TotalRequests { get; init; }

    /// <summary>Requests that hit a server error (5xx), throttling (429), a timeout or a network failure.</summary>
    public required long FailedRequests { get; init; }

    public required double AverageResponseTimeMs { get; init; }

    public required IReadOnlyList<BucketStatistics> Buckets { get; init; }
}
