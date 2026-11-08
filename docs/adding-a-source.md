# How to add a new data source

This guide shows you, step by step, how to connect one more external API to the Game Data Aggregator. You do not
need to know the whole code base. You only add one new folder and change a few lines in other places.

The guide uses the speedrun.com source as its model, because it is the smallest complete source in the project. You
can open its files next to this guide:

- [`SpeedrunOptions.cs`](../src/GameDataAggregator.Infrastructure/Providers/Speedrun/SpeedrunOptions.cs)
- [`SpeedrunModels.cs`](../src/GameDataAggregator.Infrastructure/Providers/Speedrun/SpeedrunModels.cs)
- [`SpeedrunProvider.cs`](../src/GameDataAggregator.Infrastructure/Providers/Speedrun/SpeedrunProvider.cs)
- [`SpeedrunRegistration.cs`](../src/GameDataAggregator.Infrastructure/Providers/Speedrun/SpeedrunRegistration.cs)
- [`SpeedrunProviderTests.cs`](../tests/GameDataAggregator.UnitTests/Infrastructure/SpeedrunProviderTests.cs)

---

## Contents

- [Words used in this guide](#words-used-in-this-guide)
- [The example API](#the-example-api)
- [What you get for free](#what-you-get-for-free)
- [Step 1: Create the folder](#step-1-create-the-folder)
- [Step 2: Write the options class](#step-2-write-the-options-class)
- [Step 3: Write the JSON models](#step-3-write-the-json-models)
- [Step 4: Write the provider class](#step-4-write-the-provider-class)
- [Step 5: Write the registration method](#step-5-write-the-registration-method)
- [Step 6: Add one line to `AddInfrastructure`](#step-6-add-one-line-to-addinfrastructure)
- [Step 7: Add the settings to `appsettings.json`](#step-7-add-the-settings-to-appsettingsjson)
- [Step 8: Write the unit tests](#step-8-write-the-unit-tests)
- [Step 9: Update the integration tests](#step-9-update-the-integration-tests)
- [Step 10: Run it and check the result](#step-10-run-it-and-check-the-result)
- [If your API needs a key](#if-your-api-needs-a-key)
- [If your data does not fit the existing categories](#if-your-data-does-not-fit-the-existing-categories)
- [Common mistakes](#common-mistakes)
- [Checklist](#checklist)

---

## Words used in this guide

| Word | Meaning |
|---|---|
| **Source** | One external API that the service asks for data, such as RAWG or speedrun.com. |
| **Provider** | The C# class that talks to one source. It sends the HTTP request and turns the answer into the common item shape. Every provider implements the interface `IGameDataProvider`. |
| **Item** | One result in the service's answer. Every source's data is turned into the same shape, the class `AggregatedItem`. |
| **Options class** | A C# class that holds the settings of one source, such as its web address and time limits. The values are read from `appsettings.json`. |
| **JSON model** | A small C# record that matches the JSON the external API sends back. It is also called a DTO (data transfer object). |
| **Dependency injection** | The way ASP.NET Core creates objects for you. You *register* a class once at start-up. After that, the framework builds it and hands it to whoever needs it. |
| **Extension method** | A static method that looks like a method of another type. For example, `services.AddSpeedrunProvider(configuration)` is an extension method on `IServiceCollection`. |
| **Base address** | The start of every web address the provider calls, for example `https://www.speedrun.com/api/v1/`. The provider then only writes the rest, for example `games?name=witcher`. |
| **Unit test** | A small automatic test that checks one class on its own. |
| **Integration test** | An automatic test that starts the whole application in memory and sends real HTTP requests to it. |
| **Stub** | A fake replacement for a real part. Here, a stub answers HTTP requests instead of the real internet. |

---

## The example API

This guide adds an imaginary source called **Game Reviews**. It does not exist. Replace its address, field names and
values with the ones of your real API.

The imaginary API works like this:

```http
GET https://api.gamereviews.example/v1/reviews?query=witcher&limit=10
```

```json
{
  "reviews": [
    {
      "id": "r-101",
      "gameTitle": "The Witcher 3: Wild Hunt",
      "score": 92,
      "releaseDate": "2015-05-19",
      "link": "https://gamereviews.example/r-101",
      "outlet": "PC Weekly"
    }
  ]
}
```

It needs no API key. The section [If your API needs a key](#if-your-api-needs-a-key) shows the small changes for an
API that does.

---

## What you get for free

The service does most of the work for you. You write the code that calls the API and maps its answer. Everything
else comes from the shared code, as soon as you register the provider with `AddGameDataProvider`:

| Feature | What it means for your source | Where it lives |
|---|---|---|
| **Parallel calls** | Your source is asked at the same time as all the other sources. A slow source does not make the others wait. | `AggregationService` |
| **Timeouts** | Each attempt stops after `AttemptTimeout`. The whole call, with retries, stops after `TotalTimeout`. | `ProviderRegistration` |
| **Retries** | A short failure (server error 5xx, 408, 429, or a network error) is tried again, up to `MaxRetryAttempts` times. The wait between tries grows each time. | `ProviderRegistration` |
| **Circuit breaker** | A circuit breaker is a safety switch. If at least half of 5 or more recent requests fail, your API is not called for 30 seconds. Requests fail at once instead of waiting. This protects both the service and the API. | `ProviderRegistration` |
| **Cache** | A cache keeps a copy of an answer for a while. The same search within `Aggregation:CacheDuration` (5 minutes by default) does not call your API again. Many identical searches at the same moment share one call. | `ProviderExecutor` |
| **Fallback** | When your API fails, the service returns the last good answer for that search, if it has one. It keeps that copy for `Aggregation:FallbackDuration` (6 hours by default). | `ProviderExecutor` |
| **Safe errors** | If your provider throws an exception, the request still succeeds. Your source gets the status `Fallback` or `Failed` with a short, safe message. The full error goes to the log. | `ProviderExecutor` |
| **Statistics** | Every HTTP request your provider sends is counted and timed. It appears in `GET /api/v1/statistics`. | `RequestStatisticsHandler` |
| **Slowdown warnings** | If your API becomes much slower than before, the performance monitor writes a warning to the log. | `PerformanceMonitor` |
| **Source list and filter** | Your source appears in `GET /api/v1/sources`. Clients can pick it with `sources=GameReviews`. An unknown name is rejected with a clear message. | `SourcesController`, `KnownSourcesAttribute` |
| **Filters, sorting, paging, relevance** | Your items are filtered, sorted, paged and scored together with all other items. | `ItemQueryProcessor`, `RelevanceScorer` |
| **Browser UI** | Your source gets a tick box in the search form and a status card in the results. Your items show as cards. Their metrics and attributes are shown with readable labels. | `wwwroot/js/app.js` |
| **Settings check at start-up** | Wrong settings (for example a negative timeout) stop the app at start-up with a clear message. | `ExternalApiOptions` |
| **Start-up report** | The log says at start-up whether your source is ready or which setting is missing. | `ProviderConfigurationReporter` |
| **On/off switch** | `"Enabled": false` in the settings removes your source from the whole service. | `ProviderRegistration` |

So your provider should **not** catch exceptions, retry, cache, sort, filter or count anything. It should only call
the API, map the answer, and throw when something goes wrong.

---

## Step 1: Create the folder

Every source has its own folder under `src/GameDataAggregator.Infrastructure/Providers/`. Create a new one:

```text
src/GameDataAggregator.Infrastructure/Providers/GameReviews/
    GameReviewsOptions.cs
    GameReviewsModels.cs
    GameReviewsProvider.cs
    GameReviewsRegistration.cs
```

Use the same four file names as the other sources. Then every source looks the same, and other developers know where
to look.

---

## Step 2: Write the options class

The options class holds your source's settings. It **derives from** (is based on) `ExternalApiOptions`. That base
class already gives you these settings, with checks and default values:

| Setting | Default | Allowed values |
|---|---|---|
| `Enabled` | `true` | `true` or `false` |
| `MaxResults` | `10` | 1 to 50 |
| `AttemptTimeout` | `00:00:05` (5 seconds) | 0.1 seconds to 1 minute |
| `TotalTimeout` | `00:00:15` (15 seconds) | 1 second to 5 minutes. It must be longer than `AttemptTimeout`. |
| `MaxRetryAttempts` | `2` | 1 to 10 |

You only add what is special for your source. Most sources only add the base address.

**`GameReviewsOptions.cs`**

```csharp
using System.ComponentModel.DataAnnotations;

namespace GameDataAggregator.Infrastructure.Providers.GameReviews;

/// <summary>Configuration section "ExternalApis:GameReviews". The API needs no key.</summary>
public sealed class GameReviewsOptions : ExternalApiOptions
{
    public const string SectionName = "GameReviews";

    [Required, BaseAddress]
    public Uri BaseUrl { get; set; } = new("https://api.gamereviews.example/v1/");
}
```

Notes:

- `SectionName` is the name of your block inside `ExternalApis` in `appsettings.json` (see Step 7).
- `[Required]` is a validation attribute. If the value is missing, the app does not start, and the log says why.
- **The base address must end with a slash (`/`).** Without it, .NET drops the last part of the address when it adds
  `reviews?...`. See [Common mistakes](#common-mistakes).

---

## Step 3: Write the JSON models

The JSON models are small records that match the JSON your API sends. Write **only the fields you use**. The JSON
reader ignores all other fields.

**`GameReviewsModels.cs`**

```csharp
namespace GameDataAggregator.Infrastructure.Providers.GameReviews;

// Only the fields this service uses.
// Review search: GET {base}/reviews?query=...&limit=...

/// <summary>The Game Reviews API's answer to a search.</summary>
internal sealed record GameReviewsResponse(IReadOnlyList<GameReview>? Reviews);

/// <summary>One review from a search.</summary>
internal sealed record GameReview(
    string Id,
    string GameTitle,
    double? Score,
    DateOnly? ReleaseDate,
    string? Link,
    string? Outlet);
```

Notes:

- Mark the records `internal`. That means only this project can see them. They are details of your source, not part
  of the service's public shape. The unit test project can still see them, because the Infrastructure project
  allows it (`InternalsVisibleTo` in its `.csproj` file).
- Make a field nullable (`string?`, `double?`) when the API can leave it out or send `null`. Then a missing value
  does not break the whole answer.
- The names must match the JSON names. The provider reads this API with `CamelCaseJson`, so `gameTitle` in the JSON
  fills `GameTitle` in C#. For an API that uses `snake_case` names (like `game_title`), use `SnakeCaseJson` instead.
  Both settings live in [`ProviderMapping.cs`](../src/GameDataAggregator.Infrastructure/Providers/ProviderMapping.cs).
- If a JSON name cannot be a C# name, use `[property: JsonPropertyName("release-date")]`. `SpeedrunModels.cs` does
  this.

---

## Step 4: Write the provider class

The provider is the heart of your source. It implements the interface `IGameDataProvider`, which has three members:

| Member | Required? | What it does |
|---|---|---|
| `Name` | Yes | The source's name. It is shown in answers, in statistics and in the `sources` filter. It must be unique. |
| `ConfigurationError` | No | Says why the source cannot be used, for example "API key is not configured". It returns `null` when the source is ready. Leave it out if your API needs no settings. |
| `FetchAsync(search, cancellationToken)` | Yes | Calls the API and turns the answer into a list of `AggregatedItem`. |

**`GameReviewsProvider.cs`**

```csharp
using System.Globalization;
using System.Net.Http.Json;
using GameDataAggregator.Core.Abstractions;
using GameDataAggregator.Core.Models;
using Microsoft.Extensions.Options;
using static GameDataAggregator.Infrastructure.Providers.ProviderMapping;

namespace GameDataAggregator.Infrastructure.Providers.GameReviews;

/// <summary>Game Reviews: critic review scores for a game. No API key needed.</summary>
public sealed class GameReviewsProvider(HttpClient httpClient, IOptions<GameReviewsOptions> options) : IGameDataProvider
{
    public const string SourceName = "GameReviews";

    public string Name => SourceName;

    /// <summary>Searches the review site for the game and returns its reviews.</summary>
    public async Task<IReadOnlyList<AggregatedItem>> FetchAsync(string search, CancellationToken cancellationToken)
    {
        //  Build the search address with the search text and the result limit.
        var requestUri =
            $"reviews?query={Uri.EscapeDataString(search)}" +
            $"&limit={options.Value.MaxResults.ToString(CultureInfo.InvariantCulture)}";

        //  Ask the API and turn each review into a common item.
        var response = await httpClient.GetFromJsonAsync<GameReviewsResponse>(requestUri, CamelCaseJson, cancellationToken);

        return (response?.Reviews ?? []).Select(Map).ToList();
    }

    /// <summary>Turns one review into a common item.</summary>
    private static AggregatedItem Map(GameReview review) => new()
    {
        Source = SourceName,
        Category = ItemCategory.GameInfo,
        Title = review.GameTitle,
        Url = ToUri(review.Link),
        Date = review.ReleaseDate is { } released ? new DateTimeOffset(released, TimeOnly.MinValue, TimeSpan.Zero) : null,
        DateKind = DateKind.Released,
        Tags = review.Outlet is null ? [] : [review.Outlet],
        Metrics = Metrics(("reviewScore", review.Score)),
        Attributes = Attributes(
            ("reviewId", review.Id),
            ("outlet", review.Outlet)),
    };
}
```

### What each part does

- **The constructor parameters.** `HttpClient httpClient` and `IOptions<GameReviewsOptions> options` are given to
  you by dependency injection. The `HttpClient` is already set up: it has the base address, the timeouts, the
  retries, the circuit breaker and the statistics. You only call it.
- **`SourceName`.** Keep the name in a constant. The registration in Step 5 uses the same constant, so the name in
  answers and the name in statistics can never differ.
- **The `search` text.** You receive it already trimmed and in lower case. Always pass it through
  `Uri.EscapeDataString`, so spaces and special characters are safe in the address (`witcher 3` becomes
  `witcher%203`).
- **`GetFromJsonAsync`.** It sends the request and reads the JSON. If the API answers with an error status (for
  example 503), it throws an `HttpRequestException`. **Do not catch it.** The shared code turns it into the status
  `Fallback` or `Failed`.
- **The `cancellationToken`.** Always pass it on. If the client closes the connection, or a timeout is reached, the
  call stops at once.
- **`Map`.** This method turns one API record into one `AggregatedItem`. Keep it `static` and free of network calls.
  Then it is easy to test.

### How to fill an `AggregatedItem`

| Property | What to put in it |
|---|---|
| `Source` | Always `SourceName`. |
| `Category` | One of `GameInfo` (facts about a game), `Deal` (a price or discount) or `PlayerActivity` (how many people play). |
| `Title` | The game's name. Relevance is computed from it, so use the plain game title. |
| `Url` | A link to the item on the source's website. Use `ToUri(...)`. It returns `null` for a missing or broken address. |
| `ImageUrl` | A picture, if the API has one. Also use `ToUri(...)`. |
| `Date` | The date that best describes the item, in UTC. Leave it `null` if there is none. |
| `DateKind` | What the date means: `Released`, `PriceUpdated` or `Measured`. If `Date` is `null`, it is left out automatically. |
| `Tags` | Short labels that clients can filter on with `tag=`, for example genres, platforms or store names. |
| `Metrics` | Numbers, for example a score or a price. Use the helper `Metrics(...)`. It leaves out `null` values. |
| `Attributes` | Text facts, for example an ID or a list of platforms. Use the helper `Attributes(...)`. It leaves out empty values. |

Do **not** set `Relevance`. The service computes it the same way for every source.

Use `camelCase` for the keys of `Metrics` and `Attributes` (`reviewScore`, not `ReviewScore`). The browser UI turns
them into readable labels such as "Review score".

---

## Step 5: Write the registration method

The registration method tells the application that your source exists. It is an extension method, and it calls the
shared method `AddGameDataProvider<TProvider, TOptions>`. That one call sets up everything listed in
[What you get for free](#what-you-get-for-free).

**`GameReviewsRegistration.cs`**

```csharp
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GameDataAggregator.Infrastructure.Providers.GameReviews;

/// <summary>Adds the Game Reviews source to the app.</summary>
public static class GameReviewsRegistration
{
    /// <summary>Registers Game Reviews with its settings and address.</summary>
    public static IServiceCollection AddGameReviewsProvider(this IServiceCollection services, IConfiguration configuration)
    {
        //  Connect Game Reviews using its base address from the settings.
        services.AddGameDataProvider<GameReviewsProvider, GameReviewsOptions>(
            configuration,
            GameReviewsProvider.SourceName,
            GameReviewsOptions.SectionName,
            (client, options) => client.BaseAddress = options.BaseUrl);

        return services;
    }
}
```

The arguments of `AddGameDataProvider`, in order:

| Argument | Meaning |
|---|---|
| `configuration` | The app's settings. Your settings are read from `ExternalApis:{sectionName}`. |
| `apiName` | The name used in the statistics. Pass `SourceName`, so it matches the provider's `Name`. |
| `sectionName` | Your block's name inside `ExternalApis`. Pass `SectionName`. |
| `configureClient` | Optional. Sets up the `HttpClient`, usually its base address. |
| `configureHandlers` | Optional. Adds extra steps that run on every request, for example signing in. IGDB uses it for Twitch sign-in (see [`IgdbRegistration.cs`](../src/GameDataAggregator.Infrastructure/Providers/Igdb/IgdbRegistration.cs)). Most sources leave it out. |

---

## Step 6: Add one line to `AddInfrastructure`

Open [`InfrastructureServiceCollectionExtensions.cs`](../src/GameDataAggregator.Infrastructure/InfrastructureServiceCollectionExtensions.cs).
Add a `using` line for your folder at the top, and one line to the list of sources:

```csharp
using GameDataAggregator.Infrastructure.Providers.GameReviews;

// ...

        services
            .AddRawgProvider(configuration)
            .AddCheapSharkProvider(configuration)
            .AddSteamProvider(configuration)
            .AddIgdbProvider(configuration)
            .AddSpeedrunProvider(configuration)
            .AddGameReviewsProvider(configuration);
```

The order of this list is the order of the sources in `GET /api/v1/sources` and in each answer's `sources` list.

---

## Step 7: Add the settings to `appsettings.json`

Open [`appsettings.json`](../src/GameDataAggregator.Api/appsettings.json) and add a block inside `ExternalApis`. Copy
the shared settings from another source, so every source can be tuned the same way:

```json
"ExternalApis": {
  "...": "the other sources stay as they are",
  "GameReviews": {
    "Enabled": true,
    "BaseUrl": "https://api.gamereviews.example/v1/",
    "MaxResults": 10,
    "AttemptTimeout": "00:00:05",
    "TotalTimeout": "00:00:15",
    "MaxRetryAttempts": 2
  }
}
```

Every value here is optional, because the options class has defaults. Writing them down still helps, because people
can see what they can change.

**Never put an API key or a password in `appsettings.json`.** That file is in source control. See
[If your API needs a key](#if-your-api-needs-a-key).

---

## Step 8: Write the unit tests

Unit tests check your provider without the internet. They give it a stub `HttpClient` that returns a fixed answer.
The project already has everything you need:

- [`StubHttpMessageHandler`](../tests/Shared/StubHttpMessageHandler.cs) answers every request the way your test
  decides. It also records every request, so you can check the address your provider called.
- [`SampleResponses`](../tests/Shared/SampleResponses.cs) holds sample JSON answers for all sources. Both test
  projects share it.

### 8a. Add a sample answer

Add a constant to `SampleResponses.cs`. Copy a real answer from the API's documentation, and cut it down. Include one
complete record and one record with many missing fields:

```csharp
public const string GameReviews = """
    {
      "reviews": [
        {
          "id": "r-101",
          "gameTitle": "The Witcher 3: Wild Hunt",
          "score": 92,
          "releaseDate": "2015-05-19",
          "link": "https://gamereviews.example/r-101",
          "outlet": "PC Weekly"
        },
        { "id": "r-102", "gameTitle": "The Witcher" }
      ]
    }
    """;
```

### 8b. Add the test class

Create `tests/GameDataAggregator.UnitTests/Infrastructure/GameReviewsProviderTests.cs`. Copy the structure of
`SpeedrunProviderTests`. The `Create` helper builds the provider on top of the stub:

```csharp
using System.Net;
using GameDataAggregator.Core.Abstractions;
using GameDataAggregator.Core.Models;
using GameDataAggregator.Infrastructure.Providers.GameReviews;
using GameDataAggregator.Tests.Shared;
using Microsoft.Extensions.Options;

namespace GameDataAggregator.UnitTests.Infrastructure;

/// <summary>Tests the Game Reviews source against sample answers.</summary>
public sealed class GameReviewsProviderTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>Creates the source with a fake network.</summary>
    private static (GameReviewsProvider Provider, StubHttpMessageHandler Http) Create(
        Func<HttpRequestMessage, HttpResponseMessage>? respond = null)
    {
        //  Answer with the sample reviews unless told otherwise, and build the source on top.
        var http = new StubHttpMessageHandler(respond ?? (_ => StubHttpMessageHandler.Json(SampleResponses.GameReviews)));
        var options = new GameReviewsOptions { MaxResults = 5 };
        var client = new HttpClient(http) { BaseAddress = options.BaseUrl };
        return (new GameReviewsProvider(client, Options.Create(options)), http);
    }

    /// <summary>Checks that the search asks for the name and the limit.</summary>
    [Fact]
    public async Task Searches_by_name_with_limit()
    {
        //  Set up the source.
        var (provider, http) = Create();

        //  Search it.
        await provider.FetchAsync("witcher 3", Ct);

        //  Expect one request to the exact search address.
        var request = Assert.Single(http.Received);
        Assert.Equal(new Uri("https://api.gamereviews.example/v1/reviews?query=witcher%203&limit=5"), request.Uri);
    }

    /// <summary>Checks that reviews are turned into items with all their details.</summary>
    [Fact]
    public async Task Maps_reviews_to_aggregated_items()
    {
        //  Set up the source.
        var (provider, _) = Create();

        //  Search it.
        var items = await provider.FetchAsync("witcher", Ct);

        //  Expect the first review's details to be copied across.
        var first = items[0];
        Assert.Equal("GameReviews", first.Source);
        Assert.Equal(ItemCategory.GameInfo, first.Category);
        Assert.Equal("The Witcher 3: Wild Hunt", first.Title);
        Assert.Equal(new DateTimeOffset(2015, 5, 19, 0, 0, 0, TimeSpan.Zero), first.Date);
        Assert.Equal(92, first.Metrics["reviewScore"]);
        Assert.Equal("PC Weekly", first.Attributes["outlet"]);
    }

    /// <summary>Checks that a review with few details is still handled.</summary>
    [Fact]
    public async Task Handles_missing_optional_fields()
    {
        //  Set up the source.
        var (provider, _) = Create();

        //  Search it and take the review with few details.
        var minimal = (await provider.FetchAsync("witcher", Ct))[1];

        //  Expect the missing details to be left empty.
        Assert.Null(minimal.Url);
        Assert.Null(minimal.Date);
        Assert.Null(minimal.DateKind);
        Assert.Empty(minimal.Tags);
        Assert.Empty(minimal.Metrics);
    }

    /// <summary>Checks that the source needs no settings.</summary>
    [Fact]
    public void Needs_no_configuration()
    {
        //  Look at the source the way the search service does.
        IGameDataProvider provider = Create().Provider;

        //  Expect no settings problem.
        Assert.Null(provider.ConfigurationError);
    }

    /// <summary>Checks that an error answer is passed on with its status.</summary>
    [Fact]
    public async Task Error_responses_throw()
    {
        //  Set up the source to answer "unavailable".
        var (provider, _) = Create(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));

        //  Search it.
        var exception = await Assert.ThrowsAsync<HttpRequestException>(() => provider.FetchAsync("witcher", Ct));

        //  Expect the error to carry that status.
        Assert.Equal(HttpStatusCode.ServiceUnavailable, exception.StatusCode);
    }
}
```

These five tests cover what matters for a provider:

1. It calls the **right address**.
2. It **maps** a complete record correctly.
3. It survives a record with **missing fields**.
4. It reports its **configuration** correctly.
5. It **throws** on an error answer, so the shared code can handle it.

One detail about test 4: `ConfigurationError` has a default body in the interface. If your class does not define it,
you can only read it through the interface type. That is why the test stores the provider in a variable of type
`IGameDataProvider`.

---

## Step 9: Update the integration tests

The integration tests start the whole application with a fake internet. They check a fixed list of sources, so they
fail until you tell them about yours. Change these places in `tests/GameDataAggregator.IntegrationTests/`:

1. **[`Infrastructure/FakeExternalApis.cs`](../tests/GameDataAggregator.IntegrationTests/Infrastructure/FakeExternalApis.cs).**
   Add a line to the `switch`, so the fake internet answers your API's address:

   ```csharp
   ("api.gamereviews.example", "/v1/reviews") => StubHttpMessageHandler.Json(SampleResponses.GameReviews),
   ```

   Without it, your source gets `404 Not Found` and shows as `Failed`.
2. **[`Infrastructure/GameDataAggregatorFactory.cs`](../tests/GameDataAggregator.IntegrationTests/Infrastructure/GameDataAggregatorFactory.cs).**
   Add `"GameReviews"` to the list of section names that get one retry. This keeps the tests fast.
3. **The lists of expected source names.** Add `"GameReviews"` to:
   - `AggregationEndpointTests.cs`: the list in the test that aggregates all sources.
   - `UiAndSourcesEndpointTests.cs`: both lists (all sources, and all sources except Steam).
   - `StatisticsEndpointTests.cs`: the list of APIs. This list is in **alphabetical** order.
4. **Optional but useful.** In `StatisticsEndpointTests.cs`, add the number of requests your source sends for one
   search, next to the other sources.

---

## Step 10: Run it and check the result

1. Run all tests from the repository folder:

   ```bash
   dotnet test
   ```

   The build treats every warning as an error. For example, a nullable warning or an XML comment that documents
   only some of a method's parameters stops the build. Fix those first.

2. Start the service:

   ```bash
   dotnet run --project src/GameDataAggregator.Api
   ```

3. Check these things:
   - The start-up log line `N of N sources are ready` lists `GameReviews`.
   - `http://localhost:5067/api/v1/sources` lists `GameReviews`. This endpoint is open, so a browser can show it.
   - `http://localhost:5067` shows a `GameReviews` tick box. Search for `witcher` with only `GameReviews` ticked.
     Your items appear, and your source's status card says `Live`.
   - The same search again says `Cached`.
   - The **Statistics** tab shows requests for `GameReviews`.

   The search and statistics endpoints need an access token (a JWT). The browser page and the Postman collection
   get one by themselves while the service runs in Development. To call them from your own script, first get a
   token from `POST /api/v1/auth/token` and send it as `Authorization: Bearer <token>`.

4. Update [`README.md`](../README.md) (the source table at the top) and the Postman collection if you want an example
   request for your source.

---

## If your API needs a key

Follow the RAWG source as the model
([`RawgOptions.cs`](../src/GameDataAggregator.Infrastructure/Providers/Rawg/RawgOptions.cs),
[`RawgProvider.cs`](../src/GameDataAggregator.Infrastructure/Providers/Rawg/RawgProvider.cs)). Three small changes
are needed.

**1. Add the key to the options class.** Do not mark it `[Required]`. A missing key must not stop the app. It only
switches off this one source.

```csharp
    /// <summary>The API key. Keep it out of source control (user-secrets or an environment variable).</summary>
    public string? ApiKey { get; set; }
```

**2. Add `ConfigurationError` to the provider.** It returns a message that names the missing setting, or `null` when
the key is there:

```csharp
    public string? ConfigurationError => string.IsNullOrWhiteSpace(options.Value.ApiKey)
        ? $"Game Reviews API key is not configured. Set '{ProviderRegistration.ConfigurationRoot}:{GameReviewsOptions.SectionName}:ApiKey'."
        : null;
```

The service checks this **before** it calls `FetchAsync`. If there is a message, your source gets the status
`NotConfigured` with that message. Your API is not called, nothing is thrown, and the log mentions it only once, at
start-up. Clients still get the data of all other sources.

**3. Send the key.** Put it where your API expects it. RAWG puts it in the address
(`games?key=...`). For an API that wants a header, set the header in the registration method:

```csharp
            (client, options) =>
            {
                //  Set the address and send the key with every request.
                client.BaseAddress = options.BaseUrl;
                client.DefaultRequestHeaders.Add("X-Api-Key", options.ApiKey ?? string.Empty);
            });
```

**Store the key outside the code.** Use user-secrets on your own computer:

```bash
dotnet user-secrets set "ExternalApis:GameReviews:ApiKey" "<your-key>" --project src/GameDataAggregator.Api
```

On a server, use an environment variable. Two underscores (`__`) stand for the colon (`:`):

```bash
ExternalApis__GameReviews__ApiKey=<your-key>
```

Leave `"ApiKey": ""` (empty) in `appsettings.json`, so people can see that the setting exists.

**If your API uses Twitch sign-in** (any Twitch-owned API), you do not need a key of your own. Pass
`handlers => handlers.AddTwitchAuthentication<GameReviewsOptions>(configuration)` as the last argument of
`AddGameDataProvider`, like IGDB does. Your source then shares the same Twitch application and access token. The type
in `<...>` is your options class. The app uses it to check at start-up that `TwitchAuth:Timeout` is shorter than
your `AttemptTimeout`.

**If your source makes side requests** (extra calls that only decorate the main data, like CheapShark's store list
or Steam's player counts), give them their own client with `services.AddSideRequestClient<GameReviewsOptions>(...)`
in the registration method, and get it in the provider with `IHttpClientFactory.CreateClient(name)`. See
`CheapSharkRegistration` and `CheapSharkProvider`. Side requests on the main client share its retries and circuit
breaker, so a broken side endpoint could block your main request (ISS-033).

---

## If your data does not fit the existing categories

The categories are fixed in [`ItemCategory.cs`](../src/GameDataAggregator.Core/Models/ItemCategory.cs):
`GameInfo`, `Deal` and `PlayerActivity`. Pick the closest one if you can. If none fits, for example for live
streams, add a new value, such as `StreamActivity`.

A new category needs two more changes:

1. **The browser UI.** Add the value to the `CATEGORIES` list and to `CATEGORY_LABELS` at the top of
   [`wwwroot/js/app.js`](../src/GameDataAggregator.Api/wwwroot/js/app.js). The UI does not read categories from the
   API, so it does not show a new one by itself.
2. **The documentation.** The list of valid values in the README and in `AggregationRequest.cs` mentions the
   categories by name.

The API itself checks categories against the enum, so `categories=StreamActivity` is accepted automatically.

The same is true for a new kind of date. If none of `Released`, `PriceUpdated` and `Measured` fits, add a value to
[`DateKind.cs`](../src/GameDataAggregator.Core/Models/DateKind.cs), and add a label to `DATE_KIND_LABELS` in
`app.js`.

---

## Common mistakes

| Mistake | What happens | How to avoid it |
|---|---|---|
| The base address has no slash at the end (`.../v1`) | .NET replaces the last part of the address. `reviews?...` goes to `.../reviews` instead of `.../v1/reviews`, and the API answers 404. | Always end `BaseUrl` with `/`, and mark it `[BaseAddress]` so a wrong value stops the app at start-up. Never start the relative address with `/`. |
| Catching exceptions in the provider | Errors are hidden. The service reports `Live` with no items instead of `Fallback`, and the bad answer is cached. | Let exceptions escape. The shared code handles them. |
| Returning an empty list for a missing key | The source looks `Live` but has no data. Nobody knows why. | Use `ConfigurationError`. |
| Forgetting `Uri.EscapeDataString` | A search with spaces or `&` breaks the address or changes its meaning. | Escape every value you put into an address. |
| Two sources with the same name | The app refuses to start: `Provider names must be unique`. | Pick a unique `SourceName`. Letter case does not count, so `Speedrun` and `SPEEDRUN` are the same. |
| Different names in `Name` and `apiName` | Statistics are listed under a name that clients do not know. | Use the `SourceName` constant in both places. |
| `TotalTimeout` not longer than `AttemptTimeout` | The app refuses to start with a settings error. | Keep `TotalTimeout` longer, so there is time for a retry. |
| An API key in `appsettings.json` | The key ends up in source control for everyone to see. | Use user-secrets or environment variables. |
| A test that calls the real API | The test is slow and fails when the internet or the API is down. | Use `StubHttpMessageHandler` and `SampleResponses`. |

---

## Checklist

- [ ] `Providers/GameReviews/` folder with four files: options, models, provider, registration.
- [ ] The options class derives from `ExternalApiOptions` and has `SectionName` and `BaseUrl` (ending with `/`).
- [ ] The models are `internal` records with only the fields you use.
- [ ] The provider implements `IGameDataProvider`, uses a `SourceName` constant, passes the `cancellationToken`,
      escapes the search text and does not catch exceptions.
- [ ] `ConfigurationError` is implemented if the API needs a key.
- [ ] The registration method calls `AddGameDataProvider<GameReviewsProvider, GameReviewsOptions>`.
- [ ] One new line in `InfrastructureServiceCollectionExtensions.AddInfrastructure`.
- [ ] A new block under `ExternalApis` in `appsettings.json`, without secrets.
- [ ] A sample answer in `SampleResponses.cs` and a `GameReviewsProviderTests` class.
- [ ] The integration tests know the new source (fake address, factory list, expected names).
- [ ] `dotnet test` passes.
- [ ] The source appears in `/api/v1/sources`, in `/api/v1/statistics` and in the browser UI.
- [ ] README and, if needed, the UI (`app.js`) are updated.
