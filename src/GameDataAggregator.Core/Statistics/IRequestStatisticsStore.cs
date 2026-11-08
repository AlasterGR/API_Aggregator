namespace GameDataAggregator.Core.Statistics;

/// <summary>Records every outgoing request to an external API. Implementations must be thread-safe.</summary>
public interface IRequestStatisticsStore
{
    /// <summary>Records how long one request took and whether it succeeded.</summary>
    void Record(string api, TimeSpan duration, bool isSuccess);

    /// <summary>
    /// Returns a consistent snapshot per API. APIs listed in <paramref name="knownApis"/> are included
    /// with zero counts even if they have not been called yet.
    /// </summary>
    IReadOnlyList<ApiStatistics> GetSnapshot(IEnumerable<string>? knownApis = null);

    /// <summary>
    /// Returns, per API that has been called, its average response time inside the most recent
    /// <paramref name="window"/> and before it. The window is counted in whole clock minutes, so a 5-minute window
    /// is the current minute and the 4 before it.
    /// </summary>
    /// <param name="window">Whole minutes, from 1 to 60.</param>
    IReadOnlyList<ApiPerformanceWindow> GetPerformanceWindows(TimeSpan window);
}
