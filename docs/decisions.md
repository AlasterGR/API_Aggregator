# Decisions

The important choices made in this project, written as short decision records. Each says what was decided, what else
was considered, and why. Decisions are never edited away: if one changes, a new entry *supersedes* the old one, so the
reasoning history stays readable.

**Entry format:** Context → Decision → Alternatives considered → Consequences.

| ID | Date | Decision |
|---|---|---|
| [DEC-001](#dec-001-aspnet-core-on-net-10-with-controllers) | 2026-11-07 | ASP.NET Core on .NET 10, with controllers |
| [DEC-002](#dec-002-data-sources-rawg-cheapshark-steam) | 2026-11-07 | Data sources: RAWG, CheapShark, Steam |
| [DEC-003](#dec-003-three-layers-core-infrastructure-api) | 2026-11-07 | Three layers: Core, Infrastructure, Api |
| [DEC-004](#dec-004-one-interface-per-external-api-plus-generic-registration) | 2026-11-07 | One interface per external API, plus generic registration |
| [DEC-005](#dec-005-parallel-calls-with-taskwhenall) | 2026-11-07 | Parallel calls with `Task.WhenAll` |
| [DEC-006](#dec-006-standard-http-resilience-pipeline-per-api) | 2026-11-07 | Standard HTTP resilience pipeline per API |
| [DEC-007](#dec-007-hybridcache-with-a-fresh-copy-and-a-fallback-copy) | 2026-11-07 | `HybridCache` with a fresh copy and a fallback copy |
| [DEC-008](#dec-008-search-fetches-filters-refine) | 2026-11-07 | Search fetches, filters refine |
| [DEC-009](#dec-009-one-unified-item-model) | 2026-11-07 | One unified item model |
| [DEC-010](#dec-010-relevance-is-computed-by-this-service) | 2026-11-07 | Relevance is computed by this service |
| [DEC-011](#dec-011-statistics-measured-per-http-attempt) | 2026-11-07 | Statistics measured per HTTP attempt |
| [DEC-012](#dec-012-errors-rfc-9457-problem-details-and-safe-messages) | 2026-11-07 | Errors: RFC 9457 problem details and safe messages |
| [DEC-013](#dec-013-secrets-never-in-source-control) | 2026-11-07 | Secrets never in source control |
| [DEC-014](#dec-014-versioned-routes-under-apiv1) | 2026-11-07 | Versioned routes under `/api/v1` |
| [DEC-015](#dec-015-tests-never-touch-the-real-network) | 2026-11-07 | Tests never touch the real network |
| [DEC-016](#dec-016-add-igdb-as-a-fourth-source-and-keep-rawg) | 2026-11-07 | Add IGDB as a fourth source and keep RAWG |
| [DEC-017](#dec-017-twitch-oauth-as-a-reusable-module) | 2026-11-07 | Twitch OAuth as a reusable module |
| [DEC-018](#dec-018-a-browser-ui-without-a-front-end-toolchain) | 2026-11-07 | A browser UI without a front-end toolchain |
| [DEC-019](#dec-019-postman-collection-and-openapi-for-api-clients) | 2026-11-07 | Postman collection and OpenAPI for API clients |
| [DEC-020](#dec-020-missing-configuration-is-a-state-not-an-exception) | 2026-11-07 | Missing configuration is a state, not an exception |
| [DEC-021](#dec-021-keep-an-engineering-log) | 2026-11-07 | Keep an engineering log |
| [DEC-022](#dec-022-personal-tool-folders-are-ignored-locally-not-in-gitignore) | 2026-11-07 | Personal tool folders are ignored locally, not in `.gitignore` |
| [DEC-023](#dec-023-add-speedruncom-as-a-third-source-that-needs-no-key) | 2026-11-07 | Add speedrun.com as a third source that needs no key |
| [DEC-024](#dec-024-items-state-what-their-date-means) | 2026-11-07 | Items state what their date means |
| [DEC-025](#dec-025-rate-limit-searches-per-client) | 2026-11-07 | Rate-limit searches per client |
| [DEC-026](#dec-026-validate-each-field-and-bind-query-values-strictly) | 2026-11-07 | Validate each field, and bind query values strictly |
| [DEC-027](#dec-027-api-keys-and-new-sources-are-not-entered-through-the-ui) | 2026-11-08 | API keys and new sources are not entered through the UI |
| [DEC-028](#dec-028-detect-slowdowns-by-comparing-a-recent-window-with-the-time-before-it) | 2026-11-08 | Detect slowdowns by comparing a recent window with the time before it |
| [DEC-029](#dec-029-protect-search-and-statistics-with-jwt-bearer-tokens) | 2026-11-08 | Protect search and statistics with JWT bearer tokens |
| [DEC-030](#dec-030-a-word-document-for-the-service-and-a-markdown-guide-for-new-sources) | 2026-11-08 | A Word document for the service, and a Markdown guide for new sources |
| [DEC-031](#dec-031-side-requests-use-their-own-http-client) | 2026-11-08 | Side requests use their own HTTP client |
| [DEC-032](#dec-032-the-twitch-token-request-gets-one-try-that-fits-inside-the-sources-attempt) | 2026-11-08 | The Twitch token request gets one try that fits inside the source's attempt |

---

## DEC-001: ASP.NET Core on .NET 10, with controllers

- **Context:** the assignment asks for an ASP.NET Core API.
- **Decision:** .NET 10, the current long-term-support release, with MVC controllers.
- **Alternatives:** minimal APIs. They are lighter, but controllers give automatic model validation (`[ApiController]`
  returns 400 problem details by itself), attribute-based response documentation, and a structure most .NET reviewers
  expect.
- **Consequences:** `[ApiController]` handles validation; a controller's action method stays a few lines long.

## DEC-002: Data sources: RAWG, CheapShark, Steam

- **Context:** at least three external APIs. They should be free, need no approval process, and together answer one
  question.
- **Decision:** one video-game theme, with each source contributing a different *kind* of data:
  - RAWG: catalog and design data (`GameInfo`);
  - CheapShark: prices and deals (`Deal`);
  - Steam: live player counts (`PlayerActivity`).
- **Alternatives:** the assignment's suggestions (weather, news, X/Twitter). X no longer offers a useful free tier,
  and unrelated sources would make "aggregation" an artificial exercise.
- **Consequences:** one search returns a meaningful combined picture of a game. Only RAWG needs a key.

## DEC-003: Three layers: Core, Infrastructure, Api

- **Context:** the logic (aggregation, caching, statistics) should be testable without HTTP or real APIs.
- **Decision:**
  - **Core** holds the models, the provider contract, aggregation, caching and statistics, and knows nothing about
    specific APIs or ASP.NET;
  - **Infrastructure** holds the API clients;
  - **Api** is the thin HTTP layer.
- **Alternatives:** a single project (simpler, but the boundaries would erode), or full Clean Architecture with
  use-case classes (too much ceremony for this size).
- **Consequences:** Core can be unit-tested in isolation. Dependencies point inwards only.

## DEC-004: One interface per external API, plus generic registration

- **Context:** adding another API had to be trivial.
- **Decision:**
  - every API implements `IGameDataProvider` (a name, plus `FetchAsync` returning unified items);
  - `AddGameDataProvider<TProvider, TOptions>` wires options validation, the typed `HttpClient`, resilience and
    statistics in one call.
- **Alternatives:** a hard-coded list of clients inside the aggregation service, which every new API would edit.
- **Consequences:**
  - a new API is one folder, one registration line and one config section;
  - aggregation, caching, statistics, the `sources` filter and the UI pick it up automatically;
  - IGDB proved this (DEC-016).

## DEC-005: Parallel calls with `Task.WhenAll`

- **Context:** total latency should be that of the slowest API, not the sum of all of them.
- **Decision:** start every provider call, then `await Task.WhenAll(...)`.
- **Alternatives:** `Parallel.ForEach` or `Task.Run`. Both are for CPU-bound work: they would block thread-pool
  threads while waiting on the network, for no gain.
- **Consequences:** I/O-bound concurrency with no extra threads. Each call handles its own failure, so one failure
  cannot cancel the others.

## DEC-006: Standard HTTP resilience pipeline per API

- **Context:** the assignment asks for error handling and resilience. Free APIs are unreliable (ISS-005).
- **Decision:** use `Microsoft.Extensions.Http.Resilience` (`AddStandardResilienceHandler`), configured per API:
  - total timeout;
  - retry with exponential back-off and jitter;
  - circuit breaker;
  - per-attempt timeout.
- **Alternatives:** hand-written retry loops (error-prone), or raw Polly pipelines (more code for the same result).
- **Consequences:** well-tested defaults, each tunable in configuration.

## DEC-007: `HybridCache` with a fresh copy and a fallback copy

- **Context:** the assignment asks for caching and for fallback.
- **Decision:** `HybridCache` stores two entries per source and normalised search:
  - a short-lived *fresh* entry, used to skip the API call;
  - a long-lived *fallback* entry, served with status `Fallback` when the API fails.

  Failures are never cached.
- **Alternatives:**
  - `IMemoryCache`, which has no stampede protection;
  - a single entry with stale-while-revalidate, which would blur "fresh" and "last known good".
- **Consequences:**
  - stampede protection is built in;
  - a Redis second level can be added by registering `IDistributedCache`, with no code change;
  - genuine provider exceptions pass through `HybridCache` before being caught (see ISS-011).

## DEC-008: Search fetches, filters refine

- **Context:** each API supports different filters, or none at all.
- **Decision:** only `search` is sent to the external APIs. Category, tag, date range, sorting and paging are applied
  to the combined result.
- **Alternatives:** translating each filter into each API's own parameters. That would be inconsistent between
  sources and break cache reuse.
- **Consequences:** one cache entry serves every filter combination, and filters behave the same for every source.

## DEC-009: One unified item model

- **Context:** a game listing, a deal and a player count have different fields.
- **Decision:** `AggregatedItem` has:
  - common fields: source, category, title, url, image, date, relevance, tags;
  - `metrics` for source-specific numbers;
  - `attributes` for source-specific text.
- **Alternatives:** one type per source (then clients must handle every type), or raw JSON pass-through (untyped and
  unstable).
- **Consequences:** cross-source filtering and sorting work, and adding a source does not change the response schema.

## DEC-010: Relevance is computed by this service

- **Context:** the APIs rank results differently, or not at all.
- **Decision:** one scoring rule for every item: exact match > prefix > contains > word overlap. Within each level,
  closer title matches score higher.
- **Consequences:** scores are comparable across sources, and "sort by relevance" means the same everywhere.

## DEC-011: Statistics measured per HTTP attempt

- **Context:** the assignment asks for request counts and average response time per API, split into
  fast / average / slow, stored thread-safely.
- **Decision:**
  - a `DelegatingHandler` inside the resilience pipeline measures each attempt;
  - the store is a singleton: a `ConcurrentDictionary` of per-API counters, each guarded by its own `Lock`;
  - the bucket thresholds come from configuration.
- **Alternatives:**
  - measuring at the endpoint, which would mix in cache hits and our own overhead;
  - `Interlocked` counters only, which cannot update several counters as one consistent unit.
- **Consequences:**
  - the numbers reflect the external APIs' real behaviour, retries included;
  - a snapshot never shows a half-applied update.

## DEC-012: Errors: RFC 9457 problem details and safe messages

- **Context:** callers need consistent errors, and internal details must not leak.
- **Decision:**
  - every error response is `application/problem+json`;
  - a provider failure becomes a per-source `error` string. Only `DataProviderException` messages are shown as
    written; everything else gets a generic message, and the full exception is logged.
- **Consequences:** one error format for clients, and no stack traces or upstream secrets in responses.

## DEC-013: Secrets never in source control

- **Decision:** API keys come from `dotnet user-secrets` in development and from environment variables elsewhere.
  `appsettings.json` contains only empty placeholders.
- **Consequences:** the repository is safe to make public. A missing key degrades only its own source (DEC-020).

## DEC-014: Versioned routes under `/api/v1`

- **Decision:** every endpoint lives under `/api/v1/...`.
- **Alternatives:** unversioned routes, or a versioning library (overkill for a single version).
- **Consequences:** a breaking change can be published as `/api/v2` without disturbing existing clients.

## DEC-015: Tests never touch the real network

- **Decision:**
  - unit tests use stub `HttpMessageHandler`s and fakes;
  - integration tests run the real application in memory (`WebApplicationFactory`) and replace only the outermost
    HTTP handler with a fake that impersonates the external APIs;
  - time is abstracted with `TimeProvider`, so expiry can be tested without waiting.
- **Consequences:** the suite is fast and deterministic, and still exercises the real resilience pipeline, caching,
  statistics and authentication.

## DEC-016: Add IGDB as a fourth source and keep RAWG

- **Context:** RAWG's availability was in doubt (ISS-005), and IGDB offers richer production data (developers,
  publishers, themes).
- **Decision:** add IGDB alongside RAWG. Both contribute `GameInfo`, and the client chooses with `sources=`.
- **Alternatives:** replace RAWG with IGDB.
- **Consequences:** the assignment works without either key (CheapShark and Steam need none), and the plug-in design
  of DEC-004 is proven on a real second-round addition.

## DEC-017: Twitch OAuth as a reusable module

- **Context:** IGDB needs a Twitch app access token, and a future Twitch streams source would need the same token.
- **Decision:**
  - a singleton `TwitchAccessTokenProvider`:
    - caches the token until shortly before it expires;
    - shares one refresh between concurrent callers (`SemaphoreSlim`);
    - sends credentials in the request body, never the URL;
  - a `TwitchAuthenticationHandler` attaches the headers and, on a 401, renews the token and retries once;
  - providers opt in with `handlers => handlers.AddTwitchAuthentication(configuration)`.
- **Consequences:**
  - token requests are not counted in IGDB's statistics, because the handler sits outside the measurement;
  - a second Twitch-based source needs one line.

## DEC-018: A browser UI without a front-end toolchain

- **Context:** a person should be able to try the service without Postman or curl.
- **Decision:** static HTML, CSS and plain JavaScript in `wwwroot`, served by the API at `/`, using only the public
  endpoints. Data is always inserted as text, and links must be `http(s)`.
- **Alternatives:** React, Angular or Blazor. Each adds a build step, a dependency tree and a second project to
  maintain, for a two-tab page.
- **Consequences:** nothing to install. The UI cannot drift from the API, because it *is* an API client.

## DEC-019: Postman collection and OpenAPI for API clients

- **Decision:** ship `postman/GameDataAggregator.postman_collection.json`, with a test script on every request and a
  `baseUrl` variable, and expose the OpenAPI document at `/openapi/v1.json`.
- **Consequences:** reviewers can run the whole API from Postman's Collection Runner, or generate requests from
  OpenAPI.

## DEC-020: Missing configuration is a state, not an exception

- **Context:** ISS-011. Throwing on every request for a missing key polluted the logs, wasted work, and stopped the
  debugger.
- **Decision:**
  - providers expose `ConfigurationError`, checked before any call;
  - affected sources are reported as `NotConfigured`;
  - the gap is logged once at start-up.
- **Alternatives:**
  - leave unconfigured providers out of the DI container. Callers would no longer see why a source is missing, and the
    `sources` filter would reject its name;
  - fail start-up. That is too strict, since the other sources work fine.
- **Consequences:** a new status value appears in the API contract, documented in the README and accepted by the
  Postman tests.

## DEC-021: Keep an engineering log

- **Context:** the project is also a learning exercise, and the reasoning matters as much as the result.
- **Decision:** record every issue ([issues-log.md](issues-log.md)), every important decision (this file), and the
  patterns followed ([patterns.md](patterns.md)), as the work happens.
- **Consequences:** reviewers can follow *why* the code looks the way it does, and the same mistakes are not made
  twice.

## DEC-022: Personal tool folders are ignored locally, not in `.gitignore`

- **Context:** a local development tool keeps a settings folder in the project root. It must never be committed, and
  the shared repository should not mention it either.
- **Decision:** ignore it through `.git/info/exclude`. Git reads that file only on this machine, for this repository
  only. It is never committed, pushed or cloned.
- **Alternatives:**
  - the committed `.gitignore`: it is shared with everyone who clones the repository, so it should only list files
    that the *project* produces (build output, test results), not one developer's tools;
  - the global ignore file (`~/.config/git/ignore`): it would apply to every repository on this machine, including
    ones that deliberately commit such a folder;
  - moving the folder under another, ignored parent folder: the tool only looks for its folder at the project root,
    so it would stop working, and a misnamed folder would mislead reviewers.
- **Consequences:**
  - the repository contains only project-related ignore rules;
  - the rule must be added again in any other clone where the tool is used. To check which file ignores a path, run
    `git check-ignore -v <path>`.

## DEC-023: Add speedrun.com as a third source that needs no key

- **Context:** of the four sources, only CheapShark and Steam work without setup. The assignment needs three APIs,
  and a reviewer who clones the repository should see three working sources at once, without registering for
  anything.
- **Decision:** add [speedrun.com](https://github.com/speedruncomorg/api) as `PlayerActivity`. It reports how many
  leaderboard categories a game has, how many moderators, and which platforms it is run on. One request returns
  everything: related data (platforms, genres, developers, categories) is embedded with `embed=...`.
- **Alternatives** (all probed live on 2026-11-07):
  - **FreeToGame** (free-to-play catalogue): no key, but no search. Every search would download the whole catalogue
    and filter it here, and only free-to-play games are listed;
  - **GamerPower** (free game giveaways): no key, but also no search, and the results are giveaways rather than data
    about a given game;
  - **PCGamingWiki:** its query API now refuses anonymous queries (`permissiondenied`);
  - a second Steam endpoint: it would add no new source.
- **Consequences:**
  - CheapShark, Steam and speedrun.com work with no setup, and RAWG and IGDB are a bonus once keys are set;
  - `PlayerActivity` now has two sources, live player counts and community activity;
  - speedrun.com's search is fuzzy (`witcher 3` also finds "Mini Switcher 3.0"), so our own relevance score
    (DEC-010) ranks its results;
  - the speedrun.com API allows about 100 requests a minute per client. The cache (DEC-007) keeps us well below that.

## DEC-024: Items state what their date means

- **Context:** ISS-016. `date` meant different things for different sources, and clients had no reliable way to tell
  which.
- **Decision:** a `dateKind` field on every item: `Released`, `PriceUpdated` or `Measured`, set by the provider. The
  model returns `null` for `dateKind` whenever `date` is `null`, so providers can set it without checking, and an
  item can never describe a date it does not have.
- **Alternatives:**
  - a label chosen by source name in the UI: every client would need its own copy of the rule, and every new source
    would mean editing it;
  - a separate field per meaning (`releaseDate`, `measuredAt`, ...): sorting and the `from`/`to` filters need one
    common date, so this would duplicate data;
  - leaving the date unlabelled: correct, but less useful to read.
- **Consequences:** a backward-compatible addition to the response, since existing clients ignore the new field.
  Sorting and date filtering are unchanged: they still use `date` for every item.

## DEC-025: Rate-limit searches per client

- **Context:** ISS-023.
- **Decision:**
  - ASP.NET Core's built-in rate limiter, with one **fixed window** per client IP: 60 searches a minute, set in
    `RateLimiting`;
  - no queue, so excess requests are rejected at once;
  - `429` with `Retry-After` and problem details;
  - applied only to `GET /api/v1/aggregation`, through `[EnableRateLimiting]`.
- **Alternatives:**
  - **sliding window or token bucket:** smoother at window edges (a fixed window allows up to 2 x 60 across a
    boundary), but harder to explain and not needed at this scale;
  - **one global limit:** simpler, but one client could lock out everyone;
  - **an API key per client:** the right tool for a public product, but out of scope for an assignment service with
    no user accounts.
- **Consequences:**
  - behind a reverse proxy, forwarded headers must be configured, or every client looks like the proxy's address;
  - counters are in memory per instance, so several instances would each allow 60. A shared store (e.g. Redis) would
    be needed for a global limit.

## DEC-026: Validate each field, and bind query values strictly

- **Context:** ISS-018 to ISS-021.
- **Decision:**
  - every query field validates itself with an attribute, so all field errors come back in one 400;
  - `IValidatableObject.Validate` holds only checks that compare fields;
  - two global model binders make the query-string contract strict: enums by name only, and dates without an offset
    read as UTC.
- **Alternatives:**
  - **FluentValidation:** expressive, but an extra dependency for six simple rules;
  - **checks in the controller:** they run after model validation, which caused ISS-020.
- **Consequences:**
  - `[KnownSources]` resolves `IAggregationService` from the validation context. That is a small use of service
    location, accepted because validation attributes cannot take constructor dependencies;
  - the binders apply to every controller, so a future enum or date parameter follows the same rules automatically.

## DEC-027: API keys and new sources are not entered through the UI

- **Context:** the user asked whether the UI should let people type a missing API key (RAWG, IGDB), and whether a new
  source could be added from the UI.
- **Decision:** no. Keys stay in user-secrets or environment variables (DEC-013), and a new source is added in code
  with `AddGameDataProvider`. The "Not configured" card says which setting is missing, and a written guide explains
  how to add a source.
- **Alternatives:**
  - **a key form in the UI:** the page has no login, so anyone who can open it could replace or read the server's
    keys. Keys would also need safe storage on the server;
  - **a "generic source" form (URL plus field mapping):** every API differs in address, login and JSON shape, so the
    form would grow into a small programming language. Letting users make the server call any URL also opens the
    door to SSRF (server-side request forgery: making the server call internal addresses for an attacker).
- **Consequences:** configuration needs a restart, which is normal for secrets. Adding a source needs a developer,
  which matches the assignment's goal of an architecture that makes integration easy in code.

## DEC-028: Detect slowdowns by comparing a recent window with the time before it

- **Context:** the assignment's optional background service that logs performance anomalies. The statistics store
  kept only totals since start-up, so it could not show a recent change (ISS-028).
- **Decision:**
  - the store keeps, per API, one count and one total time per clock minute, in a ring of 60 slots that are reused as
    time moves on. The slots are updated under the same lock as the totals;
  - `PerformanceMonitor`, a `BackgroundService`, runs every `CheckInterval` (1 minute) on a `PeriodicTimer` driven by
    `TimeProvider`;
  - the rule, in `SlowdownDetector`: an API is slow when its average in the last `Window` (5 minutes) is more than
    `SlowdownThresholdPercent` (50%) above its average **before** that window. Both periods need at least
    `MinimumRequests` (5) requests, or the API is not judged;
  - a warning is logged once when an API turns slow, and an information message once when it is back to normal;
  - all settings sit in `PerformanceMonitor` and are validated at start-up. The window must be whole minutes, 1 to 60.
- **Alternatives:**
  - **compare with the average since start-up, window included:** simpler, but the slow requests raise the figure
    they are compared with. A long or busy slowdown partly hides itself;
  - **a fixed limit in milliseconds** (for example the `Slow` bucket's 200 ms): one limit cannot fit every API. Steam
    is normally fast and IGDB normally slower, so it would flag normal behaviour or miss real change;
  - **an exponential moving average:** no storage at all, but the window is implicit and harder to explain, and exact
    test cases are harder to write;
  - **a queue of every request's time:** an exact window, but memory grows with traffic;
  - **percentiles (p95) instead of averages:** a better signal for tail latency, but needs histograms. The assignment
    asks for averages;
  - **logging on every check:** a ten-minute slowdown would log ten identical warnings.
- **Consequences:**
  - the window is counted in whole clock minutes, so a 5-minute window covers between 4 and 5 minutes of data;
  - after a lasting change, the earlier average slowly takes in the new speed, and the API is eventually reported as
    recovered. That is the intended meaning of "slower than usual";
  - memory is fixed: 60 small slots per API;
  - like the statistics, it is in memory and per instance. It only logs. Alerting or metrics export would be a next
    step.

## DEC-029: Protect search and statistics with JWT bearer tokens

- **Context:** the assignment's optional JWT authentication, and ISS-029. The scope below was agreed with the user on
  2026-11-08.
- **Decision:**
  - ASP.NET Core's JWT bearer handler. Tokens are signed with HMAC-SHA256 using one symmetric key from
    `Jwt:SigningKey` (user-secrets while developing, an environment variable or secret store elsewhere);
  - the issuer, audience, lifetime (30 seconds of clock skew) and signature are all checked, and only HS256 is
    accepted, so a token cannot pick a weaker algorithm;
  - `[Authorize]` on `AggregationController` and `StatisticsController`. The sources list, health check, browser UI
    and API reference stay open;
  - `POST /api/v1/auth/token` gives out a one-hour token, only in Development. It answers `404` elsewhere. The UI and
    the Postman collection call it by themselves, so they keep working with no setup;
  - in Development without a configured key, a random key is made at start-up and a warning is logged. Outside
    Development a missing key stops start-up, and a key under 32 bytes stops it anywhere;
  - authentication runs before the rate limiter, so refused requests do not use up a client's searches;
  - OpenAPI transformers add the bearer scheme and mark each protected operation, so Scalar shows a token field.
- **Alternatives:**
  - **an identity provider with asymmetric keys** (`Authority` and its published keys): the right design in
    production, because this API could then check tokens but never make them. It needs a running identity provider,
    which is too much for a self-contained assignment. Moving to it changes only the token-check settings;
  - **`dotnet user-jwts` instead of a token endpoint:** no extra endpoint, but the UI and Postman would need a token
    pasted by hand;
  - **API keys:** simpler, but they never expire and carry no claims, and the assignment asks for JWT;
  - **protect everything by default (a fallback policy):** the page, health check and API reference would need
    exceptions, and the health check must stay open for load balancers;
  - **protect `/api/v1/sources` too:** it lists only public source names, so a token adds friction for no gain.
- **Consequences:**
  - outside Development the browser UI cannot search, because it has no way to get a token. A sign-in flow or a
    token field would be needed there;
  - with a shared key, anything that can check tokens can also make them, so the key must stay with this service;
  - a token cannot be withdrawn before it expires, which is why tokens are short-lived.

## DEC-030: A Word document for the service, and a Markdown guide for new sources

- **Context:** the assignment asks to document the service, its endpoints, input and output formats, and setup and
  configuration, and judges the quality and clarity of that documentation. The README is written for developers who
  already know .NET, and readers of the documentation may not be native English speakers.
- **Decision:**
  - `docs/GameDataAggregator-Documentation.docx` is the formal documentation. It covers what the service does, the
    architecture, every endpoint with every parameter and an example, the error answers, setup, the full settings
    reference, tests and the Postman collection.
  - `docs/adding-a-source.md` is a step-by-step guide for adding an external API, written for beginners, with
    complete code for an imaginary source and its tests.
  - Both use short, plain sentences, define each technical term on first use, and were checked against the code,
    not against the README.
- **Alternatives:**
  - **only the README:** one file is easier to keep up to date, but it is dense and assumes .NET knowledge.
  - **a PDF:** easy to read, but harder for the reader to comment on or reuse.
- **Consequences:** three documents now describe the same service, so a change to an endpoint or a setting must be
  made in the README, the Word document and, for provider changes, the guide. ISS-030 and ISS-031 list the places
  where the README and the code already differ.

## DEC-031: Side requests use their own HTTP client

- **Context:** a side request is an extra call that only decorates a source's main data: CheapShark's store list
  and Steam's player counts. They shared the main client's retries and circuit breaker, so a broken side endpoint
  could delay the main request by up to 15 seconds and open its circuit (ISS-033).
- **Decision:**
  - `ProviderRegistration.AddSideRequestClient` adds a separate named `HttpClient` for a source's side requests.
  - each side request gets one try with a short time limit (`SideRequestTimeout`, 2 seconds by default).
  - the side client has its own circuit breaker, with the same rule as the main one (half of at least 5 requests
    fail, then a 30-second pause).
  - side requests are still recorded in the source's statistics, because they are real calls to that API.
- **Alternatives:**
  - **cache a failed store list for a short time:** fixes the repeated calls, but not the delay or the shared
    circuit, and does not help Steam.
  - **stop waiting for the store list after a short time:** fixes the delay only.
- **Consequences:**
  - a broken side endpoint costs each search at most `SideRequestTimeout`, and nothing at all once its own circuit
    is open.
  - side requests are no longer retried, so a single network error shows store ids or no player count for that
    search.
  - providers with side requests take an `IHttpClientFactory` as well as their typed `HttpClient`.

## DEC-032: The Twitch token request gets one try that fits inside the source's attempt

- **Context:** the Twitch token is fetched inside one attempt of the source that needs it (DEC-017). The Twitch
  client had its own retries, with 15 seconds in total, but the IGDB attempt cut it off after 5 seconds (ISS-035).
- **Decision:**
  - the Twitch token client makes one try, limited by `TwitchAuth:Timeout`, with no retries or circuit breaker of
    its own. The source's pipeline already retries the whole attempt, token request included, and its circuit
    breaker already counts the failures.
  - the time limit is a Polly timeout, not `HttpClient.Timeout`. A Polly timeout throws
    `TimeoutRejectedException`, which the source's retry treats as a temporary failure. `HttpClient.Timeout` throws
    `TaskCanceledException`, which it does not retry.
  - a start-up check, `TwitchTimeoutFitsAttemptCheck<TOptions>`, makes sure `TwitchAuth:Timeout` is shorter than
    the source's `AttemptTimeout`. The default is now 2 seconds.
- **Alternatives:**
  - **fetch the token in `IgdbProvider` before the request:** gives the token its own time, but every Twitch-based
    source would have to repeat that code, and the 401 renewal would still run inside the attempt.
  - **only document the rule:** nothing would stop a wrong setting.
- **Consequences:** `AddTwitchAuthentication` now needs the source's options type, for example
  `handlers => handlers.AddTwitchAuthentication<IgdbOptions>(configuration)`.
