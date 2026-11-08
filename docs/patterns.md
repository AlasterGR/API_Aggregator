# Patterns

The design patterns and practices this code follows. For each one: what it is, where it is used here, and why. Read
it next to the code; every entry links to an example.

Sections:

- [Architecture and extensibility](#architecture-and-extensibility)
- [HTTP and resilience](#http-and-resilience)
- [Concurrency](#concurrency)
- [Caching and degradation](#caching-and-degradation)
- [Configuration and start-up](#configuration-and-start-up)
- [Errors and logging](#errors-and-logging)
- [Testing](#testing)
- [Front end](#front-end)
- [Build hygiene](#build-hygiene)

---

## Architecture and extensibility

### Strategy / plug-in: one interface per external API

- **What:** a family of interchangeable implementations behind one interface. The caller depends only on the
  interface.
- **Where:** [`IGameDataProvider`](../src/GameDataAggregator.Core/Abstractions/IGameDataProvider.cs), implemented by
  `RawgProvider`, `CheapSharkProvider`, `SteamProvider`, `IgdbProvider` and `SpeedrunProvider`. `AggregationService` receives
  `IEnumerable<IGameDataProvider>` and never names a concrete API.
- **Why:** a new API is added without touching aggregation code (open/closed principle). See DEC-004.

### Default interface member for an optional capability

- **What:** an interface member with a default body, so only the implementations that need it override it.
- **Where:** `IGameDataProvider.ConfigurationError => null`. RAWG and IGDB override it; CheapShark and Steam need no
  key and do not.
- **Why:** the contract gains a capability without adding boilerplate to every provider. Use it sparingly: it only
  suits behaviour with an obvious neutral default.
- **Caveat:** a default member can only be called through the interface type, not through the class (ISS-015).

### Layered architecture with inward dependencies

- **What:** every project reference points inwards, towards Core. It is not a chain:
  - **Core** references no other project.
  - **Infrastructure** references Core. It implements Core's `IGameDataProvider` (dependency inversion: Core owns
    the interface, Infrastructure supplies the classes).
  - **Api** references Core and Infrastructure. Its controllers use Core's services and models. It uses
    Infrastructure only once, in `Program`, to register it with `AddInfrastructure` (the composition root).
- **Where:** the `ProjectReference` lines in the three `src/` `.csproj` files. See DEC-003.
- **Why:** business logic can be tested without HTTP, and swapping an API client cannot break the aggregation rules.

### Composition root and registration extension methods

- **What:** every dependency is wired in one place at start-up, through `AddXxx()` extension methods owned by each
  layer.
- **Where:** [`Program.cs`](../src/GameDataAggregator.Api/Program.cs) calls `AddAggregationCore` and
  `AddInfrastructure`. Each provider has its own `AddXxxProvider`, and `AddGameDataProvider<TProvider, TOptions>`
  ([`ProviderRegistration`](../src/GameDataAggregator.Infrastructure/Providers/ProviderRegistration.cs)) holds the
  shared wiring.
- **Why:** each layer knows how to register itself, and `Program.cs` stays short.

### DTOs separate from the domain model

- **What:** the external API's JSON shape (DTOs) is mapped into our own model at the edge.
- **Where:** `RawgModels.cs`, `IgdbModels.cs` and the others are deserialised, then mapped to `AggregatedItem` inside
  each provider.
- **Why:** an upstream API renaming a field breaks one mapping function, not the whole system.

### Null object

- **What:** a ready-made "empty" instance instead of `null`.
- **Where:** `ProviderExecutor.CachedItems.Empty`, for failed and not-configured sources.
- **Why:** callers never need null checks, and the empty value is allocated once.

---

## HTTP and resilience

### Typed `HttpClient` through `IHttpClientFactory`

- **What:** each provider receives its own configured `HttpClient`, created by the factory.
- **Where:** `AddGameDataProvider` calls `services.AddHttpClient<TProvider>(...)`.
- **Why:** the factory pools and rotates the underlying handlers. This avoids socket exhaustion (`new HttpClient()`
  per call) and stale DNS (one static `HttpClient` forever). It is also why `AggregationService` is scoped rather than
  a singleton.

### Delegating handler chain (Chain of Responsibility)

- **What:** outgoing requests pass through a pipeline of `DelegatingHandler`s. Each one adds one concern.
- **Where:** for every API call, from the outside in:

  ```
  resilience (retry, timeouts, circuit breaker)
    → provider-specific handlers (e.g. TwitchAuthenticationHandler)
      → RequestStatisticsHandler
        → network
  ```

- **Why:** cross-cutting concerns stay out of the providers. The order is deliberate:
  - statistics sit inside the retry policy, so every attempt is measured;
  - authentication sits inside the retry policy too, so each retry gets a valid token;
  - authentication sits outside the statistics measurement, so token requests are not counted.

### Retry with exponential back-off and jitter, circuit breaker, timeouts

- **What:** the standard resilience strategies.
  - **Retry:** transient failures are retried after growing, randomised delays, so clients don't retry in lockstep.
  - **Circuit breaker:** when most recent calls fail, the API is not called for a while, so it can recover.
  - **Timeouts:** both each attempt and the whole call are capped.
- **Where:** `AddStandardResilienceHandler`, configured per API from `ExternalApiOptions`.
- **Why:** see DEC-006.

---

## Concurrency

### Fan-out / fan-in with `Task.WhenAll`

- **Where:** `AggregationService.AggregateAsync`.
- **Why:** I/O-bound work runs concurrently without extra threads. See DEC-005.

### Async double-checked locking

- **What:** check without the lock; if a refresh is needed, take the lock, check *again*, then refresh. The second
  check stops callers that were waiting from refreshing again.
- **Where:** [`TwitchAccessTokenProvider.GetTokenAsync`](../src/GameDataAggregator.Infrastructure/Authentication/Twitch/TwitchAccessTokenProvider.cs),
  with `SemaphoreSlim(1, 1)`. The `lock` statement cannot be used around `await`.
- **Why:** 20 simultaneous requests cause one token request, not 20.

### Compare-and-swap invalidation

- **What:** `Interlocked.CompareExchange(ref _current, null, rejected)` clears the cached token only if it is still
  the one that was rejected.
- **Where:** `TwitchAccessTokenProvider.Invalidate`.
- **Why:** a slow request that fails with an old token must not throw away a newer token that another request has
  already fetched.

### Lock per key (lock striping) and consistent snapshots

- **What:** a `ConcurrentDictionary` finds each API's counters without locking. Each counter object has its own small
  `Lock`, and reads copy all counters under that same lock.
- **Where:** [`InMemoryRequestStatisticsStore`](../src/GameDataAggregator.Core/Statistics/InMemoryRequestStatisticsStore.cs).
- **Why:** updates to different APIs never wait on each other, and a reader never sees a half-applied update.

### Ring buffer of time slots (sliding window)

- **What:** a fixed array of slots, one per clock minute. A slot is found by `minute % 60`, and it is cleared when it
  still holds an older minute. Reading a window adds up the slots whose minute falls inside it.
- **Where:** the minute slots in
  [`InMemoryRequestStatisticsStore`](../src/GameDataAggregator.Core/Statistics/InMemoryRequestStatisticsStore.cs),
  updated under the same per-API lock as the totals.
- **Why:** a recent view with fixed memory whatever the traffic, and figures that always match the totals (DEC-028).

### Cancellation flows end to end

- **What:** the request's `CancellationToken` is passed down to every awaited call.
- **Where:** controller → aggregation service → executor → provider → `HttpClient`.
- **Why:** when a client disconnects, the outgoing calls stop too. Tests do the same with
  `TestContext.Current.CancellationToken` (ISS-003).

---

## Caching and degradation

### Cache-aside with stampede protection

- **What:** look in the cache; on a miss, compute the value and store it. Concurrent misses for the same key share
  one computation.
- **Where:** `ProviderExecutor.ExecuteAsync`, through `HybridCache.GetOrCreateAsync`.
- **Why:** fewer external calls, and a burst of identical searches costs one call. See DEC-007.

### Last-known-good fallback (graceful degradation)

- **What:** keep a longer-lived copy of the last successful response, and serve it, clearly labelled, when the live
  call fails.
- **Where:** the `fallback:` cache entries and `SourceStatus.Fallback` in `ProviderExecutor`.
- **Why:** an outage of one API reduces freshness instead of removing the data.

### Bulkhead per source

- **What:** each source's failure is contained, so it cannot take the others down.
- **Where:** `ProviderExecutor` never throws for a provider failure; it returns a `Failed`, `Fallback` or
  `NotConfigured` outcome. Partial failures are contained *inside* providers as well: CheapShark works without its
  store list, and Steam works without a player count. Those side requests also run on their own `HttpClient`
  (`ProviderRegistration.AddSideRequestClient`), with one short try and their own circuit breaker. So a broken
  side endpoint cannot delay the main request for long or open its circuit (ISS-033, DEC-031).
- **Why:** the response is always useful, and the `sources` array says exactly what is partial.

### Model known states as data, not exceptions

- **What:** a condition that is known in advance becomes a value the caller checks. It is not thrown and caught on
  every call.
- **Where:** `ConfigurationError` and `SourceStatus.NotConfigured` (ISS-011, DEC-020).
- **Why:** exceptions are expensive, noisy in logs and debuggers, and hide normal control flow.

---

## Configuration and start-up

### Options pattern with validation on start

- **What:** each configuration section is bound to a typed class with data-annotation rules, validated when the app
  starts.
- **Where:** `AggregationOptions`, `StatisticsOptions`, `PerformanceMonitorOptions`, `ExternalApiOptions` and its
  subclasses, `TwitchAuthOptions` and `JwtAuthOptions`, all with `.ValidateDataAnnotations().ValidateOnStart()`.
- **Why:** a typo in configuration stops start-up with a clear message, instead of failing on the first request.

### Start-up diagnostics through a hosted service

- **What:** an `IHostedService` that runs once at start-up and reports the state of the system.
- **Where:** [`ProviderConfigurationReporter`](../src/GameDataAggregator.Core/Aggregation/ProviderConfigurationReporter.cs)
  logs which sources are ready and which keys are missing.
- **Why:** an operator sees the problem once, at the right moment, not on every request.

### Background service on a timer

- **What:** a `BackgroundService` that loops on `PeriodicTimer.WaitForNextTickAsync`, with the timer driven by the
  injected `TimeProvider`. One failed check is logged and the loop goes on. Shutdown ends the loop quietly.
- **Where:** [`PerformanceMonitor`](../src/GameDataAggregator.Core/Statistics/PerformanceMonitor.cs).
- **Why:** periodic work without a scheduler library, and tests that move a fake clock instead of waiting. Since
  .NET 10 `ExecuteAsync` starts on the thread pool, so tests must wait for it to begin (ISS-028).

### Options checks that depend on the environment

- **What:** an `IValidateOptions<T>` class, which, unlike data annotations, can take services such as
  `IHostEnvironment`.
- **Where:** `JwtAuthOptionsValidator` in
  [`JwtAuthOptions.cs`](../src/GameDataAggregator.Api/Authentication/JwtAuthOptions.cs): the signing key may be
  missing in Development only.
- **Why:** the same setting can be optional while developing and required in production, and both are checked at
  start-up.

### Abstracted time with `TimeProvider`

- **What:** code asks an injected `TimeProvider` for the time, instead of calling `DateTime.UtcNow` or
  `Stopwatch` directly.
- **Where:** `ProviderExecutor`, `TwitchAccessTokenProvider`, `RequestStatisticsHandler`,
  `InMemoryRequestStatisticsStore` (minute slots), `PerformanceMonitor` (its timer) and `JwtTokenIssuer`.
- **Why:** tests use `FakeTimeProvider` to move time forward instantly (token expiry, durations).

---

## Errors and logging

### Exception translation at the boundary

- **What:** internal exceptions are turned into safe, user-facing messages at one place.
- **Where:** `ProviderExecutor.DescribeFailure`. `DataProviderException` messages are written to be shown to callers;
  every other exception gets a generic message and is logged in full.
- **Why:** callers get useful information, and internal details never leak.

### Validation at the edge, one attribute per field

- **What:** each request field carries its own validation attribute, including custom ones
  (`[TrimmedStringLength]`, `[KnownSources]`, `[EnumNameList<T>]`). Model-level `Validate` holds only cross-field
  checks.
- **Where:** [`AggregationRequest`](../src/GameDataAggregator.Api/Contracts/AggregationRequest.cs) and the
  [`Validation`](../src/GameDataAggregator.Api/Validation) folder.
- **Why:** invalid input is rejected before any work starts, and every field error is reported in one response
  (ISS-020).

### Custom model binders for a strict query contract

- **What:** an `IModelBinderProvider` picks a binder by type. Registered first, it replaces the default binding for
  that type everywhere.
- **Where:** `StrictEnumModelBinder` (names only) and `UtcDateTimeOffsetModelBinder` (UTC by default) in
  [`ModelBinding`](../src/GameDataAggregator.Api/ModelBinding).
- **Why:** how text becomes a value is part of the API contract, and the defaults were looser than we want (ISS-019,
  ISS-021).

### Rate limiting at the entrance

- **What:** a partitioned fixed-window limiter, with one partition per client IP, applied to the expensive endpoint
  only.
- **Where:** [`SearchRateLimiting`](../src/GameDataAggregator.Api/RateLimiting/SearchRateLimiting.cs), plus
  `[EnableRateLimiting]` on `AggregationController.Get`.
- **Why:** protects the upstream APIs' quotas from any single caller (DEC-025). The `429` answer follows the same
  problem-details format as every other error.

### Problem details (RFC 9457)

- **Where:** `AddProblemDetails()`, `UseExceptionHandler()`, and `[ApiController]`'s automatic 400 responses.
- **Why:** one machine-readable error format for every client. See ISS-004 for a pitfall.

### Bearer authentication, opt-in per controller

- **What:** the JWT bearer handler reads the token on every request, and `[Authorize]` marks the controllers that need
  one. Everything else stays open.
- **Where:** [`JwtAuthentication`](../src/GameDataAggregator.Api/Authentication/JwtAuthentication.cs), and
  `[Authorize]` on `AggregationController` and `StatisticsController`.
- **Why:** the protected set is visible on the controllers themselves. A `401` uses the same problem-details format
  as every other error, through `UseStatusCodePages`. See DEC-029.

### Log on change, not on every check (edge-triggered alerting)

- **What:** remember each API's last state, and log only when it changes: a warning when it turns slow, an
  information message when it recovers.
- **Where:** `PerformanceMonitor.CheckNow`.
- **Why:** one slowdown gives one warning, however long it lasts, so the log stays readable.

### Source-generated logging

- **What:** `[LoggerMessage]` partial methods instead of `logger.LogWarning("...")`.
- **Where:** `ProviderExecutor`, `TwitchAccessTokenProvider`, `ProviderConfigurationReporter`, `PerformanceMonitor`
  and `JwtSigningKey`.
- **Why:** there is no string formatting or boxing when the level is disabled, each message has a fixed event id, and
  the structured parameters are checked at compile time.

### Log levels decided per category

- **Where:** `Logging:LogLevel` in `appsettings.json`: `HttpClient` and `Polly` at `Warning` (ISS-013).
- **Why:** the log keeps signal and drops noise.

---

## Testing

### Test pyramid

- **What:** many fast unit tests, plus a smaller set of integration tests that run the whole application.
- **Where:** `tests/GameDataAggregator.UnitTests` and `tests/GameDataAggregator.IntegrationTests`.

### Fake the edge, run everything else for real

- **What:** integration tests replace only the outermost HTTP handler (`ConfigurePrimaryHttpMessageHandler`) with
  [`FakeExternalApis`](../tests/GameDataAggregator.IntegrationTests/Infrastructure/FakeExternalApis.cs).
- **Why:** the real resilience, caching, statistics and OAuth pipeline is exercised without the network. See DEC-015.

### Test doubles

- **Stub:** returns canned answers and records what it received. Used by `StubHttpMessageHandler`.
- **Fake:** a working, simplified implementation. Used by `FakeGameDataProvider` and `FakeExternalApis`.
- **Gate:** holds a response until the test releases it, to force concurrency. Used by `GatedHandler` (ISS-007).
- **Capturing logger:** records log entries so tests can assert on them.
  [`CapturingLogger`](../tests/GameDataAggregator.UnitTests/TestDoubles/CapturingLogger.cs), used in
  `ProviderConfigurationReporterTests` and `PerformanceMonitorTests`.

### Prove the test can fail

- **What:** after writing a regression test, switch the fix off, check that the test fails, then switch the fix back
  on.
- **Where:** done for ISS-018, ISS-019 and ISS-021: four tests failed without their fixes.
- **Why:** a test that passes either way protects nothing (compare ISS-007).

### One behaviour per test, named as a sentence

- **Example:** `Provider_missing_configuration_is_reported_without_being_called`.
- **Why:** a failing test name says what broke, without opening the file.

### Humble object: keep the rule apart from the scheduler

- **What:** the decision is a pure function. The class around it only schedules, keeps state and logs.
- **Where:** [`SlowdownDetector`](../src/GameDataAggregator.Core/Statistics/SlowdownDetector.cs) holds the slowdown
  rule. `PerformanceMonitor` calls it on a timer.
- **Why:** the rule's edge cases (exactly 50%, too few requests, a zero baseline) are tested with plain values, no
  timers or logs.

---

## Front end

### Text-only DOM rendering and URL allow-listing

- **What:** API data is inserted with `textContent` and never as HTML. Links and images are used only when they are
  `http(s)` URLs.
- **Where:** `el()` and `safeUrl()` in [`app.js`](../src/GameDataAggregator.Api/wwwroot/js/app.js).
- **Why:** data from third-party APIs cannot inject script (XSS).

### Prevent impossible choices, and say why (faceted filtering)

- **What:** a choice that cannot match the rest of the selection is disabled, and a short line says why. Two groups
  that limit each other adapt to what the user *wants* in the other group, so neither locks the other. A tick on a
  disabled choice is parked and comes back when the choice can match again. A group is never left empty while a
  search can run: the Search button waits until each group has a tick, and a group's only possible choice is ticked
  by the page (DEC-034).
- **Where:** `updateChoiceAvailability`, `wantedValues` and `renderChoiceHint` in
  [`app.js`](../src/GameDataAggregator.Api/wwwroot/js/app.js). The server applies the same rule
  (`SourceInfo.IsSelectedBy`), so a direct API call cannot ask for the impossible either. See DEC-033.
- **Why:** an empty page with no reason makes the user guess. Preventing the dead end, and explaining it, follows
  Joe Natoli's principles: clarity, visible feedback and user control.

### Design tokens in two tiers

- **What:** colours and sizes are named values (tokens) in two tiers. Primitives say what a value is (`--gray-50`).
  Semantic tokens say what it is for (`--bg`), and point to a primitive. Components use only semantic tokens.
- **Where:** `css/tokens.css` (primitives), `css/theme-light.css` and `css/theme-dark.css` (semantic tokens per
  theme), `css/site.css` (components). See DEC-035.
- **Why:** a theme only changes which primitive each purpose points to, so themes never touch the components.

### State on the root element, set before the first paint

- **What:** a small script in `<head>` sets the theme and layout as attributes on `<html>` before the page is drawn,
  and the CSS keys on them.
- **Where:** [`display.js`](../src/GameDataAggregator.Api/wwwroot/js/display.js).
- **Why:** the page never flashes in the wrong colours, and a user's choice can override what the device suggests.

### Explicit HTTP caching for static files

- **What:** every static file is served with `Cache-Control: no-cache` plus the default `ETag`, so browsers
  revalidate before reusing a copy.
- **Where:** `UseStaticFiles` in [`Program.cs`](../src/GameDataAggregator.Api/Program.cs).
- **Why:** an update to the UI reaches users on their next page load, and an unchanged file costs only a
  `304 Not Modified` (ISS-017).

### URL as state

- **What:** the search form is mirrored in the page address (`history.replaceState`) and restored from it.
- **Why:** a result can be bookmarked or shared. Restored values are validated first (ISS-009).

### Stable page frame

- **What:** the page always reserves the scrollbar's space (`scrollbar-gutter: stable`), and the header stays at the
  top while scrolling (`position: sticky`), except on phones. Text that can hold very long words (setting names,
  titles from APIs) may break anywhere (`overflow-wrap: anywhere`).
- **Where:** [`site.css`](../src/GameDataAggregator.Api/wwwroot/css/site.css).
- **Why:** switching between a long and a short tab no longer moves the layout sideways (ISS-026), and no card
  grows wider than its column (ISS-025).

---

## Build hygiene

- **Comment style:** each logic block has a one-sentence comment in plain words (`//  <sentence>.`), and each class
  and method has a `/// <summary>`. Example: [`UtcDateTimeOffsetModelBinder`](../src/GameDataAggregator.Api/ModelBinding/UtcDateTimeOffsetModelBinder.cs).
- **Central package management:** every package version is declared once, in `Directory.Packages.props`.
- **Warnings as errors and nullable reference types:** small problems are fixed when they appear (ISS-002, ISS-010,
  ISS-012).
- **Analyzers:** the xUnit and .NET analyzers enforce good practice (ISS-003).
