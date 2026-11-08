using System.Net;
using GameDataAggregator.Core.Abstractions;
using GameDataAggregator.Core.Models;
using GameDataAggregator.Infrastructure.Providers.Speedrun;
using GameDataAggregator.Tests.Shared;
using Microsoft.Extensions.Options;

namespace GameDataAggregator.UnitTests.Infrastructure;

/// <summary>Tests the speedrun.com source against sample answers.</summary>
public sealed class SpeedrunProviderTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>Creates the source with a fake network.</summary>
    private static (SpeedrunProvider Provider, StubHttpMessageHandler Http) Create(
        Func<HttpRequestMessage, HttpResponseMessage>? respond = null)
    {
        //  Answer with the sample games unless told otherwise, and build the source on top.
        var http = new StubHttpMessageHandler(respond ?? (_ => StubHttpMessageHandler.Json(SampleResponses.SpeedrunGames)));
        var options = new SpeedrunOptions { MaxResults = 5 };
        var client = new HttpClient(http) { BaseAddress = options.BaseUrl };
        return (new SpeedrunProvider(client, Options.Create(options)), http);
    }

    /// <summary>Checks that the search asks for the name, the limit and the extra details in one request.</summary>
    [Fact]
    public async Task Searches_by_name_with_limit_and_embedded_relations()
    {
        //  Set up the source.
        var (provider, http) = Create();

        //  Search it.
        await provider.FetchAsync("witcher 3", Ct);

        //  Expect one request to the exact search address.
        var request = Assert.Single(http.Received);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal(
            new Uri("https://www.speedrun.com/api/v1/games?name=witcher%203&max=5&embed=platforms,genres,developers,publishers,categories"),
            request.Uri);
    }

    /// <summary>Checks that the source needs no settings.</summary>
    [Fact]
    public void Needs_no_configuration()
    {
        //  Look at the source the way the search service does, which is the only way this check is visible.
        IGameDataProvider provider = Create().Provider;

        //  Expect no settings problem.
        Assert.Null(provider.ConfigurationError);
    }

    /// <summary>Checks that games are turned into items with all their details.</summary>
    [Fact]
    public async Task Maps_games_to_aggregated_items()
    {
        //  Set up the source.
        var (provider, _) = Create();

        //  Search it.
        var items = await provider.FetchAsync("witcher", Ct);

        //  Expect the first game's basic details to be copied across.
        Assert.Equal(2, items.Count);
        var witcher3 = items[0];
        Assert.Equal("Speedrun", witcher3.Source);
        Assert.Equal(ItemCategory.PlayerActivity, witcher3.Category);
        Assert.Equal("The Witcher 3: Wild Hunt", witcher3.Title);
        Assert.Equal(new Uri("https://www.speedrun.com/the_witcher_3_wild_hunt"), witcher3.Url);
        Assert.Equal(new Uri("https://www.speedrun.com/static/game/k6qre01g/cover?v=baba761"), witcher3.ImageUrl);
        Assert.Equal(new DateTimeOffset(2015, 5, 18, 0, 0, 0, TimeSpan.Zero), witcher3.Date);
        Assert.Equal(DateKind.Released, witcher3.DateKind); // a release date, although the category is PlayerActivity
        Assert.Equal(["PC", "PlayStation 4", "Action RPG"], witcher3.Tags);

        //  Expect only main full-game categories to be counted and listed, leaving out level and side ones.
        Assert.Equal(2, witcher3.Metrics["speedrunCategories"]);
        Assert.Equal(1, witcher3.Metrics["levelCategories"]);
        Assert.Equal(3, witcher3.Metrics["moderators"]);
        Assert.Equal("Any%, 100%", witcher3.Attributes["categories"]);
        Assert.Equal("CD Projekt RED", witcher3.Attributes["developers"]);
        Assert.False(witcher3.Attributes.ContainsKey("publishers")); // empty list
        Assert.Equal("k6qre01g", witcher3.Attributes["speedrunId"]);
    }

    /// <summary>Checks that a game with few details is still handled.</summary>
    [Fact]
    public async Task Handles_missing_optional_fields()
    {
        //  Set up the source.
        var (provider, _) = Create();

        //  Search it and take the game with few details.
        var minimal = (await provider.FetchAsync("witcher", Ct))[1];

        //  Expect the missing details to be left empty and the counts to be zero.
        Assert.Equal("The Witcher", minimal.Title);
        Assert.Null(minimal.Url);
        Assert.Null(minimal.ImageUrl);
        Assert.Null(minimal.Date);
        Assert.Null(minimal.DateKind);
        Assert.Empty(minimal.Tags);
        Assert.Equal(0, minimal.Metrics["speedrunCategories"]);
        Assert.Equal(0, minimal.Metrics["moderators"]);
        Assert.Equal(["speedrunId"], minimal.Attributes.Keys);
    }

    /// <summary>Checks that an error answer is passed on with its status.</summary>
    [Fact]
    public async Task Error_responses_throw()
    {
        //  Set up the source to answer "unavailable".
        var (provider, _) = Create(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));

        //  Search it.
        var exception = await Assert.ThrowsAsync<HttpRequestException>(() => provider.FetchAsync("witcher", Ct));

        //  Expect the error to carry that status.
        Assert.Equal(HttpStatusCode.ServiceUnavailable, exception.StatusCode);
    }
}
