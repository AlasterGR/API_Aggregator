using System.Net;
using GameDataAggregator.Core.Statistics;
using GameDataAggregator.Infrastructure.Http;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace GameDataAggregator.UnitTests.Infrastructure;

/// <summary>Tests how each outgoing request is measured.</summary>
public sealed class RequestStatisticsHandlerTests
{
    private readonly FakeTimeProvider _time;
    private readonly InMemoryRequestStatisticsStore _store;

    /// <summary>Creates a store that reads the same fake clock as the handler.</summary>
    public RequestStatisticsHandlerTests()
    {
        _time = new FakeTimeProvider();
        _store = new InMemoryRequestStatisticsStore(Options.Create(new StatisticsOptions()), _time);
    }

    /// <summary>Sends one request through the handler; the fake "network" takes <paramref name="latency"/>.</summary>
    private async Task SendAsync(TimeSpan latency, Func<HttpResponseMessage> respond)
    {
        //  Make the fake network move the clock forward before it answers.
        var network = new DelegatingStub(() =>
        {
            _time.Advance(latency);
            return respond();
        });

        //  Send one request through the measuring step.
        using var invoker = new HttpMessageInvoker(new RequestStatisticsHandler("TestApi", _store, _time) { InnerHandler = network });
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://example.test/");
        using var _ = await invoker.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private ApiStatistics Stats => Assert.Single(_store.GetSnapshot());

    /// <summary>Checks that a successful request's time is recorded in the right speed group.</summary>
    [Fact]
    public async Task Records_the_duration_of_a_successful_request()
    {
        //  Send a request that takes 150 ms.
        await SendAsync(TimeSpan.FromMilliseconds(150), () => new HttpResponseMessage(HttpStatusCode.OK));

        //  Expect one successful request of 150 ms in the average group.
        Assert.Equal(1, Stats.TotalRequests);
        Assert.Equal(0, Stats.FailedRequests);
        Assert.Equal(150, Stats.AverageResponseTimeMs);
        Assert.Equal(1, Stats.Buckets.Single(b => b.Bucket == PerformanceBucket.Average).Requests);
    }

    /// <summary>Checks that server errors and throttling count as failures while other errors do not.</summary>
    [Theory]
    [InlineData(HttpStatusCode.InternalServerError, true)]
    [InlineData(HttpStatusCode.ServiceUnavailable, true)]
    [InlineData(HttpStatusCode.TooManyRequests, true)]
    [InlineData(HttpStatusCode.RequestTimeout, true)]
    [InlineData(HttpStatusCode.NotFound, false)]
    [InlineData(HttpStatusCode.BadRequest, false)]
    public async Task Counts_server_errors_and_throttling_as_failures(HttpStatusCode status, bool isFailure)
    {
        //  Send a request that gets the given answer.
        await SendAsync(TimeSpan.FromMilliseconds(10), () => new HttpResponseMessage(status));

        //  Expect it to count as a failure only when it should.
        Assert.Equal(isFailure ? 1 : 0, Stats.FailedRequests);
    }

    /// <summary>Checks that a request that breaks is recorded as a failure and the error is passed on.</summary>
    [Fact]
    public async Task Records_a_failure_and_rethrows_when_the_request_throws()
    {
        //  Send a slow request that breaks, expecting the error back.
        await Assert.ThrowsAsync<HttpRequestException>(
            () => SendAsync(TimeSpan.FromMilliseconds(300), () => throw new HttpRequestException("connection reset")));

        //  Expect a failure recorded in the slow group.
        Assert.Equal(1, Stats.FailedRequests);
        Assert.Equal(1, Stats.Buckets.Single(b => b.Bucket == PerformanceBucket.Slow).Requests);
    }

    /// <summary>A fake network that answers every request with the given function.</summary>
    private sealed class DelegatingStub(Func<HttpResponseMessage> respond) : HttpMessageHandler
    {
        /// <summary>Answers the request.</summary>
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(respond());
    }
}
