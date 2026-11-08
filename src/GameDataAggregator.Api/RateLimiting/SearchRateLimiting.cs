using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace GameDataAggregator.Api.RateLimiting;

/// <summary>
/// Limits searches per client. Every new search fans out to several external APIs, and the cache only helps
/// with repeated searches, so without a limit one client sending many different searches could use up the
/// external APIs' own limits (and keys) for everyone. Statistics, sources and the UI are cheap and not limited.
/// </summary>
public static class SearchRateLimiting
{
    public const string PolicyName = "search";

    /// <summary>Registers the search limit and the answer sent when a client goes over it.</summary>
    public static IServiceCollection AddSearchRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        //  Load the limit settings and check them at start-up.
        services.AddOptions<SearchRateLimitOptions>()
            .Bind(configuration.GetSection(SearchRateLimitOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        //  Set up the limit itself.
        services.AddRateLimiter(limiter =>
        {
            //  Answer "too many requests" to clients over the limit.
            limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            //  Give each client address its own allowance, which needs forwarded headers when behind a proxy.
            limiter.AddPolicy(PolicyName, context =>
            {
                var options = context.RequestServices.GetRequiredService<IOptions<SearchRateLimitOptions>>().Value;
                return RateLimitPartition.GetFixedWindowLimiter(
                    context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = options.PermitLimit,
                        Window = options.Window,
                        QueueLimit = 0, // reject at once rather than holding requests open
                    });
            });

            //  Explain a refusal in the standard error format.
            limiter.OnRejected = async (context, cancellationToken) =>
            {
                //  Tell the client when it may search again, if known.
                var retryAfterText = string.Empty;
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    var seconds = (int)Math.Ceiling(retryAfter.TotalSeconds);
                    context.HttpContext.Response.Headers.RetryAfter = seconds.ToString(CultureInfo.InvariantCulture);
                    retryAfterText = $" Try again in {seconds} s.";
                }

                //  Write the error answer.
                await context.HttpContext.RequestServices.GetRequiredService<IProblemDetailsService>().WriteAsync(new ProblemDetailsContext
                {
                    HttpContext = context.HttpContext,
                    ProblemDetails =
                    {
                        Status = StatusCodes.Status429TooManyRequests,
                        Title = "Too many requests.",
                        Detail = $"This client has reached its search limit.{retryAfterText}",
                    },
                });
            };
        });

        return services;
    }
}
