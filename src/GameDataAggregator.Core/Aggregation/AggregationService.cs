using GameDataAggregator.Core.Abstractions;
using GameDataAggregator.Core.Models;

namespace GameDataAggregator.Core.Aggregation;

/// <summary>Searches the sources and combines their results.</summary>
public interface IAggregationService
{
    /// <summary>Names of all registered providers, usable in <see cref="AggregationQuery.Sources"/>.</summary>
    IReadOnlyCollection<string> SourceNames { get; }

    /// <summary>Searches the chosen sources and returns one combined, filtered and sorted page.</summary>
    Task<AggregationResult> AggregateAsync(AggregationQuery query, CancellationToken cancellationToken);
}

/// <summary>
/// Queries every selected provider concurrently, merges their items, scores relevance and applies
/// the caller's filters, sorting and paging.
/// </summary>
public sealed class AggregationService : IAggregationService
{
    private readonly IReadOnlyList<IGameDataProvider> _providers;
    private readonly ProviderExecutor _executor;

    /// <summary>Takes every registered source and checks that their names are unique.</summary>
    public AggregationService(IEnumerable<IGameDataProvider> providers, ProviderExecutor executor)
    {
        //  Keep the sources and the helper that calls them.
        _providers = providers.ToList();
        _executor = executor;

        //  Refuse to start when two sources share a name.
        var duplicates = _providers.GroupBy(p => p.Name, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        if (duplicates.Count > 0)
        {
            throw new InvalidOperationException($"Provider names must be unique. Duplicates: {string.Join(", ", duplicates)}");
        }

        //  Remember the source names for checking and listing.
        SourceNames = _providers.Select(p => p.Name).ToList();
    }

    public IReadOnlyCollection<string> SourceNames { get; }

    /// <summary>Searches the chosen sources and returns one combined, filtered and sorted page.</summary>
    public async Task<AggregationResult> AggregateAsync(AggregationQuery query, CancellationToken cancellationToken)
    {
        //  Refuse to run without a query.
        ArgumentNullException.ThrowIfNull(query);

        //  Use the requested sources, or all of them when none were named.
        var selected = query.Sources.Count == 0
            ? _providers
            : _providers.Where(p => query.Sources.Contains(p.Name)).ToList();

        //  Ask all sources at once, so the wait is only as long as the slowest one.
        var outcomes = await Task.WhenAll(selected.Select(p => _executor.ExecuteAsync(p, query.Search, cancellationToken)));

        //  Score how well each title matches the search.
        var scored = outcomes
            .SelectMany(o => o.Items)
            .Select(item => item with { Relevance = Math.Round(RelevanceScorer.Score(query.Search, item.Title), 3) });

        //  Filter and sort the items, and cut out the requested page.
        var (page, totalCount) = ItemQueryProcessor.Apply(scored, query);

        //  Return the page together with how each source did.
        return new AggregationResult
        {
            Search = query.Search,
            Page = query.Page,
            PageSize = query.PageSize,
            TotalCount = totalCount,
            Items = page,
            Sources = outcomes.Select(o => o.Summary).ToList(),
        };
    }
}
