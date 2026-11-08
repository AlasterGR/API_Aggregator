using System.Collections.Concurrent;
using System.Net;
using System.Text;

namespace GameDataAggregator.Tests.Shared;

/// <summary>
/// Replaces the network: every request is answered by <paramref name="respond"/>, and all requests
/// are recorded so tests can assert on what was sent.
/// </summary>
internal sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
{
    public ConcurrentQueue<Uri> Requests { get; } = new();

    /// <summary>Full details of every request, including headers and body.</summary>
    public ConcurrentQueue<RecordedRequest> Received { get; } = new();

    /// <summary>Creates a JSON answer with the given body and status.</summary>
    public static HttpResponseMessage Json(string body, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    /// <summary>Records the request and answers it as the test decided.</summary>
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        //  Keep the address and the full details of the request.
        Requests.Enqueue(request.RequestUri!);
        Received.Enqueue(new RecordedRequest(
            request.Method,
            request.RequestUri!,
            request.Headers.ToDictionary(h => h.Key, h => string.Join(",", h.Value), StringComparer.OrdinalIgnoreCase),
            request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken)));

        return respond(request);
    }
}

/// <summary>What a test request contained.</summary>
internal sealed record RecordedRequest(
    HttpMethod Method,
    Uri Uri,
    IReadOnlyDictionary<string, string> Headers,
    string? Body);
