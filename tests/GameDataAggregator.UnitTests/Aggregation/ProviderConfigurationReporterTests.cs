using GameDataAggregator.Core.Abstractions;
using GameDataAggregator.Core.Aggregation;
using GameDataAggregator.UnitTests.TestDoubles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using static GameDataAggregator.UnitTests.TestDoubles.TestData;

namespace GameDataAggregator.UnitTests.Aggregation;

/// <summary>Tests the start-up report of which sources are ready.</summary>
public sealed class ProviderConfigurationReporterTests
{
    /// <summary>Checks for one warning per source missing settings, then a summary, without calling any source.</summary>
    [Fact]
    public async Task Logs_one_warning_per_unconfigured_provider_and_a_summary()
    {
        //  Set up one source missing its key and one ready source.
        var missingKey = FakeGameDataProvider.Returning("NeedsKey", Item("x"));
        missingKey.ConfigurationError = "API key is not configured.";
        var ready = FakeGameDataProvider.Returning("Ready", Item("y"));

        //  Register both, and capture what gets logged.
        var services = new ServiceCollection();
        services.AddSingleton<IGameDataProvider>(missingKey);
        services.AddSingleton<IGameDataProvider>(ready);
        await using var serviceProvider = services.BuildServiceProvider();
        var logger = new CapturingLogger<ProviderConfigurationReporter>();

        //  Run the start-up report.
        await new ProviderConfigurationReporter(serviceProvider.GetRequiredService<IServiceScopeFactory>(), logger)
            .StartAsync(TestContext.Current.CancellationToken);

        //  Expect a warning naming the missing key, a summary line, and no calls to either source.
        var warning = Assert.Single(logger.Entries, e => e.Level == LogLevel.Warning);
        Assert.Contains("NeedsKey", warning.Message, StringComparison.Ordinal);
        Assert.Contains("API key is not configured.", warning.Message, StringComparison.Ordinal);
        var summary = Assert.Single(logger.Entries, e => e.Level == LogLevel.Information);
        Assert.Equal("1 of 2 sources are ready: Ready", summary.Message);
        Assert.Equal(0, missingKey.Calls + ready.Calls);
    }
}
