using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace GameDataAggregator.IntegrationTests.Infrastructure;

/// <summary>Hosts the real application in memory, with the external APIs replaced by <see cref="FakeExternalApis"/>.</summary>
public sealed class GameDataAggregatorFactory : WebApplicationFactory<Program>
{
    public const string TwitchClientId = "integration-test-client";

    /// <summary>A fixed signing key, so tests can also sign tokens of their own.</summary>
    public const string JwtSigningKey = "integration-test-signing-key-of-at-least-32-bytes";

    public FakeExternalApis ExternalApis { get; } = new();

    /// <summary>Gives the app test keys and connects it to the fake sources.</summary>
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        //  Run as in development, with test keys for the sources that need them.
        builder.UseEnvironment("Development");
        builder.UseSetting("ExternalApis:Rawg:ApiKey", "integration-test-key");
        builder.UseSetting("ExternalApis:TwitchAuth:ClientId", TwitchClientId);
        builder.UseSetting("ExternalApis:TwitchAuth:ClientSecret", "integration-test-secret");
        builder.UseSetting("Jwt:SigningKey", JwtSigningKey);

        //  Allow a single retry, so tests with failing sources stay fast.
        foreach (var api in new[] { "Rawg", "CheapShark", "Steam", "Igdb", "Speedrun" })
        {
            builder.UseSetting($"ExternalApis:{api}:MaxRetryAttempts", "1");
        }

        //  Send every outgoing request to the fake sources.
        builder.ConfigureTestServices(services =>
            services.ConfigureHttpClientDefaults(client =>
                client.ConfigurePrimaryHttpMessageHandler(() => ExternalApis.CreateHandler())));
    }
}
