using GameDataAggregator.Core.Models;

namespace GameDataAggregator.Core.Aggregation;

/// <summary>What <see cref="ProviderExecutor"/> returns for one provider: the summary plus the data.</summary>
public sealed record ProviderOutcome(SourceResult Summary, IReadOnlyList<AggregatedItem> Items);
