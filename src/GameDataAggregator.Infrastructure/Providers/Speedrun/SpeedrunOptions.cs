using System.ComponentModel.DataAnnotations;

namespace GameDataAggregator.Infrastructure.Providers.Speedrun;

/// <summary>Configuration section "ExternalApis:Speedrun". The public read API needs no key.</summary>
public sealed class SpeedrunOptions : ExternalApiOptions
{
    public const string SectionName = "Speedrun";

    [Required, BaseAddress]
    public Uri BaseUrl { get; set; } = new("https://www.speedrun.com/api/v1/");
}
