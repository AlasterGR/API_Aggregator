namespace GameDataAggregator.Infrastructure.Providers.Igdb;

// Only the fields this service requests. Shape of POST /games: https://api-docs.igdb.com/#game

/// <summary>One game from an IGDB search.</summary>
internal sealed record IgdbGame(
    long Id,
    string Name,
    string? Slug,
    string? Url,
    string? Summary,
    long? FirstReleaseDate,
    double? TotalRating,
    int? TotalRatingCount,
    double? AggregatedRating,
    int? Hypes,
    IgdbImage? Cover,
    IReadOnlyList<IgdbNamedEntity>? Genres,
    IReadOnlyList<IgdbNamedEntity>? Themes,
    IReadOnlyList<IgdbNamedEntity>? Platforms,
    IReadOnlyList<IgdbInvolvedCompany>? InvolvedCompanies);

/// <summary>A reference to an IGDB image.</summary>
internal sealed record IgdbImage(string? ImageId);

/// <summary>Anything IGDB lists by name, such as a genre or platform.</summary>
internal sealed record IgdbNamedEntity(string Name);

/// <summary>A company and its role in making a game.</summary>
internal sealed record IgdbInvolvedCompany(IgdbNamedEntity? Company, bool Developer, bool Publisher);
