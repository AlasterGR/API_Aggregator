using System.ComponentModel.DataAnnotations;
using GameDataAggregator.Core.Statistics;
using Microsoft.Extensions.Options;

namespace GameDataAggregator.UnitTests.Statistics;

/// <summary>Tests how request figures are counted and grouped by speed.</summary>
public sealed class InMemoryRequestStatisticsStoreTests
{
    /// <summary>Creates a store with the given speed limits.</summary>
    private static InMemoryRequestStatisticsStore NewStore(int fast = 100, int slow = 200) =>
        new(Options.Create(new StatisticsOptions { FastThresholdMs = fast, SlowThresholdMs = slow }), TimeProvider.System);

    /// <summary>Turns milliseconds into a duration.</summary>
    private static TimeSpan Ms(double milliseconds) => TimeSpan.FromMilliseconds(milliseconds);

    /// <summary>Checks which speed group each duration falls into, including the edges.</summary>
    [Theory]
    [InlineData(0, PerformanceBucket.Fast)]
    [InlineData(99.9, PerformanceBucket.Fast)]
    [InlineData(100, PerformanceBucket.Average)]
    [InlineData(200, PerformanceBucket.Average)]
    [InlineData(200.1, PerformanceBucket.Slow)]
    [InlineData(5_000, PerformanceBucket.Slow)]
    public void Classifies_durations_into_buckets(double milliseconds, PerformanceBucket expected)
    {
        Assert.Equal(expected, NewStore().Classify(Ms(milliseconds)));
    }

    /// <summary>Checks that configured speed limits are used.</summary>
    [Fact]
    public void Uses_configured_thresholds()
    {
        //  Set wider limits.
        var store = NewStore(fast: 50, slow: 500);

        //  Expect durations inside them to count as average.
        Assert.Equal(PerformanceBucket.Average, store.Classify(Ms(60)));
        Assert.Equal(PerformanceBucket.Average, store.Classify(Ms(450)));
    }

    /// <summary>Checks the totals and averages, overall and per speed group.</summary>
    [Fact]
    public void Computes_totals_and_averages_overall_and_per_bucket()
    {
        //  Record four requests across all groups, one of them failed.
        var store = NewStore();
        store.Record("RAWG", Ms(50), isSuccess: true);
        store.Record("RAWG", Ms(70), isSuccess: true);
        store.Record("RAWG", Ms(150), isSuccess: true);
        store.Record("RAWG", Ms(330), isSuccess: false);

        //  Read the figures.
        var rawg = Assert.Single(store.GetSnapshot());

        //  Expect the right overall totals.
        Assert.Equal("RAWG", rawg.Api);
        Assert.Equal(4, rawg.TotalRequests);
        Assert.Equal(1, rawg.FailedRequests);
        Assert.Equal(150, rawg.AverageResponseTimeMs);

        //  Expect the right count, average and range in each group.
        var buckets = rawg.Buckets.ToDictionary(b => b.Bucket);
        Assert.Equal((2L, 60.0), (buckets[PerformanceBucket.Fast].Requests, buckets[PerformanceBucket.Fast].AverageResponseTimeMs));
        Assert.Equal((1L, 150.0), (buckets[PerformanceBucket.Average].Requests, buckets[PerformanceBucket.Average].AverageResponseTimeMs));
        Assert.Equal((1L, 330.0), (buckets[PerformanceBucket.Slow].Requests, buckets[PerformanceBucket.Slow].AverageResponseTimeMs));
        Assert.Equal("< 100 ms", buckets[PerformanceBucket.Fast].Range);
    }

    /// <summary>Checks that sources are kept apart, and names match whatever the letter case.</summary>
    [Fact]
    public void Keeps_apis_separate_and_matches_names_case_insensitively()
    {
        //  Record requests for Steam in two spellings, and one for CheapShark.
        var store = NewStore();
        store.Record("Steam", Ms(10), isSuccess: true);
        store.Record("steam", Ms(10), isSuccess: true);
        store.Record("CheapShark", Ms(10), isSuccess: true);

        //  Read the figures.
        var snapshot = store.GetSnapshot();

        //  Expect two sources in name order, with both Steam requests together.
        Assert.Equal(["CheapShark", "Steam"], snapshot.Select(s => s.Api));
        Assert.Equal(2, snapshot.Single(s => s.Api == "Steam").TotalRequests);
    }

    /// <summary>Checks that known sources without requests are listed with zero counts.</summary>
    [Fact]
    public void Known_apis_without_requests_are_reported_with_zero_counts()
    {
        //  Read the figures for a known source that was never called.
        var snapshot = NewStore().GetSnapshot(["RAWG"]);

        //  Expect it listed with zeros in every group.
        var rawg = Assert.Single(snapshot);
        Assert.Equal(0, rawg.TotalRequests);
        Assert.Equal(0, rawg.AverageResponseTimeMs);
        Assert.Equal(3, rawg.Buckets.Count);
        Assert.All(rawg.Buckets, b => Assert.Equal(0, b.Requests));
    }

    /// <summary>Checks that no request is lost when many are recorded at once.</summary>
    [Fact]
    public async Task Concurrent_records_are_never_lost()
    {
        //  Plan many writers spread over a few sources.
        const int apis = 4;
        const int recordsPerTask = 2_500;
        const int tasks = 16;
        var store = NewStore();
        var ct = TestContext.Current.CancellationToken;

        //  Record from many threads at once, every tenth request failing.
        var writers = Enumerable.Range(0, tasks).Select(t => Task.Run(
            () =>
            {
                for (var i = 0; i < recordsPerTask; i++)
                {
                    store.Record($"api-{(t + i) % apis}", Ms(i % 3 * 100 + 50), isSuccess: i % 10 != 0);
                }
            },
            ct)).ToList();

        //  Read the figures over and over meanwhile.
        var reader = Task.Run(() =>
        {
            for (var i = 0; i < 200; i++)
            {
                foreach (var s in store.GetSnapshot())
                {
                    //  Expect every reading to add up, never showing half an update.
                    Assert.Equal(s.TotalRequests, s.Buckets.Sum(b => b.Requests));
                }
            }
        }, ct);

        //  Wait for everyone to finish.
        await Task.WhenAll([.. writers, reader]);

        //  Expect every request and every failure to be counted.
        var snapshot = store.GetSnapshot();
        Assert.Equal(tasks * recordsPerTask, snapshot.Sum(s => s.TotalRequests));
        Assert.Equal(tasks * recordsPerTask / 10, snapshot.Sum(s => s.FailedRequests));
    }

    /// <summary>Checks that a blank source name is refused.</summary>
    [Fact]
    public void Rejects_empty_api_name()
    {
        Assert.ThrowsAny<ArgumentException>(() => NewStore().Record(" ", Ms(1), isSuccess: true));
    }

    /// <summary>Checks that the slow limit must be above the fast limit.</summary>
    [Theory]
    [InlineData(100, 200, true)]
    [InlineData(200, 200, false)]
    [InlineData(300, 200, false)]
    public void Options_require_slow_threshold_above_fast_threshold(int fast, int slow, bool expectedValid)
    {
        //  Set the two limits.
        var options = new StatisticsOptions { FastThresholdMs = fast, SlowThresholdMs = slow };

        //  Run the settings checks.
        var isValid = Validator.TryValidateObject(options, new ValidationContext(options), [], validateAllProperties: true);

        //  Expect them to pass only when slow is above fast.
        Assert.Equal(expectedValid, isValid);
    }
}
