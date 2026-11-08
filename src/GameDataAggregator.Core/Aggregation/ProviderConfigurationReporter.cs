using GameDataAggregator.Core.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GameDataAggregator.Core.Aggregation;

/// <summary>
/// Logs, once at start-up, which providers are ready and which are missing configuration.
/// Requests to an unconfigured provider are then answered with <c>NotConfigured</c> without further logging,
/// so the log is not flooded with the same warning on every search.
/// </summary>
public sealed partial class ProviderConfigurationReporter(
    IServiceScopeFactory scopeFactory,
    ILogger<ProviderConfigurationReporter> logger) : IHostedService
{
    /// <summary>Logs which sources are ready and which are missing settings.</summary>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        //  Borrow the sources only briefly, since they are meant to be short-lived.
        using var scope = scopeFactory.CreateScope();
        var providers = scope.ServiceProvider.GetServices<IGameDataProvider>().ToList();

        //  Warn about each source that is missing its settings.
        foreach (var provider in providers)
        {
            if (provider.ConfigurationError is { } error)
            {
                LogNotConfigured(logger, provider.Name, error);
            }
        }

        //  Report how many sources are ready to use.
        var ready = providers.Where(p => p.ConfigurationError is null).Select(p => p.Name).ToList();
        LogReady(logger, ready.Count, providers.Count, string.Join(", ", ready));
        return Task.CompletedTask;
    }

    /// <summary>Does nothing when the app stops.</summary>
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>Logs that a source is missing its settings.</summary>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Source {Provider} is not configured and will be reported as NotConfigured: {Reason}")]
    private static partial void LogNotConfigured(ILogger logger, string provider, string reason);

    /// <summary>Logs how many sources are ready.</summary>
    [LoggerMessage(Level = LogLevel.Information, Message = "{ReadyCount} of {TotalCount} sources are ready: {ReadySources}")]
    private static partial void LogReady(ILogger logger, int readyCount, int totalCount, string readySources);
}
