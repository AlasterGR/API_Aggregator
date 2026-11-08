using System.Collections.Concurrent;
using System.Net;
using GameDataAggregator.Tests.Shared;

namespace GameDataAggregator.IntegrationTests.Infrastructure;

/// <summary>
/// Impersonates RAWG, CheapShark, Steam, IGDB, speedrun.com and the Twitch token endpoint at the HTTP level, so the whole application (HttpClient
/// pipeline, resilience, statistics, caching) runs for real without touching the network.
/// </summary>
public sealed class FakeExternalApis
{
    private readonly ConcurrentDictionary<string, HttpStatusCode> _forcedStatus = new(StringComparer.OrdinalIgnoreCase);

    public ConcurrentQueue<Uri> Requests { get; } = new();

    /// <summary>Makes every request to <paramref name="host"/> return <paramref name="status"/>.</summary>
    public void Break(string host, HttpStatusCode status) => _forcedStatus[host] = status;

    /// <summary>Creates the fake network connection the app sends its requests through.</summary>
    public HttpMessageHandler CreateHandler() => new Handler(this);

    /// <summary>Answers a request the way the real source would.</summary>
    private HttpResponseMessage Respond(HttpRequestMessage request)
    {
        //  Note every request so tests can check what was called.
        var uri = request.RequestUri!;
        Requests.Enqueue(uri);

        //  Answer with the forced error when the source was made to fail.
        if (_forcedStatus.TryGetValue(uri.Host, out var forced))
        {
            return new HttpResponseMessage(forced);
        }

        //  Return the sample answer that matches the address.
        return (uri.Host, uri.AbsolutePath) switch
        {
            ("api.rawg.io", "/api/games") => StubHttpMessageHandler.Json(SampleResponses.RawgGames),
            ("www.cheapshark.com", "/api/1.0/deals") => StubHttpMessageHandler.Json(SampleResponses.CheapSharkDeals),
            ("www.cheapshark.com", "/api/1.0/stores") => StubHttpMessageHandler.Json(SampleResponses.CheapSharkStores),
            ("store.steampowered.com", "/api/storesearch/") => StubHttpMessageHandler.Json(SampleResponses.SteamStoreSearch),
            ("api.steampowered.com", _) when uri.Query.Contains("appid=292030", StringComparison.Ordinal)
                => StubHttpMessageHandler.Json(SampleResponses.SteamPlayerCountWitcher3),
            ("api.steampowered.com", _) => StubHttpMessageHandler.Json(SampleResponses.SteamPlayerCountNotFound, HttpStatusCode.NotFound),
            ("id.twitch.tv", "/oauth2/token") when request.Method == HttpMethod.Post => StubHttpMessageHandler.Json(SampleResponses.TwitchToken),

            //  Like the real IGDB, give data only to signed-in requests.
            ("api.igdb.com", "/v4/games") when IsAuthorizedForIgdb(request) => StubHttpMessageHandler.Json(SampleResponses.IgdbGames),
            ("api.igdb.com", _) => new HttpResponseMessage(HttpStatusCode.Unauthorized),
            ("www.speedrun.com", "/api/v1/games") => StubHttpMessageHandler.Json(SampleResponses.SpeedrunGames),
            _ => new HttpResponseMessage(HttpStatusCode.NotFound),
        };
    }

    /// <summary>Checks that an IGDB request carries the expected token and client id.</summary>
    private static bool IsAuthorizedForIgdb(HttpRequestMessage request) =>
        request.Method == HttpMethod.Post &&
        request.Headers.Authorization is { Scheme: "Bearer", Parameter: "test-access-token" } &&
        request.Headers.TryGetValues("Client-ID", out var clientId) && clientId.Single() == GameDataAggregatorFactory.TwitchClientId;

    /// <summary>Hands every request to the fake sources instead of the network.</summary>
    private sealed class Handler(FakeExternalApis apis) : HttpMessageHandler
    {
        /// <summary>Answers the request from the fake sources.</summary>
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(apis.Respond(request));
    }
}
