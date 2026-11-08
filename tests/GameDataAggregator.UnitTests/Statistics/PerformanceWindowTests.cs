using GameDataAggregator.Core.Statistics;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace GameDataAggregator.UnitTests.Statistics;

/// <summary>Tests how the store splits requests into a recent window and the time before it.</summary>
public sealed class PerformanceWindowTests
{
    //  A start time 30 seconds into a minute, so moves of whole minutes stay inside predictable minutes.
    private static readonly DateTimeOffset Start = new(2026, 1, 1, 12, 0, 30, TimeSpan.Zero);

    private readonly FakeTimeProvider _time = new(Start);
    private readonly InMemoryRequestStatisticsStore _store;

    /// <summary>Creates a store that reads the fake clock.</summary>
    public PerformanceWindowTests() =>
        _store = new InMemoryRequestStatisticsStore(Options.Create(new StatisticsOptions()), _time);

    private static readonly TimeSpan FiveMinutes = TimeSpan.FromMinutes(5);

    /// <summary>Records several requests of the same duration.</summary>
    private void Record(string api, double milliseconds, int count = 1)
    {
        for (var i = 0; i < count; i++)
        {
            _store.Record(api, TimeSpan.FromMilliseconds(milliseconds), isSuccess: true);
        }
    }

    /// <summary>Checks that requests in the window and before it are counted and averaged apart.</summary>
    [Fact]
    public void Splits_requests_into_the_window_and_the_time_before_it()
    {
        //  Record 4 requests at 100 ms, then 10 minutes later 2 at 400 ms.
        Record("RAWG", 100, count: 4);
        _time.Advance(TimeSpan.FromMinutes(10));
        Record("RAWG", 400, count: 2);

        //  Read a 5-minute window.
        var rawg = Assert.Single(_store.GetPerformanceWindows(FiveMinutes));

        //  Expect the old requests as the baseline and the new ones as the window.
        Assert.Equal("RAWG", rawg.Api);
        Assert.Equal((2L, 400.0), (rawg.RecentRequests, rawg.RecentAverageResponseTimeMs));
        Assert.Equal((4L, 100.0), (rawg.BaselineRequests, rawg.BaselineAverageResponseTimeMs));
    }

    /// <summary>Checks the window's edge: the current minute and the 4 before it are inside, the 5th is not.</summary>
    [Theory]
    [InlineData(0, true)]
    [InlineData(4, true)]
    [InlineData(5, false)]
    [InlineData(59, false)]
    public void A_request_stays_in_the_window_for_the_window_length_in_whole_minutes(int minutesLater, bool expectedRecent)
    {
        //  Record one request, then let time pass.
        Record("Steam", 100);
        _time.Advance(TimeSpan.FromMinutes(minutesLater));

        //  Read a 5-minute window.
        var steam = Assert.Single(_store.GetPerformanceWindows(FiveMinutes));

        //  Expect the request in the window or in the baseline, never in both.
        Assert.Equal(expectedRecent ? 1 : 0, steam.RecentRequests);
        Assert.Equal(expectedRecent ? 0 : 1, steam.BaselineRequests);
    }

    /// <summary>Checks that a reused minute slot forgets the minute it held an hour earlier.</summary>
    [Fact]
    public void A_minute_slot_reused_an_hour_later_starts_from_zero()
    {
        //  Record a slow request, then a fast one exactly 60 minutes later in the same slot.
        Record("IGDB", 900);
        _time.Advance(TimeSpan.FromMinutes(60));
        Record("IGDB", 100);

        //  Read a 5-minute window.
        var igdb = Assert.Single(_store.GetPerformanceWindows(FiveMinutes));

        //  Expect only the new request in the window, and the old one in the baseline.
        Assert.Equal((1L, 100.0), (igdb.RecentRequests, igdb.RecentAverageResponseTimeMs));
        Assert.Equal((1L, 900.0), (igdb.BaselineRequests, igdb.BaselineAverageResponseTimeMs));
    }

    /// <summary>Checks that a longer window takes in older minutes.</summary>
    [Fact]
    public void A_longer_window_includes_older_minutes()
    {
        //  Record one request, then another 20 minutes later.
        Record("CheapShark", 100);
        _time.Advance(TimeSpan.FromMinutes(20));
        Record("CheapShark", 300);

        //  Read a 60-minute window.
        var cheapShark = Assert.Single(_store.GetPerformanceWindows(TimeSpan.FromMinutes(60)));

        //  Expect both requests in the window and none before it.
        Assert.Equal((2L, 200.0), (cheapShark.RecentRequests, cheapShark.RecentAverageResponseTimeMs));
        Assert.Equal(0, cheapShark.BaselineRequests);
    }

    /// <summary>Checks that sources never called are left out, and the rest come in name order.</summary>
    [Fact]
    public void Lists_only_called_apis_in_name_order()
    {
        //  List two known sources without calls, and call two others.
        _store.GetSnapshot(["IGDB", "Speedrun"]);
        Record("Steam", 100);
        Record("CheapShark", 100);

        //  Read a 5-minute window.
        var windows = _store.GetPerformanceWindows(FiveMinutes);

        //  Expect only the called sources, sorted by name.
        Assert.Equal(["CheapShark", "Steam"], windows.Select(w => w.Api));
    }

    /// <summary>Checks that a window the minute slots cannot cover is refused.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(30)]
    [InlineData(90)]
    [InlineData(3_660)]
    public void Rejects_windows_that_are_not_whole_minutes_from_1_to_60(int seconds)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => _store.GetPerformanceWindows(TimeSpan.FromSeconds(seconds)));
    }
}
