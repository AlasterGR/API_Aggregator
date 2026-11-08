using System.Globalization;
using GameDataAggregator.Infrastructure.Providers;
using Microsoft.Extensions.Options;

namespace GameDataAggregator.Infrastructure.Authentication.Twitch;

/// <summary>
/// Start-up check that a Twitch token request fits inside one attempt of a source that signs in with Twitch.
/// The token is fetched inside that attempt, so the attempt's time limit would otherwise cut the token
/// request short.
/// </summary>
internal sealed class TwitchTimeoutFitsAttemptCheck<TOptions>(IOptions<TwitchAuthOptions> twitchOptions)
    : IValidateOptions<TOptions>
    where TOptions : ExternalApiOptions
{
    /// <summary>Fails when the token time limit is not shorter than the source's attempt time limit.</summary>
    public ValidateOptionsResult Validate(string? name, TOptions options)
    {
        //  Pass when the token request ends before the attempt does.
        var tokenTimeout = twitchOptions.Value.Timeout;
        if (tokenTimeout < options.AttemptTimeout)
        {
            return ValidateOptionsResult.Success;
        }

        //  Otherwise explain which two settings clash.
        return ValidateOptionsResult.Fail(string.Create(
            CultureInfo.InvariantCulture,
            $"'{ProviderRegistration.ConfigurationRoot}:{TwitchAuthOptions.SectionName}:Timeout' ({tokenTimeout}) must be shorter than " +
            $"the AttemptTimeout of {typeof(TOptions).Name} ({options.AttemptTimeout}), because the token request runs inside each attempt."));
    }
}
