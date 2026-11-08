using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using GameDataAggregator.Api.Contracts;
using GameDataAggregator.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace GameDataAggregator.IntegrationTests;

/// <summary>Tests which endpoints need an access token, which tokens are accepted, and the development token endpoint.</summary>
public sealed class AuthenticationTests : IAsyncDisposable
{
    private const string Issuer = "GameDataAggregator";
    private const string Audience = "GameDataAggregator.Api";

    private readonly GameDataAggregatorFactory _factory = new();
    private readonly HttpClient _client;

    /// <summary>Starts a fresh app and a client for it that sends no token.</summary>
    public AuthenticationTests() => _client = _factory.CreateClient();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>Shuts down the client and the app.</summary>
    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    /// <summary>Signs a token with any settings, so tests can build tokens the app must refuse.</summary>
    private static string SignToken(
        string key = GameDataAggregatorFactory.JwtSigningKey,
        string issuer = Issuer,
        string audience = Audience,
        TimeSpan? expiresIn = null)
    {
        //  Date the token so it expires after the given time, which may be in the past.
        var expires = DateTime.UtcNow + (expiresIn ?? TimeSpan.FromMinutes(10));
        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = issuer,
            Audience = audience,
            IssuedAt = expires.AddHours(-1),
            NotBefore = expires.AddHours(-1),
            Expires = expires,
            Subject = new ClaimsIdentity([new Claim(JwtRegisteredClaimNames.Sub, "test")]),
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)), SecurityAlgorithms.HmacSha256),
        });
    }

    /// <summary>Sends a GET with the given bearer token.</summary>
    private Task<HttpResponseMessage> GetWithTokenAsync(string path, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(request, Ct);
    }

    /// <summary>Checks that protected endpoints refuse a request without a token, before any external API is called.</summary>
    [Theory]
    [InlineData("/api/v1/aggregation?search=witcher")]
    [InlineData("/api/v1/statistics")]
    public async Task Protected_endpoints_refuse_requests_without_a_token(string path)
    {
        //  Call the endpoint without a token.
        var response = await _client.GetAsync(path, Ct);

        //  Expect 401 in the standard error format, a hint to use a bearer token, and no external calls.
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains(response.Headers.WwwAuthenticate, h => h.Scheme == "Bearer");
        Assert.Empty(_factory.ExternalApis.Requests);
    }

    /// <summary>Checks that the open endpoints and the page need no token.</summary>
    [Theory]
    [InlineData("/health")]
    [InlineData("/api/v1/sources")]
    [InlineData("/")]
    [InlineData("/openapi/v1.json")]
    public async Task Open_endpoints_need_no_token(string path)
    {
        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync(path, Ct)).StatusCode);
    }

    /// <summary>Checks that a valid token is accepted.</summary>
    [Fact]
    public async Task A_valid_token_is_accepted()
    {
        Assert.Equal(HttpStatusCode.OK, (await GetWithTokenAsync("/api/v1/statistics", SignToken())).StatusCode);
    }

    /// <summary>Checks that tokens with a wrong key, issuer, audience or expiry, or no token format at all, are refused.</summary>
    [Theory]
    [InlineData("wrong-key")]
    [InlineData("wrong-issuer")]
    [InlineData("wrong-audience")]
    [InlineData("expired")]
    [InlineData("not-a-token")]
    public async Task Invalid_tokens_are_refused(string defect)
    {
        //  Build a token with one defect.
        var token = defect switch
        {
            "wrong-key" => SignToken(key: "another-signing-key-that-is-also-32-bytes-long"),
            "wrong-issuer" => SignToken(issuer: "someone-else"),
            "wrong-audience" => SignToken(audience: "another-api"),
            "expired" => SignToken(expiresIn: TimeSpan.FromMinutes(-5)),
            _ => "not-a-token",
        };

        //  Expect it to be refused.
        Assert.Equal(HttpStatusCode.Unauthorized, (await GetWithTokenAsync("/api/v1/statistics", token)).StatusCode);
    }

    /// <summary>Checks that the development endpoint gives out a token that the protected endpoints accept.</summary>
    [Fact]
    public async Task Development_token_endpoint_issues_a_token_the_api_accepts()
    {
        //  Ask for a token.
        var response = await _client.PostAsync("/api/v1/auth/token", content: null, Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var token = (await response.Content.ReadFromJsonAsync<TokenResponse>(ApiJson.Options, Ct))!;

        //  Expect a bearer token valid for the configured hour.
        Assert.Equal("Bearer", token.TokenType);
        Assert.InRange(token.ExpiresIn, 3_590, 3_600);

        //  Expect the protected endpoints to accept it.
        Assert.Equal(HttpStatusCode.OK, (await GetWithTokenAsync("/api/v1/statistics", token.AccessToken)).StatusCode);
    }

    /// <summary>Checks that without a configured key, Development still works with a random key.</summary>
    [Fact]
    public async Task Development_without_a_signing_key_uses_a_random_key()
    {
        //  Start the app in Development with no key.
        using var factory = _factory.WithWebHostBuilder(builder => builder.UseSetting("Jwt:SigningKey", ""));
        using var client = factory.CreateClient();

        //  Get a token and use it.
        var token = (await (await client.PostAsync("/api/v1/auth/token", content: null, Ct)).Content.ReadFromJsonAsync<TokenResponse>(ApiJson.Options, Ct))!;
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/statistics");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);

        //  Expect it to be accepted, while a token signed with the test key is not.
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(request, Ct)).StatusCode);
        var foreign = new HttpRequestMessage(HttpMethod.Get, "/api/v1/statistics");
        foreign.Headers.Authorization = new AuthenticationHeaderValue("Bearer", SignToken());
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.SendAsync(foreign, Ct)).StatusCode);
    }

    /// <summary>Checks that no tokens are given out outside Development.</summary>
    [Fact]
    public async Task Token_endpoint_does_not_exist_outside_development()
    {
        //  Start the app as in production.
        using var factory = _factory.WithWebHostBuilder(builder => builder.UseEnvironment("Production"));
        using var client = factory.CreateClient();

        //  Ask for a token.
        var response = await client.PostAsync("/api/v1/auth/token", content: null, Ct);

        //  Expect the endpoint to act as if it did not exist.
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>Checks that start-up fails outside Development without a key, or anywhere with a short key.</summary>
    [Theory]
    [InlineData("Production", "")]
    [InlineData("Development", "too-short")]
    public void Start_up_fails_without_a_usable_signing_key(string environment, string key)
    {
        //  Set up the app with a missing or short key.
        using var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment(environment);
            builder.UseSetting("Jwt:SigningKey", key);
        });

        //  Expect start-up to fail and name the setting.
        var error = Assert.ThrowsAny<Exception>(() => factory.CreateClient());
        Assert.Contains("Jwt:SigningKey", error.ToString(), StringComparison.Ordinal);
    }

    /// <summary>Checks that the API description shows the token field on protected operations only.</summary>
    [Fact]
    public async Task Api_description_marks_only_protected_operations_as_needing_a_token()
    {
        //  Read the API description.
        using var document = JsonDocument.Parse(await _client.GetStringAsync("/openapi/v1.json", Ct));
        var root = document.RootElement;
        var paths = root.GetProperty("paths");

        //  Expect the bearer scheme, on the search and statistics but not on the sources or token endpoints.
        Assert.Equal("bearer", root.GetProperty("components").GetProperty("securitySchemes").GetProperty("Bearer").GetProperty("scheme").GetString());
        Assert.True(paths.GetProperty("/api/v1/aggregation").GetProperty("get").TryGetProperty("security", out _));
        Assert.True(paths.GetProperty("/api/v1/statistics").GetProperty("get").TryGetProperty("security", out _));
        Assert.False(paths.GetProperty("/api/v1/sources").GetProperty("get").TryGetProperty("security", out _));
        Assert.False(paths.GetProperty("/api/v1/auth/token").GetProperty("post").TryGetProperty("security", out _));
    }
}
