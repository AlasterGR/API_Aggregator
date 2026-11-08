using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GameDataAggregator.Infrastructure.Providers.CheapShark;

/// <summary>Adds the CheapShark source to the app.</summary>
public static class CheapSharkRegistration
{
    /// <summary>Registers CheapShark with its settings and address.</summary>
    public static IServiceCollection AddCheapSharkProvider(this IServiceCollection services, IConfiguration configuration)
    {
        //  Connect CheapShark using its base address from the settings.
        services.AddGameDataProvider<CheapSharkProvider, CheapSharkOptions>(
            configuration,
            CheapSharkProvider.SourceName,
            CheapSharkOptions.SectionName,
            (client, options) => client.BaseAddress = options.BaseUrl);

        //  Load the store list through its own short-lived connection, so it cannot hold up or block the deals.
        services.AddSideRequestClient<CheapSharkOptions>(
            configuration,
            CheapSharkProvider.SourceName,
            CheapSharkProvider.SideRequestClientName,
            options => options.SideRequestTimeout);

        return services;
    }
}
