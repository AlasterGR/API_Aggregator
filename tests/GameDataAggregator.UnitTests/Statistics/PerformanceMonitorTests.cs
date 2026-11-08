using GameDataAggregator.Core.Statistics;
using GameDataAggregator.UnitTests.TestDoubles;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace GameDataAggregator.UnitTests.Statistics;

/// <summary>Tests the background service that logs slow external APIs.</summary>
public sealed class PerformanceMonitorTests
{
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 1, 1, 12, 0, 30, TimeSpan.Zero));
    private readonly CapturingLogger<PerformanceMonitor> _logger = new();
    private readonly InMemoryRequestStatisticsStore _store;

    /// <summary>Creates a store that reads the fake clock.</summary>
    public PerformanceMonitorTests() =>
        _store = new InMemoryRequestStatisticsStore(Options.Create(new StatisticsOptions()), _time);

    /// <summary>Creates a monitor with the default rule: 5-minute window, +50%, at least 5 requests.</summary>
    private PerformanceMonitor NewMonitor(bool enabled = true) => new(
        _store,
        Options.Create(new PerformanceMonitorOptions { Enabled = enabled }),
        _time,
        _logger);

    /// <summary>Records several requests of the same duration.</summary>
    private void Record(string api, double milliseconds, int count)
    {
        for (var i = 0; i < count; i++)
        {
            _store.Record(api, TimeSpan.FromMilliseconds(milliseconds), isSuccess: true);
        }
    }

    /// <summary>Records a normal history at 100 ms, old enough to be outside the window.</summary>
    private void RecordHistory(string api)
    {
        Record(api, 100, count: 10);
        _time.Advance(TimeSpan.FromMinutes(10));
    }

    /// <summary>Checks that a slow source is reported once with its figures, not on every check.</summary>
    [Fact]
    public void Warns_once_when_an_api_turns_slow()
    {
        //  Give RAWG a normal history, then make it twice as slow.
        RecordHistory("RAWG");
        Record("RAWG", 200, count: 5);

        //  Check twice.
        var monitor = NewMonitor();
        monitor.CheckNow();
        monitor.CheckNow();

        //  Expect one warning that names the source, both averages and the increase.
        var warning = Assert.Single(_logger.At(LogLevel.Warning));
        Assert.Contains("RAWG", warning, StringComparison.Ordinal);
        Assert.Contains("200 ms", warning, StringComparison.Ordinal);
        Assert.Contains("100 ms", warning, StringComparison.Ordinal);
        Assert.Contains("100%", warning, StringComparison.Ordinal);
    }

    /// <summary>Checks that a source within the threshold is not reported.</summary>
    [Fact]
    public void Stays_quiet_while_an_api_is_within_the_threshold()
    {
        //  Give Steam a normal history, then make it 40% slower.
        RecordHistory("Steam");
        Record("Steam", 140, count: 5);

        //  Check.
        NewMonitor().CheckNow();

        //  Expect no warning.
        Assert.Empty(_logger.At(LogLevel.Warning));
    }

    /// <summary>Checks that recovery is logged once the slow requests leave the window.</summary>
    [Fact]
    public void Logs_recovery_when_the_api_is_back_to_normal()
    {
        //  Make IGDB slow and check.
        RecordHistory("IGDB");
        Record("IGDB", 300, count: 5);
        var monitor = NewMonitor();
        monitor.CheckNow();

        //  Let the slow minutes leave the window, record normal requests and check again.
        _time.Advance(TimeSpan.FromMinutes(10));
        Record("IGDB", 100, count: 5);
        monitor.CheckNow();

        //  Expect one warning, then one recovery message.
        Assert.Single(_logger.At(LogLevel.Warning));
        Assert.Contains(_logger.At(LogLevel.Information), m => m.Contains("recovered", StringComparison.OrdinalIgnoreCase) && m.Contains("IGDB", StringComparison.Ordinal));
    }

    /// <summary>Checks that a quiet period neither clears nor repeats the slow state.</summary>
    [Fact]
    public void Keeps_the_slow_state_while_there_are_too_few_requests()
    {
        //  Make Speedrun slow and check.
        RecordHistory("Speedrun");
        Record("Speedrun", 300, count: 5);
        var monitor = NewMonitor();
        monitor.CheckNow();

        //  Let the window empty out, then check again.
        _time.Advance(TimeSpan.FromMinutes(10));
        monitor.CheckNow();

        //  Expect no recovery message and no second warning.
        Assert.Single(_logger.At(LogLevel.Warning));
        Assert.DoesNotContain(_logger.At(LogLevel.Information), m => m.Contains("recovered", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Checks that the service runs the check on its timer.</summary>
    [Fact]
    public async Task Checks_on_every_interval_once_started()
    {
        //  Make CheapShark slow and start the service.
        RecordHistory("CheapShark");
        Record("CheapShark", 300, count: 5);
        using var monitor = NewMonitor();
        await monitor.StartAsync(TestContext.Current.CancellationToken);

        //  Wait until the service runs in the background, since it starts its timer there.
        await WaitUntilAsync(() => _logger.At(LogLevel.Information).Any(m => m.Contains("started", StringComparison.Ordinal)));

        //  Expect nothing before the first tick.
        Assert.Empty(_logger.At(LogLevel.Warning));

        //  Move the clock past one interval and wait for the check to run.
        _time.Advance(TimeSpan.FromMinutes(1));
        await WaitUntilAsync(() => _logger.At(LogLevel.Warning).Count > 0);

        //  Expect the warning, and a clean stop.
        Assert.Single(_logger.At(LogLevel.Warning));
        await monitor.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Checks that a switched-off service never checks.</summary>
    [Fact]
    public async Task Does_nothing_when_disabled()
    {
        //  Make Steam slow and start a switched-off service.
        RecordHistory("Steam");
        Record("Steam", 300, count: 5);
        using var monitor = NewMonitor(enabled: false);
        await monitor.StartAsync(TestContext.Current.CancellationToken);

        //  Wait for the "disabled" message, which the service logs in the background.
        await WaitUntilAsync(() => _logger.At(LogLevel.Information).Any(m => m.Contains("disabled", StringComparison.Ordinal)));

        //  Move the clock well past several intervals, then stop.
        _time.Advance(TimeSpan.FromMinutes(5));
        await monitor.StopAsync(TestContext.Current.CancellationToken);

        //  Expect the "disabled" message and no warning.
        Assert.Contains(_logger.At(LogLevel.Information), m => m.Contains("disabled", StringComparison.Ordinal));
        Assert.Empty(_logger.At(LogLevel.Warning));
    }

    /// <summary>Waits a short while for a condition that another thread makes true.</summary>
    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        //  Poll for up to 5 seconds, so a broken service fails the test instead of hanging it.
        for (var i = 0; i < 500 && !condition(); i++)
        {
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }
    }
}
