namespace GameDataAggregator.Infrastructure.Providers.Steam;

// Only the fields this service uses.
// Store search:  GET {store}/storesearch/?term=...&l=english&cc=US
// Player count:  GET {webapi}/ISteamUserStats/GetNumberOfCurrentPlayers/v1/?appid=...

/// <summary>The Steam store's answer to a search.</summary>
internal sealed record SteamStoreSearchResponse(IReadOnlyList<SteamStoreItem>? Items);

/// <summary>One product from a Steam store search.</summary>
internal sealed record SteamStoreItem(
    string Type,
    string Name,
    int Id,
    SteamPrice? Price,
    string? TinyImage,
    string? Metascore,
    SteamPlatforms? Platforms);

/// <summary>Prices are in the currency's minor unit (cents).</summary>
internal sealed record SteamPrice(string Currency, int Initial, int Final);

/// <summary>The systems a game runs on.</summary>
internal sealed record SteamPlatforms(bool Windows, bool Mac, bool Linux);

/// <summary>Steam's answer to a player count request.</summary>
internal sealed record SteamPlayerCountResponse(SteamPlayerCount? Response);

/// <summary>The current player count, and whether Steam found it.</summary>
internal sealed record SteamPlayerCount(int? PlayerCount, int Result);
