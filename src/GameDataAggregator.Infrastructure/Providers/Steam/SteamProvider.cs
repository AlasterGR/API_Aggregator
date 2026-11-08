using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using GameDataAggregator.Core.Abstractions;
using GameDataAggregator.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using static GameDataAggregator.Infrastructure.Providers.ProviderMapping;

namespace GameDataAggregator.Infrastructure.Providers.Steam;

/// <summary>
/// Steam: live player engagement. Finds games by name in the Steam store, then fetches the current
/// number of players for each one. No API key required.
/// </summary>
public sealed partial class SteamProvider(
    HttpClient httpClient,
    IHttpClientFactory httpClientFactory,
    IOptions<SteamOptions> options,
    TimeProvider timeProvider,
    ILogger<SteamProvider> logger) : IGameDataProvider
{
    public const string SourceName = "Steam";

    /// <summary>The separate HttpClient used for player counts (see ProviderRegistration.AddSideRequestClient).</summary>
    public const string SideRequestClientName = "Steam.SideRequests";

    public string Name => SourceName;

    /// <summary>The only category of item this source gives.</summary>
    public IReadOnlySet<ItemCategory> Categories { get; } = new HashSet<ItemCategory> { ItemCategory.PlayerActivity };

    /// <summary>Finds games in the Steam store and adds how many people are playing each one now.</summary>
    public async Task<IReadOnlyList<AggregatedItem>> FetchAsync(string search, CancellationToken cancellationToken)
    {
        //  Build the store search for the configured language and country.
        var settings = options.Value;
        var searchUri = new Uri(
            settings.StoreBaseUrl,
            $"storesearch/?term={Uri.EscapeDataString(search)}&l={Uri.EscapeDataString(settings.Language)}&cc={Uri.EscapeDataString(settings.CountryCode)}");

        //  Search the store and keep only games, up to the result limit.
        var searchResponse = await httpClient.GetFromJsonAsync<SteamStoreSearchResponse>(searchUri, SnakeCaseJson, cancellationToken);
        var apps = (searchResponse?.Items ?? [])
            .Where(i => string.Equals(i.Type, "app", StringComparison.OrdinalIgnoreCase))
            .Take(settings.MaxResults)
            .ToList();

        //  Ask for every game's player count at the same time, through the side connection.
        var measuredAt = timeProvider.GetUtcNow();
        var sideClient = httpClientFactory.CreateClient(SideRequestClientName);
        var playerCounts = await Task.WhenAll(apps.Select(app => GetCurrentPlayersAsync(sideClient, app.Id, cancellationToken)));

        return apps.Zip(playerCounts, (app, players) => Map(app, players, measuredAt)).ToList();
    }

    /// <summary>
    /// Returns null when the count is unavailable. Steam answers 404 for apps without stats (soundtracks,
    /// some DLC), and a failure for one game should not discard the results for all the others.
    /// </summary>
    private async Task<int?> GetCurrentPlayersAsync(HttpClient sideClient, int appId, CancellationToken cancellationToken)
    {
        //  Build the player count address for this game.
        var uri = new Uri(
            options.Value.WebApiBaseUrl,
            $"ISteamUserStats/GetNumberOfCurrentPlayers/v1/?appid={appId.ToString(CultureInfo.InvariantCulture)}");

        //  Ask Steam for the count.
        try
        {
            //  Treat a game without player figures as having no count.
            using var response = await sideClient.GetAsync(uri, cancellationToken);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }

            //  Read the count, accepting it only when Steam says it found one.
            response.EnsureSuccessStatusCode();
            var body = await response.Content.ReadFromJsonAsync<SteamPlayerCountResponse>(SnakeCaseJson, cancellationToken);
            return body?.Response is { Result: 1, PlayerCount: { } count } ? count : null;
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            //  Note the failure and carry on without a count for this game.
            LogPlayerCountFailed(logger, ex, appId);
            return null;
        }
    }

    /// <summary>Turns a Steam game and its player count into a common item.</summary>
    private static AggregatedItem Map(SteamStoreItem app, int? currentPlayers, DateTimeOffset measuredAt) => new()
    {
        Source = SourceName,
        Category = ItemCategory.PlayerActivity,
        Title = app.Name,
        Url = new Uri($"https://store.steampowered.com/app/{app.Id.ToString(CultureInfo.InvariantCulture)}"),
        ImageUrl = ToUri(app.TinyImage),
        Date = measuredAt,
        DateKind = DateKind.Measured,
        Tags = Platforms(app.Platforms),
        Metrics = Metrics(
            ("currentPlayers", currentPlayers),
            ("price", app.Price is null ? null : app.Price.Final / 100.0),
            ("metascore", double.TryParse(app.Metascore, CultureInfo.InvariantCulture, out var score) ? score : null)),
        Attributes = Attributes(
            ("steamAppId", app.Id.ToString(CultureInfo.InvariantCulture)),
            ("currency", app.Price?.Currency)),
    };

    /// <summary>Names the systems a game runs on.</summary>
    private static List<string> Platforms(SteamPlatforms? platforms)
    {
        //  Add each system the game supports.
        var result = new List<string>(3);
        if (platforms?.Windows == true) result.Add("Windows");
        if (platforms?.Mac == true) result.Add("macOS");
        if (platforms?.Linux == true) result.Add("Linux");
        return result;
    }

    /// <summary>Logs that a game's player count could not be fetched.</summary>
    [LoggerMessage(Level = LogLevel.Debug, Message = "Could not get the current player count for Steam app {AppId}")]
    private static partial void LogPlayerCountFailed(ILogger logger, Exception exception, int appId);
}
