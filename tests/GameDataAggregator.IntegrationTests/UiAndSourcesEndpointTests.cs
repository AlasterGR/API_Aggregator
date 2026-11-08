using System.Net;
using System.Net.Http.Json;
using GameDataAggregator.IntegrationTests.Infrastructure;

namespace GameDataAggregator.IntegrationTests;

/// <summary>Tests the sources endpoint and the browser page.</summary>
public sealed class UiAndSourcesEndpointTests : IAsyncDisposable
{
    private readonly GameDataAggregatorFactory _factory = new();
    private readonly HttpClient _client;

    /// <summary>Starts a fresh app and a client for it.</summary>
    public UiAndSourcesEndpointTests() => _client = _factory.CreateClient();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>Shuts down the client and the app.</summary>
    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    /// <summary>Checks that every enabled source is listed, in the order they are searched.</summary>
    [Fact]
    public async Task Sources_endpoint_lists_every_enabled_source_in_query_order()
    {
        //  Ask for the sources.
        var sources = await _client.GetFromJsonAsync<string[]>("/api/v1/sources", Ct);

        //  Expect all five, in search order.
        Assert.Equal(["RAWG", "CheapShark", "Steam", "IGDB", "Speedrun"], sources!);
    }

    /// <summary>Checks that a switched-off source is not listed.</summary>
    [Fact]
    public async Task Disabled_source_is_not_listed()
    {
        //  Start the app with Steam switched off.
        using var factory = _factory.WithWebHostBuilder(builder => builder.UseSetting("ExternalApis:Steam:Enabled", "false"));
        using var client = factory.CreateClient();

        //  Ask for the sources.
        var sources = await client.GetFromJsonAsync<string[]>("/api/v1/sources", Ct);

        //  Expect every source except Steam.
        Assert.Equal(["RAWG", "CheapShark", "IGDB", "Speedrun"], sources!);
    }

    /// <summary>Checks that the browser page and its files are served, and always checked for updates.</summary>
    [Theory]
    [InlineData("/", "text/html")]
    [InlineData("/css/site.css", "text/css")]
    [InlineData("/js/app.js", "text/javascript")]
    public async Task Browser_ui_is_served_from_the_root(string path, string mediaType)
    {
        //  Ask for the file.
        var response = await _client.GetAsync(path, Ct);

        //  Expect it with the right type.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(mediaType, response.Content.Headers.ContentType?.MediaType);

        //  Expect browsers to be told to check for a newer version, so they never run an outdated page.
        Assert.True(response.Headers.CacheControl?.NoCache, $"{path} should be served with Cache-Control: no-cache");
    }
}
