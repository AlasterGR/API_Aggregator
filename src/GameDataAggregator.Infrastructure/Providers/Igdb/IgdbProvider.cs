using System.Globalization;
using System.Net.Http.Json;
using System.Text;
using GameDataAggregator.Core.Abstractions;
using GameDataAggregator.Core.Models;
using GameDataAggregator.Infrastructure.Authentication.Twitch;
using Microsoft.Extensions.Options;
using static GameDataAggregator.Infrastructure.Providers.ProviderMapping;

namespace GameDataAggregator.Infrastructure.Providers.Igdb;

/// <summary>
/// IGDB (Twitch's Internet Game Database): game design and production information, i.e. developers,
/// publishers, genres, themes, platforms and critic/user ratings. Requires free Twitch application
/// credentials; the access token is attached by <see cref="TwitchAuthenticationHandler"/>.
/// </summary>
public sealed class IgdbProvider(
    HttpClient httpClient,
    IOptions<IgdbOptions> options,
    IOptions<TwitchAuthOptions> authOptions) : IGameDataProvider
{
    public const string SourceName = "IGDB";

    private const string Fields =
        "name,slug,url,summary,first_release_date,total_rating,total_rating_count,aggregated_rating,hypes," +
        "cover.image_id,genres.name,themes.name,platforms.name," +
        "involved_companies.company.name,involved_companies.developer,involved_companies.publisher";

    public string Name => SourceName;

    /// <summary>The only category of item this source gives.</summary>
    public IReadOnlySet<ItemCategory> Categories { get; } = new HashSet<ItemCategory> { ItemCategory.GameInfo };

    public string? ConfigurationError => authOptions.Value.HasCredentials
        ? null
        : "IGDB needs Twitch application credentials. Set " +
          $"'{ProviderRegistration.ConfigurationRoot}:{TwitchAuthOptions.SectionName}:ClientId' and " +
          $"'{ProviderRegistration.ConfigurationRoot}:{TwitchAuthOptions.SectionName}:ClientSecret'.";

    /// <summary>Searches IGDB for the game and returns its details.</summary>
    public async Task<IReadOnlyList<AggregatedItem>> FetchAsync(string search, CancellationToken cancellationToken)
    {
        //  Refuse to run without credentials, in case this is called directly.
        if (ConfigurationError is { } configurationError)
        {
            throw new DataProviderException(configurationError);
        }

        //  Send the search to IGDB and stop if it answers with an error.
        var settings = options.Value;
        using var content = new StringContent(BuildQuery(search, settings.MaxResults), Encoding.UTF8, "text/plain");
        using var response = await httpClient.PostAsync("games", content, cancellationToken);
        response.EnsureSuccessStatusCode();

        //  Read the games and turn each one into a common item.
        var games = await response.Content.ReadFromJsonAsync<List<IgdbGame>>(SnakeCaseJson, cancellationToken) ?? [];
        return games.Select(game => Map(game, settings)).ToList();
    }

    /// <summary>
    /// Builds an Apicalypse query (IGDB's query language, sent as the request body).
    /// Quotes and backslashes are removed from the search text so it cannot break out of the string literal.
    /// </summary>
    internal static string BuildQuery(string search, int limit)
    {
        //  Remove characters that could break the query, then write it out.
        var safeSearch = search.Replace("\\", " ", StringComparison.Ordinal).Replace("\"", " ", StringComparison.Ordinal).Trim();
        return $"search \"{safeSearch}\"; fields {Fields}; limit {limit.ToString(CultureInfo.InvariantCulture)};";
    }

    /// <summary>Turns an IGDB game into a common item.</summary>
    private static AggregatedItem Map(IgdbGame game, IgdbOptions settings)
    {
        var companies = game.InvolvedCompanies ?? [];

        //  Copy the game's details into the common shape, building links where IGDB gives none.
        return new AggregatedItem
        {
            Source = SourceName,
            Category = ItemCategory.GameInfo,
            Title = game.Name,
            Url = ToUri(game.Url) ?? (game.Slug is null ? null : new Uri($"https://www.igdb.com/games/{Uri.EscapeDataString(game.Slug)}")),
            ImageUrl = game.Cover?.ImageId is { Length: > 0 } imageId
                ? new Uri($"https://images.igdb.com/igdb/image/upload/{settings.CoverImageSize}/{Uri.EscapeDataString(imageId)}.jpg")
                : null,
            Date = game.FirstReleaseDate is { } released ? DateTimeOffset.FromUnixTimeSeconds(released) : null,
            DateKind = DateKind.Released,
            Tags = (game.Genres ?? []).Concat(game.Themes ?? []).Select(e => e.Name).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            Metrics = Metrics(
                ("totalRating", game.TotalRating is { } total ? Math.Round(total, 1) : null),
                ("totalRatingCount", game.TotalRatingCount),
                ("criticRating", game.AggregatedRating is { } critic ? Math.Round(critic, 1) : null),
                ("hypes", game.Hypes)),
            Attributes = Attributes(
                ("igdbId", game.Id.ToString(CultureInfo.InvariantCulture)),
                ("developers", JoinCompanies(companies, c => c.Developer)),
                ("publishers", JoinCompanies(companies, c => c.Publisher)),
                ("platforms", game.Platforms is null ? null : string.Join(", ", game.Platforms.Select(p => p.Name))),
                ("summary", game.Summary)),
        };
    }

    /// <summary>Lists the names of the companies with the given role.</summary>
    private static string JoinCompanies(IEnumerable<IgdbInvolvedCompany> companies, Func<IgdbInvolvedCompany, bool> role) =>
        string.Join(", ", companies.Where(role).Select(c => c.Company?.Name).OfType<string>().Distinct());
}
