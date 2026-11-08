namespace GameDataAggregator.UnitTests.TestDoubles;

/// <summary>An <see cref="IHttpClientFactory"/> whose clients all send through the same handler.</summary>
internal sealed class SingleHandlerHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
{
    /// <summary>Creates a client that shares the one handler.</summary>
    public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
}
