# Mafesoft.RestClient

[![NuGet](https://img.shields.io/nuget/v/Mafesoft.RestClient?style=flat-square&label=nuget)](https://www.nuget.org/packages/Mafesoft.RestClient)
[![Downloads](https://img.shields.io/nuget/dt/Mafesoft.RestClient?style=flat-square&label=downloads)](https://www.nuget.org/packages/Mafesoft.RestClient)
[![Build](https://img.shields.io/github/actions/workflow/status/fmazzant/RestClient/ci.yml?branch=main&style=flat-square&label=build)](https://github.com/fmazzant/RestClient/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue?style=flat-square)](https://github.com/fmazzant/RestClient/blob/main/LICENSE)

[![Mafe.RestClient downloads](https://img.shields.io/nuget/dt/Mafe.RestClient?style=flat-square&label=Mafe.RestClient%20downloads)](https://www.nuget.org/packages/Mafe.RestClient)

A fluent and immutable builder over `HttpClient` to consume REST services: pooled connections, retry with backoff,
refresh token, timeout, JSON (System.Text.Json, Native AOT ready) or XML serialization, multipart and form-url-encoded
uploads, upload/download progress, `ILogger` logging, OpenTelemetry tracing and metrics. The result is a `RestResult<T>`
(status code, headers, content, error) instead of an exception.

Target frameworks: .NET 10, .NET 8, .NET Standard 2.1, .NET Standard 2.0 (.NET Framework 4.6.2+, Xamarin, Unity).

## Quick start

```
dotnet add package Mafesoft.RestClient
```

```c#
using Mafesoft.RestClient;

// configure once, reuse everywhere: every call returns a new builder
RestBuilder api = Rest.Build()
    .Url("https://api.example.com")
    .Authentication("Bearer", token)
    .Retry(3);

RestResult<User> user = await api.Command("users").Command(42).GetAsync<User>();
if (user.IsSuccessStatusCode)
{
    Console.WriteLine(user.Content!.Name);
}

RestResult<User> created = await api.Command("users").Payload(new User { Name = "Ada" }).PostAsync<User>();
```

To register the builders in the dependency injection container, on top of `IHttpClientFactory`, use
[Mafesoft.RestClient.Extensions.DependencyInjection](https://www.nuget.org/packages/Mafesoft.RestClient.Extensions.DependencyInjection).

## Support

If Mafesoft.RestClient saves you time, you can say thanks with a small donation. It is never required, but it helps me keep the project maintained and up to date.

[![Donate](https://img.shields.io/badge/donate-PayPal-00457C?style=flat-square&logo=paypal&logoColor=white)](https://paypal.me/fmazzant)

## Links

- [Documentation](https://github.com/fmazzant/RestClient#readme)
- [Why Mafesoft.RestClient](https://github.com/fmazzant/RestClient#why-mafesoftrestclient) and
  [comparison with HttpClient, RestSharp, Refit and Flurl](https://github.com/fmazzant/RestClient#comparison-with-the-alternatives)
- [Migrating to 3.0](https://github.com/fmazzant/RestClient#migrating-to-30) (from `Mafe.RestClient`)
- [Changelog](https://github.com/fmazzant/RestClient/blob/main/CHANGELOG.md)
- [Issues](https://github.com/fmazzant/RestClient/issues)

Licensed under the [MIT License](https://github.com/fmazzant/RestClient/blob/main/LICENSE).
