using GameDataAggregator.Core.Aggregation;
using Microsoft.AspNetCore.Mvc;

namespace GameDataAggregator.Api.Controllers;

/// <summary>The external APIs this service aggregates.</summary>
[ApiController]
[Route("api/v1/sources")]
public sealed class SourcesController(IAggregationService aggregationService) : ControllerBase
{
    /// <summary>Lists the enabled sources: the valid values of the aggregation endpoint's <c>sources</c> parameter.</summary>
    /// <response code="200">Source names, in the order they are queried.</response>
    [HttpGet]
    [ProducesResponseType<IReadOnlyCollection<string>>(StatusCodes.Status200OK, "application/json")]
    public ActionResult<IReadOnlyCollection<string>> Get() => Ok(aggregationService.SourceNames);
}
