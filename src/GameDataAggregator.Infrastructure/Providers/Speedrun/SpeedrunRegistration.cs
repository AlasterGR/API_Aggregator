using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GameDataAggregator.Infrastructure.Providers.Speedrun;

/// <summary>Adds the speedrun.com source to the app.</summary>
public static class SpeedrunRegistration
{
    /// <summary>Registers speedrun.com with its settings and address.</summary>
    public static IServiceCollection AddSpeedrunProvider(this IServiceCollection services, IConfiguration configuration)
    {
        //  Connect speedrun.com using its base address from the settings.
        services.AddGameDataProvider<SpeedrunProvider, SpeedrunOptions>(
            configuration,
            SpeedrunProvider.SourceName,
            SpeedrunOptions.SectionName,
            (client, options) => client.BaseAddress = options.BaseUrl);

        return services;
    }
}
