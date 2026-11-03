# Changelog

## 2.0.0-rc.1

Release candidate of 2.0.0: a major update with **breaking changes**. Read [Migrating to 2.0](README.md#migrating-to-20) before upgrading.

```
dotnet add package Mafe.RestClient --prerelease
dotnet add package Mafe.RestClient.Extensions.DependencyInjection --prerelease
```

### Platforms
- Target frameworks: `net10.0`, `net8.0`, `netstandard2.1`, `netstandard2.0` (.NET Framework 4.6.2+ through `netstandard2.0`).
- Removed the out of support targets and Newtonsoft.Json: JSON is always System.Text.Json.

### Security
- Removed the global `ServicePointManager` callback that, on .NET Framework/.NET Standard, accepted **every** TLS certificate
  for the whole process as soon as a `RestBuilder` was created. `CertificateValidation(...)` now affects only the requests of its builder.

### New
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

### Changed (breaking)
- `RestResult<T>` moved from `RestClient.Generic` to `RestClient`.
- `*AsByteArray*`, `*AsStream*`, `Download`, `CustomCall`, `Get(url)` replaced by `Get<byte[]>()`, `Get<Stream>()`, `SendAsync<T>(method)`, `Url(url).Get()`.
- All the `[Obsolete]` members have been removed.
- The builder is immutable: derived builders no longer share commands, parameters and properties.
- JSON uses `JsonSerializerDefaults.Web` by default (camelCase, case-insensitive).
- Query string keys and values are url-encoded and formatted with the invariant culture.
- Headers and authentication are set on the request message; multiple `Header(...)` calls are combined.
- Timeouts produce a `TimeoutException` (`IsTimedOut`); the timeout applies to every attempt.
- Deserialization errors keep the HTTP response (status code, headers) and report the error in `RestResult.Exception`.
- `StatusCode` is `0` and `Version` is `null` when no response has been received.

### Fixed
- Synchronous methods could deadlock in UI and classic ASP.NET synchronization contexts.
- `Xml(readerOptions, writerOptions)` swapped the reader and writer settings.
- `RestResult.Version`, `ReasonPhrase` and `Dispose` threw when no response had been received.
- After the first refresh token, the builder never refreshed the token again.
- `Parameter(RestParameter, ...)` threw when the key already existed.
- `IRestResult.Content` was always null.

## 1.9.5
- Target .NET 8.0 (net8.0)
- Upgrade all dependences

## 1.9.3
- Target .NET 7.0 (net7.0)
- Upgrade all dependences
