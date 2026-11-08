using System.Globalization;
using System.Net.Http.Json;
using GameDataAggregator.Core.Abstractions;
using GameDataAggregator.Core.Models;
using Microsoft.Extensions.Options;
using static GameDataAggregator.Infrastructure.Providers.ProviderMapping;

namespace GameDataAggregator.Infrastructure.Providers.Speedrun;

/// <summary>
/// speedrun.com: player engagement through the speedrunning community, i.e. how many leaderboard
/// categories a game has, who moderates them, and on which platforms it is run. No API key needed.
/// </summary>
public sealed class SpeedrunProvider(HttpClient httpClient, IOptions<SpeedrunOptions> options) : IGameDataProvider
{
    public const string SourceName = "Speedrun";

    /// <summary>Category names listed in the item's attributes; the full count is in the metrics.</summary>
    private const int MaxListedCategories = 8;

    public string Name => SourceName;

    /// <summary>Searches speedrun.com for the game and returns its community details.</summary>
    public async Task<IReadOnlyList<AggregatedItem>> FetchAsync(string search, CancellationToken cancellationToken)
    {
        //  Build the search, asking for platforms, genres, companies and categories in the same answer.
        var requestUri =
            $"games?name={Uri.EscapeDataString(search)}" +
            $"&max={options.Value.MaxResults.ToString(CultureInfo.InvariantCulture)}" +
            "&embed=platforms,genres,developers,publishers,categories";

        //  Ask speedrun.com and turn each game into a common item.
        var response = await httpClient.GetFromJsonAsync<SpeedrunGamesResponse>(requestUri, CamelCaseJson, cancellationToken);

        return (response?.Data ?? []).Select(Map).ToList();
    }

    /// <summary>Turns a speedrun.com game into a common item.</summary>
    private static AggregatedItem Map(SpeedrunGame game)
    {
        //  Pick out the main full-game categories, leaving out side challenges.
        var categories = game.Categories?.Data ?? [];
        var fullGameCategories = categories.Where(c => c.Type == "per-game" && !c.Miscellaneous).ToList();

        //  Copy the game's details into the common shape.
        return new AggregatedItem
        {
            Source = SourceName,
            Category = ItemCategory.PlayerActivity,
            Title = game.Names.International,
            Url = ToUri(game.Weblink),
            ImageUrl = ToUri(game.Assets?.CoverLarge?.Uri),
            Date = game.ReleaseDate is { } released ? new DateTimeOffset(released, TimeOnly.MinValue, TimeSpan.Zero) : null,
            DateKind = DateKind.Released,
            Tags = Names(game.Platforms).Concat(Names(game.Genres)).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            Metrics = Metrics(
                ("speedrunCategories", fullGameCategories.Count),
                ("levelCategories", categories.Count(c => c.Type == "per-level")),
                ("moderators", game.Moderators?.Count)),
            Attributes = Attributes(
                ("speedrunId", game.Id),
                ("abbreviation", game.Abbreviation),
                ("categories", JoinNames(fullGameCategories.Take(MaxListedCategories).Select(c => c.Name))),
                ("developers", JoinNames(Names(game.Developers))),
                ("publishers", JoinNames(Names(game.Publishers)))),
        };
    }

    /// <summary>Lists the non-blank names in an included list.</summary>
    private static IEnumerable<string> Names(SpeedrunEmbedded<SpeedrunNamedEntity>? embedded) =>
        (embedded?.Data ?? []).Select(e => e.Name).Where(n => !string.IsNullOrWhiteSpace(n));

    /// <summary>Joins names with commas, or returns nothing when there are none.</summary>
    private static string? JoinNames(IEnumerable<string> names)
    {
        //  Join the names, treating an empty result as no value.
        var joined = string.Join(", ", names);
        return joined.Length == 0 ? null : joined;
    }
}
