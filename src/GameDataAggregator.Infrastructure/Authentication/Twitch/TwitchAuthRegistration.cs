using GameDataAggregator.Infrastructure.Providers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Polly;

namespace GameDataAggregator.Infrastructure.Authentication.Twitch;

/// <summary>Adds Twitch sign-in to the sources that need it.</summary>
public static class TwitchAuthRegistration
{
    /// <summary>
    /// Adds Twitch OAuth to a provider's HttpClient. Pass it as the <c>configureHandlers</c> argument of
    /// <see cref="ProviderRegistration.AddGameDataProvider{TProvider,TOptions}"/>. Safe to use for
    /// several providers: the token provider and its HttpClient are registered only once.
    /// </summary>
    /// <typeparam name="TOptions">The settings of the source that uses the token, checked against the token time limit.</typeparam>
    public static void AddTwitchAuthentication<TOptions>(this IHttpClientBuilder builder, IConfiguration configuration)
        where TOptions : ExternalApiOptions
    {
        var services = builder.Services;

        //  Set up the shared token service only the first time.
        if (!services.Any(d => d.ServiceType == typeof(TwitchAccessTokenProvider)))
        {
            //  Load the Twitch settings and check them at start-up.
            services.AddOptions<TwitchAuthOptions>()
                .Bind(configuration.GetSection($"{ProviderRegistration.ConfigurationRoot}:{TwitchAuthOptions.SectionName}"))
                .ValidateDataAnnotations()
                .ValidateOnStart();

            //  Keep one token service for the whole app.
            services.TryAddSingleton<TwitchAccessTokenProvider>();

            //  Give token requests their own connection with one try and a time limit. The source's own retries
            //  already repeat the whole attempt, token request included.
            services.AddHttpClient(TwitchAccessTokenProvider.HttpClientName)
                .AddResilienceHandler("twitch-token", (pipeline, context) =>
                    pipeline.AddTimeout(context.ServiceProvider.GetRequiredService<IOptions<TwitchAuthOptions>>().Value.Timeout));
        }

        //  Check at start-up that a token request fits inside one attempt of this source.
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<TOptions>, TwitchTimeoutFitsAttemptCheck<TOptions>>());

        //  Attach the token to every request this source sends.
        builder.AddHttpMessageHandler(sp => new TwitchAuthenticationHandler(
            sp.GetRequiredService<TwitchAccessTokenProvider>(),
            sp.GetRequiredService<IOptions<TwitchAuthOptions>>()));
    }
}
