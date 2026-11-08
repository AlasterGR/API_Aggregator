using System.Net.Http.Headers;
using GameDataAggregator.Core.Abstractions;
using GameDataAggregator.Core.Statistics;
using GameDataAggregator.Infrastructure.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;
using Polly;

namespace GameDataAggregator.Infrastructure.Providers;

/// <summary>
/// The one place that wires an external API into the application. Every provider registered through
/// <see cref="AddGameDataProvider{TProvider,TOptions}"/> automatically gets:
/// validated options, a typed HttpClient, retries / timeouts / circuit breaker, request statistics,
/// and discovery by the aggregation service.
/// </summary>
public static class ProviderRegistration
{
    public const string ConfigurationRoot = "ExternalApis";
    private const string DefaultUserAgent = "GameDataAggregator/1.0";

    /// <summary>Connects one external API to the app, with its settings, retries, time limits and statistics.</summary>
    /// <param name="services">The application's service collection.</param>
    /// <param name="configuration">Application configuration; settings are read from "ExternalApis:{sectionName}".</param>
    /// <param name="apiName">Name used in statistics. Should match <see cref="IGameDataProvider.Name"/>.</param>
    /// <param name="sectionName">Sub-section of "ExternalApis" holding the provider's settings.</param>
    /// <param name="configureClient">Provider-specific HttpClient setup, e.g. the base address.</param>
    /// <param name="configureHandlers">
    /// Extra handlers that run on every attempt, between the resilience handler and the statistics handler
    /// (e.g. attaching an OAuth access token). Their own work, such as fetching a token, is therefore
    /// retried with the request but not counted as the API's response time.
    /// </param>
    /// <returns>The HttpClient builder, for any further provider-specific configuration.</returns>
    public static IHttpClientBuilder AddGameDataProvider<TProvider, TOptions>(
        this IServiceCollection services,
        IConfiguration configuration,
        string apiName,
        string sectionName,
        Action<HttpClient, TOptions>? configureClient = null,
        Action<IHttpClientBuilder>? configureHandlers = null)
        where TProvider : class, IGameDataProvider
        where TOptions : ExternalApiOptions
    {
        //  Find the source's settings and the name the app introduces itself with.
        var section = configuration.GetSection($"{ConfigurationRoot}:{sectionName}");
        var userAgent = configuration[$"{ConfigurationRoot}:UserAgent"] ?? DefaultUserAgent;

        //  Load the source's settings and check them at start-up.
        services.AddOptions<TOptions>()
            .Bind(section)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        //  Set up the source's connection.
        var builder = services.AddHttpClient<TProvider>((sp, client) =>
        {
            AddStandardHeaders(client, userAgent);
            configureClient?.Invoke(client, sp.GetRequiredService<IOptions<TOptions>>().Value);
        });

        //  Add time limits and retries around every request.
        builder.AddStandardResilienceHandler().Configure((resilience, sp) =>
        {
            var options = sp.GetRequiredService<IOptions<TOptions>>().Value;

            //  Apply the source's own time limits and retry count.
            resilience.TotalRequestTimeout.Timeout = options.TotalTimeout;
            resilience.AttemptTimeout.Timeout = options.AttemptTimeout;
            resilience.Retry.MaxRetryAttempts = options.MaxRetryAttempts;

            //  Pause calls to a source for 30 seconds once half of its recent requests fail.
            resilience.CircuitBreaker.FailureRatio = 0.5;
            resilience.CircuitBreaker.MinimumThroughput = 5;
            resilience.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(30);
            resilience.CircuitBreaker.SamplingDuration = TimeSpan.FromTicks(
                Math.Max(TimeSpan.FromSeconds(30).Ticks, options.AttemptTimeout.Ticks * 2));
        });

        //  Add any steps this source needs, such as signing in.
        configureHandlers?.Invoke(builder);

        //  Measure every single attempt for the statistics.
        AddStatistics(builder, apiName);

        //  Include the source in searches unless it is switched off.
        if (section.GetValue(nameof(ExternalApiOptions.Enabled), defaultValue: true))
        {
            services.AddTransient<IGameDataProvider>(sp => sp.GetRequiredService<TProvider>());
        }

        return builder;
    }

    /// <summary>
    /// Adds a separate named HttpClient for a source's side requests: extra calls that only decorate the main
    /// data, such as CheapShark's store names or Steam's player counts. Each side request gets one attempt
    /// with a short time limit and no retries, and the client has its own circuit breaker. So a broken side
    /// endpoint can neither hold up the main request for long nor switch off the main client's circuit.
    /// Side requests still count in the source's statistics.
    /// </summary>
    /// <param name="services">The application's service collection.</param>
    /// <param name="configuration">Application configuration, read for the shared user agent.</param>
    /// <param name="apiName">Name used in statistics. Should match <see cref="IGameDataProvider.Name"/>.</param>
    /// <param name="clientName">Name the provider passes to <see cref="IHttpClientFactory.CreateClient(string)"/>.</param>
    /// <param name="timeout">Picks the time limit for one side request from the source's settings.</param>
    /// <returns>The HttpClient builder, for any further configuration.</returns>
    public static IHttpClientBuilder AddSideRequestClient<TOptions>(
        this IServiceCollection services,
        IConfiguration configuration,
        string apiName,
        string clientName,
        Func<TOptions, TimeSpan> timeout)
        where TOptions : ExternalApiOptions
    {
        //  Set up the side connection with the same headers as the main one.
        var userAgent = configuration[$"{ConfigurationRoot}:UserAgent"] ?? DefaultUserAgent;
        var builder = services.AddHttpClient(clientName, client => AddStandardHeaders(client, userAgent));

        //  Give each side request one short try, and pause them alone when half of them fail.
        builder.AddResilienceHandler("side-requests", (pipeline, context) =>
        {
            var options = context.ServiceProvider.GetRequiredService<IOptions<TOptions>>().Value;
            pipeline
                .AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
                {
                    FailureRatio = 0.5,
                    MinimumThroughput = 5,
                    BreakDuration = TimeSpan.FromSeconds(30),
                    SamplingDuration = TimeSpan.FromSeconds(30),
                })
                .AddTimeout(timeout(options));
        });

        //  Measure side requests too, under the source's name.
        AddStatistics(builder, apiName);

        return builder;
    }

    /// <summary>Introduces the app by name, as some sources refuse anonymous requests, and asks for JSON.</summary>
    private static void AddStandardHeaders(HttpClient client, string userAgent)
    {
        //  Set the app's name and the wanted answer format on every request.
        client.DefaultRequestHeaders.UserAgent.ParseAdd(userAgent);
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    /// <summary>Adds the step that records each attempt's time and outcome for the statistics.</summary>
    private static void AddStatistics(IHttpClientBuilder builder, string apiName) =>
        builder.AddHttpMessageHandler(sp => new RequestStatisticsHandler(
            apiName,
            sp.GetRequiredService<IRequestStatisticsStore>(),
            sp.GetRequiredService<TimeProvider>()));
}
