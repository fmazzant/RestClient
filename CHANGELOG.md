# Changelog

All notable changes to the `Mafesoft.RestClient*` packages (`Mafe.RestClient*` up to 2.0.0) are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [3.1.1] - 2026-11-14

No changes to the library code: same features as 3.1.0.

```
dotnet add package Mafesoft.RestClient --version 3.1.1
dotnet add package Mafesoft.RestClient.Extensions.DependencyInjection --version 3.1.1
```

### Changed

- Build: Central Package Management (`Directory.Packages.props`) and `global.json` with the .NET 10 SDK.
  ([#23](https://github.com/fmazzant/RestClient/issues/23))
- Tests: XML and custom serializers, upload/download progress, cancellation and timeouts, certificate validation,
  form and multipart edge cases. ([#25](https://github.com/fmazzant/RestClient/issues/25))
- NuGet workflow: when the GitHub release is created by hand, the packages are attached to it instead of failing.
- README: Support section with a PayPal donation link.

## [3.1.0] - 2026-11-13

New features, no breaking changes: HTTP/2 and HTTP/3, proxy, cookies, URL templates, asynchronous token provider,
Problem Details, metrics, streaming of JSON arrays and Server-Sent Events.

```
dotnet add package Mafesoft.RestClient --version 3.1.0
dotnet add package Mafesoft.RestClient.Extensions.DependencyInjection --version 3.1.0
```

### Added

- `HttpVersion(Version?)` builder method: sets the HTTP version of the requests (HTTP/2, HTTP/3), also when an
  external `HttpClient` is used; `RestResult.Version` reports the negotiated version.
  ([#35](https://github.com/fmazzant/RestClient/issues/35))
- `HttpVersion(Version, HttpVersionPolicy)` builder method (.NET 8+ only): sets the version and how it is negotiated,
  e.g. HTTP/3 with fallback (`RequestVersionOrLower`) or HTTP/2 without TLS (`RequestVersionExact`).
  ([#35](https://github.com/fmazzant/RestClient/issues/35))
- `Proxy(IWebProxy?)` builder method: sends the requests through a proxy (e.g. a `WebProxy`), or directly to the server
  with `Proxy(null)`, without passing your own `HttpClient`. The proxy is part of the pooled handler configuration;
  it is not applied to an external `HttpClient`. ([#58](https://github.com/fmazzant/RestClient/issues/58))
- `SystemProxy()` builder method: goes back to the system proxy settings, the default.
  ([#58](https://github.com/fmazzant/RestClient/issues/58))
- `Cookies(CookieContainer?)` builder method: stores the `Set-Cookie` of the responses (redirects included) in the
  container and sends the cookies back with the next requests, e.g. for a cookie-based session. The container belongs
  to the builder (the derived builders share it), the pooled handlers keep `UseCookies = false`; when a container is set
  RestClient follows the redirects itself. It is not applied to an external `HttpClient`.
  ([#59](https://github.com/fmazzant/RestClient/issues/59))
- URL templates: `Route(string, object?)` builder method sets the value of a `{name}` placeholder of `Url(...)` or of a
  `Command(...)` segment, e.g. `Url("https://api/users/{id}").Route("id", 42)`. Values are formatted with the invariant
  culture and url-encoded as a single segment; a repeated placeholder gets the same value; a missing value is reported
  as `InvalidOperationException` without sending the request; values without a placeholder are ignored; `{{` and `}}`
  are literal braces. The url is a template only when at least one `Route` value is set: without `Route` it is sent
  unchanged, as before.
  ([#31](https://github.com/fmazzant/RestClient/issues/31))
- Asynchronous token provider: `BearerToken(Func<CancellationToken, Task<string>>?)` and
  `Authentication(Func<CancellationToken, Task<AuthenticationHeaderValue>>?)` builder methods, e.g.
  `BearerToken(async ct => await tokens.GetTokenAsync(ct))`, for tokens from MSAL, a cache or a secret store without
  `.Result`. The func is invoked for every attempt (first request, retries, the request sent again after a refresh
  token) with the cancellation token of the attempt; an exception thrown by it is reported by the result. With
  Dependency Injection, use the `AddRestClient` overloads that receive the `IServiceProvider`. The existing
  `Authentication` overloads are unchanged. ([#27](https://github.com/fmazzant/RestClient/issues/27))
- Problem Details (RFC 9457): `RestResult.Problem` reads the `application/problem+json` body of an error response as a
  `RestProblemDetails` (type, title, status, detail, instance, validation `Errors` and `Extensions` as `JsonElement`),
  without reflection and without ASP.NET Core; it is `null` for a success response, another content type or a body that
  is not a JSON object. `RestResult.ErrorAs<T>()` reads the error body as `string`, `byte[]`, `Stream` or any type
  deserialized with the configured serializer (use a `JsonSerializerContext` for Native AOT). The error body is read
  once and only when they are used; success responses are not affected. For a `Stream` result the error body is read
  from `Content` on first use and `Content` is replaced with a stream over the same bytes.
  ([#26](https://github.com/fmazzant/RestClient/issues/26))
- Metrics (`System.Diagnostics.Metrics`): the `Meter` `Mafesoft.RestClient` (`RestClientTelemetry.MeterName`, the
  same name as the `ActivitySource`) records `restclient.request.duration` (histogram in seconds, one measurement per
  request, retries and refresh token included), `restclient.retries` and `restclient.refresh_token` (counters), with
  the tags `http.request.method`, `http.response.status_code`, `error.type` and `restclient.client.name`. Add it to
  OpenTelemetry with `AddMeter("Mafesoft.RestClient")`; traces and logs are unchanged.
  ([#29](https://github.com/fmazzant/RestClient/issues/29))
- `ClientName(string?)` builder method: the logical name of the client, used as the `restclient.client.name` tag on
  the metrics; `IRestClientFactory` / `AddRestClient` set it to the name of the client, `null` removes it.
  ([#29](https://github.com/fmazzant/RestClient/issues/29))
- JSON arrays streamed as `IAsyncEnumerable<T>`: `GetAsyncEnumerable<T>()`, `PostAsyncEnumerable<T>()` and
  `SendAsyncEnumerable<T>(HttpMethod)` builder methods return the items while they are downloaded
  (`JsonSerializer.DeserializeAsyncEnumerable`, with the `JsonTypeInfo<T>` of the configured `JsonSerializerContext`
  for Native AOT), without buffering the body. The request is sent when the enumeration starts; headers,
  authentication, cookies, timeout per attempt, retry and refresh token apply until the headers of a successful
  response. Errors are thrown when the enumeration starts: the `HttpRequestException` of `EnsureSuccessStatusCode()`
  for an error status code or a failed request, `OperationCanceledException` for a cancellation. The response is
  released when the enumeration ends (also with `break`); the activity and the duration metric last until then.
  `Xml()` and custom serializers throw `NotSupportedException`. The existing methods are unchanged.
  ([#30](https://github.com/fmazzant/RestClient/issues/30))
- Server-Sent Events (`text/event-stream`): `GetEventsAsync<T>()`, `PostEventsAsync<T>()` and
  `SendEventsAsync<T>(HttpMethod)` builder methods return the events as `IAsyncEnumerable<SseItem<T>>` while they are
  received (`System.Net.ServerSentEvents.SseParser`), with the data deserialized with the JSON configuration of the
  builder (the `JsonTypeInfo<T>` of the configured `JsonSerializerContext` for Native AOT) and the event type, id and
  reconnection interval of every event. An overload with a `filter` (event type, data) skips the events that do not
  carry `T`, e.g. the `data: [DONE]` sentinel of LLM APIs; `GetEventsAsync()`, `PostEventsAsync()` and
  `SendEventsAsync(HttpMethod)` return the data as a string. `Accept: text/event-stream` is sent unless an `Accept`
  header is set. Same pipeline, deferred execution, exceptions, events and diagnostics as `GetAsyncEnumerable<T>()`;
  the `Last-Event-ID` reconnection is not automatic. On `net8.0`, `netstandard2.1` and `netstandard2.0` the package now
  depends on `System.Net.ServerSentEvents` (part of .NET 9+, so no new dependency on `net10.0`).
  ([#34](https://github.com/fmazzant/RestClient/issues/34))

## [3.0.0] - 2026-11-11

The `Mafesoft.*` release: the packages and the namespaces have a new name and a few APIs changed.
Read [Migrating to 3.0](README.md#migrating-to-30) before upgrading.

```
dotnet add package Mafesoft.RestClient
dotnet add package Mafesoft.RestClient.Extensions.DependencyInjection
```

### Changed

- **Breaking:** packages and assemblies renamed: `Mafe.RestClient` → `Mafesoft.RestClient`,
  `Mafe.RestClient.Extensions.DependencyInjection` → `Mafesoft.RestClient.Extensions.DependencyInjection`.
  ([#15](https://github.com/fmazzant/RestClient/issues/15))
- **Breaking:** root namespace renamed: `RestClient` → `Mafesoft.RestClient`, and the same for the sub-namespaces
  (`Mafesoft.RestClient.Http`, `Mafesoft.RestClient.Serialization`, `.Json`, `.Xml`). `IRestClientFactory` is now in
  `Mafesoft.RestClient`. ([#15](https://github.com/fmazzant/RestClient/issues/15))
- **Breaking:** `ActivitySource` renamed: `Mafe.RestClient` → `Mafesoft.RestClient`; logger category of the builders
  created by `IRestClientFactory`: `Mafe.RestClient.{name}` → `Mafesoft.RestClient.{name}`.
  ([#15](https://github.com/fmazzant/RestClient/issues/15))
- **Breaking:** `RefreshToken(Func<CancellationToken, Task<RestResult>>)` replaces `RefreshToken(bool)` +
  `RefreshTokenInvoke(...)`: one call enables the feature, the func receives the `CancellationToken` of the request and
  there is no more synchronous (sync-over-async) refresh func. `RefreshToken(null)` disables it.
  ([#17](https://github.com/fmazzant/RestClient/issues/17))
- **Breaking:** `RestProperties` is read-only (sealed, no public constructor, setters and `Clone()`); use the builder
  methods to configure the connection. ([#17](https://github.com/fmazzant/RestClient/issues/17))
- Each package shows its own short readme on nuget.org; the release notes of the packages link to this file.
  ([#19](https://github.com/fmazzant/RestClient/issues/19))
- README rewritten: why Mafesoft.RestClient, comparison with HttpClient, RestSharp, Refit and Flurl, quick start,
  reference grouped by topic; every snippet compiles against 3.0.0. ([#21](https://github.com/fmazzant/RestClient/issues/21))
- New samples: console, ASP.NET Core (typed client, resilience, OpenTelemetry) and Native AOT.
  ([#20](https://github.com/fmazzant/RestClient/issues/20))

### Added

- `CertificateOption(ClientCertificateOption)` builder method. ([#17](https://github.com/fmazzant/RestClient/issues/17))
- Package icon. ([#18](https://github.com/fmazzant/RestClient/issues/18))
- Pushing a `v*` tag also creates the GitHub Release, with the notes of the matching section of this file.
  ([#16](https://github.com/fmazzant/RestClient/issues/16))
- The 2.0.0 public API is frozen (`PublicAPI.Shipped.txt`) and the packages are validated against the 2.0.0 release:
  every API change is visible in the pull requests. ([#16](https://github.com/fmazzant/RestClient/issues/16))
- `SECURITY.md` and `CONTRIBUTING.md`. ([#19](https://github.com/fmazzant/RestClient/issues/19))

### Deprecated

- The `Mafe.RestClient` and `Mafe.RestClient.Extensions.DependencyInjection` packages: they will not receive new
  versions. ([#15](https://github.com/fmazzant/RestClient/issues/15))

### Removed

- **Breaking:** `RefreshToken(bool)`, `RefreshTokenInvoke(Func<RestResult>)`, `RefreshTokenInvoke(Func<Task<RestResult>>)`.
  ([#17](https://github.com/fmazzant/RestClient/issues/17))
- **Breaking:** `Rest.Build(RestProperties)` and `Rest.Build(Action<RestProperties>)`.
  ([#17](https://github.com/fmazzant/RestClient/issues/17))

### Fixed

- The OpenTelemetry example of the README exported no RestClient activity in ASP.NET Core: it needs
  `AddAspNetCoreInstrumentation()`. ([#37](https://github.com/fmazzant/RestClient/issues/37))

## [2.0.0] - 2026-11-04

A major update with **breaking changes**. Read [Migrating to 2.0](README.md#migrating-to-20) before upgrading.

```
dotnet add package Mafe.RestClient
dotnet add package Mafe.RestClient.Extensions.DependencyInjection
```

### Added

- Pooled HTTP handlers: TCP/TLS connections are reused across requests (no socket exhaustion); recycled every 2 minutes on .NET 8+.
- `Retry(...)`: retries transient errors with exponential backoff, jitter and `Retry-After`; idempotent methods only by default.
- Refresh token de-duplicated: concurrent 401 responses trigger a single refresh.
- `multipart/form-data` uploads with `Multipart(m => m.Add(...).AddFile(...))`.
- Real streaming for `Stream` results; successful JSON responses are deserialized directly from the network stream.
- `SendAsync<T>(HttpMethod)`, `Head`, `Options`; `T` can be `string`, `byte[]`, `Stream` or a DTO.
- Repeated query string keys (`?id=1&id=2`).
- Native AOT and trimming: `Json(JsonSerializerContext)`; the library is annotated and verified by a Native AOT smoke test in CI.
- `Logger(ILogger)` logging (query string redacted) and OpenTelemetry tracing through the `ActivitySource` `Mafe.RestClient`.
- `Rest.Build(HttpClient)` / `UseHttpClient(HttpClient)`.
- New package **Mafe.RestClient.Extensions.DependencyInjection**: `AddRestClient(name, ...)`, `AddRestClient<TClient>(...)`,
  `IRestClientFactory`, on top of `IHttpClientFactory` (works with `AddStandardResilienceHandler()`).
- `RestResult.EnsureSuccessStatusCode()`, `IsTimedOut`, `RetryCount`; `Header(name, value)`; `Command(long)`.
- Nullable reference types annotations.

### Changed

- Target frameworks: `net10.0`, `net8.0`, `netstandard2.1`, `netstandard2.0` (.NET Framework 4.6.2+ through `netstandard2.0`).
- **Breaking:** `RestResult<T>` moved from `RestClient.Generic` to `RestClient`.
- **Breaking:** `*AsByteArray*`, `*AsStream*`, `Download`, `CustomCall`, `Get(url)` replaced by `Get<byte[]>()`,
  `Get<Stream>()`, `SendAsync<T>(method)`, `Url(url).Get()`.
- **Breaking:** the builder is immutable: derived builders no longer share commands, parameters and properties.
- **Breaking:** JSON uses `JsonSerializerDefaults.Web` by default (camelCase, case-insensitive).
- **Breaking:** query string keys and values are url-encoded and formatted with the invariant culture.
- Headers and authentication are set on the request message; multiple `Header(...)` calls are combined.
- Timeouts produce a `TimeoutException` (`IsTimedOut`); the timeout applies to every attempt.
- Deserialization errors keep the HTTP response (status code, headers) and report the error in `RestResult.Exception`.
- `StatusCode` is `0` and `Version` is `null` when no response has been received.

### Removed

- **Breaking:** the out of support target frameworks (`net7.0`, `net6.0`, `netcoreapp3.1`, `netstandard1.3`, `net45x`-`net48`).
- **Breaking:** Newtonsoft.Json: JSON is always System.Text.Json.
- **Breaking:** all the `[Obsolete]` members.

### Fixed

- Synchronous methods could deadlock in UI and classic ASP.NET synchronization contexts.
- `Xml(readerOptions, writerOptions)` swapped the reader and writer settings.
- `RestResult.Version`, `ReasonPhrase` and `Dispose` threw when no response had been received.
- After the first refresh token, the builder never refreshed the token again.
- `Parameter(RestParameter, ...)` threw when the key already existed.
- `IRestResult.Content` was always null.

### Security

- Removed the global `ServicePointManager` callback that, on .NET Framework/.NET Standard, accepted **every** TLS
  certificate for the whole process as soon as a `RestBuilder` was created. `CertificateValidation(...)` now affects
  only the requests of its builder.

## [1.9.5] - 2024-04-06

### Changed

- Target .NET 8.0 (`net8.0`).
- Upgraded all the dependencies.

## [1.9.4]

### Changed

- Upgraded all the dependencies.

## [1.9.3] - 2022-11-30

### Changed

- Target .NET 7.0 (`net7.0`).
- Upgraded all the dependencies.

## [1.9.2] - 2022-07-02

### Changed

- Upgraded all the dependencies.

## [1.9.1] - 2022-06-23

### Security

- Improper Handling of Exceptional Conditions in Newtonsoft.Json ([#1](https://github.com/fmazzant/RestClient/issues/1)).

## [1.9.0] - 2022-04-27

### Changed

- Target .NET 6.0 (`net6.0`).
- Upgraded all the dependencies.

## [1.8.1] - 2022-04-27

### Changed

- Removed `.Result`.

### Removed

- Unsupported target frameworks.

## [1.8.0] - 2021-03-26

### Changed

- Target .NET 5.0 (`net5.0`).

### Removed

- Unsupported target frameworks.

## [1.7.7] - 2021-03-01

### Changed

- Managing fluent `HttpClient`.

## [1.7.6]

### Fixed

- Exception handling (hotfix).

## [1.7.5] - 2020-10-23

### Fixed

- Hotfix.

## [1.7.4] - 2020-10-23

### Fixed

- Parameters (hotfix).

## [1.7.3] - 2020-10-19

### Added

- JSON and XML settings for the serialization and deserialization of an object.

## [1.7.2] - 2020-10-19

### Added

- Target .NET Core 3.1 (`netcoreapp3.1`).

## [1.7.1] - 2020-09-25

### Added

- Target .NET Core 3.0, 2.2, 2.1, 2.0 (`netcoreapp3.0`, `netcoreapp2.2`, `netcoreapp2.1`, `netcoreapp2.0`).
- Target .NET Framework 4.7, 4.7.1, 4.7.2, 4.8 (`net47`, `net471`, `net472`, `net48`).

## [1.7.0] - 2020-07-30

### Changed

- Optimization and refactoring of `HttpClientCompressionHandler`.

## [1.6.0] - 2020-07-18

### Added

- Target .NET Framework 4.5, 4.5.1, 4.5.2, 4.6, 4.6.1, 4.6.2 (`net45`, `net451`, `net452`, `net46`, `net461`, `net462`).

## [1.5.3] - 2020-07-09

### Added

- gzip compression.

### Changed

- Refactoring of `HttpContentStream`.

## [1.5.2] - 2020-07-05

### Added

- Target .NET Standard 1.3 (`netstandard1.3`).

## [1.5.1] - 2020-07-04

### Added

- Target .NET Standard 2.1 and 2.0 (`netstandard2.1`, `netstandard2.0`).

## [1.5.0] - 2020-07-04

### Changed

- Refactoring of the builder.

### Fixed

- Parameters and general bug fixes.

## [1.4.0] - 2020-06-26

### Added

- `OnPreCompleted`, `OnCompleted` and `OnPreviewContentAsString`.
- `BufferSize`, with a check on the buffer size value.

### Changed

- Refactoring of some methods.
