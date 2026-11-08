using System.ComponentModel.DataAnnotations;

namespace GameDataAggregator.Infrastructure.Providers.Igdb;

/// <summary>
/// Configuration section "ExternalApis:Igdb". The credentials are not here: IGDB is owned by Twitch and
/// uses the Twitch application credentials from "ExternalApis:TwitchAuth".
/// </summary>
public sealed class IgdbOptions : ExternalApiOptions
{
    public const string SectionName = "Igdb";

    [Required, BaseAddress]
    public Uri BaseUrl { get; set; } = new("https://api.igdb.com/v4/");

    /// <summary>IGDB image size preset used for cover URLs (see https://api-docs.igdb.com/#images).</summary>
    [Required]
    [RegularExpression("^[a-z0-9_]+$")]
    public string CoverImageSize { get; set; } = "t_cover_big";
}
