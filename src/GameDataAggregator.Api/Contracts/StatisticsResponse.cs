using GameDataAggregator.Core.Statistics;

namespace GameDataAggregator.Api.Contracts;

/// <summary>Response of <c>GET /api/v1/statistics</c>.</summary>
public sealed record StatisticsResponse
{
    public required DateTimeOffset GeneratedAt { get; init; }

    public required IReadOnlyList<ApiStatistics> Apis { get; init; }
}
