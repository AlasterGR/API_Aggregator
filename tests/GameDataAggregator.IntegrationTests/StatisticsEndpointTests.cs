using System.Net;
using System.Net.Http.Json;
using GameDataAggregator.Api.Contracts;
using GameDataAggregator.Core.Statistics;
using GameDataAggregator.IntegrationTests.Infrastructure;

namespace GameDataAggregator.IntegrationTests;

/// <summary>Tests the statistics and health endpoints end to end.</summary>
public sealed class StatisticsEndpointTests : IAsyncDisposable
{
    private readonly GameDataAggregatorFactory _factory = new();
    private readonly HttpClient _client;

    /// <summary>Starts a fresh app and a client for it that sends a valid token.</summary>
    public StatisticsEndpointTests() => _client = _factory.CreateAuthorizedClient();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>Shuts down the client and the app.</summary>
    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    /// <summary>Reads the statistics, expecting success.</summary>
    private async Task<StatisticsResponse> GetStatisticsAsync()
    {
        var response = await _client.GetAsync("/api/v1/statistics", Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<StatisticsResponse>(ApiJson.Options, Ct))!;
    }

    /// <summary>Checks that every source is listed with three empty speed groups before any search.</summary>
    [Fact]
    public async Task Lists_every_api_with_three_buckets_before_any_request()
    {
        //  Read the statistics straight after start-up.
        var statistics = await GetStatisticsAsync();

        //  Expect every source, with no requests and all three speed groups.
        Assert.Equal(["CheapShark", "IGDB", "RAWG", "Speedrun", "Steam"], statistics.Apis.Select(a => a.Api));
        Assert.All(statistics.Apis, api =>
        {
            Assert.Equal(0, api.TotalRequests);
            Assert.Equal([PerformanceBucket.Fast, PerformanceBucket.Average, PerformanceBucket.Slow], api.Buckets.Select(b => b.Bucket));
        });
    }

    /// <summary>Checks that every real call is counted and saved results are not.</summary>
    [Fact]
    public async Task Counts_each_outgoing_http_request_but_not_cache_hits()
    {
        //  Search twice, the second time from saved results.
        await _client.GetAsync("/api/v1/aggregation?search=witcher", Ct);
        await _client.GetAsync("/api/v1/aggregation?search=witcher", Ct); // cache hit

        //  Read the statistics.
        var statistics = (await GetStatisticsAsync()).Apis.ToDictionary(a => a.Api);

        //  Expect only the first search's calls, all successful and all in a speed group.
        Assert.Equal(1, statistics["RAWG"].TotalRequests);        // games search
        Assert.Equal(2, statistics["CheapShark"].TotalRequests);  // deals + stores
        Assert.Equal(3, statistics["Steam"].TotalRequests);       // store search + 2 player counts
        Assert.Equal(1, statistics["IGDB"].TotalRequests);        // games query; the Twitch token request is not an IGDB request
        Assert.Equal(1, statistics["Speedrun"].TotalRequests);    // games search, relations embedded in the same response
        Assert.All(statistics.Values, api =>
        {
            Assert.Equal(0, api.FailedRequests);
            Assert.Equal(api.TotalRequests, api.Buckets.Sum(b => b.Requests));
        });
    }

    /// <summary>Checks that retries and failures are counted.</summary>
    [Fact]
    public async Task Counts_retries_and_failures()
    {
        //  Make RAWG fail.
        _factory.ExternalApis.Break("api.rawg.io", HttpStatusCode.InternalServerError);

        //  Search RAWG.
        await _client.GetAsync("/api/v1/aggregation?search=witcher&sources=RAWG", Ct);

        //  Expect both the first try and the retry to be counted as failures.
        var rawg = (await GetStatisticsAsync()).Apis.Single(a => a.Api == "RAWG");
        Assert.Equal(2, rawg.TotalRequests); // first attempt + one retry
        Assert.Equal(2, rawg.FailedRequests);
    }

    /// <summary>Checks that the health check reports the app as healthy.</summary>
    [Fact]
    public async Task Health_endpoint_reports_healthy()
    {
        //  Ask for the health check.
        var response = await _client.GetAsync("/health", Ct);

        //  Expect a healthy answer.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync(Ct));
    }
}
