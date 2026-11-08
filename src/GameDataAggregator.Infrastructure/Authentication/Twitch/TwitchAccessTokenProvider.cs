using System.Net;
using System.Net.Http.Json;
using GameDataAggregator.Core.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using static GameDataAggregator.Infrastructure.Providers.ProviderMapping;

namespace GameDataAggregator.Infrastructure.Authentication.Twitch;

/// <summary>
/// Obtains and caches a Twitch app access token using the OAuth 2.0 client credentials flow.
/// </summary>
/// <remarks>
/// Singleton. The token is reused until shortly before it expires (Twitch app tokens last about two
/// months), so normally only the very first request pays for a token round trip. Concurrent callers
/// that find no valid token wait for a single shared refresh instead of each requesting their own.
/// The token is kept in process memory only: it is a credential, so it is deliberately not written
/// to a cache that could be shared or persisted.
/// </remarks>
public sealed partial class TwitchAccessTokenProvider(
    IHttpClientFactory httpClientFactory,
    IOptions<TwitchAuthOptions> options,
    TimeProvider timeProvider,
    ILogger<TwitchAccessTokenProvider> logger) : IDisposable
{
    public const string HttpClientName = "TwitchAuth";

    /// <summary>Refresh this long before the reported expiry, so a token never expires mid-request.</summary>
    private static readonly TimeSpan RefreshMargin = TimeSpan.FromMinutes(5);

    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private AccessToken? _current;

    /// <summary>Returns a valid access token, fetching a new one only when needed.</summary>
    public async ValueTask<string> GetTokenAsync(CancellationToken cancellationToken)
    {
        //  Reuse the current token while it is still valid.
        if (TryGetValidToken() is { } token)
        {
            return token;
        }

        //  Let only one caller at a time fetch a new token.
        await _refreshLock.WaitAsync(cancellationToken);
        try
        {
            //  Use the token another caller fetched while this one was waiting.
            if (TryGetValidToken() is { } refreshedByOtherCaller)
            {
                return refreshedByOtherCaller;
            }

            //  Fetch a new token and keep it for everyone.
            var fresh = await RequestTokenAsync(cancellationToken);
            Volatile.Write(ref _current, fresh);
            return fresh.Value;
        }
        finally
        {
            //  Let the next waiting caller in.
            _refreshLock.Release();
        }
    }

    /// <summary>
    /// Discards <paramref name="rejectedToken"/> after an API refused it (HTTP 401), so the next call
    /// fetches a new one. A token that has already been replaced is left alone.
    /// </summary>
    public void Invalidate(string rejectedToken)
    {
        //  Forget the token only if it is still the current one.
        var current = Volatile.Read(ref _current);
        if (current is not null && current.Value == rejectedToken)
        {
            Interlocked.CompareExchange(ref _current, null, current);
        }
    }

    /// <summary>Releases the lock used for fetching tokens.</summary>
    public void Dispose() => _refreshLock.Dispose();

    /// <summary>Returns the current token if it is not about to expire.</summary>
    private string? TryGetValidToken()
    {
        //  Hand out the token only before its refresh time.
        var current = Volatile.Read(ref _current);
        return current is not null && timeProvider.GetUtcNow() < current.RefreshAt ? current.Value : null;
    }

    /// <summary>Asks Twitch for a new token and works out when to renew it.</summary>
    private async Task<AccessToken> RequestTokenAsync(CancellationToken cancellationToken)
    {
        //  Stop with a clear message when the credentials are missing.
        var settings = options.Value;
        if (!settings.HasCredentials)
        {
            throw new DataProviderException(
                "Twitch client credentials are not configured. Set " +
                $"'ExternalApis:{TwitchAuthOptions.SectionName}:ClientId' and 'ExternalApis:{TwitchAuthOptions.SectionName}:ClientSecret'.");
        }

        //  Send the credentials in the request body, so they never show up in logged addresses.
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = settings.ClientId!,
            ["client_secret"] = settings.ClientSecret!,
            ["grant_type"] = "client_credentials",
        });

        //  Ask Twitch for the token.
        var httpClient = httpClientFactory.CreateClient(HttpClientName);
        using var response = await httpClient.PostAsync(settings.TokenUrl, content, cancellationToken);

        //  Report wrong credentials clearly.
        if (response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            throw new DataProviderException(
                $"Twitch rejected the client credentials (HTTP {(int)response.StatusCode}). Check the ClientId and ClientSecret.");
        }

        //  Treat any other error answer as a failure.
        response.EnsureSuccessStatusCode();

        //  Reject an answer without a usable token.
        var token = await response.Content.ReadFromJsonAsync<TwitchTokenResponse>(SnakeCaseJson, cancellationToken);
        if (string.IsNullOrEmpty(token?.AccessToken) || token.ExpiresIn <= 0)
        {
            throw new DataProviderException("Twitch returned an invalid access token response.");
        }

        //  Plan to renew the token a little before it expires.
        var lifetime = TimeSpan.FromSeconds(token.ExpiresIn);
        var margin = lifetime > RefreshMargin * 2 ? RefreshMargin : lifetime / 2;
        LogTokenAcquired(logger, lifetime);

        return new AccessToken(token.AccessToken, timeProvider.GetUtcNow() + lifetime - margin);
    }

    /// <summary>Logs that a new token was received and how long it lasts.</summary>
    [LoggerMessage(Level = LogLevel.Information, Message = "Acquired a Twitch app access token valid for {Lifetime}")]
    private static partial void LogTokenAcquired(ILogger logger, TimeSpan lifetime);

    /// <summary>A token and the time it should be renewed.</summary>
    private sealed record AccessToken(string Value, DateTimeOffset RefreshAt);

    /// <summary>Twitch's answer to a token request.</summary>
    private sealed record TwitchTokenResponse(string? AccessToken, long ExpiresIn);
}
