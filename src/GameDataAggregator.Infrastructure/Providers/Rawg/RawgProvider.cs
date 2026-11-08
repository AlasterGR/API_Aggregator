using System.Net.Http.Json;
using GameDataAggregator.Core.Abstractions;
using GameDataAggregator.Core.Models;
using Microsoft.Extensions.Options;
using static GameDataAggregator.Infrastructure.Providers.ProviderMapping;

namespace GameDataAggregator.Infrastructure.Providers.Rawg;

/// <summary>
/// RAWG video game database: catalog and design information (genres, platforms, ratings, release dates).
/// Requires a free API key.
/// </summary>
public sealed class RawgProvider(HttpClient httpClient, IOptions<RawgOptions> options) : IGameDataProvider
{
    public const string SourceName = "RAWG";

    public string Name => SourceName;

    public string? ConfigurationError => string.IsNullOrWhiteSpace(options.Value.ApiKey)
        ? $"RAWG API key is not configured. Set '{ProviderRegistration.ConfigurationRoot}:{RawgOptions.SectionName}:ApiKey'."
        : null;

    /// <summary>Searches RAWG for the game and returns its details.</summary>
    public async Task<IReadOnlyList<AggregatedItem>> FetchAsync(string search, CancellationToken cancellationToken)
    {
        //  Refuse to run without a key, in case this is called directly.
        if (ConfigurationError is { } configurationError)
        {
            throw new DataProviderException(configurationError);
        }

        var settings = options.Value;
        var apiKey = settings.ApiKey!; // not null or blank: ConfigurationError checked it above

        //  Build the search address with the key and the result limit.
        var requestUri =
            $"games?key={Uri.EscapeDataString(apiKey)}" +
            $"&search={Uri.EscapeDataString(search)}" +
            $"&search_precise=true&page_size={settings.MaxResults}";

        //  Ask RAWG and turn each game into a common item.
        var response = await httpClient.GetFromJsonAsync<RawgGamesResponse>(requestUri, SnakeCaseJson, cancellationToken);

        return (response?.Results ?? []).Select(Map).ToList();
    }

    /// <summary>Turns a RAWG game into a common item.</summary>
    private static AggregatedItem Map(RawgGame game) => new()
    {
        Source = SourceName,
        Category = ItemCategory.GameInfo,
        Title = game.Name,
        Url = new Uri($"https://rawg.io/games/{Uri.EscapeDataString(game.Slug)}"),
        ImageUrl = ToUri(game.BackgroundImage),
        Date = game.Released is { } released ? new DateTimeOffset(released, TimeOnly.MinValue, TimeSpan.Zero) : null,
        DateKind = DateKind.Released,
        Tags = game.Genres?.Select(g => g.Name).ToList() ?? [],
        Metrics = Metrics(
            ("rating", game.Rating),
            ("ratingsCount", game.RatingsCount),
            ("metacritic", game.Metacritic),
            ("averagePlaytimeHours", game.Playtime)),
        Attributes = Attributes(
            ("rawgId", game.Id.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            ("platforms", game.Platforms is null ? null : string.Join(", ", game.Platforms.Select(p => p.Platform.Name)))),
    };
}
