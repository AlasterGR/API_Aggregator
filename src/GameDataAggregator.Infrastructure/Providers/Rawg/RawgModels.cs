namespace GameDataAggregator.Infrastructure.Providers.Rawg;

// Only the fields this service uses. Shape of GET /games: https://api.rawg.io/docs/#operation/games_list

/// <summary>RAWG's answer to a game search.</summary>
internal sealed record RawgGamesResponse(IReadOnlyList<RawgGame>? Results);

/// <summary>One game from a RAWG search.</summary>
internal sealed record RawgGame(
    int Id,
    string Slug,
    string Name,
    DateOnly? Released,
    string? BackgroundImage,
    double Rating,
    int RatingsCount,
    int? Metacritic,
    int Playtime,
    IReadOnlyList<RawgNamedEntity>? Genres,
    IReadOnlyList<RawgPlatformEntry>? Platforms);

/// <summary>Anything RAWG lists by name, such as a genre or platform.</summary>
internal sealed record RawgNamedEntity(string Name);

/// <summary>A platform the game is on.</summary>
internal sealed record RawgPlatformEntry(RawgNamedEntity Platform);
