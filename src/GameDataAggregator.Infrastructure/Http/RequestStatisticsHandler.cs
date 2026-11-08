using System.Net;
using GameDataAggregator.Core.Statistics;

namespace GameDataAggregator.Infrastructure.Http;

/// <summary>
/// Records the duration and outcome of every HTTP request sent to an external API.
/// It sits inside the resilience handler, so each retry attempt is measured as its own request,
/// and cache hits (which never reach HttpClient) are not counted at all.
/// </summary>
internal sealed class RequestStatisticsHandler(
    string apiName,
    IRequestStatisticsStore store,
    TimeProvider timeProvider) : DelegatingHandler
{
    /// <summary>Sends the request and records how long it took and whether it succeeded.</summary>
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        //  Start timing, assuming failure until an answer arrives.
        var startedAt = timeProvider.GetTimestamp();
        var isSuccess = false;

        //  Send the request and note whether the answer counts as a success.
        try
        {
            var response = await base.SendAsync(request, cancellationToken);
            isSuccess = !IsFailure(response.StatusCode);
            return response;
        }
        finally
        {
            //  Record the time until the answer started arriving, even when the request failed.
            store.Record(apiName, timeProvider.GetElapsedTime(startedAt), isSuccess);
        }
    }

    /// <summary>
    /// A request "failed" when the API could not answer properly: server errors, throttling or a timeout.
    /// Other 4xx responses (e.g. Steam's 404 for a game without player data) are valid answers.
    /// </summary>
    private static bool IsFailure(HttpStatusCode status) =>
        (int)status >= 500 || status is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests;
}
