using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;

namespace GameDataAggregator.Api.Authentication;

/// <summary>
/// Sets up JWT bearer authentication. Endpoints marked <c>[Authorize]</c> need a valid token signed with the app's
/// key. Everything else stays open.
/// </summary>
public static class JwtAuthentication
{
    /// <summary>The name of the security scheme in the API description.</summary>
    public const string SecuritySchemeName = "Bearer";

    /// <summary>Registers the token settings, the key, the token check and the development token issuer.</summary>
    public static IServiceCollection AddJwtAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        //  Load the token settings and check them at start-up, including the key rules.
        services.AddOptions<JwtAuthOptions>()
            .Bind(configuration.GetSection(JwtAuthOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<JwtAuthOptions>, JwtAuthOptionsValidator>();

        //  Share one signing key and one token issuer for the app's whole life.
        services.AddSingleton<JwtSigningKey>();
        services.AddSingleton<JwtTokenIssuer>();

        //  Check bearer tokens on every request.
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtAuthOptions>, JwtSigningKey>((bearer, options, signingKey) =>
            {
                //  Keep claim names as they are in the token, such as "sub".
                bearer.MapInboundClaims = false;

                //  Accept only unexpired tokens from this issuer, for this audience, signed with this key.
                bearer.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = options.Value.Issuer,
                    ValidAudience = options.Value.Audience,
                    IssuerSigningKey = signingKey.Key,
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateIssuerSigningKey = true,
                    ValidateLifetime = true,
                    RequireExpirationTime = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                };
            });

        services.AddAuthorization();
        return services;
    }

    /// <summary>
    /// Describes the bearer token in the API description, and marks each protected operation as needing it, so
    /// Scalar shows a token field and sends the token.
    /// </summary>
    public static OpenApiOptions AddBearerSecurityScheme(this OpenApiOptions options)
    {
        //  Add the bearer scheme to the description.
        options.AddDocumentTransformer((document, _, _) =>
        {
            document.Components ??= new OpenApiComponents();
            document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
            document.Components.SecuritySchemes[SecuritySchemeName] = new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                Description = "A JWT access token. While developing, get one from POST /api/v1/auth/token.",
            };
            return Task.CompletedTask;
        });

        //  Mark each operation that needs a token, and leave the open ones alone.
        options.AddOperationTransformer((operation, context, _) =>
        {
            var metadata = context.Description.ActionDescriptor.EndpointMetadata;
            if (metadata.OfType<IAuthorizeData>().Any() && !metadata.OfType<IAllowAnonymous>().Any())
            {
                operation.Security ??= [];
                operation.Security.Add(new OpenApiSecurityRequirement
                {
                    [new OpenApiSecuritySchemeReference(SecuritySchemeName, context.Document)] = [],
                });
            }

            return Task.CompletedTask;
        });

        return options;
    }
}
