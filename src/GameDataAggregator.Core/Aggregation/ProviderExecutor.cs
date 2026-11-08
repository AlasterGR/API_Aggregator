using GameDataAggregator.Core.Abstractions;
using GameDataAggregator.Core.Models;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GameDataAggregator.Core.Aggregation;

/// <summary>
/// Runs a single provider with caching and fallback:
/// <list type="number">
///   <item>Provider missing configuration (e.g. an API key): report it as NotConfigured without calling anything.</item>
///   <item>Fresh cache hit: return it without calling the external API.</item>
///   <item>Cache miss: call the API, then store the result both as fresh data and as a long-lived fallback copy.</item>
///   <item>API failure: serve the last-known-good fallback copy if one exists, otherwise report the source as failed.</item>
/// </list>
/// A provider failure never throws out of this class, so one broken API cannot break the whole response.
/// </summary>
public sealed partial class ProviderExecutor
{
    /// <summary>Entry options that turn GetOrCreateAsync into a pure "try get" (no factory call, no write).</summary>
    private static readonly HybridCacheEntryOptions ReadOnlyEntry = new()
    {
        Flags = HybridCacheEntryFlags.DisableUnderlyingData
              | HybridCacheEntryFlags.DisableLocalCacheWrite
              | HybridCacheEntryFlags.DisableDistributedCacheWrite,
    };

    private readonly HybridCache _cache;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ProviderExecutor> _logger;
    private readonly HybridCacheEntryOptions _freshEntry;
    private readonly HybridCacheEntryOptions _fallbackEntry;

    /// <summary>Prepares how long fresh and backup copies of results are kept.</summary>
    public ProviderExecutor(
        HybridCache cache,
        IOptions<AggregationOptions> options,
        TimeProvider timeProvider,
        ILogger<ProviderExecutor> logger)
    {
        //  Keep the cache, clock and log, and set how long fresh and backup copies last.
        _cache = cache;
        _timeProvider = timeProvider;
        _logger = logger;
        _freshEntry = new HybridCacheEntryOptions { Expiration = options.Value.CacheDuration, LocalCacheExpiration = options.Value.CacheDuration };
        _fallbackEntry = new HybridCacheEntryOptions { Expiration = options.Value.FallbackDuration, LocalCacheExpiration = options.Value.FallbackDuration };
    }

    /// <summary>Gets one source's results from a saved copy or the source itself, falling back to an older copy if it fails.</summary>
    public async Task<ProviderOutcome> ExecuteAsync(
        IGameDataProvider provider,
        string search,
        CancellationToken cancellationToken)
    {
        //  Start timing the request.
        var startedAt = _timeProvider.GetTimestamp();

        //  Answer at once for a source missing its settings, without calling it or logging again.
        if (provider.ConfigurationError is { } configurationError)
        {
            return Outcome(provider, SourceStatus.NotConfigured, CachedItems.Empty, startedAt, configurationError);
        }

        //  Name this search's fresh and backup copies, and note that the source has not been called yet.
        var normalizedSearch = search.Trim().ToLowerInvariant();
        var freshKey = $"fresh:{provider.Name}:{normalizedSearch}";
        var fallbackKey = $"fallback:{provider.Name}:{normalizedSearch}";
        var calledApi = false;

        //  Use the fresh copy if there is one, otherwise ask the source.
        try
        {
            //  Callers asking for the same search at the same time share one call to the source.
            var entry = await _cache.GetOrCreateAsync(
                freshKey,
                async token =>
                {
                    //  Ask the source, then keep its answer as both a fresh and a backup copy.
                    calledApi = true;
                    var items = await provider.FetchAsync(normalizedSearch, token);
                    var fetched = new CachedItems(items, _timeProvider.GetUtcNow());
                    await _cache.SetAsync(fallbackKey, fetched, _fallbackEntry, cancellationToken: token);
                    return fetched;
                },
                _freshEntry,
                cancellationToken: cancellationToken);

            //  Report the data as live if the source was asked, otherwise as cached.
            return Outcome(provider, calledApi ? SourceStatus.Live : SourceStatus.Cached, entry, startedAt);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            //  Handle every failure except the caller giving up, and look for a backup copy.
            var fallback = await TryGetFallbackAsync(fallbackKey, cancellationToken);
            LogProviderFailed(_logger, ex, provider.Name, fallback is not null);

            //  Report the source as failed, or as served from the backup copy.
            var error = DescribeFailure(ex);
            return fallback is null
                ? Outcome(provider, SourceStatus.Failed, CachedItems.Empty, startedAt, error)
                : Outcome(provider, SourceStatus.Fallback, fallback, startedAt, error);
        }
    }

    /// <summary>Reads the backup copy, or returns nothing if there is none or the cache fails.</summary>
    private async Task<CachedItems?> TryGetFallbackAsync(string key, CancellationToken cancellationToken)
    {
        //  Read the backup copy without creating one.
        try
        {
            return await _cache.GetOrCreateAsync(
                key,
                _ => ValueTask.FromResult<CachedItems?>(null),
                ReadOnlyEntry,
                cancellationToken: cancellationToken);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            //  Log a cache failure and carry on without a backup copy.
            LogFallbackLookupFailed(_logger, ex, key);
            return null;
        }
    }

    /// <summary>Builds the report for one source, including how long it took.</summary>
    private ProviderOutcome Outcome(
        IGameDataProvider provider,
        SourceStatus status,
        CachedItems entry,
        long startedAt,
        string? error = null)
    {
        //  Describe how the source did and how long it took.
        var summary = new SourceResult
        {
            Source = provider.Name,
            Status = status,
            ItemCount = entry.Items.Count,
            FetchedAt = entry.FetchedAt,
            DurationMs = Math.Round(_timeProvider.GetElapsedTime(startedAt).TotalMilliseconds, 2),
            Error = error,
        };

        return new ProviderOutcome(summary, entry.Items);
    }

    /// <summary>Turns an exception into a message that is safe to return to API callers.</summary>
    internal static string DescribeFailure(Exception exception) => exception switch
    {
        DataProviderException e => e.Message,
        HttpRequestException { StatusCode: { } status } => $"The external API responded with HTTP {(int)status}.",
        HttpRequestException => "The external API could not be reached.",
        TimeoutException or OperationCanceledException => "The external API did not respond in time.",
        _ => "The external API is currently unavailable.",
    };

    /// <summary>Logs that a source failed and whether a backup copy was found.</summary>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Provider {Provider} failed. Fallback data available: {HasFallback}")]
    private static partial void LogProviderFailed(ILogger logger, Exception exception, string provider, bool hasFallback);

    /// <summary>Logs that reading a backup copy failed.</summary>
    [LoggerMessage(Level = LogLevel.Error, Message = "Reading fallback cache entry {CacheKey} failed")]
    private static partial void LogFallbackLookupFailed(ILogger logger, Exception exception, string cacheKey);

    /// <summary>What is stored in the cache: the items plus when they were fetched from the API.</summary>
    internal sealed record CachedItems(IReadOnlyList<AggregatedItem> Items, DateTimeOffset? FetchedAt)
    {
        public static CachedItems Empty { get; } = new([], null);
    }
}
