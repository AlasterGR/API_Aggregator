using System.Net;
using GameDataAggregator.Core.Abstractions;
using GameDataAggregator.Core.Aggregation;
using GameDataAggregator.Core.Models;
using GameDataAggregator.UnitTests.TestDoubles;
using static GameDataAggregator.UnitTests.TestDoubles.TestData;

namespace GameDataAggregator.UnitTests.Aggregation;

/// <summary>Tests how one source is called, saved and backed up.</summary>
public sealed class ProviderExecutorTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>Checks that the first call reaches the source and the second uses the saved copy.</summary>
    [Fact]
    public async Task First_call_is_live_and_second_call_is_served_from_cache()
    {
        //  Set up a source.
        var provider = FakeGameDataProvider.Returning("Fake", Item("The Witcher"));
        var executor = NewExecutor();

        //  Search it twice.
        var first = await executor.ExecuteAsync(provider, "witcher", Ct);
        var second = await executor.ExecuteAsync(provider, "witcher", Ct);

        //  Expect one real call, with the second answer coming from the saved copy.
        Assert.Equal(SourceStatus.Live, first.Summary.Status);
        Assert.Equal(SourceStatus.Cached, second.Summary.Status);
        Assert.Equal(1, provider.Calls);
        Assert.Equal("The Witcher", Assert.Single(second.Items).Title);
        Assert.Equal(first.Summary.FetchedAt, second.Summary.FetchedAt);
    }

    /// <summary>Checks that searches differing only in letter case or spaces share a saved copy.</summary>
    [Fact]
    public async Task Search_terms_differing_only_in_case_or_whitespace_share_a_cache_entry()
    {
        //  Set up a source.
        var provider = FakeGameDataProvider.Returning("Fake", Item("The Witcher"));
        var executor = NewExecutor();

        //  Search with different case and spacing.
        await executor.ExecuteAsync(provider, "Witcher", Ct);
        var second = await executor.ExecuteAsync(provider, "  witcher ", Ct);

        //  Expect the second search to use the saved copy.
        Assert.Equal(SourceStatus.Cached, second.Summary.Status);
        Assert.Equal(1, provider.Calls);
    }

    /// <summary>Checks that each source keeps its own saved copies.</summary>
    [Fact]
    public async Task Different_providers_do_not_share_cache_entries()
    {
        //  Set up two sources.
        var executor = NewExecutor();
        var a = FakeGameDataProvider.Returning("A", Item("From A"));
        var b = FakeGameDataProvider.Returning("B", Item("From B"));

        //  Search both for the same thing.
        await executor.ExecuteAsync(a, "witcher", Ct);
        var fromB = await executor.ExecuteAsync(b, "witcher", Ct);

        //  Expect the second source to be asked itself.
        Assert.Equal(SourceStatus.Live, fromB.Summary.Status);
        Assert.Equal("From B", Assert.Single(fromB.Items).Title);
    }

    /// <summary>Checks that a failure with no backup is reported safely instead of thrown.</summary>
    [Fact]
    public async Task Failure_without_fallback_data_reports_failed_instead_of_throwing()
    {
        //  Set up a source that fails with an internal detail.
        var provider = FakeGameDataProvider.Throwing("Fake", new InvalidOperationException("secret internal detail"));

        //  Search it.
        var outcome = await NewExecutor().ExecuteAsync(provider, "witcher", Ct);

        //  Expect a failed report with a general message that hides the detail.
        Assert.Equal(SourceStatus.Failed, outcome.Summary.Status);
        Assert.Empty(outcome.Items);
        Assert.Equal(0, outcome.Summary.ItemCount);
        Assert.Equal("The external API is currently unavailable.", outcome.Summary.Error);
        Assert.DoesNotContain("secret", outcome.Summary.Error, StringComparison.Ordinal);
    }

    /// <summary>Checks that a failure after the fresh copy expires falls back to the last good answer.</summary>
    [Fact]
    public async Task Failure_after_cache_expiry_serves_last_known_good_data_as_fallback()
    {
        //  Set up a source that can be switched to failing, with fresh copies lasting very briefly.
        var shouldFail = false;
        var provider = new FakeGameDataProvider("Fake", (_, _) => shouldFail
            ? throw new HttpRequestException("down", null, HttpStatusCode.ServiceUnavailable)
            : Task.FromResult<IReadOnlyList<AggregatedItem>>([Item("The Witcher")]));
        var executor = NewExecutor(cacheDuration: TimeSpan.FromMilliseconds(50));

        //  Search once, wait for the fresh copy to expire, then search again while the source fails.
        var live = await executor.ExecuteAsync(provider, "witcher", Ct);
        await Task.Delay(TimeSpan.FromMilliseconds(250), Ct);
        shouldFail = true;
        var fallback = await executor.ExecuteAsync(provider, "witcher", Ct);

        //  Expect the earlier answer, marked as a fallback with the reason.
        Assert.Equal(SourceStatus.Fallback, fallback.Summary.Status);
        Assert.Equal("The Witcher", Assert.Single(fallback.Items).Title);
        Assert.Equal(live.Summary.FetchedAt, fallback.Summary.FetchedAt);
        Assert.Equal("The external API responded with HTTP 503.", fallback.Summary.Error);
        Assert.Equal(2, provider.Calls);
    }

    /// <summary>Checks that failures are not saved, so the next search tries the source again.</summary>
    [Fact]
    public async Task Failures_are_not_cached_so_the_next_request_retries_the_api()
    {
        //  Set up a source that fails only the first time.
        var attempt = 0;
        var provider = new FakeGameDataProvider("Fake", (_, _) => ++attempt == 1
            ? throw new HttpRequestException("down")
            : Task.FromResult<IReadOnlyList<AggregatedItem>>([Item("The Witcher")]));
        var executor = NewExecutor();

        //  Search twice.
        var failed = await executor.ExecuteAsync(provider, "witcher", Ct);
        var recovered = await executor.ExecuteAsync(provider, "witcher", Ct);

        //  Expect a failure, then a live answer.
        Assert.Equal(SourceStatus.Failed, failed.Summary.Status);
        Assert.Equal(SourceStatus.Live, recovered.Summary.Status);
    }

    /// <summary>Checks that a source missing settings is reported without being called.</summary>
    [Fact]
    public async Task Provider_missing_configuration_is_reported_without_being_called()
    {
        //  Set up a source missing its key.
        var provider = FakeGameDataProvider.Returning("Fake", Item("The Witcher"));
        provider.ConfigurationError = "API key is not configured.";
        var executor = NewExecutor();

        //  Search it.
        var outcome = await executor.ExecuteAsync(provider, "witcher", Ct);

        //  Expect a not-configured report with the reason, and no call.
        Assert.Equal(SourceStatus.NotConfigured, outcome.Summary.Status);
        Assert.Equal("API key is not configured.", outcome.Summary.Error);
        Assert.Empty(outcome.Items);
        Assert.Null(outcome.Summary.FetchedAt);
        Assert.Equal(0, provider.Calls);
    }

    /// <summary>Checks that a source is called as soon as its settings are fixed.</summary>
    [Fact]
    public async Task Provider_is_called_as_soon_as_its_configuration_is_fixed()
    {
        //  Set up a source missing its key.
        var provider = FakeGameDataProvider.Returning("Fake", Item("The Witcher"));
        provider.ConfigurationError = "API key is not configured.";
        var executor = NewExecutor();

        //  Search once, add the key, and search again.
        await executor.ExecuteAsync(provider, "witcher", Ct);
        provider.ConfigurationError = null; // e.g. the key was added and options reloaded
        var outcome = await executor.ExecuteAsync(provider, "witcher", Ct);

        //  Expect a live answer from a single call.
        Assert.Equal(SourceStatus.Live, outcome.Summary.Status);
        Assert.Equal(1, provider.Calls);
    }

    /// <summary>Checks that messages marked as safe are shown to callers as they are.</summary>
    [Fact]
    public async Task DataProviderException_message_is_shown_to_callers()
    {
        //  Set up a source that fails with a safe message.
        var provider = FakeGameDataProvider.Throwing("Fake", new DataProviderException("API key is not configured."));

        //  Search it.
        var outcome = await NewExecutor().ExecuteAsync(provider, "witcher", Ct);

        //  Expect the message unchanged.
        Assert.Equal("API key is not configured.", outcome.Summary.Error);
    }

    /// <summary>Checks that a caller giving up stops the search instead of being reported as a source failure.</summary>
    [Fact]
    public async Task Caller_cancellation_is_propagated_not_treated_as_a_provider_failure()
    {
        //  Set up a source that cancels the caller's request while running.
        using var cts = new CancellationTokenSource();
        var provider = new FakeGameDataProvider("Fake", async (_, token) =>
        {
            await cts.CancelAsync();
            token.ThrowIfCancellationRequested();
            return [];
        });

        //  Expect the cancellation to reach the caller.
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => NewExecutor().ExecuteAsync(provider, "witcher", cts.Token));
    }

    /// <summary>Checks that simultaneous identical searches call the source only once.</summary>
    [Fact]
    public async Task Concurrent_requests_for_the_same_key_call_the_api_only_once()
    {
        //  Set up a source that holds its answer until released.
        var release = new TaskCompletionSource();
        var provider = new FakeGameDataProvider("Fake", async (_, _) =>
        {
            await release.Task;
            return [Item("The Witcher")];
        });
        var executor = NewExecutor();

        //  Start ten identical searches, then let the source answer.
        var requests = Enumerable.Range(0, 10).Select(_ => executor.ExecuteAsync(provider, "witcher", Ct)).ToList();
        release.SetResult();
        var outcomes = await Task.WhenAll(requests);

        //  Expect one call, with every search getting the answer.
        Assert.Equal(1, provider.Calls);
        Assert.All(outcomes, o => Assert.Single(o.Items));
    }

    /// <summary>Checks that each kind of failure gets a safe, general message.</summary>
    [Theory]
    [MemberData(nameof(Failures))]
    public void Describes_failures_without_leaking_details(Exception exception, string expected)
    {
        Assert.Equal(expected, ProviderExecutor.DescribeFailure(exception));
    }

    /// <summary>Failures paired with the message callers should see.</summary>
    public static TheoryData<Exception, string> Failures() => new()
    {
        { new DataProviderException("Shown as is."), "Shown as is." },
        { new HttpRequestException("x", null, HttpStatusCode.BadGateway), "The external API responded with HTTP 502." },
        { new HttpRequestException("connection refused"), "The external API could not be reached." },
        { new TaskCanceledException(), "The external API did not respond in time." },
        { new TimeoutException(), "The external API did not respond in time." },
        { new InvalidOperationException("internal"), "The external API is currently unavailable." },
    };
}
