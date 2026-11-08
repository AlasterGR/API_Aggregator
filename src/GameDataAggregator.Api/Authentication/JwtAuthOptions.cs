using System.ComponentModel.DataAnnotations;
using System.Text;
using Microsoft.Extensions.Options;

namespace GameDataAggregator.Api.Authentication;

/// <summary>Configuration section "Jwt": how access tokens are signed and checked.</summary>
public sealed class JwtAuthOptions
{
    public const string SectionName = "Jwt";

    /// <summary>The fewest bytes a signing key may have. HMAC-SHA256 needs a key at least as long as its 256-bit output.</summary>
    public const int MinimumKeyBytes = 32;

    /// <summary>Who issues the tokens. A token from anyone else is refused.</summary>
    [Required]
    public string Issuer { get; set; } = "GameDataAggregator";

    /// <summary>Who the tokens are for. A token meant for another service is refused.</summary>
    [Required]
    public string Audience { get; set; } = "GameDataAggregator.Api";

    /// <summary>
    /// The shared secret that signs and checks tokens, at least <see cref="MinimumKeyBytes"/> bytes of UTF-8 text.
    /// Keep it out of source control: use user-secrets while developing and an environment variable or a vault
    /// elsewhere. In Development it may be left empty, and a random key is then made at each start-up.
    /// </summary>
    public string SigningKey { get; set; } = string.Empty;

    /// <summary>How long a token from the development token endpoint stays valid.</summary>
    [Range(typeof(TimeSpan), "00:01:00", "1.00:00:00")]
    public TimeSpan TokenLifetime { get; set; } = TimeSpan.FromHours(1);
}

/// <summary>Checks the signing key, which may only be missing in Development.</summary>
internal sealed class JwtAuthOptionsValidator(IHostEnvironment environment) : IValidateOptions<JwtAuthOptions>
{
    /// <summary>Refuses a missing key outside Development, and a short key anywhere.</summary>
    public ValidateOptionsResult Validate(string? name, JwtAuthOptions options)
    {
        //  Allow an empty key only in Development, where a random one is made instead.
        if (string.IsNullOrEmpty(options.SigningKey))
        {
            return environment.IsDevelopment()
                ? ValidateOptionsResult.Success
                : ValidateOptionsResult.Fail($"'{JwtAuthOptions.SectionName}:{nameof(JwtAuthOptions.SigningKey)}' must be set outside Development.");
        }

        //  Refuse a key too short to sign safely.
        return Encoding.UTF8.GetByteCount(options.SigningKey) < JwtAuthOptions.MinimumKeyBytes
            ? ValidateOptionsResult.Fail($"'{JwtAuthOptions.SectionName}:{nameof(JwtAuthOptions.SigningKey)}' must be at least {JwtAuthOptions.MinimumKeyBytes} bytes long.")
            : ValidateOptionsResult.Success;
    }
}
