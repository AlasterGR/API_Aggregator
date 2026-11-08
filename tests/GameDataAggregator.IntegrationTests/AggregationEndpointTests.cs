using System.Net;
using System.Net.Http.Json;
using GameDataAggregator.Core.Models;
using GameDataAggregator.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace GameDataAggregator.IntegrationTests;

/// <summary>Tests the search endpoint end to end; each test gets its own app, so no cache or statistics are shared.</summary>
public sealed class AggregationEndpointTests : IAsyncDisposable
{
    private readonly GameDataAggregatorFactory _factory = new();
    private readonly HttpClient _client;

    /// <summary>Starts a fresh app and a client for it that sends a valid token.</summary>
    public AggregationEndpointTests() => _client = _factory.CreateAuthorizedClient();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>Shuts down the client and the app.</summary>
    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    /// <summary>Runs a search and returns its result, expecting success.</summary>
    private async Task<AggregationResult> GetResultAsync(string query)
    {
        var response = await _client.GetAsync($"/api/v1/aggregation?{query}", Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<AggregationResult>(ApiJson.Options, Ct))!;
    }

    /// <summary>Checks that one search returns items from every source.</summary>
    [Fact]
    public async Task Returns_items_from_all_five_apis()
    {
        //  Search every source.
        var result = await GetResultAsync("search=witcher&pageSize=100");

        //  Expect each source to answer live with its own kind of item.
        Assert.Equal(["RAWG", "CheapShark", "Steam", "IGDB", "Speedrun"], result.Sources.Select(s => s.Source));
        Assert.All(result.Sources, s => Assert.Equal(SourceStatus.Live, s.Status));
        Assert.Equal(10, result.TotalCount); // 2 each from RAWG, CheapShark, Steam, IGDB and speedrun.com
        Assert.Contains(result.Items, i => i is { Source: "RAWG", Category: ItemCategory.GameInfo });
        Assert.Contains(result.Items, i => i is { Source: "CheapShark", Category: ItemCategory.Deal });
        Assert.Contains(result.Items, i => i is { Source: "Steam", Category: ItemCategory.PlayerActivity });
        Assert.Contains(result.Items, i => i is { Source: "IGDB", Category: ItemCategory.GameInfo });
        Assert.Contains(result.Items, i => i is { Source: "Speedrun", Category: ItemCategory.PlayerActivity });
    }

    /// <summary>Checks that IGDB signs in once and reuses the token.</summary>
    [Fact]
    public async Task Igdb_gets_one_twitch_token_and_reuses_it()
    {
        //  Search IGDB twice.
        await GetResultAsync("search=witcher&sources=IGDB");
        await GetResultAsync("search=elden ring&sources=IGDB"); // different search: not a cache hit

        //  Expect one sign-in and two searches.
        Assert.Single(_factory.ExternalApis.Requests, uri => uri.Host == "id.twitch.tv");
        Assert.Equal(2, _factory.ExternalApis.Requests.Count(uri => uri.Host == "api.igdb.com"));
    }

    /// <summary>Checks that IGDB without credentials is reported as not configured and never contacted.</summary>
    [Fact]
    public async Task Igdb_without_credentials_is_reported_as_not_configured_and_never_called()
    {
        //  Start the app without the Twitch client id.
        using var factory = _factory.WithWebHostBuilder(builder => builder.UseSetting("ExternalApis:TwitchAuth:ClientId", ""));
        using var client = factory.CreateAuthorizedClient();

        //  Search every source.
        var result = await client.GetFromJsonAsync<AggregationResult>("/api/v1/aggregation?search=witcher", ApiJson.Options, Ct);

        //  Expect IGDB to explain what is missing, the others to answer, and Twitch and IGDB to be left alone.
        var igdb = result!.Sources.Single(s => s.Source == "IGDB");
        Assert.Equal(SourceStatus.NotConfigured, igdb.Status);
        Assert.Contains("Twitch application credentials", igdb.Error, StringComparison.Ordinal);
        Assert.All(result.Sources.Where(s => s.Source != "IGDB"), s => Assert.Equal(SourceStatus.Live, s.Status));
        Assert.DoesNotContain(_factory.ExternalApis.Requests, uri => uri.Host is "id.twitch.tv" or "api.igdb.com");
    }

    /// <summary>Checks that repeating a search uses saved results instead of calling the sources.</summary>
    [Fact]
    public async Task Repeated_request_is_served_from_cache_without_calling_the_apis()
    {
        //  Search once and count the calls made.
        await GetResultAsync("search=witcher");
        var requestsAfterFirstCall = _factory.ExternalApis.Requests.Count;

        //  Repeat the search in different letter case and order.
        var second = await GetResultAsync("search=WITCHER&sortBy=Title");

        //  Expect saved results and no new calls.
        Assert.All(second.Sources, s => Assert.Equal(SourceStatus.Cached, s.Status));
        Assert.Equal(requestsAfterFirstCall, _factory.ExternalApis.Requests.Count);
    }

    /// <summary>Checks that filters and sorting apply across all sources together.</summary>
    [Fact]
    public async Task Filters_and_sorts_across_sources()
    {
        //  Ask for deals and game info, sorted by title.
        var result = await GetResultAsync("search=witcher&categories=Deal,GameInfo&sortBy=Title&sortDirection=Ascending");

        //  Expect only those categories, in title order.
        Assert.Equal(6, result.TotalCount);
        Assert.All(result.Items, i => Assert.NotEqual(ItemCategory.PlayerActivity, i.Category));
        Assert.Equal(result.Items.Select(i => i.Title).Order(StringComparer.OrdinalIgnoreCase), result.Items.Select(i => i.Title));
    }

    /// <summary>Checks filtering by tag and dates together.</summary>
    [Fact]
    public async Task Filters_by_tag_and_date_range()
    {
        //  Ask for role-playing games dated in 2015.
        var result = await GetResultAsync("search=witcher&tag=rpg&from=2015-01-01&to=2015-12-31");

        //  Expect only The Witcher 3 from RAWG.
        var item = Assert.Single(result.Items);
        Assert.Equal("The Witcher 3: Wild Hunt", item.Title);
        Assert.Equal("RAWG", item.Source);
    }

    /// <summary>Checks that only the chosen sources are contacted.</summary>
    [Fact]
    public async Task Only_queries_the_requested_sources()
    {
        //  Search Steam only.
        var result = await GetResultAsync("search=witcher&sources=steam");

        //  Expect only Steam in the answer, and no calls to the other sources.
        Assert.Equal("Steam", Assert.Single(result.Sources).Source);
        Assert.DoesNotContain(_factory.ExternalApis.Requests, uri => uri.Host is "api.rawg.io" or "www.cheapshark.com" or "api.igdb.com" or "id.twitch.tv" or "www.speedrun.com");
    }

    /// <summary>Checks that one source being down still leaves a useful answer.</summary>
    [Fact]
    public async Task An_unavailable_api_degrades_the_response_instead_of_failing_it()
    {
        //  Make CheapShark unavailable.
        _factory.ExternalApis.Break("www.cheapshark.com", HttpStatusCode.ServiceUnavailable);

        //  Search every source.
        var result = await GetResultAsync("search=witcher");

        //  Expect CheapShark to be reported as failed while the others still answer.
        var cheapShark = result.Sources.Single(s => s.Source == "CheapShark");
        Assert.Equal(SourceStatus.Failed, cheapShark.Status);
        Assert.Equal("The external API responded with HTTP 503.", cheapShark.Error);
        Assert.DoesNotContain(result.Items, i => i.Source == "CheapShark");
        Assert.Contains(result.Items, i => i.Source == "RAWG");
        Assert.Contains(result.Items, i => i.Source == "Steam");

        //  Expect one retry before giving up.
        Assert.Equal(2, _factory.ExternalApis.Requests.Count(uri => uri.AbsolutePath.EndsWith("/deals", StringComparison.Ordinal)));
    }

    /// <summary>Checks that each kind of invalid input is refused with a clear error for the right field.</summary>
    [Theory]
    [InlineData("", "Search")]
    [InlineData("search=a", "Search")]
    [InlineData("search=%20a%20", "Search")] // the length is checked after trimming
    [InlineData("search=witcher&pageSize=500", "PageSize")]
    [InlineData("search=witcher&page=0", "Page")]
    [InlineData("search=witcher&sortBy=Popularity", "sortBy")]
    [InlineData("search=witcher&sortBy=1", "sortBy")] // names only, no numbers
    [InlineData("search=witcher&sortDirection=0", "sortDirection")]
    [InlineData("search=witcher&categories=1", "Categories")]
    [InlineData("search=witcher&from=yesterday", "from")]
    [InlineData("search=witcher&sources=Twitter", "Sources")]
    [InlineData("search=witcher&categories=Music", "Categories")]
    [InlineData("search=witcher&from=2025-01-01&to=2024-01-01", "From")]
    public async Task Invalid_parameters_return_problem_details(string query, string expectedErrorKey)
    {
        //  Send the invalid search.
        var response = await _client.GetAsync($"/api/v1/aggregation?{query}", Ct);

        //  Expect a standard error answer naming the field.
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(Ct);
        Assert.Contains(problem!.Errors.Keys, key => key.Equals(expectedErrorKey, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Checks that all field mistakes come back in a single answer.</summary>
    [Fact]
    public async Task All_field_errors_are_reported_in_one_response()
    {
        //  Send a search with five invalid fields.
        var response = await _client.GetAsync(
            "/api/v1/aggregation?search=a&sources=Twitter&categories=Music&sortBy=Popularity&page=0", Ct);

        //  Expect every one of them in the same error answer.
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(Ct);
        string[] expected = ["Search", "Sources", "Categories", "SortBy", "Page"];
        Assert.All(expected, key => Assert.Contains(problem!.Errors.Keys, k => k.Equals(key, StringComparison.OrdinalIgnoreCase)));
    }

    /// <summary>Checks that dates without a time zone are read as UTC, whatever the server's zone.</summary>
    [Fact]
    public async Task Dates_without_an_offset_are_read_as_utc()
    {
        //  Ask for items dated on The Witcher 3's release day, which only includes it when the date is read as UTC.
        var result = await GetResultAsync("search=witcher&sources=RAWG&from=2015-05-18&to=2015-05-18");

        //  Expect the game to be found.
        Assert.Equal("The Witcher 3: Wild Hunt", Assert.Single(result.Items).Title);
    }

    /// <summary>Checks that searches are limited per client while other endpoints are not.</summary>
    [Fact]
    public async Task Searches_are_rate_limited_per_client_and_other_endpoints_are_not()
    {
        //  Start the app with a limit of two searches.
        using var factory = _factory.WithWebHostBuilder(builder => builder.UseSetting("RateLimiting:PermitLimit", "2"));
        using var client = factory.CreateAuthorizedClient();

        //  Search three times.
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/aggregation?search=witcher", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/aggregation?search=elden ring", Ct)).StatusCode);
        var rejected = await client.GetAsync("/api/v1/aggregation?search=celeste", Ct);

        //  Expect the third to be refused with a wait time, while statistics still answer.
        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
        Assert.Equal("application/problem+json", rejected.Content.Headers.ContentType?.MediaType);
        Assert.True(rejected.Headers.RetryAfter?.Delta > TimeSpan.Zero, "Retry-After should tell the client when to try again");
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/statistics", Ct)).StatusCode);
    }
}
