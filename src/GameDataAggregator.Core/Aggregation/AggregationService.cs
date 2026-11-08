using GameDataAggregator.Core.Abstractions;
using GameDataAggregator.Core.Models;

namespace GameDataAggregator.Core.Aggregation;

/// <summary>Searches the sources and combines their results.</summary>
public interface IAggregationService
{
    /// <summary>Names of all registered providers, usable in <see cref="AggregationQuery.Sources"/>.</summary>
    IReadOnlyCollection<string> SourceNames { get; }

    /// <summary>All registered providers with the categories each can give, in the order they are queried.</summary>
    IReadOnlyList<SourceInfo> Sources { get; }

    /// <summary>Searches the chosen sources and returns one combined, filtered and sorted page.</summary>
    Task<AggregationResult> AggregateAsync(AggregationQuery query, CancellationToken cancellationToken);
}

/// <summary>
/// Queries every selected provider concurrently, merges their items, scores relevance and applies
/// the caller's filters, sorting and paging. A provider that cannot give any of the requested categories
/// is not queried at all.
/// </summary>
public sealed class AggregationService : IAggregationService
{
    private readonly IReadOnlyList<(IGameDataProvider Provider, SourceInfo Info)> _providers;
    private readonly ProviderExecutor _executor;

    /// <summary>
    /// Takes every registered source. Their names are unique: <see cref="ProviderConfigurationReporter"/> checks that
    /// once at start-up, because this service is created again for every request.
    /// </summary>
    public AggregationService(IEnumerable<IGameDataProvider> providers, ProviderExecutor executor)
    {
        //  Keep each source together with its name and categories, and the helper that calls them.
        _providers = providers
            .Select(p => (p, new SourceInfo(p.Name, p.Categories.Order().ToList())))
            .ToList();
        _executor = executor;

        //  Remember the sources for checking and listing.
        Sources = _providers.Select(p => p.Info).ToList();
        SourceNames = Sources.Select(s => s.Name).ToList();
    }

    public IReadOnlyCollection<string> SourceNames { get; }

    public IReadOnlyList<SourceInfo> Sources { get; }

    /// <summary>Searches the chosen sources and returns one combined, filtered and sorted page.</summary>
    public async Task<AggregationResult> AggregateAsync(AggregationQuery query, CancellationToken cancellationToken)
    {
        //  Refuse to run without a query.
        ArgumentNullException.ThrowIfNull(query);

        //  Use the chosen sources that can give a chosen category, so none is asked for items that would all be dropped.
        var selected = _providers.Where(p => p.Info.IsSelectedBy(query)).Select(p => p.Provider).ToList();

        //  Ask all sources at once, so the wait is only as long as the slowest one.
        var outcomes = await Task.WhenAll(selected.Select(p => _executor.ExecuteAsync(p, query.Search, cancellationToken)));

        //  Score how well each title matches the search.
        var scored = outcomes
            .SelectMany(o => o.Items)
            .Select(item => item with { Relevance = Math.Round(RelevanceScorer.Score(query.Search, item.Title), 3) });

        //  Filter and sort the items, and cut out the requested page.
        var (page, totalCount, matchCountBySource) = ItemQueryProcessor.Apply(scored, query);

        //  Return the page together with how each source did and how many of its items matched.
        return new AggregationResult
        {
            Search = query.Search,
            Page = query.Page,
            PageSize = query.PageSize,
            TotalCount = totalCount,
            Items = page,
            Sources = outcomes
                .Select(o => o.Summary with { MatchCount = matchCountBySource.GetValueOrDefault(o.Summary.Source) })
                .ToList(),
        };
    }
}
