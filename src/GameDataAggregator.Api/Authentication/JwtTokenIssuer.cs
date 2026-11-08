using System.Security.Claims;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace GameDataAggregator.Api.Authentication;

/// <summary>A signed access token and when it expires.</summary>
/// <param name="AccessToken">The token, to send as <c>Authorization: Bearer &lt;token&gt;</c>.</param>
/// <param name="ExpiresAt">When the token stops being accepted.</param>
public sealed record IssuedToken(string AccessToken, DateTimeOffset ExpiresAt);

/// <summary>
/// Signs access tokens with the app's own key. Only the development token endpoint and the tests use it. In a real
/// deployment an identity provider issues the tokens, and this API only checks them.
/// </summary>
public sealed class JwtTokenIssuer(IOptions<JwtAuthOptions> options, JwtSigningKey signingKey, TimeProvider timeProvider)
{
    private static readonly JsonWebTokenHandler Handler = new();

    /// <summary>Creates a token for <paramref name="subject"/>, valid for <paramref name="lifetime"/> or the configured lifetime.</summary>
    public IssuedToken Issue(string subject, TimeSpan? lifetime = null)
    {
        //  Work out when the token starts and stops being valid.
        var settings = options.Value;
        var now = timeProvider.GetUtcNow();
        var expiresAt = now + (lifetime ?? settings.TokenLifetime);

        //  Describe the token: who it is for, who made it, its lifetime and a unique id.
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = settings.Issuer,
            Audience = settings.Audience,
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = expiresAt.UtcDateTime,
            Subject = new ClaimsIdentity(
            [
                new Claim(JwtRegisteredClaimNames.Sub, subject),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            ]),
            SigningCredentials = new SigningCredentials(signingKey.Key, SecurityAlgorithms.HmacSha256),
        };

        //  Sign it and hand it back with its expiry.
        return new IssuedToken(Handler.CreateToken(descriptor), expiresAt);
    }
}
