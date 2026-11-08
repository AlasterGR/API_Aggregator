using GameDataAggregator.Core.Models;

namespace GameDataAggregator.Core.Abstractions;

/// <summary>
/// One external API. This is the only contract a new integration has to implement: the aggregation
/// service discovers every registered provider and handles caching, fallback, parallelism,
/// filtering and sorting for it.
/// </summary>
public interface IGameDataProvider
{
    /// <summary>Unique, human-readable name, used in responses, statistics and the "sources" filter.</summary>
    string Name { get; }

    /// <summary>
    /// Why the provider cannot be called, e.g. "API key is not configured"; <see langword="null"/> when it is ready.
    /// The aggregation service checks this before calling <see cref="FetchAsync"/> and reports the source as
    /// <see cref="Models.SourceStatus.NotConfigured"/>, so a known configuration gap never costs an exception,
    /// an API call or a log entry per request. Providers that need no configuration keep the default.
    /// </summary>
    string? ConfigurationError => null;

    /// <summary>
    /// Fetches items matching <paramref name="search"/> and maps them to <see cref="AggregatedItem"/>.
    /// Implementations should throw on failure; they do not need to catch or retry.
    /// </summary>
    Task<IReadOnlyList<AggregatedItem>> FetchAsync(string search, CancellationToken cancellationToken);
}
