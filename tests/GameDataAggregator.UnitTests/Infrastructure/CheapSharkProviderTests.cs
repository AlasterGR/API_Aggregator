using System.Net;
using GameDataAggregator.Core.Models;
using GameDataAggregator.Infrastructure.Providers.CheapShark;
using GameDataAggregator.Tests.Shared;
using GameDataAggregator.UnitTests.TestDoubles;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace GameDataAggregator.UnitTests.Infrastructure;

/// <summary>Tests the CheapShark source against sample answers.</summary>
public sealed class CheapSharkProviderTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>Creates the source with a fake network that answers deals and stores with the given results.</summary>
    private static (CheapSharkProvider Provider, StubHttpMessageHandler Http) Create(
        HttpStatusCode dealsStatus = HttpStatusCode.OK,
        HttpStatusCode storesStatus = HttpStatusCode.OK)
    {
        //  Answer deal and store requests with the sample data.
        var http = new StubHttpMessageHandler(request => request.RequestUri!.AbsolutePath switch
        {
            "/api/1.0/deals" => StubHttpMessageHandler.Json(SampleResponses.CheapSharkDeals, dealsStatus),
            "/api/1.0/stores" => StubHttpMessageHandler.Json(SampleResponses.CheapSharkStores, storesStatus),
            _ => new HttpResponseMessage(HttpStatusCode.NotFound),
        });

        //  Build the source on top of the fake network.
        var options = new CheapSharkOptions { MaxResults = 5 };
        var client = new HttpClient(http) { BaseAddress = options.BaseUrl };
        var provider = new CheapSharkProvider(client, new SingleHandlerHttpClientFactory(http), TestData.NewCache(), Options.Create(options), NullLogger<CheapSharkProvider>.Instance);
        return (provider, http);
    }

    /// <summary>Checks that deals are turned into items labelled with store names.</summary>
    [Fact]
    public async Task Maps_deals_with_store_names()
    {
        //  Set up the source.
        var (provider, http) = Create();

        //  Search it.
        var items = await provider.FetchAsync("witcher", Ct);

        //  Expect every detail of the first deal to be copied across, and the search to be sent.
        Assert.Equal(2, items.Count);
        var deal = items[0];
        Assert.Equal("CheapShark", deal.Source);
        Assert.Equal(ItemCategory.Deal, deal.Category);
        Assert.Equal("The Witcher 3: Wild Hunt", deal.Title);
        Assert.Equal("https://www.cheapshark.com/redirect?dealID=abc%2F123%3D", deal.Url!.OriginalString);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1793867755), deal.Date);
        Assert.Equal(DateKind.PriceUpdated, deal.DateKind);
        Assert.Equal(["Steam", "On Sale"], deal.Tags);
        Assert.Equal(9.99, deal.Metrics["salePrice"]);
        Assert.Equal(39.99, deal.Metrics["normalPrice"]);
        Assert.Equal(75.02, deal.Metrics["savingsPercent"]);
        Assert.Equal("Steam", deal.Attributes["store"]);
        Assert.Equal("2015-05-18", deal.Attributes["releaseDate"]);
        Assert.Contains(http.Requests, uri => uri.Query.Contains("title=witcher", StringComparison.Ordinal));
    }

    /// <summary>Checks that values CheapShark reports as zero or missing are left out.</summary>
    [Fact]
    public async Task Leaves_out_metrics_and_attributes_the_api_reports_as_zero_or_missing()
    {
        //  Set up the source.
        var (provider, _) = Create();

        //  Search it and take the deal with missing values.
        var reigns = (await provider.FetchAsync("witcher", Ct))[1];

        //  Expect those values to be absent.
        Assert.Equal(["Epic Games Store"], reigns.Tags);
        Assert.False(reigns.Metrics.ContainsKey("metacriticScore"));
        Assert.False(reigns.Metrics.ContainsKey("steamRatingPercent"));
        Assert.False(reigns.Attributes.ContainsKey("releaseDate"));
        Assert.False(reigns.Attributes.ContainsKey("steamAppId"));
    }

    /// <summary>Checks that deals still come back when store names cannot be loaded.</summary>
    [Fact]
    public async Task Still_returns_deals_when_the_store_list_is_unavailable()
    {
        //  Set up the source with the store list failing.
        var (provider, _) = Create(storesStatus: HttpStatusCode.InternalServerError);

        //  Search it.
        var items = await provider.FetchAsync("witcher", Ct);

        //  Expect the deals, labelled with store numbers.
        Assert.Equal(2, items.Count);
        Assert.Equal("Store #1", items[0].Attributes["store"]);
    }

    /// <summary>Checks that the store list is fetched once and reused.</summary>
    [Fact]
    public async Task Caches_the_store_list_across_searches()
    {
        //  Set up the source.
        var (provider, http) = Create();

        //  Run two different searches.
        await provider.FetchAsync("witcher", Ct);
        await provider.FetchAsync("cyberpunk", Ct);

        //  Expect one store list request and two deal searches.
        Assert.Single(http.Requests, uri => uri.AbsolutePath.EndsWith("/stores", StringComparison.Ordinal));
        Assert.Equal(2, http.Requests.Count(uri => uri.AbsolutePath.EndsWith("/deals", StringComparison.Ordinal)));
    }

    /// <summary>Checks that a failed deal search is passed on as an error.</summary>
    [Fact]
    public async Task Throws_when_the_deals_request_fails()
    {
        //  Set up the source with the deal search failing.
        var (provider, _) = Create(dealsStatus: HttpStatusCode.BadGateway);

        //  Expect the search to fail.
        await Assert.ThrowsAsync<HttpRequestException>(() => provider.FetchAsync("witcher", Ct));
    }
}
