using System.ComponentModel.DataAnnotations;

namespace GameDataAggregator.Infrastructure.Authentication.Twitch;

/// <summary>
/// Configuration section "ExternalApis:TwitchAuth": the credentials of a Twitch developer application.
/// They are shared by every Twitch-owned API (IGDB today, the Twitch Helix API later).
/// Register an application at https://dev.twitch.tv/console to get them, and keep them out of
/// source control (user-secrets / environment variables).
/// </summary>
public sealed class TwitchAuthOptions
{
    public const string SectionName = "TwitchAuth";

    /// <summary>OAuth 2.0 token endpoint (client credentials flow).</summary>
    [Required]
    public Uri TokenUrl { get; set; } = new("https://id.twitch.tv/oauth2/token");

    public string? ClientId { get; set; }

    public string? ClientSecret { get; set; }

    /// <summary>
    /// Time limit for a token request. The request runs inside one attempt of the source that needs the token,
    /// so it must be shorter than that source's <c>AttemptTimeout</c>. Start-up checks this.
    /// </summary>
    [Range(typeof(TimeSpan), "00:00:00.100", "00:01:00")]
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(2);

    public bool HasCredentials =>
        !string.IsNullOrWhiteSpace(ClientId) && !string.IsNullOrWhiteSpace(ClientSecret);
}
