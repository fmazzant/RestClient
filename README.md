# RestClient

[![NuGet](https://img.shields.io/nuget/v/Mafesoft.RestClient?style=flat-square&label=nuget)](https://www.nuget.org/packages/Mafesoft.RestClient)
[![Downloads](https://img.shields.io/nuget/dt/Mafesoft.RestClient?style=flat-square&label=downloads)](https://www.nuget.org/packages/Mafesoft.RestClient)
[![Build](https://img.shields.io/github/actions/workflow/status/fmazzant/RestClient/ci.yml?branch=main&style=flat-square&label=build)](https://github.com/fmazzant/RestClient/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue?style=flat-square)](LICENSE)
[![CodeQL](https://img.shields.io/github/actions/workflow/status/fmazzant/RestClient/codeql.yml?branch=main&style=flat-square&label=codeql)](https://github.com/fmazzant/RestClient/actions/workflows/codeql.yml)
[![.NET](https://img.shields.io/badge/.NET-10%20%7C%208%20%7C%20Standard%202.1%20%7C%20Standard%202.0-512BD4?style=flat-square)](#packages)

[![Mafe.RestClient downloads](https://img.shields.io/nuget/dt/Mafe.RestClient?style=flat-square&label=Mafe.RestClient%20downloads)](https://www.nuget.org/packages/Mafe.RestClient)

**Mafesoft.RestClient** is a fluent and immutable builder over `HttpClient` to consume REST services. You configure a
root once — URL, authentication, serialization, retry — and derive every request from it, safely, also from many
threads. The result is a `RestResult<T>` (status code, headers, content, error) instead of an exception. The same API
works from `netstandard2.0` (.NET Framework 4.6.2+, Xamarin, Unity) to `net10.0`.

> **Version 3.0.0: the packages are now published as `Mafesoft.*`** and all the namespaces start with `Mafesoft.`.
> If you are using the `Mafe.RestClient*` packages, read [Migrating to 3.0](#migrating-to-30).

The changes of every release are listed in the [CHANGELOG](CHANGELOG.md). To report a security problem, read the
[security policy](SECURITY.md).

## Table of contents

- [Why Mafesoft.RestClient](#why-mafesoftrestclient)
- [Comparison with the alternatives](#comparison-with-the-alternatives)
- [Features](#features)
- [Packages](#packages)
- [Installation](#installation)
- [Quick start](#quick-start)
- [Requests](#requests)
- [Content](#content)
- [Authentication](#authentication)
- [Resilience](#resilience)
- [Diagnostics](#diagnostics)
- [Events](#events)
- [Dependency Injection](#dependency-injection)
- [Samples](#samples)
- [Migrating to 3.0](#migrating-to-30)
- [Build and test](#build-and-test)
- [Contributing](#contributing)
- [License](#license)

## Why Mafesoft.RestClient

`HttpClient` with `System.Net.Http.Json` is enough for a single call. As soon as an application talks to a real API,
the same code is written again and again: base address and headers, a retry loop, the refresh of an expired token,
error handling, progress bars, logging. Mafesoft.RestClient puts that code in one place:

- **Immutable builder, safe to share.** Every call returns a new builder, so a configured root can be stored in a field
  and reused by concurrent requests. The HTTP handlers are pooled (or come from `IHttpClientFactory`): no socket
  exhaustion, no new TLS handshake for every call.
- **Resilience built in.** Retry with exponential backoff, jitter and `Retry-After`; refresh token on `401`, run
  **once** for all the concurrent requests that received it.
- **Results, not exceptions.** `RestResult<T>` reports status code, headers, content, timeout, retry count and error;
  call `EnsureSuccessStatusCode()` when you prefer an exception.
- **Real-world content.** Upload/download progress, real streaming for large files, `multipart/form-data`,
  form-url-encoded, JSON or XML.
- **Ready for production .NET.** Native AOT and trimming (verified in CI), `ILogger` logging and OpenTelemetry tracing
  out of the box, `IHttpClientFactory` integration.
- **One API everywhere.** The same code runs on .NET Framework 4.6.2+, Xamarin/Unity (`netstandard2.0`) and .NET 10.

## Comparison with the alternatives

| Feature | Mafesoft.RestClient | HttpClient + System.Net.Http.Json | RestSharp | Refit | Flurl |
|---|---|---|---|---|---|
| Style | fluent builder | imperative | request object | interface + attributes | fluent URL |
| Immutable, thread-safe builder | ✅ | n/a | ❌ | n/a | ⚠️ shared client, mutable request |
| Built-in retry with backoff / `Retry-After` | ✅ | ❌ (Polly / Resilience package) | ❌ | ✅ `HttpRetryHandler` (.NET 8+) | ❌ |
| Refresh token on 401, de-duplicated | ✅ | ❌ | ⚠️ OAuth2 authenticators, expiry-based | ❌ | ❌ |
| Result object instead of exceptions | ✅ | ⚠️ `SendAsync` only | ✅ `ExecuteAsync` | ⚠️ `IApiResponse<T>` | ⚠️ `AllowAnyHttpStatus()` |
| Upload / download progress | ✅ | ❌ | ❌ | ❌ | ❌ |
| `multipart/form-data` | ✅ | ⚠️ manual | ✅ | ✅ | ✅ |
| Native AOT / trimming | ✅ (verified in CI) | ✅ | ⚠️ not annotated | ✅ generated clients | ⚠️ not annotated |
| `IHttpClientFactory` integration | ✅ | ✅ | ✅ DI package | ✅ | ⚠️ manual |
| `ILogger` / OpenTelemetry of its own | ✅ | ✅ built-in spans and metrics | ❌ | ❌ | ❌ |
| Problem Details (RFC 9457) | ✅ `result.Problem`, `ErrorAs<T>()` | ⚠️ manual | ❌ | ✅ `ValidationApiException` | ❌ |
| Typed client from an interface | ❌ | ❌ | ❌ | ✅ | ❌ |
| Test helpers | ⚠️ write a fake `HttpMessageHandler` | ❌ | ❌ | ✅ `Refit.Testing` | ✅ `HttpTest` |
| `netstandard2.0` (.NET Framework 4.6.2+, Xamarin, Unity) | ✅ | ✅ | ✅ | ⚠️ `net462`+ and `net8.0`+ only | ✅ |
| URL templates / path parameters (`users/{id}`) | ✅ `Route` (url-encoded, invariant culture) | ❌ | ✅ `AddUrlSegment` | ✅ route attributes | ⚠️ `AppendPathSegment`, no templates |
| OAuth1 / OAuth2 flows | ❌ bearer/basic header, async token provider and refresh token only | ❌ | ✅ authenticators | ❌ | ❌ |
| Cookies | ✅ `Cookies(CookieContainer)` | ✅ `CookieContainer` | ✅ `CookieContainer` | ⚠️ via the handler | ✅ `CookieJar` |
| Proxy | ✅ `Proxy` / `SystemProxy` | ✅ handler `Proxy` | ✅ `RestClientOptions.Proxy` | ⚠️ via the handler | ⚠️ via the handler |
| Newtonsoft.Json ready to use | ⚠️ write a custom serializer | ⚠️ extra package | ✅ `RestSharp.Serializers.NewtonsoftJson` | ✅ `Refit.Newtonsoft.Json` | ✅ `Flurl.Http.Newtonsoft` |
| Streaming a JSON array as `IAsyncEnumerable<T>` | ❌ planned ([#30](https://github.com/fmazzant/RestClient/issues/30)) | ✅ `GetFromJsonAsAsyncEnumerable` | ❔ | ❔ | ❔ |
| Community, documentation, Q&A | ❌ small | ✅ part of .NET | ✅ large | ✅ large | ✅ large |

✅ built in, ⚠️ partial or extra work, ❌ not available, ❔ not verified. Checked in November 2026 against .NET 10, RestSharp 114.1.0,
Refit 16.4.0 and Flurl.Http 4.0.2. All these libraries run on `HttpClient`, so they all get its spans and metrics:
the "of its own" row is about the telemetry added by the library. "Not annotated" means that the library does not
declare Native AOT compatibility, not that it was tested and fails.
The last rows list what the alternatives have and Mafesoft.RestClient does not (or only partially), so that the
comparison is not one-sided.

**When to choose something else**

- **Plain `HttpClient`** for one or two calls, or when you already have the `IHttpClientFactory` + resilience
  pipeline you want: no extra dependency.
- **Refit** if you prefer to describe the API as a C# interface and let a source generator write the client.
- **Flurl** if you mostly build URLs fluently and want its `HttpTest` to fake the HTTP calls in the tests.
- **RestSharp** if you already use it, or need its OAuth1/OAuth2 authenticators.

## Features

- Fluent, immutable and thread-safe builder (`Rest.Build()`)
- `Get`, `Post`, `Put`, `Patch`, `Delete`, `Head`, `Options` and any other method with `SendAsync<T>(HttpMethod)`
- Results as `string`, `byte[]`, `Stream` (not buffered) or a DTO
- JSON with System.Text.Json (source generated `JsonSerializerContext` for Native AOT), XML, custom serializers
- `multipart/form-data`, form-url-encoded, upload and download progress
- URL templates (`users/{id}`) with url-encoded, culture invariant route values
- Query string with url-encoding, invariant culture and repeated keys
- Bearer/basic authentication, asynchronous token provider (`BearerToken(async ct => ...)`), refresh token, `NetworkCredential`, per-builder certificate validation
- Retry with backoff and `Retry-After`, timeout per attempt, cancellation
- Problem Details (RFC 9457) and typed error bodies (`result.Problem`, `result.ErrorAs<T>()`)
- Pooled handlers, or your own `HttpClient` / `IHttpClientFactory`
- HTTP/2 and HTTP/3 (`HttpVersion`, with `HttpVersionPolicy` on .NET 8+)
- Proxy (`WebProxy`, system proxy or no proxy)
- Cookies (`CookieContainer` per builder, e.g. a cookie-based session)
- `ILogger` logging and OpenTelemetry tracing
- Events: `OnStart`, `OnPreCompleted`, `OnCompleted`, `OnException`, request/response preview

## Packages

<!-- markdownlint-disable MD013 -->
| Package | Target frameworks | NuGet | Downloads | Legacy downloads |
|---|---|---|---|---|
| [Mafesoft.RestClient](src/RestClient/README.md) | `net10.0`, `net8.0`, `netstandard2.1`, `netstandard2.0` | [![NuGet](https://img.shields.io/nuget/v/Mafesoft.RestClient?style=flat-square&label=nuget)](https://www.nuget.org/packages/Mafesoft.RestClient) | [![Downloads](https://img.shields.io/nuget/dt/Mafesoft.RestClient?style=flat-square&label=downloads)](https://www.nuget.org/packages/Mafesoft.RestClient) | [![Mafe.RestClient downloads](https://img.shields.io/nuget/dt/Mafe.RestClient?style=flat-square&label=Mafe.RestClient)](https://www.nuget.org/packages/Mafe.RestClient) |
| [Mafesoft.RestClient.Extensions.DependencyInjection](src/RestClient.Extensions.DependencyInjection/README.md) | `net10.0`, `net8.0`, `netstandard2.0` | [![NuGet](https://img.shields.io/nuget/v/Mafesoft.RestClient.Extensions.DependencyInjection?style=flat-square&label=nuget)](https://www.nuget.org/packages/Mafesoft.RestClient.Extensions.DependencyInjection) | [![Downloads](https://img.shields.io/nuget/dt/Mafesoft.RestClient.Extensions.DependencyInjection?style=flat-square&label=downloads)](https://www.nuget.org/packages/Mafesoft.RestClient.Extensions.DependencyInjection) | [![Mafe.RestClient.Extensions.DependencyInjection downloads](https://img.shields.io/nuget/dt/Mafe.RestClient.Extensions.DependencyInjection?style=flat-square&label=Mafe.RestClient.Extensions.DependencyInjection)](https://www.nuget.org/packages/Mafe.RestClient.Extensions.DependencyInjection) |
<!-- markdownlint-enable MD013 -->

## Installation

```
dotnet add package Mafesoft.RestClient
```

With dependency injection and `IHttpClientFactory`:

```
dotnet add package Mafesoft.RestClient.Extensions.DependencyInjection
```

## Quick start

```c#
using Mafesoft.RestClient;

// configure the root once: every call returns a new builder, the root never changes
RestBuilder api = Rest.Build()
    .Url("https://api.example.com")
    .Authentication("Bearer", token)
    .Retry(3);

// GET a DTO
RestResult<User> user = await api.Command("users").Command(42).GetAsync<User>();
if (user.IsSuccessStatusCode)
{
    Console.WriteLine(user.Content!.Name);
}
else
{
    Console.WriteLine($"{(int)user.StatusCode} {user.Exception?.Message}");
}

// POST a DTO as JSON
RestResult<User> created = await api.Command("users")
    .Payload(new User { Name = "Ada" })
    .PostAsync<User>();
```

## Requests

### Builder

`Rest.Build()` creates a `RestBuilder`. Every method returns a **new, independent** builder: a configured root can be
stored and reused as the base of many requests, also from different threads.

```c#
RestBuilder users = Rest.Build().Url("https://api.example.com").Command("users");

RestResult<User> user1 = await users.Command(1).GetAsync<User>();   // https://api.example.com/users/1
RestResult<User> user2 = await users.Command(2).GetAsync<User>();   // https://api.example.com/users/2
RestResult<List<User>> all = await users.GetAsync<List<User>>();    // https://api.example.com/users
```

`Command(...)` appends a path segment (`string`, `int`, `long` or `Guid`); for path parameters see
[URL templates](#url-templates).
`RestBuilder.Properties` returns the connection properties as a read-only `RestProperties`
(`EndPoint`, `Timeout`, `BufferSize`, `CertificateOption`).

### HTTP methods

`Get`, `Post`, `Put`, `Patch`, `Delete`, `Head` and `Options`, each one with an `Async` version. Any other method can be
sent with `SendAsync<T>(HttpMethod)`:

```c#
RestResult<string> report = await api.Command("calendar").SendAsync<string>(new HttpMethod("REPORT"));
```

> Prefer the async methods. The synchronous ones (`Get()`, `Post()`, `Send()`, ...) block the calling thread until
> the response is received (sync-over-async): use them only where you cannot `await`.

### Response types

Every method has a non generic version, that returns the body as `string`, and a generic one. `T` can be:

| T | Content |
|---|---|
| `string` | the raw body |
| `byte[]` | the raw body |
| `Stream` | the body **not buffered**: it is read from the network while you read it. Dispose the result when done. |
| any other type | the body deserialized with the configured serializer (JSON by default) |

```c#
RestResult<User> user = await api.Command("users").Command(1).GetAsync<User>();
RestResult<byte[]> image = await api.Command("avatar.png").GetAsync<byte[]>();

using RestResult<Stream> file = await api.Command("big-file.zip").GetAsync<Stream>();
await file.Content!.CopyToAsync(destination);
```

An empty body (for example `204 No Content`) is a `null` content.

### URL templates

Write the path parameters as `{name}` placeholders and set their values with `Route`:

```c#
RestBuilder orders = Rest.Build().Url("https://api.example.com/users/{id}/orders/{orderId}");

RestResult<Order> order = await orders
    .Route("id", 42)
    .Route("orderId", "A/1")
    .GetAsync<Order>();   // https://api.example.com/users/42/orders/A%2F1
```

- The values are formatted with the invariant culture and url-encoded like the [query string](#query-string), as a
  single segment: `/`, `?`, `#`, spaces and non-ASCII characters are percent-encoded. Do not encode them yourself.
- The placeholders can be in `Url(...)` (path and query) and in `Command(...)`:
  `api.Command("users/{id}").Route("id", 42)`. They are not supported in the scheme, host and port.
  The query string parameters (`Parameter`) are never expanded.
- A placeholder used more than once gets the same value everywhere. The names are case-sensitive.
- When a placeholder has no value the request is not sent: the result reports an `InvalidOperationException`
  (as for a missing `Url`). `Route(name, null)` removes a value set before.
- A value without a placeholder is ignored, so a shared root can set a value (for example `{tenant}`) used only by
  some requests. Setting a value again replaces it; like every builder method it only affects the derived builders.
- `{{` and `}}` are literal braces; any other `{` must open a `{name}` placeholder.
- The url is a template only when the builder has at least one `Route` value: without `Route` (or after removing the
  last value) `Url` and `Command` are sent untouched, braces included, as in 3.0.

### Query string

```c#
RestResult<List<User>> users = await api
    .Command("users")
    .Parameter("role", "admin")
    .Parameter("page", 2)
    .GetAsync<List<User>>();   // /users?role=admin&page=2
```

Keys and values are url-encoded (`"a b&c"` becomes `a%20b%26c`) and formatted with the invariant culture (`1.5` is
always `1.5`, `true` is `true`, dates are ISO 8601): do not encode them yourself. Setting a key again replaces its
value. To repeat a key pass a collection, or add more parameters with the same key in one call:

```c#
RestBuilder byIds = api.Parameter("id", new[] { 1, 2, 3 });   // ?id=1&id=2&id=3
RestBuilder byTags = api.Parameter(p =>
{
    p.Add(new RestParameter("tag", "a"));
    p.Add(new RestParameter("tag", "b"));                       // ?tag=a&tag=b
});
```

### Headers

```c#
RestBuilder withHeaders = api
    .Header("X-Correlation-Id", Guid.NewGuid().ToString())
    .Header(h => h.Accept.ParseAdd("application/json"));
```

Headers are set on the request message (not on `HttpClient.DefaultRequestHeaders`); calling `Header` more than once
adds all the headers.

### Timeout and cancellation

The default timeout is 100 seconds and it applies to **every attempt** (see [Retry](#retry)). For `Stream` results it
covers the response headers, not the reading of the stream.

```c#
using var cancellation = new CancellationTokenSource();

RestResult<User> result = await api
    .Timeout(TimeSpan.FromSeconds(10))      // or Timeout(Timeout.InfiniteTimeSpan)
    .Command("users").Command(1)
    .GetAsync<User>(cancellation.Token);

bool timedOut = result.IsTimedOut;                  // TimeoutException in result.Exception
bool canceled = result.IsCancellationRequested;     // OperationCanceledException in result.Exception
```

### Connections and HttpClient

RestClient keeps a pool of HTTP handlers, one for each combination of compression, credentials, certificate option,
certificate validation callback and proxy. The TCP/TLS connections are reused by all the requests and, on .NET 8+, recycled
every 2 minutes so that DNS changes are honored. The pooled handlers never store cookies: use `Cookies` (see
[Cookies](#cookies)) to keep them in a container of the builder.

You can send the requests with your own `HttpClient`, for example one created by `IHttpClientFactory`
(see also [Dependency Injection](#dependency-injection)):

```c#
HttpClient httpClient = httpClientFactory.CreateClient("api");

RestResult<List<User>> users = await Rest.Build(httpClient)   // or Rest.Build().UseHttpClient(httpClient)
    .Url("https://api.example.com")
    .Command("users")
    .GetAsync<List<User>>();
```

The external `HttpClient` is never disposed by RestClient. `NetworkCredential`, `CertificateValidation`,
`CertificateOption`, `EnableGZipCompression`, `Proxy`, `SystemProxy`, `Cookies` and `OnConfigureHttpClient` configure the `HttpClient` managed by
RestClient, so they are not applied to an external one: configure them on your handler. Headers, authentication,
HTTP version, timeout, retry, serialization, progress and all the events work in both cases.

### HTTP/2 and HTTP/3

The requests use HTTP/1.1 unless you choose another version with `HttpVersion`. The version is set on every request,
so it works also with your own `HttpClient`; `RestResult.Version` reports the version negotiated with the server.

```c#
// HTTP/2 over https: negotiated with the server (ALPN), HTTP/1.1 when the server does not support it
RestResult<User> user = await api
    .HttpVersion(HttpVersion.Version20)
    .Command("users").Command(1)
    .GetAsync<User>();
Version? negotiated = user.Version;

// .NET 8+: HTTP/3 with a fallback to HTTP/2 or HTTP/1.1
RestBuilder http3 = api.HttpVersion(HttpVersion.Version30, HttpVersionPolicy.RequestVersionOrLower);

// .NET 8+: HTTP/2 without TLS (h2c, prior knowledge), for example an internal service over http
RestBuilder h2c = Rest.Build()
    .Url("http://localhost:5000")
    .HttpVersion(HttpVersion.Version20, HttpVersionPolicy.RequestVersionExact);
```

- On .NET 8+ `HttpVersion(version)` uses the default policy `RequestVersionOrLower`: over `http://` it means HTTP/1.1,
  use `RequestVersionExact` or `RequestVersionOrHigher` for HTTP/2 without TLS. `HttpVersion(null)` goes back to the
  default.
- The `HttpVersionPolicy` overload exists only on .NET 8+. On `netstandard2.x` only the version is set: HTTP/2 works
  when the app runs on .NET Core 3.0+, while .NET Framework supports only HTTP/1.0 and HTTP/1.1.
- HTTP/3 needs .NET 8+ and QUIC support in the operating system (msquic on Windows 11 / Windows Server 2022+, libmsquic
  on Linux).

### Proxy

By default the requests use the system proxy settings (`HttpClient.DefaultProxy` on .NET, the environment variables
`HTTP_PROXY`/`HTTPS_PROXY`/`NO_PROXY` or the operating system settings). `Proxy` sends them through a proxy of your
choice, `Proxy(null)` sends them directly to the server and `SystemProxy` goes back to the default.

```c#
// create the proxy once (e.g. a static field) and reuse it: the same instance shares the pooled handler
IWebProxy corporateProxy = new WebProxy("http://proxy:8080")
{
    Credentials = CredentialCache.DefaultCredentials,
    BypassProxyOnLocal = true,
};

RestBuilder viaProxy = api.Proxy(corporateProxy);

// no proxy, ignoring the system settings
RestBuilder direct = api.Proxy(null);

// back to the system proxy (the default)
RestBuilder system = viaProxy.SystemProxy();
```

- The proxy is part of the pooled handler configuration (see [Connections and HttpClient](#connections-and-httpclient)):
  the same `IWebProxy` instance shares the handler and its connections, a new instance gets a new handler, so create
  the proxy once instead of for every request.
- The proxy is not applied to your own `HttpClient` (`UseHttpClient`): set `Proxy` on its handler.

### Cookies

By default cookies are neither stored nor sent. `Cookies` gives the builder a `CookieContainer`: the cookies of the
responses (`Set-Cookie`) are stored in it and sent back (`Cookie`) with the next requests, for example for an API with
a cookie-based session.

```c#
var cookies = new CookieContainer();
RestBuilder api = Rest.Build().Url("https://example.com").Cookies(cookies);

await api.Command("login").Payload(credentials).PostAsync();     // stores the Set-Cookie
RestResult<Order[]> orders = await api.Command("orders").GetAsync<Order[]>(); // sends the cookies

// stop storing and sending cookies
RestBuilder anonymous = api.Cookies(null);
```

- The container belongs to the builder, not to the pooled handler, so the handlers and their connections are still
  shared: builders with different containers never see each other's cookies, and two users of the same application
  get two separate sessions if each has its own container.
- The builder is immutable, but the container is an object: the builders derived from the one with `Cookies(container)`
  share the same container, which is the session. `CookieContainer` is thread-safe; create a new container (or call
  `Cookies` again) for a new session.
- The cookies set by the redirects are stored too: when a container is set, RestClient follows the redirects itself
  (with the same rules as `HttpClientHandler`: up to 50 redirects, `POST` becomes `GET` after 300/301/302 and every
  method except `HEAD` becomes `GET` after 303, method and content are kept for 307/308, the `Authorization` header
  is not sent to the new location, never from HTTPS to HTTP).
- The cookies are not applied to your own `HttpClient` (`UseHttpClient`): set `CookieContainer` on its handler.

## Content

### JSON

JSON is the default serializer, based on System.Text.Json. Without options it uses `JsonSerializerDefaults.Web`:
property names are written in camelCase and read case-insensitively.

```c#
RestResult<User> created = await api
    .Json(new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true })   // optional
    .Command("users")
    .Payload(new User { Name = "Ada" })
    .PostAsync<User>();
```

Successful JSON responses are deserialized directly from the network stream, without buffering the body as a string
(unless `OnPreviewContentResponseAsString` is used).

### Native AOT and trimming

The JSON serializer is compatible with Native AOT and trimming when it uses a source generated `JsonSerializerContext`:

```c#
[JsonSerializable(typeof(User))]
[JsonSerializable(typeof(List<User>))]
[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
public partial class AppJsonContext : JsonSerializerContext { }
```

```c#
RestResult<List<User>> users = await Rest.Build()
    .Url("https://api.example.com")
    .Json(AppJsonContext.Default)
    .Command("users")
    .GetAsync<List<User>>();
```

The library is annotated for trimming and verified by a Native AOT smoke test in CI. See the
[Native AOT sample](samples/RestClient.Samples.NativeAot).

### XML

`Xml()` uses `XmlSerializer`. The reader settings are used to read the response, the writer settings to write the
payload. `XmlSerializer` is not compatible with trimming: the method is annotated, so the compiler warns you.

```c#
RestResult<User> user = await api
    .Xml(new XmlReaderSettings { IgnoreWhitespace = true }, new XmlWriterSettings { Indent = true })
    .Command("users").Command(1)
    .GetAsync<User>();
```

### Custom serialization

Implement `ISerializerContent` (and optionally `IAsyncSerializerContent`, that adds `DeserializeObjectAsync(Stream, ...)`
to deserialize directly from the response stream):

```c#
public class CsvSerializer : ISerializerContent
{
    public string MediaTypeAsString => "text/csv";

    public object? DeserializeObject(string value, Type typeOf, object? options = null)
        => throw new NotImplementedException();

    public string SerializeObject(object? value, Type typeOf, object? options = null)
        => throw new NotImplementedException();
}
```

```c#
RestBuilder csv = api.CustomSerializer(new CsvSerializer(), options: null);
```

### Payload

`Payload(object)` sets the body of the request, serialized with the configured serializer:

```c#
RestResult<User> updated = await api
    .Command("users").Command(42)
    .Payload(new User { Name = "Ada Lovelace" })
    .PutAsync<User>();
```

`Payload`, `FormUrlEncoded` and `Multipart` replace each other.

### Form url encoded

```c#
RestResult<string> token = await api
    .Command("token")
    .FormUrlEncoded(new Dictionary<string, string>
    {
        ["grant_type"] = "client_credentials",
        ["client_id"] = "my-client"
    })
    .PostAsync();
```

`FormUrlEncoded(values => values.Add("key", "value"))` builds the dictionary in a callback.

### Multipart

```c#
RestResult<UploadResponse> upload = await api
    .Command("upload")
    .Multipart(m => m
        .Add("title", "My document")
        .AddFile("document", @"C:\docs\report.pdf", "application/pdf")   // opened when the request is sent
        .AddFile("thumbnail", thumbnailBytes, "thumb.png", "image/png")
        .AddFile("data", stream, "data.csv", "text/csv"))               // not disposed by RestClient
    .PostAsync<UploadResponse>();
```

The content is created again for every attempt (retry, refresh token): byte arrays, file paths and stream factories
(`AddFile(name, () => OpenStream(), fileName)`) can always be resent, a `Stream` only if it is seekable.

### Download and progress

Use `byte[]` for small files and `Stream` for large ones: the stream is read from the network while you copy it.

```c#
using RestResult<Stream> file = await api
    .Command("files").Command("big.zip")
    .OnDownloadProgress(p => Console.WriteLine($"{p.CurrentBytes}/{p.TotalBytes} ({p.ProgressPercentage}%)"))
    .GetAsync<Stream>();

using FileStream output = File.Create("big.zip");
await file.Content!.CopyToAsync(output);   // OnDownloadProgress is raised while copying
```

`OnUploadProgress(p => ...)` reports the progress of the request body in the same way. `BufferSize(int)` sets the
buffer used to upload and download (default 80 KB).

### Compression

`EnableGZipCompression()` sends the `Accept-Encoding` header and decompresses the response automatically (gzip,
deflate and, on .NET 8+, brotli).

## Authentication

### Authorization header

```c#
RestBuilder bearer = api.Authentication("Bearer", accessToken);

// evaluated for every request (and every attempt): always the current token
RestBuilder current = api.Authentication(() => new AuthenticationHeaderValue("Bearer", tokenStore.AccessToken));
```

### Asynchronous token provider

When the token comes from an asynchronous source (MSAL, a token cache, a secret store, an OAuth2/OIDC client), pass an
async func to `BearerToken` (`Authorization: Bearer <token>`) or to `Authentication` (any scheme): no `.Result` needed.

```c#
RestBuilder msal = api.BearerToken(async cancellationToken =>
    (await app.AcquireTokenForClient(scopes).ExecuteAsync(cancellationToken)).AccessToken);

RestBuilder custom = api.Authentication(async cancellationToken =>
    new AuthenticationHeaderValue("Bearer", await tokenProvider.GetTokenAsync(cancellationToken)));
```

- The func is invoked for **every attempt**: the first request, every [retry](#retry) and the request sent again after
  a [refresh token](#refresh-token), so it always provides the current token (cache it in your provider if needed).
  The redirects followed by RestClient are part of the same attempt and never receive the `Authorization` header.
- It receives the `CancellationToken` of the attempt: canceled by the token of the request and by the `Timeout`
  (a provider that times out is retried like a request that times out).
- An exception thrown by the func is reported by the result (`result.Exception`, `OnException`) like the other errors:
  `SendAsync` never throws. A `null` header, or a `null` / empty token, sends the request without authorization.
- The last `Authentication(...)` / `BearerToken(...)` call wins; `BearerToken(null)` removes the authentication.

### Refresh token

Pass the refresh func to `RefreshToken`: when a response is `401 Unauthorized` the func is invoked and, if it returns a
2xx result, the request is sent again (once). The func receives the `CancellationToken` of the request.

```c#
RestBuilder secured = api
    .Authentication(() => new AuthenticationHeaderValue("Bearer", tokenStore.AccessToken))
    .RefreshToken(async cancellationToken =>
    {
        RestResult<TokenResponse> refreshed = await api
            .Command("auth").Command("refresh")
            .Payload(new { refreshToken = tokenStore.RefreshToken })
            .PostAsync<TokenResponse>(cancellationToken);

        if (refreshed.IsSuccessStatusCode)
        {
            tokenStore.AccessToken = refreshed.Content!.AccessToken;
        }
        return refreshed;
    });
```

When many concurrent requests of the same builder receive 401, the refresh func is invoked **only once** and the other
requests wait for it. The requests sent from inside the refresh func never trigger another refresh.
`RefreshToken(null)` disables the refresh token on a derived builder.

### Network credentials

For basic, digest, NTLM and Kerberos authentication:

```c#
RestBuilder intranet = api.NetworkCredential("username", "password", "DOMAIN");
RestBuilder fromCache = api.NetworkCredential(() => CredentialCache.DefaultCredentials);
```

### Certificates

By default the server certificate is validated by the operating system. `CertificateValidation` replaces the
validation **only for the requests of that builder** (for example to pin a certificate):

```c#
RestBuilder pinned = api.CertificateValidation(ValidateCertificate);

static bool ValidateCertificate(object sender, X509Certificate? certificate, X509Chain? chain, SslPolicyErrors errors)
    => errors == SslPolicyErrors.None && certificate?.GetCertHashString() == "EXPECTED-THUMBPRINT";
```

Since the HTTP handlers are pooled by configuration, define the callback once (a static method or a field) instead of
creating a new closure for every request. `CertificateOption(ClientCertificateOption.Automatic)` lets the handler
select a client certificate automatically.

## Resilience

### Retry

Transient errors (network errors, timeouts, 408, 429, 500, 502, 503, 504) can be retried with an exponential backoff
with jitter. The `Retry-After` header is honored (up to 30 seconds).

```c#
RestResult<User> result = await api
    .Retry(3)                                // 3 retries: 200ms, 400ms, 800ms (+ jitter)
    .Command("users").Command(1)
    .GetAsync<User>();

int retries = result.RetryCount;
```

Only idempotent methods (GET, HEAD, OPTIONS, PUT, DELETE, TRACE) are retried, because a POST or a PATCH could be
executed twice. Use `Retry(3, TimeSpan.FromSeconds(1), retryNonIdempotentMethods: true)` if your API is idempotent.
For advanced policies (circuit breaker, hedging) use `IHttpClientFactory` with `Microsoft.Extensions.Http.Resilience`,
see [Dependency Injection](#dependency-injection).

### Handling the result

Requests never throw: HTTP errors, network errors, timeouts and cancellations are reported in the result.

```c#
RestResult<User> result = await api.Command("users").Command(1).GetAsync<User>();

if (result.IsSuccessStatusCode)          // 2xx and no exception
{
    User? user = result.Content;
}
else if (result.Response != null)        // a response was received
{
    HttpStatusCode status = result.StatusCode;   // e.g. 404
    Exception? error = result.Exception;         // not null if the body could not be deserialized
}
else                                     // no response: network error, timeout, cancellation (StatusCode is 0)
{
    Exception? error = result.Exception;
}

// or, if you prefer exceptions: HttpRequestException when the request did not succeed
result.EnsureSuccessStatusCode();
```

### Problem Details and error bodies

Most APIs (ASP.NET Core included) describe their errors with **Problem Details** (RFC 9457,
`application/problem+json`). `result.Problem` reads it from the body of an error response, and `result.ErrorAs<T>()`
reads the error body as any other type, for example the error DTO of your API:

```c#
RestResult<User> result = await api.Command("users").Payload(user).PostAsync<User>();

if (!result.IsSuccessStatusCode)
{
    RestProblemDetails? problem = result.Problem;     // null if the content type is not application/problem+json
    if (problem != null)
    {
        Console.WriteLine($"{problem.Status} {problem.Title}: {problem.Detail}");

        foreach (var field in problem.Errors)         // validation errors (ASP.NET Core ValidationProblem)
        {
            Console.WriteLine($"{field.Key}: {string.Join(", ", field.Value)}");
        }

        if (problem.Extensions.TryGetValue("traceId", out JsonElement traceId))   // any other member
        {
            Console.WriteLine(traceId.GetString());
        }
    }

    MyError? error = result.ErrorAs<MyError>();       // any custom error DTO, with the configured serializer
    string? body = result.ErrorAs<string>();          // or the raw body (also byte[] and Stream)
}
```

- `Problem` is `null` for a success response, when no response has been received, for another content type and when
  the body is not a valid JSON object (it never throws). It is read without reflection, so it is Native AOT safe.
- `ErrorAs<T>()` uses the serializer configured on the builder: with `Json(MyJsonContext.Default)` add `MyError` to
  the context to be Native AOT safe. It returns `default` for a success response or a body that cannot be read.
- The error body is read only once, and only when `Problem` or `ErrorAs` are used: the success responses are not
  affected. For a `Stream` result the error body is read from `Content` on first use, and `Content` is then replaced
  with a stream over the same bytes: use `Problem`/`ErrorAs` before reading `Content`.
- The type is called `RestProblemDetails`, not `ProblemDetails`, so that it does not clash with
  `Microsoft.AspNetCore.Mvc.ProblemDetails` in the files that import both namespaces.

## Diagnostics

### Logging

Pass an `ILogger` to log the requests (Debug), the responses (Information), the retries (Warning) and the errors
(Error). The query string is never logged because it can contain secrets.

```c#
RestBuilder logged = api.Logger(loggerFactory.CreateLogger("MyApi"));
```

The builders created by [Dependency Injection](#dependency-injection) log with the category `Mafesoft.RestClient.{name}`.

### Tracing (OpenTelemetry)

Every request creates an `Activity` named `RestClient {METHOD}` from the `ActivitySource` `Mafesoft.RestClient`, with
the tags `http.request.method`, `url.full` (without query string), `http.response.status_code`,
`rest_client.retry_count` and `error.type`. The activities of `HttpClient` (one for each attempt) are its children.

```c#
builder.Services.AddOpenTelemetry().WithTracing(tracing => tracing
    .AddAspNetCoreInstrumentation()   // in ASP.NET Core, see below
    .AddSource(RestClientTelemetry.ActivitySourceName)
    .AddHttpClientInstrumentation());
```

In ASP.NET Core add also `AddAspNetCoreInstrumentation()` (package `OpenTelemetry.Instrumentation.AspNetCore`):
ASP.NET Core always creates an activity for the incoming request, and when it is not recorded the default sampler
(parent based) drops its children, the RestClient activities included.

## Events

| Event | Raised |
|---|---|
| `OnStart(e => ...)` | before sending; `e.Url`, `e.Payload`; set `e.Cancel = true` to cancel the request |
| `OnPreviewContentRequestAsString(e => ...)` | with the serialized request body, before sending |
| `OnPreviewContentResponseAsString(e => ...)` | with the response body as string, before deserializing |
| `OnUploadProgress(p => ...)` / `OnDownloadProgress(p => ...)` | while the body is sent / received |
| `OnPreCompleted(e => ...)` | when the result is ready, before `OnCompleted` |
| `OnCompleted(e => ...)` | when the request is completed; `e.Result`, `e.ExecutionTime` |
| `OnException(ex => ...)` | when the request fails with an exception (also reported in `result.Exception`) |
| `OnConfigureHttpClient(c => ...)` | to configure the `HttpClient` managed by RestClient |

```c#
RestBuilder audited = api
    .OnStart(e => Console.WriteLine($"-> {e.Url}"))
    .OnCompleted(e => Console.WriteLine($"<- {(int)e.Result.StatusCode} in {e.ExecutionTime.TotalMilliseconds}ms"))
    .OnException(ex => Console.WriteLine($"!! {ex.Message}"));
```

## Dependency Injection

The `Mafesoft.RestClient.Extensions.DependencyInjection` package registers the builders on top of `IHttpClientFactory`.
`AddRestClient` returns the `IHttpClientBuilder`, so every `IHttpClientFactory` extension works: delegating handlers,
`AddStandardResilienceHandler()`, ...

```c#
// named client
builder.Services.AddRestClient("github", rest => rest
        .Url("https://api.github.com")
        .Header("User-Agent", "my-app"))
    .AddStandardResilienceHandler();

public class ReposService(IRestClientFactory factory)
{
    private readonly RestBuilder github = factory.CreateClient("github");

    public Task<RestResult<Repo>> GetAsync(string owner, string name)
        => github.Command("repos").Command(owner).Command(name).GetAsync<Repo>();
}

// typed client: UsersClient receives a configured RestBuilder in its constructor
builder.Services.AddRestClient<UsersClient>((provider, rest) => rest
    .Url(provider.GetRequiredService<IConfiguration>()["Api:Url"]!));

public class UsersClient(RestBuilder rest)
{
    public Task<RestResult<User>> GetAsync(int id) => rest.Command("users").Command(id).GetAsync<User>();
}
```

The overloads of `AddRestClient` that receive the `IServiceProvider` are the place for an
[asynchronous token provider](#asynchronous-token-provider) registered in the container:

```c#
builder.Services.AddSingleton<ITokenProvider, MsalTokenProvider>();
builder.Services.AddRestClient("graph", (provider, rest) =>
{
    ITokenProvider tokens = provider.GetRequiredService<ITokenProvider>();
    return rest.Url("https://graph.microsoft.com/v1.0").BearerToken(tokens.GetTokenAsync);
});
```

The provider passed to the configuration is the root `IServiceProvider`: resolve singletons (a token provider usually is
one, it owns the token cache), not scoped services.

`IRestClientFactory` is in the `Mafesoft.RestClient` namespace, `AddRestClient` in
`Microsoft.Extensions.DependencyInjection`. The builders log with the category `Mafesoft.RestClient.{name}` (the full
type name for a typed client).

## Samples

| Sample | What it shows |
|---|---|
| [Console](samples/RestClient.Samples.Console) | GET/POST, query string, error handling, retry, cancellation |
| [ASP.NET Core](samples/RestClient.Samples.AspNetCore) | typed client, `AddStandardResilienceHandler()`, logging, OpenTelemetry |
| [Native AOT](samples/RestClient.Samples.NativeAot) | `Json(JsonSerializerContext)` published with `PublishAot=true` |

See [samples/README.md](samples/README.md) to run them.

## Migrating to 3.0

In 3.0.0 the packages, the assemblies and the namespaces have been renamed to `Mafesoft.*` (steps 1-3) and a few
APIs have been cleaned up (step 4).

1. Replace the package references (the `Mafe.*` packages are deprecated and will not receive new versions):

   ```
   dotnet remove package Mafe.RestClient
   dotnet add package Mafesoft.RestClient

   dotnet remove package Mafe.RestClient.Extensions.DependencyInjection
   dotnet add package Mafesoft.RestClient.Extensions.DependencyInjection
   ```

2. Update the `using` directives:

   | 2.x | 3.0 |
   |---|---|
   | `using RestClient;` | `using Mafesoft.RestClient;` |
   | `using RestClient.Http;` | `using Mafesoft.RestClient.Http;` |
   | `using RestClient.Serialization;` | `using Mafesoft.RestClient.Serialization;` |
   | `using RestClient.Serialization.Json;` | `using Mafesoft.RestClient.Serialization.Json;` |
   | `using RestClient.Serialization.Xml;` | `using Mafesoft.RestClient.Serialization.Xml;` |

   `IRestClientFactory` is now in `Mafesoft.RestClient`. `AddRestClient(...)` is still in `Microsoft.Extensions.DependencyInjection`.

3. If you listen to the traces or filter the logs, update the names:
   * `ActivitySource`: `Mafe.RestClient` → `Mafesoft.RestClient` (or use `RestClientTelemetry.ActivitySourceName`);
   * logger category of the builders created by `IRestClientFactory`: `Mafe.RestClient.{name}` → `Mafesoft.RestClient.{name}`.

4. Update the calls to the changed APIs:

   | 2.x | 3.0 |
   |---|---|
   | `.RefreshToken().RefreshTokenInvoke(async () => await RefreshAsync())` | `.RefreshToken(async ct => await RefreshAsync(ct))` |
   | `.RefreshTokenInvoke(() => Refresh())` (synchronous func) | `.RefreshToken(async ct => await RefreshAsync(ct))` |
   | `.RefreshToken(false)` | `.RefreshToken(null)` |
   | `Rest.Build(new RestProperties { EndPoint = ..., Timeout = ..., BufferSize = ..., CertificateOption = ... })` | `Rest.Build().Url(...).Timeout(...).BufferSize(...).CertificateOption(...)` |
   | `Rest.Build(p => p.Timeout = ...)` | `Rest.Build().Timeout(...)` |

   `RestProperties` is read-only: it can no longer be created or changed, `RestBuilder.Properties` still returns it to read the configuration.

<a id="migrating-to-20"></a>
<details>
<summary><strong>Migrating to 2.0</strong> (from 1.x)</summary>

Version 2.0.0 modernized the library and fixed several long-standing issues. It contains the following breaking changes
(the names are the 2.x ones: apply also [Migrating to 3.0](#migrating-to-30)).

#### Target frameworks

* Supported: `net10.0`, `net8.0`, `netstandard2.1`, `netstandard2.0`.
* Removed (out of support): `net7.0`, `net6.0`, `netcoreapp3.1`, `netstandard1.3`, `net452`, `net46`, `net461`, `net462`, `net47`, `net471`, `net472`, `net48`.
  .NET Framework 4.6.2+ applications keep working through `netstandard2.0`.
* Newtonsoft.Json is no longer used: JSON is always System.Text.Json. `Json(JsonSerializerSettings)` has been removed; use `Json(JsonSerializerOptions)` or a custom `ISerializerContent`.
* New dependencies: `Microsoft.Extensions.Logging.Abstractions` (and, on .NET Standard, `System.Text.Json` and `System.Diagnostics.DiagnosticSource`).

#### Namespaces

`RestResult<T>` moved from `RestClient.Generic` to `RestClient`: remove `using RestClient.Generic;`.

#### Removed APIs

| Removed | Use instead |
|---|---|
| `GetAsByteArray()`, `PostAsByteArrayAsync()`, ... (all `*AsByteArray*`) | `Get<byte[]>()`, `PostAsync<byte[]>()`, ... |
| `GetAsStream()`, `PostAsStreamAsync()`, ... (all `*AsStream*`) | `Get<Stream>()`, `PostAsync<Stream>()`, ... (now really streamed: dispose the result) |
| `Download(url)`, `DownloadAsync(url)` | `Url(url).GetAsync<byte[]>()` |
| `CustomCall(method)`, `CustomCallAsync<T>(method)`, ... | `Send<string>(method)`, `SendAsync<T>(method)`, ... |
| `Get(string url)`, `Get(Uri url)` | `Url(url).Get()` |
| `Command(uint)` | `Command(long)` (an `uint` argument still compiles) |
| `OnAuthentication(...)` | `Authentication(...)` |
| `Compression()` | `EnableGZipCompression()` |
| `EnableGZipCompression(bool deflate)` | `EnableGZipCompression()` (gzip, deflate and brotli are all enabled) |
| `OnPreResult(...)` | `OnPreCompleted(e => e.Result)` |
| `OnPreviewContentAsString(...)` | `OnPreviewContentResponseAsString(...)` |
| `RestResult<T>.StringContent` | `OnPreviewContentResponseAsString(...)` |
| `HttpClientCompressionHandler` | automatic decompression of the pooled handlers |
| `ProgressBytesChangedEventHandler` | `OnUploadProgress` / `OnDownloadProgress` |

#### Changed signatures

* `GetAsync<T>()` with `T = string` returns the raw body (in 1.x it was deserialized as a JSON string).
* The synchronous methods (`Get()`, `Post<T>()`, ...) accept an optional `CancellationToken`.
* `CertificateValidation` takes `Func<object, X509Certificate?, X509Chain?, SslPolicyErrors, bool>` (existing lambdas compile unchanged).
* `NetworkCredential(Func<ICredentials?>)` instead of `Func<NetworkCredential>` (existing lambdas compile unchanged).
* `RestBuilder.Properties` returns a copy of the properties (in 1.x changing it changed every builder sharing the instance).
* `RestResult<T>` is sealed and it can no longer be created or subclassed outside the library.
* The whole API is annotated for nullable reference types.

#### Behavior changes

* **Connections**: the HTTP handlers are pooled and the connections are reused (in 1.x a new handler, and a new TCP/TLS connection,
  was created for every request). Cookies are not stored by default, as in 1.x (see [Cookies](#cookies)).
* **TLS certificate validation**: 1.x set `ServicePointManager.ServerCertificateValidationCallback` to *accept every certificate*
  for the whole process on .NET Framework/.NET Standard, as soon as a `RestBuilder` was created. This has been removed:
  certificates are validated by default and `CertificateValidation(...)` only affects the requests of that builder.
* **Immutable builder**: in 1.x commands and parameters were shared between a builder and the builders derived from it,
  so reusing a "root" builder could accumulate url segments. Now each call returns an independent copy.
* **JSON defaults**: when no options are passed, JSON uses `JsonSerializerDefaults.Web`: payloads are serialized in **camelCase**
  and responses are read case-insensitively. To keep the 1.x behavior use `.Json(new JsonSerializerOptions())`.
* **Query string**: keys and values are url-encoded and formatted with the invariant culture. If you encoded them manually,
  remove the manual encoding. A trailing `/` in the endpoint is removed before appending the commands.
  Collections are expanded into repeated keys (`?id=1&id=2`).
* **Headers**: `Header(...)` and `Authentication(...)` are applied to the request message instead of `HttpClient.DefaultRequestHeaders`.
  Multiple `Header(...)` calls are combined (in 1.x the last one won).
* **FormUrlEncoded**: `FormUrlEncoded(dictionary)` and `FormUrlEncoded(action)` enable the form-url-encoded content automatically.
  `Payload`, `FormUrlEncoded` and `Multipart` replace each other.
* **Timeout**: the timeout is managed by RestClient and applies to every attempt. When it elapses the result contains a `TimeoutException`
  (`IsTimedOut == true`, `IsCancellationRequested == false`). For `Stream` results it covers the response headers only.
* **Streams**: `Stream` results are not buffered anymore: the connection stays open until the result (or the stream) is disposed.
* **RestResult**:
  * `StatusCode` is `0` (instead of `404 NotFound`) when no response has been received.
  * `Version` is `null` (in 1.x it threw a `FormatException`) and `ReasonPhrase` is the exception message when no response has been received.
  * `Exception` is available on the base `RestResult` class.
  * When the response body cannot be deserialized, the result keeps `Response`, `StatusCode` and `Headers` and the error is in `Exception`
    (in 1.x the whole response was lost). `IsSuccessStatusCode` is false in this case. An empty body is a `null` content, not an error.
  * `IRestResult.Content` returns the content (in 1.x it was always null).
  * Disposing a `RestResult<Stream>` disposes the stream.
* **Refresh token**: the refresh func is accepted when it returns any 2xx status code (not only 200). It runs once for all the concurrent
  requests that receive 401, the retry happens once per request, and the builder can be reused for later requests
  (in 1.x, after the first refresh the builder never refreshed again).
* **Generic methods**: the `where T : new()` constraint has been removed, so interfaces, arrays, records and immutable types can be used.
* **Sync methods** (`Get()`, `Post()`, ...) no longer deadlock in UI or classic ASP.NET synchronization contexts.
* **XML**: `Xml(readerOptions, writerOptions)` used the reader settings to serialize and the writer settings to deserialize; this has been fixed.
* `Parameter(RestParameter, ...)` no longer throws when the key already exists: the value is replaced.

</details>

## Build and test

```
dotnet build -c Release src/RestClient.sln
dotnet test -c Release tests/RestClient.Tests/RestClient.Tests.csproj
dotnet build samples/RestClient.Samples.sln
```

The tests run on .NET 10, .NET 8 and, on Windows, .NET Framework 4.8. See [CONTRIBUTING.md](CONTRIBUTING.md) for the
Native AOT smoke test, the public API files and the release steps.

## Contributing

Bug reports, ideas and pull requests are welcome: open an [issue](https://github.com/fmazzant/RestClient/issues) first
and read [CONTRIBUTING.md](CONTRIBUTING.md). For security problems read [SECURITY.md](SECURITY.md).

## License

Licensed under the [MIT License](LICENSE). Copyright (c) 2020-2026 Federico Mazzanti.
