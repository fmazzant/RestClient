# Mafesoft.RestClient.Testing

[![NuGet](https://img.shields.io/nuget/v/Mafesoft.RestClient.Testing?style=flat-square&label=nuget)](https://www.nuget.org/packages/Mafesoft.RestClient.Testing)
[![Downloads](https://img.shields.io/nuget/dt/Mafesoft.RestClient.Testing?style=flat-square&label=downloads)](https://www.nuget.org/packages/Mafesoft.RestClient.Testing)
[![Build](https://img.shields.io/github/actions/workflow/status/fmazzant/RestClient/ci.yml?branch=main&style=flat-square&label=build)](https://github.com/fmazzant/RestClient/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue?style=flat-square)](https://github.com/fmazzant/RestClient/blob/main/LICENSE)

Test helpers for [Mafesoft.RestClient](https://www.nuget.org/packages/Mafesoft.RestClient): `RestTest` is a fake
server that answers the requests of the code under test, without the network and without writing a fake
`HttpMessageHandler`. It works with `Rest.Build()`, with `Rest.Build(httpClient)` and with the builders of
`IRestClientFactory` (Mafesoft.RestClient.Extensions.DependencyInjection). Test framework agnostic (xUnit, NUnit,
MSTest, ...): a failed assertion throws `RestCallAssertionException`.

Target frameworks: .NET 10, .NET 8, .NET Standard 2.0.

## Quick start

```
dotnet add package Mafesoft.RestClient.Testing
```

```c#
using Mafesoft.RestClient.Testing;

[Fact]
public async Task Gets_the_user()
{
    using var http = new RestTest();
    http.RespondWithJson(new User { Name = "fede" }).For(HttpMethod.Get, "*/users/*");

    User user = await sut.GetUserAsync("fede");

    http.ShouldHaveCalled("*/users/fede").WithVerb(HttpMethod.Get).Times(1);
}
```

## Responses

- `RespondWith(body, status, mediaType)`, `RespondWith(status)`, `RespondWithJson(value)` (web defaults, camelCase),
  `RespondWithJson(value, MyJsonContext.Default.User)` (source generated, Native AOT safe),
  `RespondWith(request => new HttpResponseMessage(...))`, `SimulateException(exception)`, `SimulateTimeout()`.
- `.For("*/users/*")`, `.For(HttpMethod.Get, "*/users/*")` and `.When(request => ...)` restrict a response to some
  requests. `*` matches any text; the match is case-insensitive and covers the whole URL (with the query string).
- The responses are used in the order they are added; the last matching response is used again for all the following
  matching requests. A request without a matching response gets `200 OK` with an empty body.

## Assertions

- `ShouldHaveCalled(urlPattern)`, `ShouldNotHaveCalled(urlPattern)`, `ShouldHaveMadeACall()`, `ShouldNotHaveMadeACall()`.
- `ShouldHaveCalled` returns an assertion that narrows the matching calls: `WithVerb`, `WithHeader`, `WithoutHeader`,
  `WithQueryParam`, `WithRequestBody`, `WithContentType`, `WithOAuthBearerToken`, `With(predicate)`, and `Times(n)`.
- `CallLog` lists every request received (method, URL, headers, body, status code).

## How it works

`new RestTest()` installs the fake server for the current async flow (an `AsyncLocal`), `Dispose` removes it. Tests
running in parallel do not see each other's fake server. Code that runs outside the async flow of the test (e.g. a
hosted service started before the test) is not intercepted: pass `http.CreateClient()` (or `http.Handler`) to it.

The full documentation is on [GitHub](https://github.com/fmazzant/RestClient#testing).
