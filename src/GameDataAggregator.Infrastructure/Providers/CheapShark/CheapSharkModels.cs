namespace GameDataAggregator.Infrastructure.Providers.CheapShark;

// Only the fields this service uses. Docs: https://apidocs.cheapshark.com/
// CheapShark sends most numbers as strings ("4.19"); the JSON options accept that.

/// <summary>One deal from a CheapShark search.</summary>
internal sealed record CheapSharkDeal(
    string Title,
    string DealId,
    string StoreId,
    string GameId,
    double SalePrice,
    double NormalPrice,
    string? IsOnSale,
    double Savings,
    double MetacriticScore,
    string? SteamRatingText,
    double? SteamRatingPercent,
    string? SteamAppId,
    long ReleaseDate,
    long LastChange,
    double DealRating,
    string? Thumb);

/// <summary>A store's id and name.</summary>
internal sealed record CheapSharkStore(string StoreId, string StoreName);
