using GameDataAggregator.Api.Contracts;
using GameDataAggregator.Api.RateLimiting;
using GameDataAggregator.Core.Aggregation;
using GameDataAggregator.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace GameDataAggregator.Api.Controllers;

/// <summary>Aggregated video game data from all external APIs.</summary>
[ApiController]
[Authorize]
[Route("api/v1/aggregation")]
public sealed class AggregationController(IAggregationService aggregationService) : ControllerBase
{
    /// <summary>Searches every source for a game and returns the combined, filtered and sorted result.</summary>
    /// <remarks>
    /// Sources are queried in parallel. A source that fails does not fail the response: it is still 200, and that
    /// source's entry in <c>sources</c> says what happened (<c>Live</c>, <c>Cached</c>, <c>Fallback</c>,
    /// <c>Failed</c> or <c>NotConfigured</c>). Searches are rate-limited per client and need an access token.
    /// </remarks>
    /// <response code="200">The aggregated data, with a per-source status.</response>
    /// <response code="400">Invalid query parameters. Every invalid field is listed.</response>
    /// <response code="401">No valid access token was sent.</response>
    /// <response code="429">Too many searches from this client; see the Retry-After header.</response>
    [HttpGet]
    [EnableRateLimiting(SearchRateLimiting.PolicyName)]
    [ProducesResponseType<AggregationResult>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests, "application/problem+json")]
    public async Task<ActionResult<AggregationResult>> Get(
        [FromQuery] AggregationRequest request,
        CancellationToken cancellationToken) =>
        await aggregationService.AggregateAsync(request.ToQuery(), cancellationToken);
}
