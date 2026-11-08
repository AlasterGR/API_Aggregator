using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GameDataAggregator.Infrastructure.Providers.Steam;

/// <summary>Adds the Steam source to the app.</summary>
public static class SteamRegistration
{
    /// <summary>Registers Steam with its settings.</summary>
    public static IServiceCollection AddSteamProvider(this IServiceCollection services, IConfiguration configuration)
    {
        //  Connect Steam without a base address, since it uses two Steam sites with full addresses.
        services.AddGameDataProvider<SteamProvider, SteamOptions>(
            configuration,
            SteamProvider.SourceName,
            SteamOptions.SectionName);

        //  Ask for player counts through their own short-lived connection, so they cannot hold up or block the search.
        services.AddSideRequestClient<SteamOptions>(
            configuration,
            SteamProvider.SourceName,
            SteamProvider.SideRequestClientName,
            options => options.SideRequestTimeout);

        return services;
    }
}
