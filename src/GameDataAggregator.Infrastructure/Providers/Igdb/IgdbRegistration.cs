using GameDataAggregator.Infrastructure.Authentication.Twitch;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GameDataAggregator.Infrastructure.Providers.Igdb;

/// <summary>Adds the IGDB source to the app.</summary>
public static class IgdbRegistration
{
    /// <summary>Registers IGDB with its settings, address and Twitch sign-in.</summary>
    public static IServiceCollection AddIgdbProvider(this IServiceCollection services, IConfiguration configuration)
    {
        //  Connect IGDB using its base address, and sign its requests in with Twitch.
        services.AddGameDataProvider<IgdbProvider, IgdbOptions>(
            configuration,
            IgdbProvider.SourceName,
            IgdbOptions.SectionName,
            (client, options) => client.BaseAddress = options.BaseUrl,
            handlers => handlers.AddTwitchAuthentication<IgdbOptions>(configuration));

        return services;
    }
}
