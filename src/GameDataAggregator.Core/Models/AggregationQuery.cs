namespace GameDataAggregator.Core.Models;

/// <summary>Field used to order aggregated items.</summary>
public enum SortField
{
    Relevance,
    Date,
    Title,
    Source,
}

/// <summary>Whether sorted items run from low to high or high to low.</summary>
public enum SortDirection
{
    Ascending,
    Descending,
}

/// <summary>
/// A validated request for aggregated data. <see cref="Search"/> decides what is fetched from the
/// external APIs; every other property is applied afterwards to the combined result.
/// </summary>
public sealed record AggregationQuery
{
    public required string Search { get; init; }

    /// <summary>Restrict to these providers. Empty means all providers.</summary>
    public IReadOnlySet<string> Sources { get; init; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Restrict to these categories. Empty means all categories.</summary>
    public IReadOnlySet<ItemCategory> Categories { get; init; } = new HashSet<ItemCategory>();

    /// <summary>Only items carrying this tag (case-insensitive).</summary>
    public string? Tag { get; init; }

    /// <summary>Only items dated on or after this instant. Items without a date are excluded.</summary>
    public DateTimeOffset? From { get; init; }

    /// <summary>Only items dated on or before this instant. Items without a date are excluded.</summary>
    public DateTimeOffset? To { get; init; }

    public SortField SortBy { get; init; } = SortField.Relevance;

    public SortDirection SortDirection { get; init; } = SortDirection.Descending;

    /// <summary>1-based page number.</summary>
    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = 20;
}
