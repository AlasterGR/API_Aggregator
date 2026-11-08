using GameDataAggregator.Api.Authentication;
using GameDataAggregator.Api.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GameDataAggregator.Api.Controllers;

/// <summary>Access tokens for local development.</summary>
[ApiController]
[Route("api/v1/auth/token")]
[AllowAnonymous]
public sealed class DevelopmentTokenController(
    JwtTokenIssuer tokenIssuer,
    IHostEnvironment environment,
    TimeProvider timeProvider) : ControllerBase
{
    /// <summary>The subject written into every development token.</summary>
    public const string DeveloperSubject = "developer";

    /// <summary>Issues a short-lived access token for the protected endpoints. Exists only in Development.</summary>
    /// <remarks>
    /// Anyone who can reach this endpoint gets a token, so it is switched off outside Development. In a real
    /// deployment, tokens come from an identity provider that shares the signing key or publishes its keys.
    /// </remarks>
    /// <response code="200">A signed token and when it expires.</response>
    /// <response code="404">Not in Development, so no tokens are given out.</response>
    [HttpPost]
    [ProducesResponseType<TokenResponse>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public ActionResult<TokenResponse> Post()
    {
        //  Act as if the endpoint does not exist outside Development.
        if (!environment.IsDevelopment())
        {
            return NotFound();
        }

        //  Sign a token and say when it expires.
        var token = tokenIssuer.Issue(DeveloperSubject);
        return new TokenResponse
        {
            AccessToken = token.AccessToken,
            TokenType = "Bearer",
            ExpiresIn = (int)(token.ExpiresAt - timeProvider.GetUtcNow()).TotalSeconds,
            ExpiresAt = token.ExpiresAt,
        };
    }
}
