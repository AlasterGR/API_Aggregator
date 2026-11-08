using System.Net;
using GameDataAggregator.Core.Models;
using GameDataAggregator.Infrastructure.Providers.Steam;
using GameDataAggregator.Tests.Shared;
using GameDataAggregator.UnitTests.TestDoubles;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace GameDataAggregator.UnitTests.Infrastructure;

/// <summary>Tests the Steam source against sample answers.</summary>
public sealed class SteamProviderTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 7, 12, 0, 0, TimeSpan.Zero);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>Creates the source with a fake network, a fixed clock and the given result limit.</summary>
    private static (SteamProvider Provider, StubHttpMessageHandler Http) Create(
        int maxResults = 10,
        HttpStatusCode searchStatus = HttpStatusCode.OK)
    {
        //  Answer store searches with the sample games, and player counts for The Witcher 3 only.
        var http = new StubHttpMessageHandler(request =>
        {
            //  Answer a store search with the sample games.
            var uri = request.RequestUri!;
            if (uri.Host == "store.steampowered.com")
            {
                return StubHttpMessageHandler.Json(SampleResponses.SteamStoreSearch, searchStatus);
            }

            //  Give a player count for The Witcher 3 and "not found" for anything else.
            return uri.Query.Contains("appid=292030", StringComparison.Ordinal)
                ? StubHttpMessageHandler.Json(SampleResponses.SteamPlayerCountWitcher3)
                : StubHttpMessageHandler.Json(SampleResponses.SteamPlayerCountNotFound, HttpStatusCode.NotFound);
        });

        //  Build the source on top of the fake network and clock.
        var provider = new SteamProvider(
            new HttpClient(http),
            new SingleHandlerHttpClientFactory(http),
            Options.Create(new SteamOptions { MaxResults = maxResults }),
            new FakeTimeProvider(Now),
            NullLogger<SteamProvider>.Instance);

        return (provider, http);
    }

    /// <summary>Checks that games are turned into items with their current player counts.</summary>
    [Fact]
    public async Task Maps_games_with_their_current_player_count()
    {
        //  Set up the source.
        var (provider, _) = Create();

        //  Search it.
        var items = await provider.FetchAsync("witcher", Ct);

        //  Expect every item to have a category the source declares.
        Assert.All(items, item => Assert.Contains(item.Category, provider.Categories));

        //  Expect every detail of the first game, dated when the count was taken.
        var witcher3 = items[0];
        Assert.Equal("Steam", witcher3.Source);
        Assert.Equal(ItemCategory.PlayerActivity, witcher3.Category);
        Assert.Equal("The Witcher 3: Wild Hunt", witcher3.Title);
        Assert.Equal(new Uri("https://store.steampowered.com/app/292030"), witcher3.Url);
        Assert.Equal(Now, witcher3.Date);
        Assert.Equal(DateKind.Measured, witcher3.DateKind);
        Assert.Equal(29765, witcher3.Metrics["currentPlayers"]);
        Assert.Equal(12.49, witcher3.Metrics["price"]);
        Assert.Equal(93, witcher3.Metrics["metascore"]);
        Assert.Equal(["Windows"], witcher3.Tags);
        Assert.Equal("USD", witcher3.Attributes["currency"]);
    }

    /// <summary>Checks that bundles and other non-games are left out.</summary>
    [Fact]
    public async Task Ignores_bundles_and_other_non_game_results()
    {
        //  Set up the source.
        var (provider, http) = Create();

        //  Search it.
        var items = await provider.FetchAsync("witcher", Ct);

        //  Expect only the two games, and no player count request for the bundle.
        Assert.Equal(["The Witcher 3: Wild Hunt", "The Witcher 2: Assassins of Kings Enhanced Edition"], items.Select(i => i.Title));
        Assert.DoesNotContain(http.Requests, uri => uri.Query.Contains("appid=9999", StringComparison.Ordinal));
    }

    /// <summary>Checks that a game without player figures is kept, just without a count.</summary>
    [Fact]
    public async Task Keeps_a_game_without_player_statistics_but_omits_the_metric()
    {
        //  Set up the source.
        var (provider, _) = Create();

        //  Search it and take the game without player figures.
        var witcher2 = (await provider.FetchAsync("witcher", Ct))[1];

        //  Expect no count or score, but its systems still listed.
        Assert.False(witcher2.Metrics.ContainsKey("currentPlayers"));
        Assert.False(witcher2.Metrics.ContainsKey("metascore"));
        Assert.Equal(["Windows", "macOS", "Linux"], witcher2.Tags);
    }

    /// <summary>Checks that the result limit is respected.</summary>
    [Fact]
    public async Task Respects_max_results()
    {
        //  Set up the source with a limit of one game.
        var (provider, http) = Create(maxResults: 1);

        //  Search it.
        var items = await provider.FetchAsync("witcher", Ct);

        //  Expect one game and only its player count to be fetched.
        Assert.Single(items);
        Assert.Equal(2, http.Requests.Count); // one search + one player count
    }

    /// <summary>Checks that a failed store search is passed on as an error.</summary>
    [Fact]
    public async Task Throws_when_the_store_search_fails()
    {
        //  Set up the source with the store search failing.
        var (provider, _) = Create(searchStatus: HttpStatusCode.ServiceUnavailable);

        //  Expect the search to fail.
        await Assert.ThrowsAsync<HttpRequestException>(() => provider.FetchAsync("witcher", Ct));
    }
}
