using System.Net;
using GameDataAggregator.Core.Abstractions;
using GameDataAggregator.Infrastructure.Authentication.Twitch;
using GameDataAggregator.Tests.Shared;
using GameDataAggregator.UnitTests.TestDoubles;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace GameDataAggregator.UnitTests.Infrastructure;

/// <summary>Tests the Twitch token service and the step that signs requests.</summary>
public sealed class TwitchAuthenticationTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
    private int _tokensIssued;

    /// <summary>Creates Twitch settings with the given client id.</summary>
    private static TwitchAuthOptions Credentials(string? clientId = "client-id") =>
        new() { ClientId = clientId, ClientSecret = "client-secret" };

    /// <summary>A token endpoint that issues "token-1", "token-2"... each valid for one hour.</summary>
    private HttpResponseMessage IssueToken(HttpRequestMessage request) =>
        StubHttpMessageHandler.Json(
            $$"""{"access_token":"token-{{Interlocked.Increment(ref _tokensIssued)}}","expires_in":3600,"token_type":"bearer"}""");

    /// <summary>Creates the token service with a fake token endpoint.</summary>
    private (TwitchAccessTokenProvider Provider, StubHttpMessageHandler TokenEndpoint) CreateTokenProvider(
        TwitchAuthOptions? options = null,
        Func<HttpRequestMessage, HttpResponseMessage>? respond = null)
    {
        //  Issue numbered tokens unless told otherwise, and build the service on top.
        var tokenEndpoint = new StubHttpMessageHandler(respond ?? IssueToken);
        var provider = new TwitchAccessTokenProvider(
            new SingleHandlerHttpClientFactory(tokenEndpoint),
            Options.Create(options ?? Credentials()),
            _time,
            NullLogger<TwitchAccessTokenProvider>.Instance);
        return (provider, tokenEndpoint);
    }

    /// <summary>Checks that a token is requested with the credentials in the body, never in the address.</summary>
    [Fact]
    public async Task Requests_a_token_with_client_credentials_in_the_body()
    {
        //  Set up the service.
        var (provider, tokenEndpoint) = CreateTokenProvider();

        //  Ask for a token.
        var token = await provider.GetTokenAsync(Ct);

        //  Expect the first token, requested once with the credentials in the body.
        Assert.Equal("token-1", token);
        var request = Assert.Single(tokenEndpoint.Received);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal(new Uri("https://id.twitch.tv/oauth2/token"), request.Uri);
        Assert.Equal("client_id=client-id&client_secret=client-secret&grant_type=client_credentials", request.Body);
        Assert.Empty(request.Uri.Query); // the secret never appears in the URL
    }

    /// <summary>Checks that a token is reused until shortly before it expires.</summary>
    [Fact]
    public async Task Reuses_the_token_until_shortly_before_it_expires()
    {
        //  Set up the service.
        var (provider, tokenEndpoint) = CreateTokenProvider();

        //  Get a token, wait 54 minutes, and expect the same one.
        await provider.GetTokenAsync(Ct);
        _time.Advance(TimeSpan.FromMinutes(54));
        Assert.Equal("token-1", await provider.GetTokenAsync(Ct));

        //  Wait until close to expiry, and expect a new one.
        _time.Advance(TimeSpan.FromMinutes(2)); // within 5 minutes of the 60-minute expiry
        Assert.Equal("token-2", await provider.GetTokenAsync(Ct));
        Assert.Equal(2, tokenEndpoint.Requests.Count);
    }

    /// <summary>Checks that callers asking at the same time share one token request.</summary>
    [Fact]
    public async Task Concurrent_callers_share_one_token_request()
    {
        //  Hold the token endpoint's answer until every caller has asked.
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var tokenEndpoint = new StubHttpMessageHandler(IssueToken);
        var provider = new TwitchAccessTokenProvider(
            new SingleHandlerHttpClientFactory(new GatedHandler(release.Task) { InnerHandler = tokenEndpoint }),
            Options.Create(Credentials()),
            _time,
            NullLogger<TwitchAccessTokenProvider>.Instance);

        //  Have twenty callers ask, check they are all waiting, then let the endpoint answer.
        var callers = Enumerable.Range(0, 20).Select(_ => provider.GetTokenAsync(Ct).AsTask()).ToList();
        Assert.All(callers, caller => Assert.False(caller.IsCompleted));
        release.SetResult();
        var tokens = await Task.WhenAll(callers);

        //  Expect everyone to get the same token from a single request.
        Assert.All(tokens, token => Assert.Equal("token-1", token));
        Assert.Single(tokenEndpoint.Requests);
    }

    /// <summary>Checks that a refused token is replaced, but an outdated refusal keeps the newer token.</summary>
    [Fact]
    public async Task Invalidated_token_is_replaced_but_a_newer_one_is_kept()
    {
        //  Set up the service and get a token.
        var (provider, _) = CreateTokenProvider();
        var first = await provider.GetTokenAsync(Ct);

        //  Drop it, get a new one, then try to drop the old one again.
        provider.Invalidate(first);
        var second = await provider.GetTokenAsync(Ct);
        provider.Invalidate(first); // stale: must not discard the newer token

        //  Expect the new token to stay.
        Assert.Equal("token-2", second);
        Assert.Equal("token-2", await provider.GetTokenAsync(Ct));
    }

    /// <summary>Checks that wrong credentials fail with a clear message.</summary>
    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task Rejected_credentials_fail_with_a_clear_message(HttpStatusCode status)
    {
        //  Set up the token endpoint to refuse the credentials.
        var (provider, _) = CreateTokenProvider(respond: _ => new HttpResponseMessage(status));

        //  Ask for a token.
        var exception = await Assert.ThrowsAsync<DataProviderException>(() => provider.GetTokenAsync(Ct).AsTask());

        //  Expect the error to say the credentials were refused.
        Assert.Contains("rejected the client credentials", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>Checks that missing credentials fail without sending anything.</summary>
    [Fact]
    public async Task Missing_credentials_fail_without_a_request()
    {
        //  Set up the service without a client id.
        var (provider, tokenEndpoint) = CreateTokenProvider(Credentials(clientId: null));

        //  Expect asking for a token to fail.
        await Assert.ThrowsAsync<DataProviderException>(() => provider.GetTokenAsync(Ct).AsTask());

        //  Expect no request to the token endpoint.
        Assert.Empty(tokenEndpoint.Requests);
    }

    /// <summary>Checks that requests are signed with the client id and token.</summary>
    [Fact]
    public async Task Handler_adds_client_id_and_bearer_token()
    {
        //  Set up the signing step in front of a fake source.
        var (provider, _) = CreateTokenProvider();
        var api = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        using var client = new HttpClient(new TwitchAuthenticationHandler(provider, Options.Create(Credentials())) { InnerHandler = api });

        //  Send a request.
        await client.GetAsync(new Uri("https://api.igdb.com/v4/games"), Ct);

        //  Expect both sign-in headers on it.
        var request = Assert.Single(api.Received);
        Assert.Equal("client-id", request.Headers["Client-ID"]);
        Assert.Equal("Bearer token-1", request.Headers["Authorization"]);
    }

    /// <summary>Checks that a refused token is replaced and the request is sent once more.</summary>
    [Fact]
    public async Task Handler_replaces_a_rejected_token_and_retries_once()
    {
        //  Set up a fake source that refuses only the first token.
        var (provider, tokenEndpoint) = CreateTokenProvider();
        var api = new StubHttpMessageHandler(request =>
            new HttpResponseMessage(request.Headers.Authorization?.Parameter == "token-1" ? HttpStatusCode.Unauthorized : HttpStatusCode.OK));
        using var client = new HttpClient(new TwitchAuthenticationHandler(provider, Options.Create(Credentials())) { InnerHandler = api });

        //  Send a request.
        using var response = await client.GetAsync(new Uri("https://api.igdb.com/v4/games"), Ct);

        //  Expect success on the second try, with a new token.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(["Bearer token-1", "Bearer token-2"], api.Received.Select(r => r.Headers["Authorization"]));
        Assert.Equal(2, tokenEndpoint.Requests.Count);
    }

    /// <summary>Checks that the signing step gives up after one retry.</summary>
    [Fact]
    public async Task Handler_gives_up_after_one_retry()
    {
        //  Set up a fake source that refuses every token.
        var (provider, _) = CreateTokenProvider();
        var api = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized));
        using var client = new HttpClient(new TwitchAuthenticationHandler(provider, Options.Create(Credentials())) { InnerHandler = api });

        //  Send a request.
        using var response = await client.GetAsync(new Uri("https://api.igdb.com/v4/games"), Ct);

        //  Expect the refusal after exactly two tries.
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(2, api.Requests.Count);
    }

    /// <summary>A step that holds every request until the gate opens.</summary>
    private sealed class GatedHandler(Task gate) : DelegatingHandler
    {
        /// <summary>Waits for the gate, then passes the request on.</summary>
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            //  Wait for the gate to open, then send the request on.
            await gate.WaitAsync(cancellationToken);
            return await base.SendAsync(request, cancellationToken);
        }
    }
}
