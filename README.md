# Game Data Aggregator

An ASP.NET Core (.NET 10) service that searches several video game APIs **in parallel** and returns one unified,
filterable and sortable result through a single endpoint, plus an endpoint with per-API request statistics.
Both endpoints are protected with JWT bearer tokens, and a background service logs a warning when an external API
becomes noticeably slower than usual. It ships with a browser UI for people and a Postman collection for API clients.

| Source | What it contributes | Category | API key |
|---|---|---|---|
| [RAWG](https://rawg.io/apidocs) | Game catalog and design data: genres, platforms, ratings, release dates | `GameInfo` | Free key required |
| [CheapShark](https://apidocs.cheapshark.com/) | Sales: current prices and discounts across PC stores | `Deal` | None |
| [Steam](https://steamcommunity.com/dev) | Player engagement: live concurrent player counts | `PlayerActivity` | None |
| [IGDB](https://api-docs.igdb.com/) | Game production data: developers, publishers, genres, themes, critic and user ratings | `GameInfo` | Free Twitch application credentials |
| [speedrun.com](https://github.com/speedruncomorg/api) | Community engagement: speedrun leaderboard categories, moderators, platforms | `PlayerActivity` | None |

Search for `witcher` and you get the catalog entries, who made the game, every current deal, how many people are
playing right now and how active its speedrunning community is, in one response.

**Three sources work with no setup at all** (CheapShark, Steam and speedrun.com), so the service returns real data
straight after cloning. RAWG and IGDB add more once their free keys are configured.

---

## Contents

- [Getting started](#getting-started)
- [Using the service: browser UI, Postman and other clients](#using-the-service-browser-ui-postman-and-other-clients)
- [Endpoints](#endpoints)
- [Authentication](#authentication)
- [Configuration](#configuration)
- [Architecture](#architecture)
- [Error handling, resilience and fallback](#error-handling-resilience-and-fallback)
- [Caching](#caching)
- [Request statistics](#request-statistics)
- [Performance monitoring](#performance-monitoring)
- [Adding a new API](#adding-a-new-api)
- [Testing](#testing)
- [Design decisions](#design-decisions)
- [Engineering log](#engineering-log)

---

## Getting started

**Prerequisites:** [.NET 10 SDK](https://dotnet.microsoft.com/download).

```bash
git clone <repository-url>
cd <repository-folder>

# Optional but recommended: a free RAWG key from https://rawg.io/apidocs
dotnet user-secrets set "ExternalApis:Rawg:ApiKey" "<your-key>" --project src/GameDataAggregator.Api

# Optional but recommended: Twitch application credentials for IGDB (see below)
dotnet user-secrets set "ExternalApis:TwitchAuth:ClientId" "<client-id>" --project src/GameDataAggregator.Api
dotnet user-secrets set "ExternalApis:TwitchAuth:ClientSecret" "<client-secret>" --project src/GameDataAggregator.Api

# Optional: a fixed key for signing access tokens, so tokens survive a restart (at least 32 bytes)
dotnet user-secrets set "Jwt:SigningKey" "$(openssl rand -base64 48)" --project src/GameDataAggregator.Api

dotnet run --project src/GameDataAggregator.Api
```

Then open **http://localhost:5067** for the browser UI. The interactive API reference is at
**http://localhost:5067/scalar** (Development only), and the OpenAPI document at `/openapi/v1.json`.

**Getting IGDB credentials.** IGDB belongs to Twitch and uses Twitch's OAuth. Registering an "application" is free and
is just a way of identifying your program:

1. Sign in at [dev.twitch.tv/console](https://dev.twitch.tv/console) with a Twitch account that has two-factor
   authentication enabled.
2. **Register Your Application**: any name, OAuth redirect URL `http://localhost`, category *Application Integration*.
3. Copy the **Client ID**, create a **Client Secret**, and store both with the commands above.

The service exchanges them for an app access token (client credentials flow) by itself and renews it when needed.

**Missing keys are not fatal.** Without a RAWG key or Twitch credentials, that source is reported as `NotConfigured`
with a message saying which setting is missing, its API is not called, and the other sources are returned as normal.
The missing setting is logged once, as a warning at start-up, not on every request. Secrets can also be supplied
as environment variables, e.g. `ExternalApis__Rawg__ApiKey=<your-key>`. Never put them in `appsettings.json`.

Quick check from the command line. The search and statistics endpoints need an access token, which the
development-only token endpoint gives out (see [Authentication](#authentication)):

```bash
TOKEN=$(curl -s -X POST http://localhost:5067/api/v1/auth/token | sed -E 's/.*"accessToken":"([^"]+)".*/\1/')
curl -H "Authorization: Bearer $TOKEN" "http://localhost:5067/api/v1/aggregation?search=witcher&pageSize=5"
curl -H "Authorization: Bearer $TOKEN" "http://localhost:5067/api/v1/statistics"
```

---

## Using the service: browser UI, Postman and other clients

The service is a plain REST API: `GET` requests with query-string parameters, JSON responses. Anything that speaks
HTTP can use it, including a browser, Postman, curl, or another program.

### Browser UI

Open **http://localhost:5067**. The page is served by the API itself from
[`wwwroot`](src/GameDataAggregator.Api/wwwroot), and calls the same public endpoints as every other client. It is
plain HTML, CSS and JavaScript, so there is no build step and nothing to install.

- **Search tab:**
  - a search box with every filter and sort option, plus a status card per source (`Live`, `Cached`, `Fallback`,
    `Failed` or `Not configured`, item count, time taken, and why a source failed);
  - result cards with images, tags, metrics and details, and paging;
  - validation errors shown as returned by the API.
- **Request URL:** each search shows the exact request it sent, with a **Copy URL** button for Postman or curl. The
  page's own address keeps the search, so a result can be bookmarked or shared.
- **Statistics tab:** the per-API statistics table with the fast, average and slow buckets, with optional
  auto-refresh.
- **Safe rendering:** data from external APIs is always inserted as text, never as HTML. Links and images are
  accepted only with `http(s)` URLs.
- **Access token:** in Development the page gets a token from `POST /api/v1/auth/token` by itself, reuses it until
  shortly before it expires, and fetches a new one once if a request is refused (for example after a restart).
  Outside Development there is no token endpoint, so the search and statistics show a "needs an access token"
  message.

### Postman

Import [`postman/GameDataAggregator.postman_collection.json`](postman/GameDataAggregator.postman_collection.json)
(**Import**, then choose the file). The collection has:

- a `baseUrl` variable, defaulting to `http://localhost:5067`;
- requests for every endpoint, including one that uses every parameter, one limited to the three sources that need
  no key, and one that shows a `400` validation error;
- test scripts, so the **Collection Runner** can check the whole API in one click;
- bearer authentication for the whole collection, using the `accessToken` variable. A collection pre-request script
  fills it from `POST /api/v1/auth/token` when it is empty or about to expire, so nothing needs setting up in
  Development. Outside Development, paste a token into `accessToken` instead. The **Authentication** folder shows
  the token request and a search refused without a token.

You can also import the API definition straight from the running service: **Import**, then
`http://localhost:5067/openapi/v1.json`.

> **HTTPS:** the `https` profile (`https://localhost:7252`) uses the ASP.NET Core development certificate. Run
> `dotnet dev-certs https --trust`, or turn off *SSL certificate verification* in Postman's settings, or simply use
> the `http` URL locally.

### Other clients

The UI is served from the same origin as the API, so it needs no CORS. Server-side clients, Postman and curl are not
subject to CORS either. Only a browser app hosted on a *different* origin would need a CORS policy added in
`Program.cs`.

---

## Endpoints

| Method | Route | Token | Description |
|---|---|---|---|
| `GET` | `/api/v1/aggregation` | Required | Search all sources and return aggregated, filtered, sorted, paged items |
| `GET` | `/api/v1/statistics` | Required | Request count and average response time per external API, by performance bucket |
| `POST` | `/api/v1/auth/token` | - | Issue a short-lived access token (Development only, `404` elsewhere) |
| `GET` | `/api/v1/sources` | - | Names of the enabled sources (valid values of the `sources` parameter) |
| `GET` | `/health` | - | Liveness check (`Healthy`) |
| `GET` | `/` | - | Browser UI |
| `GET` | `/openapi/v1.json`, `/scalar` | - | OpenAPI document and interactive reference (Development) |

### `GET /api/v1/aggregation`

| Parameter | Type | Default | Description |
|---|---|---|---|
| `search` | string, 2-100 chars | **required** | Game name to search for in every source. Outer spaces are trimmed before the length is checked |
| `sources` | comma-separated | all | `RAWG`, `CheapShark`, `Steam`, `IGDB`, `Speedrun` (case-insensitive; `GET /api/v1/sources` lists them). Unselected sources are not called |
| `categories` | comma-separated | all | `GameInfo`, `Deal`, `PlayerActivity` |
| `tag` | string | - | Only items with this tag: a genre (`RPG`), a store (`GOG`), a platform (`Linux`), `On Sale` |
| `from`, `to` | ISO 8601 date | - | Inclusive date range on the item's `date`. Undated items are excluded. A value without an offset (`2015-05-18`) is UTC, so `to=2015-05-18` ends at the *start* of that day; use `2015-05-18T23:59:59Z` to include all of it |
| `sortBy` | enum | `Relevance` | `Relevance`, `Date`, `Title`, `Source` (names only, case-insensitive; numbers are rejected) |
| `sortDirection` | enum | `Descending` | `Ascending`, `Descending` |
| `page` | int, 1-1000 | `1` | 1-based page number |
| `pageSize` | int, 1-100 | `20` | Items per page |

**Example:** `GET /api/v1/aggregation?search=witcher 3&categories=Deal&sortBy=Date&pageSize=1`

```json
{
  "search": "witcher 3",
  "page": 1,
  "pageSize": 1,
  "totalCount": 7,
  "items": [
    {
      "source": "CheapShark",
      "category": "Deal",
      "title": "The Witcher 3: Wild Hunt",
      "url": "https://www.cheapshark.com/redirect?dealID=...",
      "imageUrl": "https://shared.fastly.steamstatic.com/...jpg",
      "date": "2026-11-05T08:30:51+00:00",
      "dateKind": "PriceUpdated",
      "relevance": 0.659,
      "tags": [ "GOG", "On Sale" ],
      "metrics": { "salePrice": 9.99, "normalPrice": 39.99, "savingsPercent": 75.02, "metacriticScore": 93 },
      "attributes": { "store": "GOG", "currency": "USD", "steamRating": "Overwhelmingly Positive" }
    }
  ],
  "sources": [
    { "source": "RAWG", "status": "Live", "itemCount": 10, "fetchedAt": "2026-11-07T10:14:52Z", "durationMs": 412.3 },
    { "source": "CheapShark", "status": "Cached", "itemCount": 7, "fetchedAt": "2026-11-07T10:12:01Z", "durationMs": 0.2 },
    { "source": "Steam", "status": "Fallback", "itemCount": 6, "fetchedAt": "2026-11-07T09:40:10Z", "durationMs": 15002.1,
      "error": "The external API did not respond in time." }
  ]
}
```

**Item fields.** Every source is normalised into the same shape:

| Field | Meaning |
|---|---|
| `source`, `category`, `title`, `url`, `imageUrl` | Where the item comes from and what it is |
| `date` | The date that best describes the item. `dateKind` says what it is |
| `dateKind` | `Released`, `PriceUpdated` (deals) or `Measured` (Steam player counts). Omitted when there is no date |
| `relevance` | 0-1 match of the title against `search`, computed the same way for every source (see [Design decisions](#design-decisions)) |
| `tags` | Labels used by the `tag` filter |
| `metrics` | Numeric facts, e.g. `rating`, `metacritic`, `salePrice`, `savingsPercent`, `currentPlayers` |
| `attributes` | Text facts, e.g. `store`, `platforms`, `steamAppId` |

**Source statuses.** A failing API never fails the request. Each entry in `sources` tells you how its data was obtained:

| Status | Meaning |
|---|---|
| `Live` | Fetched from the external API during this request |
| `Cached` | Served from the cache. `fetchedAt` says how old it is |
| `Fallback` | The API failed. The last successful response was served instead, and `error` says why |
| `Failed` | The API failed and no earlier data exists. The source contributes no items |
| `NotConfigured` | A required setting such as an API key is missing, so the API was not called. `error` names the setting |

**Errors** use [RFC 9457 problem details](https://www.rfc-editor.org/rfc/rfc9457) (`application/problem+json`).
A `400` lists every invalid field at once, so a client can fix them all in one go:

```json
{
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "Sources": [ "Unknown sources: Twitter. Valid values: RAWG, CheapShark, Steam, IGDB, Speedrun." ],
    "Page": [ "The field Page must be between 1 and 1000." ]
  }
}
```

The check that `from` is not after `to` runs once every field is valid on its own.

**Rate limit.** Each client (by IP address) may make 60 searches a minute; see `RateLimiting` under
[Configuration](#configuration). Every new search calls several external APIs, so the limit protects their quotas,
and our keys, from a single heavy client. Beyond it the API answers `429 Too Many Requests` with a `Retry-After`
header (seconds) and a problem details body. `/statistics`, `/sources`, `/health` and the UI are not limited.

### `GET /api/v1/statistics`

Every HTTP request sent to an external API is measured, retries included. Responses served from the cache never reach the API, so they are not counted.

```json
{
  "generatedAt": "2026-11-07T10:15:05Z",
  "apis": [
    {
      "api": "CheapShark",
      "totalRequests": 2,
      "failedRequests": 0,
      "averageResponseTimeMs": 195.58,
      "buckets": [
        { "bucket": "Fast",    "range": "< 100 ms",   "requests": 1, "averageResponseTimeMs": 83.44 },
        { "bucket": "Average", "range": "100-200 ms", "requests": 0, "averageResponseTimeMs": 0 },
        { "bucket": "Slow",    "range": "> 200 ms",   "requests": 1, "averageResponseTimeMs": 307.72 }
      ]
    }
  ]
}
```

`failedRequests` counts server errors (5xx), throttling (429), timeouts (408 or client-side) and network failures.
Other 4xx responses are valid answers from the API, for example Steam's 404 for a game with no player statistics.

---

## Authentication

The search and statistics endpoints need a **JWT bearer token**: `Authorization: Bearer <token>`. Without a valid
token they answer `401 Unauthorized` with a `WWW-Authenticate: Bearer` header and a problem details body, before any
external API is called. `/api/v1/sources`, `/health`, the browser UI and the API reference stay open, because they
cost nothing and reveal nothing private.

**What makes a token valid.** It must be signed with HMAC-SHA256 using the key in `Jwt:SigningKey`, come from the
issuer in `Jwt:Issuer`, be meant for the audience in `Jwt:Audience`, and not be expired (30 seconds of clock skew are
allowed). Any other algorithm is refused.

**Getting a token while developing.** `POST /api/v1/auth/token` returns a token valid for `Jwt:TokenLifetime`
(1 hour):

```json
{ "accessToken": "eyJhbGciOi...", "tokenType": "Bearer", "expiresIn": 3600, "expiresAt": "2026-11-08T17:00:00+00:00" }
```

It exists only in the `Development` environment, because anyone who can reach it gets a token. Elsewhere it answers
`404`. The browser UI and the Postman collection call it by themselves. In Scalar, call it once, then paste the
`accessToken` into the **Bearer** authentication field. Every protected operation in the OpenAPI document is marked
as needing the token.

**The signing key.** Keep it out of source control: user-secrets while developing (see
[Getting started](#getting-started)), and an environment variable (`Jwt__SigningKey`) or a secret store elsewhere. It
must be at least 32 bytes. In Development it may be left out: a random key is then made at each start-up and a
warning is logged, so the app works straight after cloning, but tokens stop working when it restarts. Outside
Development a missing or short key stops start-up.

**In production** an identity provider would issue the tokens, and this API would only check them. Moving to that
means changing the token check to the provider's published keys (`Authority`) and removing the development endpoint.
See DEC-029.

---

## Configuration

All settings live in [`appsettings.json`](src/GameDataAggregator.Api/appsettings.json). They are validated at
start-up, so an invalid value stops the app immediately with a clear message instead of failing at the first request.

| Key | Default | Description |
|---|---|---|
| `RateLimiting:PermitLimit` | `60` | Searches allowed per client per window |
| `RateLimiting:Window` | `00:01:00` | Length of the fixed rate-limit window |
| `Aggregation:CacheDuration` | `00:05:00` | How long a source's response is reused before calling the API again |
| `Aggregation:FallbackDuration` | `06:00:00` | How long the last successful response is kept for fallback |
| `Statistics:FastThresholdMs` | `100` | Requests faster than this are `Fast` |
| `Statistics:SlowThresholdMs` | `200` | Requests slower than this are `Slow`. Anything in between is `Average` |
| `PerformanceMonitor:Enabled` | `true` | Turns the background slowdown check on or off |
| `PerformanceMonitor:CheckInterval` | `00:01:00` | How often the statistics are checked (5 s to 1 h) |
| `PerformanceMonitor:Window` | `00:05:00` | Recent period compared with everything before it. Whole minutes, 1 to 60 |
| `PerformanceMonitor:SlowdownThresholdPercent` | `50` | How much slower, in percent, the recent average must be to log a warning |
| `PerformanceMonitor:MinimumRequests` | `5` | Fewest requests needed both inside the window and before it to judge an API |
| `Jwt:SigningKey` | - | Key that signs and checks access tokens, at least 32 bytes. Set it via user-secrets or an environment variable. Optional in Development only |
| `Jwt:Issuer` / `Jwt:Audience` | `GameDataAggregator` / `GameDataAggregator.Api` | Values a token must carry to be accepted |
| `Jwt:TokenLifetime` | `01:00:00` | Lifetime of tokens from the development token endpoint |
| `ExternalApis:UserAgent` | `GameDataAggregator/1.0` | Sent with every outgoing request (CheapShark requires one) |
| `ExternalApis:{Api}:Enabled` | `true` | A disabled API is never called and does not appear in responses |
| `ExternalApis:{Api}:MaxResults` | `10` | Items requested per search (1-50) |
| `ExternalApis:{Api}:AttemptTimeout` | `00:00:05` | Timeout per HTTP attempt |
| `ExternalApis:{Api}:TotalTimeout` | `00:00:15` | Timeout including all retries (must exceed `AttemptTimeout`) |
| `ExternalApis:{Api}:MaxRetryAttempts` | `2` | Retries for transient failures (1-10) |
| `ExternalApis:Rawg:ApiKey` | - | RAWG key. Set it via user-secrets or an environment variable |
| `ExternalApis:Rawg:BaseUrl` / `CheapShark:BaseUrl` | public URLs | API base addresses. Every base address must be a full http(s) address ending with `/`, or the app does not start |
| `ExternalApis:CheapShark:StoreListCacheDuration` | `1.00:00:00` | The store list rarely changes, so it is cached longer |
| `ExternalApis:CheapShark:SideRequestTimeout` / `Steam:SideRequestTimeout` | `00:00:02` | Time limit for the store list (CheapShark) and each player count (Steam). These side requests get one try and their own circuit breaker |
| `ExternalApis:Steam:StoreBaseUrl` / `WebApiBaseUrl` | public URLs | Steam store and Web API hosts |
| `ExternalApis:Steam:CountryCode` / `Language` | `US` / `english` | Store region (prices) and language |
| `ExternalApis:Igdb:BaseUrl` | `https://api.igdb.com/v4/` | IGDB API base address |
| `ExternalApis:Igdb:CoverImageSize` | `t_cover_big` | [IGDB image size](https://api-docs.igdb.com/#images) used for cover URLs |
| `ExternalApis:Speedrun:BaseUrl` | `https://www.speedrun.com/api/v1/` | speedrun.com API base address |
| `ExternalApis:TwitchAuth:ClientId` / `ClientSecret` | - | Twitch application credentials, used by IGDB. Set them via user-secrets or environment variables |
| `ExternalApis:TwitchAuth:TokenUrl` | `https://id.twitch.tv/oauth2/token` | Twitch OAuth token endpoint |
| `ExternalApis:TwitchAuth:Timeout` | `00:00:02` | Time limit for a token request (one try). Must be shorter than `Igdb:AttemptTimeout`, or the app does not start |

---

## Architecture

```mermaid
flowchart LR
    Client -->|GET /api/v1/aggregation| Controller[AggregationController]
    Controller --> Service[AggregationService]
    Service -->|Task.WhenAll| Exec[ProviderExecutor<br/>cache / fallback]
    Exec --> Cache[(HybridCache)]
    Exec --> P1[RawgProvider]
    Exec --> P2[CheapSharkProvider]
    Exec --> P3[SteamProvider]
    Exec --> P4[IgdbProvider]
    Exec --> P5[SpeedrunProvider]
    subgraph HttpClient pipeline, per provider
        R[Resilience: timeout, retry, circuit breaker] --> A[Provider handlers<br/>e.g. Twitch OAuth for IGDB] --> S[RequestStatisticsHandler]
    end
    P1 & P2 & P3 & P4 & P5 --> R
    S --> APIs((External APIs))
    S --> Store[(InMemoryRequestStatisticsStore)]
    Stats[StatisticsController] --> Store
```

```
src/
  GameDataAggregator.Api             HTTP layer: controllers, request validation, OpenAPI, error handling,
                                     browser UI (wwwroot)
  GameDataAggregator.Core            Domain models, IGameDataProvider contract, aggregation, caching/fallback
                                     logic, filtering/sorting, statistics store. No knowledge of any specific API.
  GameDataAggregator.Infrastructure  One folder per external API (options, DTOs, provider, registration),
                                     shared HttpClient pipeline (resilience + statistics handler),
                                     reusable authentication (Twitch OAuth)
tests/
  GameDataAggregator.UnitTests         Core logic and each provider's mapping, with HTTP stubbed
  GameDataAggregator.IntegrationTests  The whole app in memory (WebApplicationFactory), only the network faked
postman/                               Postman collection for every endpoint
docs/                                  Engineering log: issues, decisions, patterns
```

**Request flow**

1. `AggregationController` validates the query and maps it to an `AggregationQuery`.
2. `AggregationService` starts every selected provider at once and awaits them all with `Task.WhenAll`.
   The calls are I/O-bound, so total latency is roughly that of the slowest API rather than the sum of all of them,
   and no extra threads are needed.
3. For each provider, `ProviderExecutor` returns cached data, or calls the API through its resilient `HttpClient`,
   or falls back to the last good data. It never throws, so one bad API cannot break the response.
4. The merged items get a relevance score. Filters, sorting and paging are applied in memory (`ItemQueryProcessor`).
   Only the search term is part of the cache key, so different filters on the same search reuse the cache.

---

## Error handling, resilience and fallback

Each provider's `HttpClient` gets the
[standard resilience pipeline](https://learn.microsoft.com/dotnet/core/resilience/http-resilience) (Polly), configured per API:

| Layer | Behaviour |
|---|---|
| Total timeout | Caps the whole call, retries included (`TotalTimeout`) |
| Retry | Retries transient failures (5xx, 408, 429, network errors) with exponential back-off and jitter (`MaxRetryAttempts`) |
| Circuit breaker | If at least half of 5 or more requests fail within the sampling window, the API is not called for 30 s. Requests then fail fast instead of piling up |
| Attempt timeout | Caps each individual attempt (`AttemptTimeout`) |

When the pipeline gives up, `ProviderExecutor`:

- serves the **last-known-good** copy of that source's data (kept for `FallbackDuration`) with status `Fallback`, or
- reports the source as `Failed` with an empty item list.

In both cases the client gets a safe, generic `error` message. The full exception is logged on the server, and
internal details never reach the caller. Partial failures inside a provider degrade gracefully as well:

- If CheapShark's store list is down, deals are still returned, labelled `Store #id`.
- If one Steam player count fails, that game is returned without the `currentPlayers` metric.
- These side requests (store list, player counts) use their own `HttpClient`, with one short try
  (`SideRequestTimeout`) and their own circuit breaker. A broken side endpoint therefore cannot hold up the main
  request for long or pause it.

Other cases:

- Invalid input returns `400` problem details.
- Unexpected exceptions return a `500` problem details response with no stack trace.
- A client that disconnects cancels all outgoing calls, because the `CancellationToken` flows all the way down.

---

## Caching

`HybridCache` (Microsoft.Extensions.Caching.Hybrid) stores each source's response per normalised search term.
`Witcher` and ` witcher ` share one entry.

- **Fewer calls:** repeated searches within `CacheDuration` do not call the external APIs. The cache key leaves out
  filters, sorting and paging, so many different requests are served from one entry.
- **Stampede protection:** if 100 requests for the same uncached search arrive at once, the API is called once and
  all 100 share the result.
- **Fallback store:** a second, longer-lived copy keeps the last good data per source for outages.
- **Ready to scale out:** today the cache is in-memory. Registering an `IDistributedCache` (e.g. Redis) adds a shared
  second level across instances with no code changes.
- **Failures are never cached,** so the next request retries the API.

---

## Request statistics

`RequestStatisticsHandler` is a `DelegatingHandler` placed *inside* the resilience pipeline, so each attempt is
measured, from the request being sent until the response headers arrive. Results go to
`InMemoryRequestStatisticsStore`, a singleton.

Provider-specific handlers such as Twitch OAuth sit between the resilience pipeline and the statistics handler. The
time spent fetching an access token is therefore not counted as IGDB response time, and token requests are not
counted as IGDB requests.

**Thread safety.** Requests from many threads update the store at once:

- **Finding an API's counters:** a `ConcurrentDictionary` maps each API to its own counter object, with lock-free
  look-ups.
- **Updating:** each counter object has its own small `Lock`, so one update (request count, total time, bucket,
  failures) is applied atomically.
- **Reading:** a snapshot copies the counters under that same lock, so it never sees a half-applied update. Bucket
  counts always add up to the total.
- **Contention:** the locked section is a few additions, and different APIs never wait on each other.

The unit tests hammer the store from 16 threads while reading snapshots, and check that no update is lost.

**Recent view.** Besides the totals since start-up, each API keeps one small count per clock minute for the last
60 minutes, in a ring of 60 slots that are reused as time moves on. These slots are updated under the same lock as
the totals. Memory stays fixed whatever the traffic, and the recent figures always match the totals.

---

## Performance monitoring

`PerformanceMonitor` is a `BackgroundService` that checks the statistics every `CheckInterval` (1 minute). For each
external API it compares two averages:

- the average response time in the last `Window` (5 minutes), counted in whole clock minutes: the current minute and
  the 4 before it;
- the average of all requests before that window, since start-up.

When the recent average is more than `SlowdownThresholdPercent` (50%) above the earlier one, it logs a warning:

```text
warn: GameDataAggregator.Core.Statistics.PerformanceMonitor
      Performance anomaly: RAWG averaged 412.5 ms over the last 5 min (8 requests), 175% above its earlier average of 150 ms (40 requests)
```

- **No false alarms from a handful of requests.** An API needs at least `MinimumRequests` (5) both inside the window
  and before it. Otherwise it is not judged.
- **Before the window, not overall.** The earlier average leaves out the window itself, so a long slowdown does not
  raise the figure it is compared with and hide itself.
- **One warning per slowdown.** The warning is logged once when an API turns slow, and an information message once
  when it is back to normal. A quiet period with too few requests changes nothing.
- **Testable.** The rule is a pure function (`SlowdownDetector`), and the service and the store read time from
  `TimeProvider`, so the tests move a fake clock instead of waiting.

All settings are under `PerformanceMonitor` in [Configuration](#configuration), and are checked at start-up.

---

## Adding a new API

The aggregation, caching, fallback, parallelism, statistics, filtering, UI and API surface are all generic. A new
source needs only its own folder in `Infrastructure/Providers/`. IGDB and speedrun.com were added exactly this way:
`Speedrun/` is the smallest complete example (no key), and `Igdb/` shows one with authentication. For another source
such as Twitch live streams, you would add:

1. **Options:** `TwitchOptions : ExternalApiOptions`, with the base URL.
2. **DTOs:** records for the JSON you consume.
3. **Provider:** `TwitchProvider : IGameDataProvider`. It calls the API through the injected `HttpClient`, maps the
   response to `AggregatedItem`, and throws on failure. If the data doesn't fit an existing `ItemCategory`, add one
   (e.g. `StreamActivity`). If the API needs a key, override `ConfigurationError` to return a message naming the
   missing setting (see `RawgProvider`); the source is then reported as `NotConfigured` instead of being called.
4. **Registration:**
   ```csharp
   public static IServiceCollection AddTwitchProvider(this IServiceCollection services, IConfiguration configuration)
   {
       services.AddGameDataProvider<TwitchProvider, TwitchOptions>(
           configuration,
           TwitchProvider.SourceName,
           TwitchOptions.SectionName,
           (client, options) => client.BaseAddress = options.BaseUrl,
           handlers => handlers.AddTwitchAuthentication<TwitchOptions>(configuration)); // same Twitch app and token as IGDB
       return services;
   }
   ```
5. One line in `InfrastructureServiceCollectionExtensions`: `.AddTwitchProvider(configuration)`.
6. A config section `ExternalApis:Twitch`.

The new source then shows up automatically in `/aggregation` (including the `sources` filter and validation), in
`/sources`, `/statistics`, the browser UI, and the resilience and caching behaviour.

`configureHandlers` is how any provider adds per-request behaviour, such as an API key header or OAuth. Handlers added
there run on every attempt, inside the retry policy and outside the statistics measurement.

---

## Testing

```bash
dotnet test
```

| Project | Covers |
|---|---|
| Unit tests | Relevance scoring; filtering, sorting and paging; statistics bucketing, averages and concurrency; the recent window (edges, slot reuse, invalid windows); the slowdown rule and the background monitor (one warning, recovery, quiet periods, timer, disabled); cache hit, stampede protection, fallback, failure, missing configuration and cancellation; start-up configuration report; failure messages; each provider's request building and mapping (incl. missing fields, partial failures, IGDB query escaping and speedrun.com's embedded relations); Twitch OAuth (token reuse, expiry, one shared refresh under concurrency, rejected credentials, 401 retry); statistics handler; options validation |
| Integration tests | The full application via `WebApplicationFactory` with only the network faked: all five sources aggregated, IGDB authenticated end to end with one shared token, missing credentials degrading only their source, caching across requests, filters and sorting over HTTP, an unavailable API degrading gracefully (including a real retry), validation problem details, statistics counting (retries counted, cache hits and token requests not), sources endpoint, browser UI served, health check; authentication (which endpoints need a token, tokens with a wrong key, issuer, audience or expiry refused, the development token endpoint and its absence outside Development, start-up refusing a missing or short key, the OpenAPI security markers) |

No test touches the real network, so the suite is fast and deterministic.

**Debugging in Visual Studio.** When an external API really fails (it is down, times out, or rejects a key), the
provider throws inside the cache's factory and `ProviderExecutor` catches it, so the request still succeeds. With
*Just My Code* enabled, Visual Studio may still pause there with "Exception User-Unhandled", because the exception
passes through library code (HybridCache) before being caught. Press **Continue** (F5), or untick *Break when this
exception type is user-unhandled* for that exception type. A missing API key no longer causes this, because it is
reported as `NotConfigured` without throwing.

---

## Design decisions

- **Unified item model.** Common fields (title, date, category, url) make cross-source filtering and sorting
  possible. Source-specific data goes into `metrics` (numbers) and `attributes` (text). This keeps the model stable
  as APIs are added, without falling back to untyped JSON.
- **Relevance is computed by this service,** not taken from each API. The sources rank results
  differently (or not at all). One rule for everything keeps scores comparable:
  - exact match > prefix > contains > word overlap.
  - for a prefix or contains match, a shorter title, which the search term covers more fully, ranks higher.
  - for word overlap, the more search words a title contains, the higher it ranks.
- **Search fetches, filters refine.** Only `search` reaches the external APIs. Everything else is applied to the
  combined result. This maximises cache reuse and keeps filters consistent across sources.
- **Clean layering.** Core has no dependency on HTTP or on any specific API. Infrastructure plugs the APIs in. The
  API project is a thin HTTP layer.
- **Lifetimes.** Providers are typed `HttpClient`s (transient), so `AggregationService` is scoped. The statistics
  store, cache and executor are singletons.
- **Statistics measure real API calls.** Measuring at the `HttpClient` level, per attempt, reflects the external
  API's actual performance. Measuring at the endpoint level would mix in cache hits and the service's own overhead.
- **Twitch OAuth as a reusable piece.** The token provider is a singleton that caches the app access token in memory
  until shortly before it expires. Concurrent requests share a single refresh, and a `401` triggers one token renewal
  and retry. Credentials are sent in the request body, never in a URL. Any future Twitch-owned API reuses the same
  token with one line.
- **Missing configuration is a state, not an error.** A provider without its API key says so through
  `ConfigurationError`, checked before any call. Throwing an exception for it on every search would log the same
  warning each time, run a pointless fallback look-up, and make the debugger stop on every request.
- **JWT on the endpoints that cost or reveal something.** Searches spend external quotas and statistics describe the
  service, so both need a token. The sources list, health check and page stay open. The development token endpoint
  keeps the UI, Scalar and Postman working with no setup. See DEC-029.
- **Slowdowns are judged against the API's own past.** Each API has its own normal speed, so a fixed limit in
  milliseconds would either flag slow-but-normal APIs or miss fast ones getting slower. See DEC-028.
- **A UI without a front-end toolchain.** The browser UI is static files served by the API, using only the public
  endpoints. It stays in sync with the API, needs no Node build, and shows that the API is all a client needs.

---

## Engineering log

[`docs/`](docs) records how the project got here, for study and review:

- [Issues log](docs/issues-log.md): every bug and problem found, its root cause, the fix, and the lesson.
- [Decisions](docs/decisions.md): the important choices, the alternatives considered, and why.
- [Patterns](docs/patterns.md): the design patterns and practices the code follows, and where to find them.
