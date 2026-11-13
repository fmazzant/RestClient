# Changelog

All notable changes to the `Mafesoft.RestClient*` packages (`Mafe.RestClient*` up to 2.0.0) are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- `HttpVersion(Version?)` builder method: sets the HTTP version of the requests (HTTP/2, HTTP/3), also when an
  external `HttpClient` is used; `RestResult.Version` reports the negotiated version.
  ([#35](https://github.com/fmazzant/RestClient/issues/35))
- `HttpVersion(Version, HttpVersionPolicy)` builder method (.NET 8+ only): sets the version and how it is negotiated,
  e.g. HTTP/3 with fallback (`RequestVersionOrLower`) or HTTP/2 without TLS (`RequestVersionExact`).
  ([#35](https://github.com/fmazzant/RestClient/issues/35))

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
