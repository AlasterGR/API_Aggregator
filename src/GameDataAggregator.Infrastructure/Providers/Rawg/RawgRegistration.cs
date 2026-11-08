using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GameDataAggregator.Infrastructure.Providers.Rawg;

/// <summary>Adds the RAWG source to the app.</summary>
public static class RawgRegistration
{
    /// <summary>Registers RAWG with its settings and address.</summary>
    public static IServiceCollection AddRawgProvider(this IServiceCollection services, IConfiguration configuration)
    {
        //  Connect RAWG using its base address from the settings.
        services.AddGameDataProvider<RawgProvider, RawgOptions>(
            configuration,
            RawgProvider.SourceName,
            RawgOptions.SectionName,
            (client, options) => client.BaseAddress = options.BaseUrl);

        return services;
    }
}
