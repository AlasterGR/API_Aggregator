using System.Text.Json.Serialization;
using GameDataAggregator.Api.Authentication;
using GameDataAggregator.Api.ModelBinding;
using GameDataAggregator.Api.RateLimiting;
using GameDataAggregator.Core.DependencyInjection;
using GameDataAggregator.Infrastructure;
using Scalar.AspNetCore;

//  Start building the app from its settings.
var builder = WebApplication.CreateBuilder(args);

//  Add the API endpoints, with strict rules for reading requests and names for enums in answers.
builder.Services
    .AddControllers(options =>
    {
        //  Accept enums by name only, and read dates without a time zone as UTC.
        options.ModelBinderProviders.Insert(0, new StrictEnumModelBinderProvider());
        options.ModelBinderProviders.Insert(0, new UtcDateTimeOffsetModelBinderProvider());
    })
    .AddJsonOptions(options =>
    {
        //  Write enums as names and leave out empty values.
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    });

//  Add standard error answers, the API description with its token field, health checks and the search limit.
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi(options => options.AddBearerSecurityScheme());
builder.Services.AddHealthChecks();
builder.Services.AddSearchRateLimiting(builder.Configuration);

//  Require a signed access token on the endpoints marked as protected.
builder.Services.AddJwtAuthentication(builder.Configuration);

//  Add the search, the statistics and the external sources.
builder.Services
    .AddAggregationCore(builder.Configuration)
    .AddInfrastructure(builder.Configuration);

var app = builder.Build();

//  Answer unexpected and empty errors in the standard format, keeping the details in the log only.
app.UseExceptionHandler();
app.UseStatusCodePages();

//  Show the API description while developing, and require HTTPS everywhere else.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}
else
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

//  Serve the browser page from the site root.
app.UseDefaultFiles();
app.UseStaticFiles(new StaticFileOptions
{
    //  Make browsers check for a newer file every time, which costs little when nothing changed.
    OnPrepareResponse = context => context.Context.Response.Headers.CacheControl = "no-cache",
});

//  Read the caller's token, then refuse protected endpoints without a valid one.
app.UseAuthentication();
app.UseAuthorization();

//  Apply the search limit, after the token check, so refused requests do not use up a client's searches.
app.UseRateLimiter();

//  Connect the endpoints and the health check, then start answering requests.
app.MapControllers();
app.MapHealthChecks("/health");

app.Run();
