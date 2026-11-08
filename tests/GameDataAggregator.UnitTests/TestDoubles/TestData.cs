using GameDataAggregator.Core.Aggregation;
using GameDataAggregator.Core.Models;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace GameDataAggregator.UnitTests.TestDoubles;

/// <summary>Ready-made items and services for unit tests.</summary>
internal static class TestData
{
    /// <summary>Creates an item with the given details and sensible defaults for the rest.</summary>
    public static AggregatedItem Item(
        string title,
        string source = "Source",
        ItemCategory category = ItemCategory.GameInfo,
        DateTimeOffset? date = null,
        double relevance = 0,
        params string[] tags) => new()
        {
            Source = source,
            Category = category,
            Title = title,
            Date = date,
            Relevance = relevance,
            Tags = tags,
        };

    /// <summary>A real, isolated in-memory HybridCache for each test.</summary>
    public static HybridCache NewCache() =>
        new ServiceCollection().AddHybridCache().Services.BuildServiceProvider().GetRequiredService<HybridCache>();

    /// <summary>Creates the source runner with its own cache and the given storage times.</summary>
    public static ProviderExecutor NewExecutor(
        HybridCache? cache = null,
        TimeSpan? cacheDuration = null,
        TimeSpan? fallbackDuration = null) =>
        new(
            cache ?? NewCache(),
            Options.Create(new AggregationOptions
            {
                CacheDuration = cacheDuration ?? TimeSpan.FromMinutes(5),
                FallbackDuration = fallbackDuration ?? TimeSpan.FromHours(1),
            }),
            TimeProvider.System,
            NullLogger<ProviderExecutor>.Instance);
}
