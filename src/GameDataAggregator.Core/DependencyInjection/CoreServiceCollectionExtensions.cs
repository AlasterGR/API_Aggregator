using GameDataAggregator.Core.Aggregation;
using GameDataAggregator.Core.Statistics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace GameDataAggregator.Core.DependencyInjection;

/// <summary>Adds the core services to the app.</summary>
public static class CoreServiceCollectionExtensions
{
    /// <summary>Registers aggregation and statistics services. A <c>HybridCache</c> must be registered separately.</summary>
    public static IServiceCollection AddAggregationCore(this IServiceCollection services, IConfiguration configuration)
    {
        //  Load the aggregation settings and check them at start-up.
        services.AddOptions<AggregationOptions>()
            .Bind(configuration.GetSection(AggregationOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        //  Load the statistics settings and check them at start-up.
        services.AddOptions<StatisticsOptions>()
            .Bind(configuration.GetSection(StatisticsOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        //  Load the slow-source check settings and check them at start-up.
        services.AddOptions<PerformanceMonitorOptions>()
            .Bind(configuration.GetSection(PerformanceMonitorOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        //  Add the services shared for the app's whole life, the start-up report and the slow-source check.
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IRequestStatisticsStore, InMemoryRequestStatisticsStore>();
        services.TryAddSingleton<ProviderExecutor>();
        services.AddHostedService<ProviderConfigurationReporter>();
        services.AddHostedService<PerformanceMonitor>();

        //  Create the search service per request, so the connections its sources hold can be renewed.
        services.TryAddScoped<IAggregationService, AggregationService>();

        return services;
    }
}
