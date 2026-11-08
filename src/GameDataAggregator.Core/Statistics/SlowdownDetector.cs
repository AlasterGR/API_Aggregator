namespace GameDataAggregator.Core.Statistics;

/// <summary>What the slowdown rule concluded about one API.</summary>
public enum SlowdownState
{
    /// <summary>Too few requests inside the window or before it to judge fairly.</summary>
    NotEnoughData,

    /// <summary>The recent average is within the allowed increase.</summary>
    Normal,

    /// <summary>The recent average is more than the allowed percentage above the earlier average.</summary>
    Slow,
}

/// <summary>The outcome of the slowdown rule for one API.</summary>
/// <param name="State">The conclusion.</param>
/// <param name="IncreasePercent">How much slower the recent average is than before, in percent. Negative when faster.</param>
public sealed record SlowdownCheck(SlowdownState State, double IncreasePercent);

/// <summary>
/// The rule that decides whether an external API has become slow. It compares the average response time inside the
/// recent window with the average from before the window. Kept apart from the background service, so the rule can be
/// tested without timers or logs.
/// </summary>
public static class SlowdownDetector
{
    /// <summary>Applies the rule to one API's figures.</summary>
    public static SlowdownCheck Check(ApiPerformanceWindow window, PerformanceMonitorOptions options)
    {
        //  Give no verdict when either period has too few requests, or the earlier average is zero.
        if (window.RecentRequests < options.MinimumRequests
            || window.BaselineRequests < options.MinimumRequests
            || window.BaselineAverageResponseTimeMs <= 0)
        {
            return new SlowdownCheck(SlowdownState.NotEnoughData, 0);
        }

        //  Work out how much slower the recent average is, and compare it with the allowed increase.
        var increasePercent = ((window.RecentAverageResponseTimeMs / window.BaselineAverageResponseTimeMs) - 1) * 100;
        var state = increasePercent > options.SlowdownThresholdPercent ? SlowdownState.Slow : SlowdownState.Normal;
        return new SlowdownCheck(state, Math.Round(increasePercent, 1));
    }
}
