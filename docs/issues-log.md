# Issues log

Every bug, failure and problem met while building this project, in the order it was found. The point is to learn from
them, so each entry records not only the fix but *why* it happened and what to do differently.

**Entry format**

- **Symptom:** what was observed.
- **Root cause:** why it happened.
- **Fix:** what changed, with file references.
- **Lesson:** the general takeaway.
- **Guarded by:** the test that would catch a regression, if any.

**Status:** `Fixed`, `Open` (not resolved yet), or `External` (outside our control; mitigated).

| ID | Date | Area | Summary | Status |
|---|---|---|---|---|
| [ISS-001](#iss-001-undated-items-sorted-first-when-sorting-by-date-descending) | 2026-11-07 | Sorting | Undated items sorted first when sorting by date descending | Fixed |
| [ISS-002](#iss-002-build-failed-on-incomplete-xml-documentation) | 2026-11-07 | Build | Build failed on incomplete XML documentation | Fixed |
| [ISS-003](#iss-003-xunit-analyzer-rejected-a-test-that-ignored-the-test-cancellation-token) | 2026-11-07 | Tests | xUnit analyzer rejected a test that ignored the test cancellation token | Fixed |
| [ISS-004](#iss-004-validation-errors-returned-as-applicationjson-instead-of-problemjson) | 2026-11-07 | API | Validation errors returned as `application/json` instead of `problem+json` | Fixed |
| [ISS-005](#iss-005-rawg-answered-http-522-during-a-live-check) | 2026-11-07 | External | RAWG answered HTTP 522 during a live check | External |
| [ISS-006](#iss-006-configure-not-found-on-the-resilience-builder) | 2026-11-07 | Build | `Configure` not found on the resilience builder | Fixed |
| [ISS-007](#iss-007-concurrency-test-for-the-token-provider-could-pass-without-testing-anything) | 2026-11-07 | Tests | Concurrency test for the token provider could pass without testing anything | Fixed |
| [ISS-008](#iss-008-pager-stayed-visible-after-a-failed-search) | 2026-11-07 | UI | Pager stayed visible after a failed search | Fixed |
| [ISS-009](#iss-009-an-unsupported-page-size-in-the-address-blanked-the-select) | 2026-11-07 | UI | An unsupported page size in the address blanked the select | Fixed |
| [ISS-010](#iss-010-nullable-warning-on-a-deserialised-test-response) | 2026-11-07 | Tests | Nullable warning on a deserialised test response | Fixed |
| [ISS-011](#iss-011-exception-on-every-search-when-an-api-key-is-missing) | 2026-11-07 | Runtime | Exception on every search when an API key is missing | Fixed |
| [ISS-012](#iss-012-nullable-analysis-lost-track-of-a-checked-api-key) | 2026-11-07 | Build | Nullable analysis lost track of a checked API key | Fixed |
| [ISS-013](#iss-013-polly-logged-every-successful-http-call) | 2026-11-07 | Logging | Polly logged every successful HTTP call | Fixed |
| [ISS-014](#iss-014-a-paused-debugger-looks-like-a-hung-api-and-locks-the-build-output) | 2026-11-07 | Dev setup | A paused debugger looks like a hung API and locks the build output | Workaround |
| [ISS-015](#iss-015-a-default-interface-member-was-not-visible-on-the-class) | 2026-11-07 | Tests | A default interface member was not visible on the class | Fixed |
| [ISS-016](#iss-016-speedruncom-release-dates-were-labelled-measured-in-the-ui) | 2026-11-07 | UI / API | speedrun.com release dates were labelled "Measured" in the UI | Fixed |
| [ISS-017](#iss-017-the-browser-kept-running-an-outdated-appjs) | 2026-11-07 | UI / hosting | The browser kept running an outdated `app.js` | Fixed |
| [ISS-018](#iss-018-spaces-got-around-the-minimum-search-length) | 2026-11-07 | Validation | Spaces got around the minimum search length | Fixed |
| [ISS-019](#iss-019-dates-without-an-offset-were-read-in-the-servers-time-zone) | 2026-11-07 | Validation | Dates without an offset were read in the server's time zone | Fixed |
| [ISS-020](#iss-020-validation-errors-came-back-in-two-rounds) | 2026-11-07 | Validation | Validation errors came back in two rounds | Fixed |
| [ISS-021](#iss-021-enum-parameters-accepted-numbers) | 2026-11-07 | Validation | Enum parameters accepted numbers | Fixed |
| [ISS-022](#iss-022-api-documentation-comments-went-stale) | 2026-11-07 | Docs | API documentation comments went stale | Fixed |
| [ISS-023](#iss-023-no-limit-on-incoming-searches) | 2026-11-07 | Security / cost | No limit on incoming searches | Fixed |
| [ISS-024](#iss-024-the-word-null-appeared-after-a-ui-error-message) | 2026-11-08 | UI | The word "null" appeared after a UI error message | Fixed |
| [ISS-025](#iss-025-long-setting-names-overflowed-the-source-cards) | 2026-11-08 | UI | Long setting names overflowed the source cards | Fixed |
| [ISS-026](#iss-026-the-page-shifted-sideways-when-switching-tabs) | 2026-11-08 | UI | The page shifted sideways when switching tabs | Fixed |
| [ISS-027](#iss-027-statistics-could-not-show-a-recent-slowdown) | 2026-11-08 | Statistics | Statistics could not show a recent slowdown | Fixed |
| [ISS-028](#iss-028-background-service-tests-moved-the-clock-before-the-timer-existed) | 2026-11-08 | Testing | Background service tests moved the clock before the timer existed | Fixed |
| [ISS-029](#iss-029-search-and-statistics-were-open-to-anyone) | 2026-11-08 | Security | Search and statistics were open to anyone | Fixed |
| [ISS-030](#iss-030-timeouts-are-reported-as-currently-unavailable) | 2026-11-08 | Runtime | Timeouts are reported as "currently unavailable" | Open |
| [ISS-031](#iss-031-small-gaps-between-the-readme-and-the-code) | 2026-11-08 | Docs | Small gaps between the README and the code | Open |
| [ISS-032](#iss-032-a-noconflict-error-in-the-browser-console-while-debugging) | 2026-11-08 | Dev setup | A `$.noConflict` error in the browser console while debugging | External |
| [ISS-033](#iss-033-side-requests-can-slow-down-or-switch-off-a-sources-main-request) | 2026-11-08 | Resilience | Side requests can slow down or switch off a source's main request | Fixed |
| [ISS-034](#iss-034-a-base-address-without-a-trailing-slash-loses-part-of-its-path) | 2026-11-08 | Configuration | A base address without a trailing slash loses part of its path | Fixed |
| [ISS-035](#iss-035-the-igdb-attempt-time-limit-also-limits-the-twitch-token-request) | 2026-11-08 | Resilience | The IGDB attempt time limit also limits the Twitch token request | Fixed |

---

## ISS-001: Undated items sorted first when sorting by date descending

- **Symptom:** with `sortBy=Date&sortDirection=Descending`, items with no date (e.g. unreleased games) came first.
- **Root cause:** `OrderByDescending` on a nullable date treats `null` as the smallest value, so reversing the order
  moves the nulls from the end to the start.
- **Fix:** sort first on "has a date", then on the date, so undated items are last in both directions
  ([`ItemQueryProcessor`](../src/GameDataAggregator.Core/Aggregation/ItemQueryProcessor.cs)).
- **Lesson:** reversing a sort also reverses where `null`s land. Decide explicitly where missing values go.
- **Guarded by:** `ItemQueryProcessorTests.Sorting_by_date_puts_undated_items_last_in_both_directions`.

## ISS-002: Build failed on incomplete XML documentation

- **Symptom:** `CS1573: Parameter 'services' has no matching param tag in the XML comment` for
  `AddGameDataProvider`.
- **Root cause:** the method documented some parameters with `<param>` but not all. With
  `TreatWarningsAsErrors`, the documentation warning fails the build.
- **Fix:** documented every parameter in
  [`ProviderRegistration`](../src/GameDataAggregator.Infrastructure/Providers/ProviderRegistration.cs).
- **Lesson:** warnings-as-errors is doing its job: half-documented public APIs mislead readers. Document all
  parameters or none.

## ISS-003: xUnit analyzer rejected a test that ignored the test cancellation token

- **Symptom:** `xUnit1051: Calls to methods which accept CancellationToken should use
  TestContext.Current.CancellationToken`, in the statistics concurrency test.
- **Root cause:** `Task.Run(...)` was called without a token, so an aborted or timed-out test run could not cancel it.
- **Fix:** passed `TestContext.Current.CancellationToken` to every call that accepts one.
- **Lesson:** in xUnit v3, tests should flow the test's own cancellation token, the same way production code flows
  the request's token.

## ISS-004: Validation errors returned as `application/json` instead of `problem+json`

- **Symptom:** 8 of 18 integration tests failed: `Expected "application/problem+json", Actual "application/json"` on
  every 400 response.
- **Root cause:** a controller-level `[Produces("application/json")]` attribute forces that content type on *every*
  response of the controller, including the automatic 400 validation problem details.
- **Fix:** removed the class-level attribute and declared the content type per response:
  `[ProducesResponseType<ValidationProblemDetails>(400, "application/problem+json")]`
  ([`AggregationController`](../src/GameDataAggregator.Api/Controllers/AggregationController.cs)).
- **Lesson:** `[Produces]` is not only documentation; it changes runtime content negotiation. Use
  `[ProducesResponseType]` to document responses without changing them.
- **Guarded by:** `AggregationEndpointTests.Invalid_parameters_return_problem_details`.

## ISS-005: RAWG answered HTTP 522 during a live check

- **Symptom:** a manual call to RAWG returned `522` (a Cloudflare status: the origin server did not respond in time).
- **Root cause:** RAWG's own infrastructure. Nothing in this service.
- **Mitigation:** this is the scenario the resilience design exists for. The call is retried with back-off. If it
  still fails, the last good data is served as `Fallback`, or the source is reported as `Failed`, and the other
  sources are unaffected.
- **Status:** External. RAWG has not been verified live with a key yet.
- **Lesson:** free public APIs go down. Build for partial failure from the start rather than as an afterthought.

## ISS-006: `Configure` not found on the resilience builder

- **Symptom:** `CS1929: 'IHttpStandardResiliencePipelineBuilder' does not contain a definition for 'Configure'` in
  `TwitchAuthRegistration`.
- **Root cause:** `Configure` is an extension method from the `Microsoft.Extensions.Http.Resilience` namespace, which
  the file did not import. The compiler then suggested an unrelated overload, which made the message confusing.
- **Fix:** added `using Microsoft.Extensions.Http.Resilience;`.
- **Lesson:** when a fluent method seems to be missing, check the namespace imports before anything else.

## ISS-007: Concurrency test for the token provider could pass without testing anything

- **Symptom:** the test "concurrent callers share one token request" passed, but on review it proved nothing.
- **Root cause:** the fake token endpoint answered synchronously. Each caller finished before the next one started,
  so the callers never overlapped and the shared-refresh code was never exercised.
- **Fix:** a `GatedHandler` holds the token response until all 20 callers are waiting, and the test asserts that
  none of them has finished before the gate opens
  ([`TwitchAuthenticationTests`](../tests/GameDataAggregator.UnitTests/Infrastructure/TwitchAuthenticationTests.cs)).
- **Lesson:** a concurrency test must *force* the overlap it claims to test. Otherwise it passes by luck, and keeps
  passing after the code breaks.
- **Guarded by:** `TwitchAuthenticationTests.Concurrent_callers_share_one_token_request`.

## ISS-008: Pager stayed visible after a failed search

- **Symptom:** in the browser UI, after a validation error the "Previous / Next" pager was still shown.
- **Root cause:** the script set the `hidden` attribute, but the stylesheet's `.pager { display: flex }` has higher
  priority than the browser's built-in `[hidden] { display: none }`, so the element stayed visible.
- **Fix:** a global `[hidden] { display: none !important; }`
  ([`site.css`](../src/GameDataAggregator.Api/wwwroot/css/site.css)).
- **Lesson:** the `hidden` attribute is only a default style, and any `display` rule overrides it. Make it win
  explicitly, which is what most CSS resets do.

## ISS-009: An unsupported page size in the address blanked the select

- **Symptom:** opening the UI with `?pageSize=500` showed an empty "Per page" box.
- **Root cause:** the page restores the form from the address bar. Setting a `<select>` to a value it has no option
  for leaves it with no selection.
- **Fix:** values are restored only when the select has a matching option; otherwise the default stays
  ([`app.js`](../src/GameDataAggregator.Api/wwwroot/js/app.js), `restoreFromLocation`).
- **Lesson:** anything read from the URL is user input. Validate it before use, even on the client.

## ISS-010: Nullable warning on a deserialised test response

- **Symptom:** `CS8604: Possible null reference argument` on `Assert.Equal(expected, sources)` in an integration
  test.
- **Root cause:** `GetFromJsonAsync<T>` returns `T?`, because the body could be the JSON literal `null`.
- **Fix:** `sources!`. In a test, a `null` here would make the assertion fail anyway, which is the desired outcome.
- **Lesson:** nullable annotations on deserialisation are honest: JSON can be `null`. In production code handle it;
  in tests, asserting is enough.

## ISS-011: Exception on every search when an API key is missing

- **Symptom:** while debugging in Visual Studio, searching for a game stopped the debugger on an exception, and the
  browser request hung until the debugger was resumed. Outside the debugger every search returned `200`.
- **Root cause:** without a RAWG key or Twitch credentials, the RAWG and IGDB providers threw a
  `DataProviderException` on **every** search. `ProviderExecutor` did catch it, so the API was correct. But the
  exception travelled through `HybridCache`'s factory callback, which is library code, before being caught. With
  *Just My Code*, Visual Studio reports an exception that leaves user code into library code as "User-Unhandled" and
  pauses. The design had two further costs:
  - a full warning with stack trace was logged twice per search;
  - a pointless fallback cache look-up ran each time.

  The deeper mistake: a missing key is a fixed fact about the deployment, but it was being rediscovered and reported
  as a new failure on every request.
- **Fix:**
  - `IGameDataProvider.ConfigurationError`: a provider states up front what it is missing (`null` when ready). It is
    a default interface member, so providers that need no key change nothing.
  - `ProviderExecutor` checks it first and answers with the new status `SourceStatus.NotConfigured`, without calling
    the provider, throwing, logging or touching the cache.
  - `ProviderConfigurationReporter`, a hosted service, logs one warning per unconfigured source at start-up.
  - The UI shows a grey "Not configured" badge. The Postman tests and README list the new status.
- **Lesson:**
  - Exceptions are for unexpected failures, not for known states. A state that is known in advance should be modelled
    as data.
  - "The debugger stops here" is a useful smell even when the code is technically correct.
  - Genuine API failures still pass through `HybridCache` and can still pause the debugger. That is expected; the
    README explains how to continue.
- **Guarded by:**
  - `ProviderExecutorTests.Provider_missing_configuration_is_reported_without_being_called`;
  - `ProviderExecutorTests.Provider_is_called_as_soon_as_its_configuration_is_fixed`;
  - `ProviderConfigurationReporterTests`;
  - `AggregationEndpointTests.Igdb_without_credentials_is_reported_as_not_configured_and_never_called`;
  - the RAWG and IGDB provider tests.

## ISS-012: Nullable analysis lost track of a checked API key

- **Symptom:** after ISS-011, `CS8604: Possible null reference argument` on `Uri.EscapeDataString(settings.ApiKey)` in
  `RawgProvider`.
- **Root cause:** the null check moved into the `ConfigurationError` property. The compiler's nullable flow analysis
  works within a single method, so it cannot see that the property already ruled out `null`.
- **Fix:** `var apiKey = settings.ApiKey!;` with a comment pointing at the check. (`[MemberNotNullWhen]` cannot help
  here: it only describes members of the same class, and the key lives on the options object.)
- **Lesson:** when a check moves into a helper, the compiler loses that knowledge. Restate it at the point of use,
  with a short comment saying why it is safe.

## ISS-013: Polly logged every successful HTTP call

- **Symptom:** the console showed an `info: Polly[3] Execution attempt ... Result: '200'` line for every outgoing
  request, several per search.
- **Root cause:** the resilience pipeline's telemetry logs each attempt at `Information`. `appsettings.json` silenced
  `System.Net.Http.HttpClient` but not the `Polly` category.
- **Fix:** `"Polly": "Warning"` in `appsettings.json`. Retries and failures are logged at `Warning` or above, so
  they still appear.
- **Lesson:** decide log levels per category deliberately. Noise hides the lines that matter, and in production it
  costs money.

## ISS-014: A paused debugger looks like a hung API and locks the build output

- **Symptom:** while Visual Studio was paused on ISS-011:
  - `curl` requests to port 5067 hung;
  - a second instance could not start (`address already in use`);
  - a rebuild could not overwrite the DLLs in `bin/`.
- **Root cause:** a paused process keeps its port and its files open but processes nothing.
- **Workaround:**
  - press **Continue** (F5) or stop debugging before testing from outside;
  - to run a second copy anyway, build to another folder (`dotnet build --artifacts-path <folder>`) and start it with
    `--urls http://localhost:5070`.
- **Lesson:** when an API "hangs", check for a paused debugger before suspecting the code.

## ISS-015: A default interface member was not visible on the class

- **Symptom:** `CS1061: 'SpeedrunProvider' does not contain a definition for 'ConfigurationError'` in a unit test,
  although `RawgProvider.ConfigurationError` compiled fine.
- **Root cause:** `SpeedrunProvider` needs no key, so it does not override `ConfigurationError` and inherits the
  interface's default body. In C#, a default interface member belongs to the *interface*: it can only be called
  through a variable of the interface type, never through the class. `RawgProvider` declares its own public property,
  which is why it worked there.
- **Fix:** the test declares the variable as `IGameDataProvider`, which is also how `ProviderExecutor` calls it.
- **Lesson:** default interface members are not inherited like base-class members. Callers see them only through
  the interface. This is usually what you want (the service works with the interface), but tests and other
  direct callers have to know it.
- **Guarded by:** `SpeedrunProviderTests.Needs_no_configuration`.

## ISS-016: speedrun.com release dates were labelled "Measured" in the UI

- **Symptom:** in the browser UI, speedrun.com cards read "Measured Jan 25, 2018". That date is the game's release
  date.
- **Root cause:** the UI guessed what `date` meant from the item's category: `PlayerActivity` meant "Measured".
  That held while Steam was the only `PlayerActivity` source, but speedrun.com is in the same category and its date is
  a release date. The meaning of a date belongs to the source, while the guess was made per category, one level too
  coarse.
- **Fix:** the API now says what each date means (`dateKind`: `Released`, `PriceUpdated`, `Measured`, see DEC-024).
  Each provider sets it, and the UI only displays it.
- **Lesson:**
  - a client should not infer meaning that the server knows and could state;
  - a rule that is true of today's data ("player activity dates are measurements") is not a rule of the model, and
    the next source can break it;
  - checking the UI with real data from the new source is what caught this. No unit test would have.
- **Guarded by:** the `DateKind` assertions in every provider test, and `AggregatedItemTests`.

## ISS-017: The browser kept running an outdated `app.js`

- **Symptom:** after the ISS-016 fix, the server sent the new `dateKind` and served the new `app.js`, but the page
  still showed "Measured". Even a forced reload did not help.
- **Root cause:** static files were served without a `Cache-Control` header. The browser is then free to decide for
  itself how long its copy stays fresh ("heuristic freshness", usually a fraction of the file's age since
  `Last-Modified`), and it kept using the old script. Every user would hit this after an update.
- **Fix:** `Cache-Control: no-cache` on every static file
  ([`Program.cs`](../src/GameDataAggregator.Api/Program.cs)). `no-cache` does not mean "don't store". It means "check
  with the server before reusing". Unchanged files cost a tiny `304 Not Modified` thanks to the `ETag`, and changed
  files are fetched at once. A copy cached *before* the fix has to be refreshed once.
- **Lesson:** always state a caching policy for what you serve. Without one, browsers apply their own. Production
  front ends usually go further, with file names that contain a content hash (`app.3f9a1c.js`) cached for a year. For
  three hand-written files, revalidation is the simpler, safe choice.
- **Guarded by:** `UiAndSourcesEndpointTests.Browser_ui_is_served_from_the_root` (checks the header for every file).

> ISS-018 to ISS-023 were found in part 1 of the code review (the request entry point). ISS-018, ISS-020 and
> ISS-021 were confirmed against the running API before fixing. Each fix was checked the other way round too: with
> the fix switched off, its new test fails.

## ISS-018: Spaces got around the minimum search length

- **Symptom:** `search=%20a%20` (" a ") was accepted and searched for `a`, although `search` needs at least 2
  characters.
- **Root cause:** `[StringLength(100, MinimumLength = 2)]` measured the raw value (3 characters). The value was
  trimmed only later, in `ToQuery()`. The check and the use saw two different strings.
- **Fix:** `[TrimmedStringLength]`, a `StringLengthAttribute` that measures the trimmed value
  ([`TrimmedStringLengthAttribute`](../src/GameDataAggregator.Api/Validation/TrimmedStringLengthAttribute.cs)).
- **Lesson:** validate exactly the value you are going to use. If the input is normalised (trimmed, lower-cased),
  normalise it before checking it.
- **Guarded by:** `Invalid_parameters_return_problem_details("search=%20a%20")`.

## ISS-019: Dates without an offset were read in the server's time zone

- **Symptom:** from .NET's parsing rules: `from=2015-05-18` meant midnight *server* time. On this UTC+2 machine
  that is 2015-05-17T22:00Z, so the same request would filter differently on servers in different zones. The UI was
  not affected, because it always sends an explicit `Z`.
- **Root cause:** the default `DateTimeOffset` model binder uses `DateTimeOffset.Parse`, which assumes the local time
  zone when the text has no offset.
- **Fix:** [`UtcDateTimeOffsetModelBinder`](../src/GameDataAggregator.Api/ModelBinding/UtcDateTimeOffsetModelBinder.cs)
  parses with `DateTimeStyles.AssumeUniversal`. The README also explains that a date-only `to` ends at the start of
  that day.
- **Lesson:** a server's time zone is an accident of deployment, so never let it change the meaning of a request.
  Choose UTC and document it.
- **Guarded by:** `Dates_without_an_offset_are_read_as_utc`. It can only catch the bug on a machine whose time zone
  is ahead of UTC; on a UTC machine both readings agree.

## ISS-020: Validation errors came back in two rounds

- **Symptom:** `sources=Twitter&page=0` returned only the `page` error. After fixing it, the client got a second
  400 for `sources`.
- **Root cause:** the source names were checked in the controller, which runs only after model validation has
  passed. The `categories` check sat in `IValidatableObject.Validate`, which ASP.NET Core also runs only once every
  field is valid on its own.
- **Fix:** each field validates itself with an attribute: `[KnownSources]` (resolves the registered sources from the
  request's services) and `[EnumNameList<ItemCategory>]`. Attribute checks always run in full, so every field error
  arrives together. `Validate` keeps only the cross-field check (`from` <= `to`).
- **Lesson:** know when each validation hook runs. Per-field rules belong in per-field validators; model-level hooks
  run last and only if the fields are valid.
- **Guarded by:** `All_field_errors_are_reported_in_one_response`.

## ISS-021: Enum parameters accepted numbers

- **Symptom:** `sortBy=1` was accepted and meant `Date`, while `categories=1` was rejected.
- **Root cause:** the default enum model binder accepts underlying numeric values (and comma-combined names), and
  `[EnumDataType]` only checks that the number is defined. Clients could come to depend on numbers that change
  meaning whenever the enum is reordered.
- **Fix:** [`StrictEnumModelBinder`](../src/GameDataAggregator.Api/ModelBinding/StrictEnumModelBinder.cs) accepts
  exact names only (case-insensitive) and lists the valid names in the error. It is registered for every enum, so
  future enum parameters get the same rule.
- **Lesson:** an enum's numbers are an implementation detail. Make the public contract the names, and make it
  consistent across parameters.
- **Guarded by:** `Invalid_parameters_return_problem_details("sortBy=1")` and `("sortDirection=0")`.

## ISS-022: API documentation comments went stale

- **Symptom:** the endpoint description listed only the `Fallback` and `Failed` statuses (not `NotConfigured`), and
  the `sources` description listed four sources (not Speedrun). Both feed the OpenAPI document.
- **Root cause:** the comments copied lists that live elsewhere in the code, so they fell behind when the code
  changed.
- **Fix:** both comments were updated. The `sources` comment now points to `GET /api/v1/sources` instead of listing
  names.
- **Lesson:** documentation that repeats a list goes stale. Point to the source of truth where possible.

## ISS-023: No limit on incoming searches

- **Symptom:** from reading the code: a client sending many *different* searches makes up to six external calls for
  each one. The cache does not help, because each search is new. One script could use up speedrun.com's limit
  (about 100 a minute) or RAWG's monthly quota for every user.
- **Root cause:** the service protected the external APIs from failure (retries, circuit breaker) but not from
  volume.
- **Fix:** ASP.NET Core's rate limiter allows each client IP 60 searches a minute (configurable). Above that it
  answers `429` with `Retry-After` and a problem details body. Cheap endpoints are not limited. See DEC-025.
- **Lesson:** an API that calls other APIs spends their quotas on its callers' behalf. Budget that spending at the
  entrance.
- **Guarded by:** `Searches_are_rate_limited_per_client_and_other_endpoints_are_not`.

## ISS-024: The word "null" appeared after a UI error message

- **Symptom:** searching with fewer than 2 characters showed "Enter at least 2 characters.null" in the error box.
  Reported by the user from the running UI.
- **Root cause:** `showSearchError()` in `wwwroot/js/app.js` passes `null` as the second child when there are no
  details. It passes it to the browser's own `replaceChildren()`, not to our `el()` helper. `el()` skips `null`
  children, but the DOM method turns any value that is not a node into text, so `null` became the text "null".
- **Fix:** `showSearchError()` adds the details list only when there are details, by spreading an array that is
  empty otherwise. Nothing that is not a node reaches `replaceChildren()` any more. Checked in the browser.
- **Lesson:** a helper that filters empty values only protects the calls that go through it. Calls straight to the
  DOM need the same care.
- **Guarded by:** nothing yet.

## ISS-025: Long setting names overflowed the source cards

- **Symptom:** the IGDB "Not configured" card ran past its right edge. Text such as
  `'ExternalApis:TwitchAuth:ClientSecret'` and the "NOT CONFIGURED" badge did not fit. Reported by the user with a
  screenshot.
- **Root cause:** a setting name has no spaces, so the browser cannot break it. `.source-card .error` in
  `wwwroot/css/site.css` has no `overflow-wrap` rule, and the cards are only 220 px wide at minimum.
- **Fix:** `.source-card .error` got `overflow-wrap: anywhere`. The card got `min-width: 0` (a grid item may then
  shrink below its content), its title row may wrap, and the status badge never breaks (`white-space: nowrap`).
  Checked in the browser at desktop and phone width: no card is wider than its content.
- **Lesson:** any text that comes from configuration or from an external API can contain very long words. Give
  such text `overflow-wrap: anywhere`, as the item titles already have.
- **Guarded by:** nothing yet (visual only).

## ISS-026: The page shifted sideways when switching tabs

- **Symptom:** the header and the tab buttons sat about 8 px further right on the Statistics tab than on the
  Search tab. Reported by the user with two screenshots.
- **Root cause:** the Search tab is taller than the window, so the browser shows a vertical scrollbar. The
  Statistics tab is shorter, so the scrollbar disappears. The page becomes wider and the centred content moves.
- **Fix:** `html { scrollbar-gutter: stable; }` keeps the scrollbar's space on every page. The header is now
  `position: sticky` at the top, except on phones, where it wraps to several lines and would hide too much. Checked
  in the browser: the tabs sit at the same position with and without a scrollbar.
- **Lesson:** centred layouts move when a scrollbar appears or disappears. Reserve the space for it.
- **Guarded by:** nothing yet (visual only).

## ISS-027: Statistics could not show a recent slowdown

- **Symptom:** from reading the code, while adding the performance monitor: `InMemoryRequestStatisticsStore` kept
  only totals since start-up. After an hour of normal traffic, five minutes of slow answers barely move the average,
  so nothing could tell a recent slowdown from normal behaviour.
- **Root cause:** the store had no notion of time. It answered "how fast since start-up", not "how fast lately".
- **Fix:** each API also keeps one count and one total time per clock minute, in a ring of 60 slots under the same
  lock as the totals. `GetPerformanceWindows` splits the figures into the recent window and the time before it.
  `PerformanceMonitor` compares the two. See DEC-028.
- **Lesson:** an average since start-up hides change. Monitoring needs a recent view, and a baseline that does not
  include it.
- **Guarded by:** `PerformanceWindowTests`, `SlowdownDetectorTests` and `PerformanceMonitorTests`.

## ISS-028: Background service tests moved the clock before the timer existed

- **Symptom:** two new tests failed. `Checks_on_every_interval_once_started` saw no warning after moving the fake
  clock one interval forward. `Does_nothing_when_disabled` never saw the "disabled" message.
- **Root cause:** since .NET 10, `BackgroundService.StartAsync` runs all of `ExecuteAsync` on the thread pool and
  returns at once. Before, the part up to the first `await` ran inside `StartAsync`. So the test moved the clock
  before `ExecuteAsync` had created its `PeriodicTimer`, and the timer then counted from the new time and never
  fired. In the other test, `StopAsync` cancelled the service before its background task had even started, so
  `ExecuteAsync` never ran.
- **Fix:** the tests wait for the service's "started" or "disabled" log message before moving the clock or
  stopping it.
- **Lesson:** after `StartAsync` returns, assume nothing in `ExecuteAsync` has run yet. Wait for a visible sign that
  it has, such as a log line.
- **Guarded by:** the two tests themselves. Both failed before the fix.

## ISS-029: Search and statistics were open to anyone

- **Symptom:** from reading the code: anyone who could reach the service could search, which spends the external
  APIs' quotas, and read the statistics, which describe the service's traffic.
- **Root cause:** the API had no authentication. The rate limit (ISS-023) slows a single caller down but does not
  say who may call.
- **Fix:** JWT bearer authentication on the search and statistics endpoints, with a development-only token endpoint
  so the UI, Scalar and Postman still work. See DEC-029.
- **Lesson:** decide per endpoint what it costs and what it reveals, and protect those that cost or reveal something.
- **Guarded by:** `AuthenticationTests`.

## ISS-030: Timeouts are reported as "currently unavailable"

- **Symptom:** found while checking the documentation against the code. The README shows a source in `Fallback`
  with the error "The external API did not respond in time." In practice, a timeout gives "The external API is
  currently unavailable." instead.
- **Root cause:** the resilience pipeline (Polly) turns a timeout into a `TimeoutRejectedException`, and an open
  circuit breaker throws a `BrokenCircuitException`. Both derive from `Polly.ExecutionRejectedException`, not from
  `TimeoutException` or `OperationCanceledException`. `ProviderExecutor.DescribeFailure` only checks the two .NET
  types, so these exceptions fall through to the generic message. Checked with Polly.Core 8.4.2.
- **Fix:** not fixed yet. Suggested: map `TimeoutRejectedException` to "did not respond in time" and
  `BrokenCircuitException` to a message such as "The external API is temporarily paused after repeated failures."
  Core has no Polly dependency, so either add the Polly abstractions package to Core, or wrap the exceptions in
  Infrastructure (for example in a delegating handler) as `TimeoutException` or `DataProviderException`. Then update
  the README example.
- **Lesson:** unit tests that throw hand-made exceptions (`new TimeoutException()`) only prove the mapping, not
  which exception the real pipeline throws. One integration test with a real timeout would have shown it.
- **Guarded by:** nothing yet. `ProviderExecutorTests` covers the .NET exception types only.

## ISS-031: Small gaps between the README and the code

- **Symptom:** found while writing `GameDataAggregator-Documentation.docx` and checking every claim against the code.
- **Root cause:** the README was written as the features grew, and a few details were not carried over:
  - It says the Postman collection has "one that uses every parameter". That request includes `tag`, `from` and
    `to`, but they are switched off (`"disabled": true`), so it does not send them.
  - The parameter table does not say that `tag` is limited to 50 characters (`[StringLength(50)]` in
    `AggregationRequest`).
  - "Adding a new API" does not mention that the integration tests list every source by name
    (`FakeExternalApis`, `GameDataAggregatorFactory`, and the expected lists in three test classes). They fail
    until the new source is added there.
  - It says a new source shows up in the UI automatically. That is true for sources, but a new `ItemCategory` or
    `DateKind` also needs an entry in `CATEGORIES`, `CATEGORY_LABELS` or `DATE_KIND_LABELS` in `wwwroot/js/app.js`.
  - `Aggregation:FallbackDuration` should be longer than `CacheDuration` to be useful, but nothing checks it at
    start-up (the options class only says so in a comment).
- **Fix:** not fixed in the README yet. The new documents (`docs/GameDataAggregator-Documentation.docx` and
  `docs/adding-a-source.md`) describe the actual behaviour.
- **Lesson:** check documentation against the code, not against older documentation.
- **Guarded by:** nothing (documentation only).

## ISS-032: A `$.noConflict` error in the browser console while debugging

- **Symptom:** `Uncaught TypeError: $.noConflict is not a function at browserLink`, seen in the browser console when
  the app runs from Visual Studio.
- **Root cause:** Visual Studio's Browser Link feature injects its own script into pages during debugging. That
  script expects jQuery, which this UI does not use. The error is not in the app's code and does not appear when
  the app runs without Visual Studio.
- **Fix:** none needed. It can be silenced by turning off Browser Link in Visual Studio (the Browser Link button
  on the toolbar, or Tools > Options > Projects and Solutions > ASP.NET Core).
- **Lesson:** check where a console error comes from (the file name in the stack) before treating it as an app bug.
- **Guarded by:** nothing (development tool only).

## ISS-033: Side requests can slow down or switch off a source's main request

- **Symptom:** found while reviewing the Infrastructure project. Nothing has failed yet.
- **Root cause:** a "side request" here means an extra call a provider makes to decorate its main data. CheapShark
  loads the store list next to the deal search. Steam asks for a player count for each game it found. These side
  requests go through the same `HttpClient` as the main request, so they share its resilience pipeline:
  - **Delay.** `CheapSharkProvider.FetchAsync` waits for the deals and the store list together (`Task.WhenAll`). If
    the store endpoint hangs, the deals wait too, up to `TotalTimeout` (15 seconds by default) with retries.
    `SteamProvider.FetchAsync` waits for every player count in the same way.
  - **Circuit breaker.** The circuit breaker counts every attempt on that client. A store endpoint that keeps
    answering 500 gives one success (deals) and three failures (stores, with two retries) per search. That is a
    75% failure rate, so after two searches the circuit opens and the deal search is blocked for 30 seconds too.
    The same happens if Steam starts answering 429 (too many requests) to the player count calls.
  - **No memory of the failure.** `HybridCache` never stores an exception, so every search tries the store list
    again.

  This goes against the "Bulkhead per source" pattern, which says CheapShark works without its store list.
- **Fix:** side requests now use their own named `HttpClient`, added by the new
  `ProviderRegistration.AddSideRequestClient`. That client gives each side request one try, with a short time limit
  (`SideRequestTimeout`, 2 seconds by default, in `CheapSharkOptions` and `SteamOptions`). It has its own circuit
  breaker, so a broken side endpoint is paused on its own. It still records its requests in the source's
  statistics. `CheapSharkProvider` and `SteamProvider` get the client from `IHttpClientFactory` by the name in
  their `SideRequestClientName` constant. See DEC-031.
- **Lesson:** a fallback inside the code ("carry on without store names") is not enough if the shared pipeline
  still lets the side request delay or block the main one. Check what the pipeline counts, not only what the code
  catches.
- **Guarded by:** `ProviderWiringTests.A_failing_store_list_neither_retries_nor_blocks_the_deals` and
  `Refused_player_counts_neither_retry_nor_block_the_store_search`. They build the sources with the real pipeline.
  With the old wiring, the first test fails with `BrokenCircuitException` from the deal search.

## ISS-034: A base address without a trailing slash loses part of its path

- **Symptom:** found while reviewing the Infrastructure project. Nothing has failed yet.
- **Root cause:** providers combine a base address with a relative path, for example `BaseUrl` plus `games?...`.
  .NET follows the web rules for this. If the base does not end with `/`, its last segment is replaced. So
  `https://api.rawg.io/api` plus `games` gives `https://api.rawg.io/games`, which returns 404. Every default
  value ends with `/`, but the options classes only check `[Required]`. A relative value such as
  `api.rawg.io/api/` is also accepted at start-up, and only fails when the first `HttpClient` is created.
- **Fix:** a new `[BaseAddress]` validation attribute (`Providers/BaseAddressAttribute.cs`) accepts only a full
  http or https address whose path ends with `/`. Every base address setting now has `[Required, BaseAddress]`, so
  `ValidateOnStart` stops the app with a clear message.
- **Lesson:** validate the shape of a setting, not only that it is present.
- **Guarded by:** `ExternalApiOptionsTests.Base_url_must_be_a_full_web_address_ending_with_a_slash`.

## ISS-035: The IGDB attempt time limit also limits the Twitch token request

- **Symptom:** found while reviewing the Infrastructure project. Nothing has failed yet.
- **Root cause:** `TwitchAuthenticationHandler` runs inside IGDB's resilience pipeline. When it needs a new token,
  it calls `TwitchAccessTokenProvider.GetTokenAsync` with the cancellation token of the current IGDB attempt. That
  attempt ends after IGDB's `AttemptTimeout` (5 seconds by default). The Twitch client has its own pipeline, with
  `TwitchAuth:Timeout` per attempt (5 seconds) and up to three attempts (15 seconds in total). The outer 5 seconds
  always win, so the Twitch retries can never run in full. A slow token answer also leaves less time for the IGDB
  call itself in the same attempt. Nothing checks how the two settings relate.
- **Fix:**
  - The Twitch token client now makes one try with the `TwitchAuth:Timeout` time limit, and no retries of its own.
    IGDB's own retries already repeat the whole attempt, token request included.
  - A new start-up check, `TwitchTimeoutFitsAttemptCheck<TOptions>`, refuses a `TwitchAuth:Timeout` that is not
    shorter than the `AttemptTimeout` of a source that signs in with Twitch. `AddTwitchAuthentication` now takes
    that source's options type, for example `AddTwitchAuthentication<IgdbOptions>(configuration)`.
  - The default `TwitchAuth:Timeout` is now 2 seconds (it was 5, the same as IGDB's attempt).

  See DEC-032.
- **Lesson:** when one resilience pipeline runs inside another, the outer time limit is the real one.
- **Guarded by:** `ProviderWiringTests.Start_up_refuses_a_token_time_limit_that_does_not_fit_in_an_attempt`,
  `Default_token_time_limit_fits_in_an_attempt` and `A_failed_token_request_is_tried_once`.
