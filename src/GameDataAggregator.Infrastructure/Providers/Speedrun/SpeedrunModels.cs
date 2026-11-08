using System.Text.Json.Serialization;

namespace GameDataAggregator.Infrastructure.Providers.Speedrun;

// Only the fields this service uses.
// Game search: GET {base}/games?name=...&max=...&embed=platforms,genres,developers,publishers,categories
// With "embed", each listed relation is returned as { "data": [...] } instead of a list of ids.

/// <summary>speedrun.com's answer to a game search.</summary>
internal sealed record SpeedrunGamesResponse(IReadOnlyList<SpeedrunGame>? Data);

/// <summary>One game from a speedrun.com search.</summary>
internal sealed record SpeedrunGame(
    string Id,
    SpeedrunNames Names,
    string? Abbreviation,
    string? Weblink,
    [property: JsonPropertyName("release-date")] DateOnly? ReleaseDate,
    IReadOnlyDictionary<string, string>? Moderators,
    SpeedrunAssets? Assets,
    SpeedrunEmbedded<SpeedrunNamedEntity>? Platforms,
    SpeedrunEmbedded<SpeedrunNamedEntity>? Genres,
    SpeedrunEmbedded<SpeedrunNamedEntity>? Developers,
    SpeedrunEmbedded<SpeedrunNamedEntity>? Publishers,
    SpeedrunEmbedded<SpeedrunCategory>? Categories);

/// <summary>A game's names.</summary>
internal sealed record SpeedrunNames(string International);

/// <summary>A game's images.</summary>
internal sealed record SpeedrunAssets([property: JsonPropertyName("cover-large")] SpeedrunAsset? CoverLarge);

/// <summary>The address of one image.</summary>
internal sealed record SpeedrunAsset(string? Uri);

/// <summary>A list that speedrun.com includes inside the game.</summary>
internal sealed record SpeedrunEmbedded<T>(IReadOnlyList<T>? Data);

/// <summary>Anything speedrun.com lists by name, such as a platform or genre.</summary>
internal sealed record SpeedrunNamedEntity(string Name);

/// <summary>
/// A leaderboard category, e.g. "Any%". <see cref="Type"/> is "per-game" (full runs) or "per-level";
/// miscellaneous categories are side challenges shown separately on the site.
/// </summary>
internal sealed record SpeedrunCategory(string Name, string Type, bool Miscellaneous);
