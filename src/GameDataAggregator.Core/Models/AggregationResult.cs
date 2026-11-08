namespace GameDataAggregator.Core.Models;

/// <summary>How a provider's data was obtained for a request.</summary>
public enum SourceStatus
{
    /// <summary>Fetched from the external API during this request.</summary>
    Live,

    /// <summary>Served from the cache; the external API was not called.</summary>
    Cached,

    /// <summary>The external API failed; older, last-known-good data was served instead.</summary>
    Fallback,

    /// <summary>The external API failed and no fallback data was available.</summary>
    Failed,

    /// <summary>
    /// Required configuration (such as an API key) is missing, so the external API was not called.
    /// <see cref="SourceResult.Error"/> names the missing setting.
    /// </summary>
    NotConfigured,
}

/// <summary>Outcome of querying a single provider.</summary>
public sealed record SourceResult
{
    public required string Source { get; init; }

    public required SourceStatus Status { get; init; }

    /// <summary>Items this provider contributed, before filtering.</summary>
    public required int ItemCount { get; init; }

    /// <summary>When the data was fetched from the external API. Older than now for Cached and Fallback.</summary>
    public DateTimeOffset? FetchedAt { get; init; }

    /// <summary>Time spent obtaining the data, including cache look-ups.</summary>
    public required double DurationMs { get; init; }

    /// <summary>A safe-to-display reason when <see cref="Status"/> is Fallback, Failed or NotConfigured.</summary>
    public string? Error { get; init; }
}

/// <summary>The combined, filtered, sorted and paged result returned to callers.</summary>
public sealed record AggregationResult
{
    public required string Search { get; init; }

    public required int Page { get; init; }

    public required int PageSize { get; init; }

    /// <summary>Number of items matching the filters, across all pages.</summary>
    public required int TotalCount { get; init; }

    public required IReadOnlyList<AggregatedItem> Items { get; init; }

    /// <summary>Per-provider status, so callers can tell when data is partial or stale.</summary>
    public required IReadOnlyList<SourceResult> Sources { get; init; }
}
