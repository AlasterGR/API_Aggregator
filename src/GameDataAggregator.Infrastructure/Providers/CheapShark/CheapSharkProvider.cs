using System.Globalization;
using System.Net.Http.Json;
using GameDataAggregator.Core.Abstractions;
using GameDataAggregator.Core.Models;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using static GameDataAggregator.Infrastructure.Providers.ProviderMapping;

namespace GameDataAggregator.Infrastructure.Providers.CheapShark;

/// <summary>
/// CheapShark: current prices and discounts for PC games across digital stores (Steam, GOG, Humble...).
/// No API key required.
/// </summary>
public sealed partial class CheapSharkProvider(
    HttpClient httpClient,
    IHttpClientFactory httpClientFactory,
    HybridCache cache,
    IOptions<CheapSharkOptions> options,
    ILogger<CheapSharkProvider> logger) : IGameDataProvider
{
    public const string SourceName = "CheapShark";

    /// <summary>The separate HttpClient used to load the store list (see ProviderRegistration.AddSideRequestClient).</summary>
    public const string SideRequestClientName = "CheapShark.SideRequests";

    /// <summary>CheapShark's documented maximum page size for /deals.</summary>
    private const int MaxPageSize = 60;

    private const string StoreListCacheKey = "cheapshark:stores";

    public string Name => SourceName;

    /// <summary>The only category of item this source gives.</summary>
    public IReadOnlySet<ItemCategory> Categories { get; } = new HashSet<ItemCategory> { ItemCategory.Deal };

    /// <summary>Finds deals for the game, labelled with the store names.</summary>
    public async Task<IReadOnlyList<AggregatedItem>> FetchAsync(string search, CancellationToken cancellationToken)
    {
        //  Build the deal search, within CheapShark's page size limit.
        var pageSize = Math.Min(options.Value.MaxResults, MaxPageSize);
        var requestUri = $"deals?title={Uri.EscapeDataString(search)}&pageSize={pageSize}";

        //  Fetch the deals and the store names at the same time.
        var dealsTask = httpClient.GetFromJsonAsync<List<CheapSharkDeal>>(requestUri, CamelCaseJson, cancellationToken);
        var storesTask = GetStoreNamesAsync(cancellationToken);
        await Task.WhenAll(dealsTask, storesTask);

        //  Collect both answers.
        var deals = await dealsTask ?? [];
        var stores = await storesTask;

        return deals.Select(deal => Map(deal, stores)).ToList();
    }

    /// <summary>
    /// Store names are a nice-to-have: if they cannot be loaded the deals are still returned, labelled
    /// with the store id. A secondary call should never fail the primary data.
    /// </summary>
    private async Task<IReadOnlyDictionary<string, string>> GetStoreNamesAsync(CancellationToken cancellationToken)
    {
        //  Use the saved store list, fetching it only when it has expired.
        try
        {
            return await cache.GetOrCreateAsync(
                StoreListCacheKey,
                async token =>
                {
                    //  Load the list through the side connection, which tries once and gives up quickly.
                    var storesUri = new Uri(options.Value.BaseUrl, "stores");
                    var sideClient = httpClientFactory.CreateClient(SideRequestClientName);
                    var stores = await sideClient.GetFromJsonAsync<List<CheapSharkStore>>(storesUri, CamelCaseJson, token) ?? [];
                    return (IReadOnlyDictionary<string, string>)stores.ToDictionary(s => s.StoreId, s => s.StoreName);
                },
                new HybridCacheEntryOptions
                {
                    Expiration = options.Value.StoreListCacheDuration,
                    LocalCacheExpiration = options.Value.StoreListCacheDuration,
                },
                cancellationToken: cancellationToken);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            //  Carry on without store names if they cannot be loaded.
            LogStoreListFailed(logger, ex);
            return new Dictionary<string, string>();
        }
    }

    /// <summary>Turns a CheapShark deal into a common item.</summary>
    private static AggregatedItem Map(CheapSharkDeal deal, IReadOnlyDictionary<string, string> stores)
    {
        //  Name the store, falling back to its number.
        var storeName = stores.GetValueOrDefault(deal.StoreId) ?? $"Store #{deal.StoreId}";
        var isOnSale = deal.IsOnSale == "1";

        //  Copy the deal's details into the common shape.
        return new AggregatedItem
        {
            Source = SourceName,
            Category = ItemCategory.Deal,
            Title = deal.Title,

            //  Link through CheapShark's official redirect, which takes the deal id as it comes.
            Url = new Uri($"https://www.cheapshark.com/redirect?dealID={deal.DealId}"),
            ImageUrl = ToUri(deal.Thumb),
            Date = deal.LastChange > 0 ? DateTimeOffset.FromUnixTimeSeconds(deal.LastChange) : null,
            DateKind = DateKind.PriceUpdated,
            Tags = isOnSale ? [storeName, "On Sale"] : [storeName],
            Metrics = Metrics(
                ("salePrice", deal.SalePrice),
                ("normalPrice", deal.NormalPrice),
                ("savingsPercent", Math.Round(deal.Savings, 2)),
                ("dealRating", deal.DealRating),
                ("metacriticScore", deal.MetacriticScore > 0 ? deal.MetacriticScore : null),
                ("steamRatingPercent", deal.SteamRatingPercent > 0 ? deal.SteamRatingPercent : null)),
            Attributes = Attributes(
                ("store", storeName),
                ("currency", "USD"),
                ("steamRating", deal.SteamRatingText),
                ("steamAppId", deal.SteamAppId),
                ("cheapSharkGameId", deal.GameId),
                ("releaseDate", deal.ReleaseDate > 0
                    ? DateTimeOffset.FromUnixTimeSeconds(deal.ReleaseDate).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                    : null)),
        };
    }

    /// <summary>Logs that the store list could not be loaded.</summary>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not load the CheapShark store list; deals will show store ids instead of names")]
    private static partial void LogStoreListFailed(ILogger logger, Exception exception);
}
