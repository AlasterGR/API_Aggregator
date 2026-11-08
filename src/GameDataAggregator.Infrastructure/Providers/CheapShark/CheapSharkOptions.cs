using System.ComponentModel.DataAnnotations;

namespace GameDataAggregator.Infrastructure.Providers.CheapShark;

/// <summary>Configuration section "ExternalApis:CheapShark". No API key required.</summary>
public sealed class CheapSharkOptions : ExternalApiOptions
{
    public const string SectionName = "CheapShark";

    [Required, BaseAddress]
    public Uri BaseUrl { get; set; } = new("https://www.cheapshark.com/api/1.0/");

    /// <summary>The store list rarely changes, so it is cached much longer than search results.</summary>
    [Range(typeof(TimeSpan), "00:01:00", "7.00:00:00")]
    public TimeSpan StoreListCacheDuration { get; set; } = TimeSpan.FromHours(24);

    /// <summary>Time limit for loading the store list. It gets one try, so deals never wait long for store names.</summary>
    [Range(typeof(TimeSpan), "00:00:00.100", "00:01:00")]
    public TimeSpan SideRequestTimeout { get; set; } = TimeSpan.FromSeconds(2);
}
