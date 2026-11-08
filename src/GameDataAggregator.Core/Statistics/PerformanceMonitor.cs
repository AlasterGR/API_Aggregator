using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GameDataAggregator.Core.Statistics;

/// <summary>
/// Background service that checks the request statistics on a timer and logs when an external API becomes slow.
/// <para>
/// It logs a warning once when an API turns slow and an information message once when it is back to normal, so a
/// long slowdown does not fill the log with the same warning. An API without enough recent requests keeps its last
/// state, because silence says nothing about its speed.
/// </para>
/// </summary>
public sealed partial class PerformanceMonitor(
    IRequestStatisticsStore store,
    IOptions<PerformanceMonitorOptions> options,
    TimeProvider timeProvider,
    ILogger<PerformanceMonitor> logger) : BackgroundService
{
    private readonly PerformanceMonitorOptions _options = options.Value;
    private readonly HashSet<string> _slowApis = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Checks the statistics on every tick of the timer until the app stops.</summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        //  Do nothing when the check is switched off.
        if (!_options.Enabled)
        {
            LogDisabled(logger);
            return;
        }

        //  Wait for each tick, then check, until the app stops.
        LogStarted(logger, _options.CheckInterval, _options.Window, _options.SlowdownThresholdPercent);
        using var timer = new PeriodicTimer(_options.CheckInterval, timeProvider);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                //  Keep the service alive if one check fails, and log why.
                try
                {
                    CheckNow();
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    LogCheckFailed(logger, ex);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            //  Stop quietly when the app shuts down.
        }
    }

    /// <summary>Runs one check over every API and logs each change between normal and slow.</summary>
    internal void CheckNow()
    {
        //  Judge each source that has been called.
        foreach (var window in store.GetPerformanceWindows(_options.Window))
        {
            //  Apply the rule, and leave the state alone when there is not enough data.
            var check = SlowdownDetector.Check(window, _options);
            if (check.State == SlowdownState.Slow && _slowApis.Add(window.Api))
            {
                //  Warn once when the API turns slow.
                LogSlowdown(
                    logger,
                    window.Api,
                    window.RecentAverageResponseTimeMs,
                    _options.Window.TotalMinutes,
                    window.RecentRequests,
                    check.IncreasePercent,
                    window.BaselineAverageResponseTimeMs,
                    window.BaselineRequests);
            }
            else if (check.State == SlowdownState.Normal && _slowApis.Remove(window.Api))
            {
                //  Say once when the API is back to normal.
                LogRecovered(logger, window.Api, window.RecentAverageResponseTimeMs, window.BaselineAverageResponseTimeMs);
            }
        }
    }

    /// <summary>Logs that the check is switched off.</summary>
    [LoggerMessage(Level = LogLevel.Information, Message = "Performance monitor is disabled")]
    private static partial void LogDisabled(ILogger logger);

    /// <summary>Logs the check's settings when it starts.</summary>
    [LoggerMessage(Level = LogLevel.Information, Message = "Performance monitor started: checking every {Interval}, comparing the last {Window} with earlier requests, alerting above +{ThresholdPercent}%")]
    private static partial void LogStarted(ILogger logger, TimeSpan interval, TimeSpan window, double thresholdPercent);

    /// <summary>Logs that an API has become slow.</summary>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Performance anomaly: {Api} averaged {RecentAverageMs} ms over the last {WindowMinutes} min ({RecentRequests} requests), {IncreasePercent}% above its earlier average of {BaselineAverageMs} ms ({BaselineRequests} requests)")]
    private static partial void LogSlowdown(
        ILogger logger,
        string api,
        double recentAverageMs,
        double windowMinutes,
        long recentRequests,
        double increasePercent,
        double baselineAverageMs,
        long baselineRequests);

    /// <summary>Logs that an API is back to its usual speed.</summary>
    [LoggerMessage(Level = LogLevel.Information, Message = "Performance recovered: {Api} averaged {RecentAverageMs} ms recently, against {BaselineAverageMs} ms earlier")]
    private static partial void LogRecovered(ILogger logger, string api, double recentAverageMs, double baselineAverageMs);

    /// <summary>Logs that one check failed.</summary>
    [LoggerMessage(Level = LogLevel.Error, Message = "Performance check failed")]
    private static partial void LogCheckFailed(ILogger logger, Exception exception);
}
