namespace GameDataAggregator.Core.Models;

/// <summary>
/// The unified shape every external API's data is normalised into, so items from different
/// sources can be filtered, sorted and paged together.
/// </summary>
public sealed record AggregatedItem
{
    /// <summary>Name of the provider that produced the item (e.g. "RAWG").</summary>
    public required string Source { get; init; }

    public required ItemCategory Category { get; init; }

    public required string Title { get; init; }

    /// <summary>Link to the item on the source's website.</summary>
    public Uri? Url { get; init; }

    public Uri? ImageUrl { get; init; }

    private readonly DateKind? _dateKind;

    /// <summary>
    /// The date that best describes the item, e.g. a release date or a last price change.
    /// <see cref="DateKind"/> says which.
    /// </summary>
    public DateTimeOffset? Date { get; init; }

    /// <summary>What <see cref="Date"/> means. Always <see langword="null"/> when there is no date.</summary>
    public DateKind? DateKind
    {
        get => Date is null ? null : _dateKind;
        init => _dateKind = value;
    }

    /// <summary>How well the item matches the search term, from 0 (no match) to 1 (exact match).</summary>
    public double Relevance { get; init; }

    /// <summary>Free-form labels used for filtering, e.g. genres or store names.</summary>
    public IReadOnlyList<string> Tags { get; init; } = [];

    /// <summary>Numeric facts, e.g. "rating", "salePrice", "currentPlayers".</summary>
    public IReadOnlyDictionary<string, double> Metrics { get; init; } = new Dictionary<string, double>();

    /// <summary>Non-numeric facts, e.g. "store", "platforms".</summary>
    public IReadOnlyDictionary<string, string> Attributes { get; init; } = new Dictionary<string, string>();
}
