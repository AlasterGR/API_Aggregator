using System.ComponentModel.DataAnnotations;

namespace GameDataAggregator.Infrastructure.Providers.Rawg;

/// <summary>Configuration section "ExternalApis:Rawg".</summary>
public sealed class RawgOptions : ExternalApiOptions
{
    public const string SectionName = "Rawg";

    [Required, BaseAddress]
    public Uri BaseUrl { get; set; } = new("https://api.rawg.io/api/");

    /// <summary>Free key from https://rawg.io/apidocs. Keep it out of source control (user-secrets / env var).</summary>
    public string? ApiKey { get; set; }
}
