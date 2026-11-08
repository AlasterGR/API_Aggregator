using GameDataAggregator.Api.Contracts;
using GameDataAggregator.Core.Aggregation;
using GameDataAggregator.Core.Statistics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GameDataAggregator.Api.Controllers;

/// <summary>Request statistics for the external APIs.</summary>
[ApiController]
[Authorize]
[Route("api/v1/statistics")]
public sealed class StatisticsController(
    IRequestStatisticsStore statisticsStore,
    IAggregationService aggregationService,
    TimeProvider timeProvider) : ControllerBase
{
    /// <summary>
    /// Returns, per external API, the total number of requests and the average response time,
    /// overall and per performance bucket (fast / average / slow).
    /// </summary>
    /// <remarks>
    /// Every HTTP attempt is counted, including retries. Responses served from the cache are not,
    /// because they never reach the external API. Statistics are kept in memory since start-up.
    /// Needs an access token.
    /// </remarks>
    /// <response code="200">Statistics for every registered API.</response>
    /// <response code="401">No valid access token was sent.</response>
    [HttpGet]
    [ProducesResponseType<StatisticsResponse>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    public ActionResult<StatisticsResponse> Get() => new StatisticsResponse
    {
        GeneratedAt = timeProvider.GetUtcNow(),
        Apis = statisticsStore.GetSnapshot(aggregationService.SourceNames),
    };
}
