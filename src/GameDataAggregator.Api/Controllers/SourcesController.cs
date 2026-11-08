using GameDataAggregator.Core.Aggregation;
using GameDataAggregator.Core.Models;
using Microsoft.AspNetCore.Mvc;

namespace GameDataAggregator.Api.Controllers;

/// <summary>The external APIs this service aggregates.</summary>
[ApiController]
[Route("api/v1/sources")]
public sealed class SourcesController(IAggregationService aggregationService) : ControllerBase
{
    /// <summary>
    /// Lists the enabled sources and the categories each can give. The names are the valid values of the aggregation
    /// endpoint's <c>sources</c> parameter. A source is skipped when a search asks only for categories it cannot give.
    /// </summary>
    /// <response code="200">Sources with their categories, in the order they are queried.</response>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<SourceInfo>>(StatusCodes.Status200OK, "application/json")]
    public ActionResult<IReadOnlyList<SourceInfo>> Get() => Ok(aggregationService.Sources);
}
