using GameDataAggregator.Infrastructure.Providers.CheapShark;
using GameDataAggregator.Infrastructure.Providers.Igdb;
using GameDataAggregator.Infrastructure.Providers.Rawg;
using GameDataAggregator.Infrastructure.Providers.Speedrun;
using GameDataAggregator.Infrastructure.Providers.Steam;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GameDataAggregator.Infrastructure;

/// <summary>Adds the cache and the external sources to the app.</summary>
public static class InfrastructureServiceCollectionExtensions
{
    /// <summary>Registers the cache and every external API provider.</summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        //  Keep results in memory, which a shared cache such as Redis can extend later without code changes.
        services.AddHybridCache();

        //  Add every external source, which is also where a new one goes.
        services
            .AddRawgProvider(configuration)
            .AddCheapSharkProvider(configuration)
            .AddSteamProvider(configuration)
            .AddIgdbProvider(configuration)
            .AddSpeedrunProvider(configuration);

        return services;
    }
}
