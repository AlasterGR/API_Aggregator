using System.Net;
using GameDataAggregator.Core.Abstractions;
using GameDataAggregator.Core.Models;
using GameDataAggregator.Infrastructure.Authentication.Twitch;
using GameDataAggregator.Infrastructure.Providers.Igdb;
using GameDataAggregator.Tests.Shared;
using Microsoft.Extensions.Options;

namespace GameDataAggregator.UnitTests.Infrastructure;

/// <summary>Tests the IGDB source against sample answers.</summary>
public sealed class IgdbProviderTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>Creates the source with a fake network and the given client id.</summary>
    private static (IgdbProvider Provider, StubHttpMessageHandler Http) Create(
        string? clientId = "client-id",
        Func<HttpRequestMessage, HttpResponseMessage>? respond = null)
    {
        //  Answer with the sample games unless told otherwise, and build the source on top.
        var http = new StubHttpMessageHandler(respond ?? (_ => StubHttpMessageHandler.Json(SampleResponses.IgdbGames)));
        var options = new IgdbOptions { MaxResults = 5 };
        var auth = new TwitchAuthOptions { ClientId = clientId, ClientSecret = "secret" };
        var client = new HttpClient(http) { BaseAddress = options.BaseUrl };
        return (new IgdbProvider(client, Options.Create(options), Options.Create(auth)), http);
    }

    /// <summary>Checks that the search is sent as an IGDB query with the search text and limit.</summary>
    [Fact]
    public async Task Posts_an_apicalypse_query_with_search_and_limit()
    {
        //  Set up the source.
        var (provider, http) = Create();

        //  Search it.
        await provider.FetchAsync("witcher 3", Ct);

        //  Expect one query to the games address with the search and limit.
        var request = Assert.Single(http.Received);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal(new Uri("https://api.igdb.com/v4/games"), request.Uri);
        Assert.StartsWith("search \"witcher 3\"; fields ", request.Body, StringComparison.Ordinal);
        Assert.EndsWith("limit 5;", request.Body, StringComparison.Ordinal);
    }

    /// <summary>Checks that the search text cannot change the rest of the query.</summary>
    [Theory]
    [InlineData("witcher\"; fields *; limit 500; \"", "search \"witcher ; fields *; limit 500;\";")]
    [InlineData("back\\slash", "search \"back slash\";")]
    public void Search_text_cannot_break_out_of_the_query_string(string search, string expectedStart)
    {
        //  Build a query from tricky text.
        var query = IgdbProvider.BuildQuery(search, 10);

        //  Expect the text to stay inside the search, with the real limit kept.
        Assert.StartsWith(expectedStart, query, StringComparison.Ordinal);
        Assert.EndsWith("limit 10;", query, StringComparison.Ordinal);
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
        Assert.Equal("IGDB", witcher3.Source);
        Assert.Equal(ItemCategory.GameInfo, witcher3.Category);
        Assert.Equal("The Witcher 3: Wild Hunt", witcher3.Title);
        Assert.Equal(new Uri("https://www.igdb.com/games/the-witcher-3-wild-hunt"), witcher3.Url);
        Assert.Equal(new Uri("https://images.igdb.com/igdb/image/upload/t_cover_big/co1wyy.jpg"), witcher3.ImageUrl);
        Assert.Equal(new DateTimeOffset(2015, 5, 19, 0, 0, 0, TimeSpan.Zero), witcher3.Date);
        Assert.Equal(DateKind.Released, witcher3.DateKind);
        Assert.Equal(["Role-playing (RPG)", "Adventure", "Action", "Fantasy"], witcher3.Tags);
        Assert.Equal(93.5, witcher3.Metrics["totalRating"]);
        Assert.Equal(3120, witcher3.Metrics["totalRatingCount"]);
        Assert.Equal(92.7, witcher3.Metrics["criticRating"]);
        Assert.Equal("CD Projekt RED", witcher3.Attributes["developers"]);
        Assert.Equal("CD Projekt, Bandai Namco", witcher3.Attributes["publishers"]);
        Assert.Equal("PC (Microsoft Windows), PlayStation 4", witcher3.Attributes["platforms"]);
    }

    /// <summary>Checks that a game with few details is still handled.</summary>
    [Fact]
    public async Task Handles_missing_optional_fields()
    {
        //  Set up the source.
        var (provider, _) = Create();

        //  Search it and take the game with few details.
        var announced = (await provider.FetchAsync("witcher", Ct))[1];

        //  Expect a link built from its short name and the missing details left empty.
        Assert.Equal(new Uri("https://www.igdb.com/games/the-witcher-iv"), announced.Url); // built from the slug
        Assert.Null(announced.Date);
        Assert.Null(announced.DateKind);
        Assert.Null(announced.ImageUrl);
        Assert.Empty(announced.Tags);
        Assert.Empty(announced.Metrics);
        Assert.Equal(["igdbId"], announced.Attributes.Keys);
    }

    /// <summary>Checks that missing credentials fail with a clear message and nothing is sent.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task Missing_credentials_fail_with_a_clear_message_and_no_request(string? clientId)
    {
        //  Set up the source without a client id.
        var (provider, http) = Create(clientId);

        //  Search it.
        var exception = await Assert.ThrowsAsync<DataProviderException>(() => provider.FetchAsync("witcher", Ct));

        //  Expect the error to name the missing setting, and no request.
        Assert.Contains("TwitchAuth:ClientId", exception.Message, StringComparison.Ordinal);
        Assert.Equal(exception.Message, provider.ConfigurationError);
        Assert.Empty(http.Requests);
    }

    /// <summary>Checks that no settings problem is reported when credentials are set.</summary>
    [Fact]
    public void Reports_no_configuration_error_when_credentials_are_set()
    {
        //  Set up the source with credentials.
        var (provider, _) = Create();

        //  Expect no settings problem.
        Assert.Null(provider.ConfigurationError);
    }

    /// <summary>Checks that an error answer is passed on with its status.</summary>
    [Fact]
    public async Task Error_responses_throw()
    {
        //  Set up the source to answer "too many requests".
        var (provider, _) = Create(respond: _ => new HttpResponseMessage(HttpStatusCode.TooManyRequests));

        //  Search it.
        var exception = await Assert.ThrowsAsync<HttpRequestException>(() => provider.FetchAsync("witcher", Ct));

        //  Expect the error to carry that status.
        Assert.Equal(HttpStatusCode.TooManyRequests, exception.StatusCode);
    }
}
