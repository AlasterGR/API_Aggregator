using System.Net;
using System.Net.Http.Headers;
using Microsoft.Extensions.Options;

namespace GameDataAggregator.Infrastructure.Authentication.Twitch;

/// <summary>
/// Adds the headers every Twitch-owned API requires: <c>Client-ID</c> and <c>Authorization: Bearer</c>.
/// If the API answers 401 (token revoked or expired early), the token is replaced and the request is
/// sent once more.
/// </summary>
internal sealed class TwitchAuthenticationHandler(
    TwitchAccessTokenProvider tokenProvider,
    IOptions<TwitchAuthOptions> options) : DelegatingHandler
{
    private const string ClientIdHeader = "Client-ID";

    /// <summary>Sends the request signed in, and retries once with a new token if it is refused.</summary>
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        //  Sign the request with the current token.
        var token = await tokenProvider.GetTokenAsync(cancellationToken);
        Authorize(request, token);

        //  Send it, and return the answer unless the token was refused.
        var response = await base.SendAsync(request, cancellationToken);
        if (response.StatusCode != HttpStatusCode.Unauthorized)
        {
            return response;
        }

        //  Drop the refused token and sign again with a new one.
        response.Dispose();
        tokenProvider.Invalidate(token);
        Authorize(request, await tokenProvider.GetTokenAsync(cancellationToken));

        return await base.SendAsync(request, cancellationToken);
    }

    /// <summary>Puts the client id and token on the request.</summary>
    private void Authorize(HttpRequestMessage request, string token)
    {
        //  Replace any earlier sign-in headers with the current ones.
        request.Headers.Remove(ClientIdHeader);
        request.Headers.Add(ClientIdHeader, options.Value.ClientId);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }
}
