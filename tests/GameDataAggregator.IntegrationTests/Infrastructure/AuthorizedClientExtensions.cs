using System.Net.Http.Headers;
using GameDataAggregator.Api.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace GameDataAggregator.IntegrationTests.Infrastructure;

/// <summary>Creates test clients that send a valid access token.</summary>
public static class AuthorizedClientExtensions
{
    /// <summary>Creates a client that sends a token signed by the app's own key with every request.</summary>
    public static HttpClient CreateAuthorizedClient(this WebApplicationFactory<Program> factory)
    {
        //  Sign a token with the app's own issuer and attach it to the client.
        var client = factory.CreateClient();
        var token = factory.Services.GetRequiredService<JwtTokenIssuer>().Issue("integration-test");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
        return client;
    }
}
