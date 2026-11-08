using System.ComponentModel.DataAnnotations;

namespace GameDataAggregator.Infrastructure.Providers.Steam;

/// <summary>Configuration section "ExternalApis:Steam". The endpoints used need no API key.</summary>
public sealed class SteamOptions : ExternalApiOptions
{
    public const string SectionName = "Steam";

    /// <summary>Steam store API, used to find games by name.</summary>
    [Required, BaseAddress]
    public Uri StoreBaseUrl { get; set; } = new("https://store.steampowered.com/api/");

    /// <summary>Steam Web API, used for current player counts.</summary>
    [Required, BaseAddress]
    public Uri WebApiBaseUrl { get; set; } = new("https://api.steampowered.com/");

    /// <summary>Country code for store prices (ISO 3166-1 alpha-2).</summary>
    [Required, StringLength(2, MinimumLength = 2)]
    public string CountryCode { get; set; } = "US";

    [Required]
    public string Language { get; set; } = "english";

    /// <summary>Time limit for one player count request. It gets one try, so the search never waits long for counts.</summary>
    [Range(typeof(TimeSpan), "00:00:00.100", "00:01:00")]
    public TimeSpan SideRequestTimeout { get; set; } = TimeSpan.FromSeconds(2);
}
