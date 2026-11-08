using System.Net;
using GameDataAggregator.Core.Abstractions;
using GameDataAggregator.Core.Models;
using GameDataAggregator.Infrastructure.Providers.Rawg;
using GameDataAggregator.Tests.Shared;
using Microsoft.Extensions.Options;

namespace GameDataAggregator.UnitTests.Infrastructure;

/// <summary>Tests the RAWG source against sample answers.</summary>
public sealed class RawgProviderTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>Creates the source with a fake network and the given key.</summary>
    private static (RawgProvider Provider, StubHttpMessageHandler Http) Create(
        string? apiKey = "test-key",
        Func<HttpRequestMessage, HttpResponseMessage>? respond = null)
    {
        //  Answer with the sample games unless told otherwise, and build the source on top.
        var http = new StubHttpMessageHandler(respond ?? (_ => StubHttpMessageHandler.Json(SampleResponses.RawgGames)));
        var options = new RawgOptions { ApiKey = apiKey, MaxResults = 5 };
        var client = new HttpClient(http) { BaseAddress = options.BaseUrl };
        return (new RawgProvider(client, Options.Create(options)), http);
    }

    /// <summary>Checks that the search, key and result limit are sent.</summary>
    [Fact]
    public async Task Sends_search_key_and_page_size()
    {
        //  Set up the source.
        var (provider, http) = Create();

        //  Search it.
        await provider.FetchAsync("witcher 3", Ct);

        //  Expect one request to the games address with all three values.
        var uri = Assert.Single(http.Requests);
        Assert.Equal("/api/games", uri.AbsolutePath);
        Assert.Contains("key=test-key", uri.Query, StringComparison.Ordinal);
        Assert.Contains("search=witcher%203", uri.Query, StringComparison.Ordinal);
        Assert.Contains("page_size=5", uri.Query, StringComparison.Ordinal);
    }

    /// <summary>Checks that games are turned into items with all their details.</summary>
    [Fact]
    public async Task Maps_games_to_aggregated_items()
    {
        //  Set up the source.
        var (provider, _) = Create();

        //  Search it.
        var items = await provider.FetchAsync("witcher", Ct);

        //  Expect every detail of the first game to be copied across.
        Assert.Equal(2, items.Count);
        var witcher3 = items[0];
        Assert.Equal("RAWG", witcher3.Source);
        Assert.Equal(ItemCategory.GameInfo, witcher3.Category);
        Assert.Equal("The Witcher 3: Wild Hunt", witcher3.Title);
        Assert.Equal(new Uri("https://rawg.io/games/the-witcher-3-wild-hunt"), witcher3.Url);
        Assert.Equal(new DateTimeOffset(2015, 5, 18, 0, 0, 0, TimeSpan.Zero), witcher3.Date);
        Assert.Equal(DateKind.Released, witcher3.DateKind);
        Assert.Equal(["Action", "RPG"], witcher3.Tags);
        Assert.Equal(4.65, witcher3.Metrics["rating"]);
        Assert.Equal(92, witcher3.Metrics["metacritic"]);
        Assert.Equal("PC, PlayStation 5", witcher3.Attributes["platforms"]);
    }

    /// <summary>Checks that a game with few details is still handled.</summary>
    [Fact]
    public async Task Handles_missing_optional_fields()
    {
        //  Set up the source.
        var (provider, _) = Create();

        //  Search it and take the unreleased game.
        var unreleased = (await provider.FetchAsync("witcher", Ct))[1];

        //  Expect the missing details to be left empty.
        Assert.Null(unreleased.Date);
        Assert.Null(unreleased.DateKind);
        Assert.Null(unreleased.ImageUrl);
        Assert.Empty(unreleased.Tags);
        Assert.False(unreleased.Metrics.ContainsKey("metacritic"));
        Assert.False(unreleased.Attributes.ContainsKey("platforms"));
    }

    /// <summary>Checks that a missing key fails with a clear message and nothing is sent.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task Missing_api_key_fails_with_a_clear_message_and_no_request(string? apiKey)
    {
        //  Set up the source without a usable key.
        var (provider, http) = Create(apiKey);

        //  Search it.
        var exception = await Assert.ThrowsAsync<DataProviderException>(() => provider.FetchAsync("witcher", Ct));

        //  Expect the error to mention the key, and no request.
        Assert.Contains("API key", exception.Message, StringComparison.Ordinal);
        Assert.Equal(exception.Message, provider.ConfigurationError);
        Assert.Empty(http.Requests);
    }

    /// <summary>Checks that no settings problem is reported when a key is set.</summary>
    [Fact]
    public void Reports_no_configuration_error_when_an_api_key_is_set()
    {
        //  Set up the source with a key.
        var (provider, _) = Create();

        //  Expect no settings problem.
        Assert.Null(provider.ConfigurationError);
    }

    /// <summary>Checks that an error answer is passed on with its status.</summary>
    [Fact]
    public async Task Error_responses_throw()
    {
        //  Set up the source to refuse the request.
        var (provider, _) = Create(respond: _ => new HttpResponseMessage(HttpStatusCode.Unauthorized));

        //  Search it.
        var exception = await Assert.ThrowsAsync<HttpRequestException>(() => provider.FetchAsync("witcher", Ct));

        //  Expect the error to carry that status.
        Assert.Equal(HttpStatusCode.Unauthorized, exception.StatusCode);
    }
}
