using System.Net;
using GameDataAggregator.Core.Statistics;
using GameDataAggregator.Infrastructure.Authentication.Twitch;
using GameDataAggregator.Infrastructure.Providers.CheapShark;
using GameDataAggregator.Infrastructure.Providers.Igdb;
using GameDataAggregator.Infrastructure.Providers.Steam;
using GameDataAggregator.Tests.Shared;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace GameDataAggregator.UnitTests.Infrastructure;

/// <summary>
/// Tests the sources as the app wires them: real HttpClient pipeline with retries, time limits and circuit
/// breakers, but a fake network.
/// </summary>
public sealed class ProviderWiringTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>Builds the services for one source, with the given settings, on top of a fake network.</summary>
    private static ServiceProvider Build(
        Action<IServiceCollection, IConfiguration> addSource,
        StubHttpMessageHandler network,
        Dictionary<string, string?>? settings = null)
    {
        //  Load the test settings.
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings ?? []).Build();

        //  Add what the source needs from the rest of the app: logging, a clock, statistics and a cache.
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IRequestStatisticsStore>(new InMemoryRequestStatisticsStore(Options.Create(new StatisticsOptions()), TimeProvider.System));
        services.AddHybridCache();

        //  Add the source, and send all of its requests to the fake network.
        addSource(services, configuration);
        services.ConfigureHttpClientDefaults(client => client.ConfigurePrimaryHttpMessageHandler(() => network));
        return services.BuildServiceProvider();
    }

    /// <summary>Checks that a broken store list is tried once per search, stops on its own, and never blocks the deals.</summary>
    [Fact]
    public async Task A_failing_store_list_neither_retries_nor_blocks_the_deals()
    {
        //  Answer deal searches normally and fail every store list request.
        var network = new StubHttpMessageHandler(request => request.RequestUri!.AbsolutePath.EndsWith("/deals", StringComparison.Ordinal)
            ? StubHttpMessageHandler.Json(SampleResponses.CheapSharkDeals)
            : new HttpResponseMessage(HttpStatusCode.InternalServerError));
        using var services = Build((s, c) => s.AddCheapSharkProvider(c), network);
        var provider = services.GetRequiredService<CheapSharkProvider>();

        //  Search eight times, and expect the deals every time.
        for (var i = 0; i < 8; i++)
        {
            Assert.Equal(2, (await provider.FetchAsync("witcher", Ct)).Count);
        }

        //  Expect one store list try per search until its own circuit opens after five failures.
        Assert.Equal(8, network.Requests.Count(uri => uri.AbsolutePath.EndsWith("/deals", StringComparison.Ordinal)));
        Assert.Equal(5, network.Requests.Count(uri => uri.AbsolutePath.EndsWith("/stores", StringComparison.Ordinal)));
    }

    /// <summary>Checks that refused player counts stop on their own, never block the store search, and still count in the statistics.</summary>
    [Fact]
    public async Task Refused_player_counts_neither_retry_nor_block_the_store_search()
    {
        //  Answer store searches normally and refuse every player count as "too many requests".
        var network = new StubHttpMessageHandler(request => request.RequestUri!.Host == "store.steampowered.com"
            ? StubHttpMessageHandler.Json(SampleResponses.SteamStoreSearch)
            : new HttpResponseMessage(HttpStatusCode.TooManyRequests));
        using var services = Build((s, c) => s.AddSteamProvider(c), network, new() { ["ExternalApis:Steam:MaxResults"] = "1" });
        var provider = services.GetRequiredService<SteamProvider>();

        //  Search seven times, and expect a game without a player count every time.
        for (var i = 0; i < 7; i++)
        {
            var game = Assert.Single(await provider.FetchAsync("witcher", Ct));
            Assert.False(game.Metrics.ContainsKey("currentPlayers"));
        }

        //  Expect one count try per search until the side circuit opens after five failures.
        var playerCountRequests = network.Requests.Count(uri => uri.Host == "api.steampowered.com");
        Assert.Equal(5, playerCountRequests);

        //  Expect the side requests in Steam's statistics as failures.
        var steam = Assert.Single(services.GetRequiredService<IRequestStatisticsStore>().GetSnapshot());
        Assert.Equal(7 + 5, steam.TotalRequests);
        Assert.Equal(5, steam.FailedRequests);
    }

    /// <summary>Checks that start-up refuses a token time limit that does not fit inside an IGDB attempt.</summary>
    [Fact]
    public void Start_up_refuses_a_token_time_limit_that_does_not_fit_in_an_attempt()
    {
        //  Set both time limits to five seconds.
        using var services = Build((s, c) => s.AddIgdbProvider(c), new StubHttpMessageHandler(_ => new HttpResponseMessage()), new()
        {
            ["ExternalApis:TwitchAuth:Timeout"] = "00:00:05",
            ["ExternalApis:Igdb:AttemptTimeout"] = "00:00:05",
        });

        //  Expect the IGDB settings to be refused, naming the Twitch setting.
        var error = Assert.Throws<OptionsValidationException>(() => services.GetRequiredService<IOptions<IgdbOptions>>().Value);
        Assert.Contains("TwitchAuth:Timeout", error.Message, StringComparison.Ordinal);
    }

    /// <summary>Checks that the default time limits pass the start-up check.</summary>
    [Fact]
    public void Default_token_time_limit_fits_in_an_attempt()
    {
        //  Build IGDB with its default settings.
        using var services = Build((s, c) => s.AddIgdbProvider(c), new StubHttpMessageHandler(_ => new HttpResponseMessage()));

        //  Expect the settings to load.
        Assert.Equal(TimeSpan.FromSeconds(5), services.GetRequiredService<IOptions<IgdbOptions>>().Value.AttemptTimeout);
    }

    /// <summary>Checks that a failed token request is tried once, leaving retries to the source's own pipeline.</summary>
    [Fact]
    public async Task A_failed_token_request_is_tried_once()
    {
        //  Fail every request, and give the app Twitch credentials.
        var network = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        using var services = Build((s, c) => s.AddIgdbProvider(c), network, new()
        {
            ["ExternalApis:TwitchAuth:ClientId"] = "client-id",
            ["ExternalApis:TwitchAuth:ClientSecret"] = "client-secret",
        });

        //  Expect asking for a token to fail after a single request.
        await Assert.ThrowsAsync<HttpRequestException>(() => services.GetRequiredService<TwitchAccessTokenProvider>().GetTokenAsync(Ct).AsTask());
        Assert.Single(network.Requests);
    }
}
