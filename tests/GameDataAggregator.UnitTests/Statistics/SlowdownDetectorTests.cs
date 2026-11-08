using System.ComponentModel.DataAnnotations;
using GameDataAggregator.Core.Statistics;

namespace GameDataAggregator.UnitTests.Statistics;

/// <summary>Tests the rule that decides whether an external API has become slow.</summary>
public sealed class SlowdownDetectorTests
{
    private static readonly PerformanceMonitorOptions Defaults = new() { SlowdownThresholdPercent = 50, MinimumRequests = 5 };

    /// <summary>Builds one API's figures.</summary>
    private static ApiPerformanceWindow Window(double recentMs, double baselineMs, long recentRequests = 10, long baselineRequests = 10) => new()
    {
        Api = "RAWG",
        RecentRequests = recentRequests,
        RecentAverageResponseTimeMs = recentMs,
        BaselineRequests = baselineRequests,
        BaselineAverageResponseTimeMs = baselineMs,
    };

    /// <summary>Checks the threshold: more than 50% slower is slow, exactly 50% is still normal.</summary>
    [Theory]
    [InlineData(100, 100, SlowdownState.Normal, 0)]
    [InlineData(80, 100, SlowdownState.Normal, -20)]
    [InlineData(150, 100, SlowdownState.Normal, 50)]
    [InlineData(150.1, 100, SlowdownState.Slow, 50.1)]
    [InlineData(400, 100, SlowdownState.Slow, 300)]
    public void Flags_an_average_more_than_the_threshold_above_the_baseline(
        double recentMs,
        double baselineMs,
        SlowdownState expectedState,
        double expectedIncrease)
    {
        //  Apply the rule.
        var check = SlowdownDetector.Check(Window(recentMs, baselineMs), Defaults);

        //  Expect the verdict and the increase.
        Assert.Equal(expectedState, check.State);
        Assert.Equal(expectedIncrease, check.IncreasePercent);
    }

    /// <summary>Checks that a configured threshold is used.</summary>
    [Fact]
    public void Uses_the_configured_threshold()
    {
        //  Allow only a 10% increase.
        var strict = new PerformanceMonitorOptions { SlowdownThresholdPercent = 10, MinimumRequests = 5 };

        //  Expect a 20% increase to count as slow.
        Assert.Equal(SlowdownState.Slow, SlowdownDetector.Check(Window(120, 100), strict).State);
    }

    /// <summary>Checks that too few requests on either side give no verdict, however slow.</summary>
    [Theory]
    [InlineData(4, 10)]
    [InlineData(10, 4)]
    [InlineData(0, 0)]
    public void Gives_no_verdict_with_too_few_requests(long recentRequests, long baselineRequests)
    {
        //  Apply the rule to a big slowdown with few requests.
        var check = SlowdownDetector.Check(Window(1_000, 100, recentRequests, baselineRequests), Defaults);

        //  Expect no verdict.
        Assert.Equal(SlowdownState.NotEnoughData, check.State);
    }

    /// <summary>Checks that a zero baseline cannot cause a division by zero.</summary>
    [Fact]
    public void Gives_no_verdict_when_the_baseline_average_is_zero()
    {
        Assert.Equal(SlowdownState.NotEnoughData, SlowdownDetector.Check(Window(100, 0), Defaults).State);
    }

    /// <summary>Checks the settings rules: the window must be whole minutes and within range.</summary>
    [Theory]
    [InlineData("00:05:00", true)]
    [InlineData("01:00:00", true)]
    [InlineData("00:05:30", false)]
    [InlineData("00:00:30", false)]
    [InlineData("02:00:00", false)]
    public void Options_require_a_window_of_whole_minutes_up_to_an_hour(string window, bool expectedValid)
    {
        //  Set the window.
        var options = new PerformanceMonitorOptions { Window = TimeSpan.Parse(window, System.Globalization.CultureInfo.InvariantCulture) };

        //  Run the settings checks.
        var isValid = Validator.TryValidateObject(options, new ValidationContext(options), [], validateAllProperties: true);

        //  Expect them to pass only for whole minutes from 1 to 60.
        Assert.Equal(expectedValid, isValid);
    }
}
