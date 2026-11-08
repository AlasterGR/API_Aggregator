using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace GameDataAggregator.Api.Authentication;

/// <summary>
/// The one key that both signs and checks tokens, shared for the app's whole life.
/// It comes from configuration, or in Development without a configured key it is made at random, so the app still
/// runs straight after cloning. Tokens signed with a random key stop working when the app restarts.
/// </summary>
public sealed partial class JwtSigningKey
{
    /// <summary>Loads the configured key, or makes a random one when none is set.</summary>
    public JwtSigningKey(IOptions<JwtAuthOptions> options, ILogger<JwtSigningKey> logger)
    {
        //  Use the configured key when there is one.
        var configured = options.Value.SigningKey;
        if (!string.IsNullOrEmpty(configured))
        {
            Key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configured));
            return;
        }

        //  Otherwise make a random key, which the options check allows only in Development.
        Key = new SymmetricSecurityKey(RandomNumberGenerator.GetBytes(JwtAuthOptions.MinimumKeyBytes));
        LogRandomKey(logger);
    }

    /// <summary>The key itself.</summary>
    public SymmetricSecurityKey Key { get; }

    /// <summary>Logs that tokens will not survive a restart.</summary>
    [LoggerMessage(Level = LogLevel.Warning, Message = "No 'Jwt:SigningKey' is configured, so a random key is used. Tokens stop working when the app restarts.")]
    private static partial void LogRandomKey(ILogger logger);
}
