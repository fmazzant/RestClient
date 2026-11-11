# Mafesoft.RestClient

A fluent and immutable builder over `HttpClient` to consume REST services: pooled connections, retry with backoff,
refresh token, timeout, JSON (System.Text.Json, Native AOT ready) or XML serialization, multipart and form-url-encoded
uploads, upload/download progress, `ILogger` logging and OpenTelemetry tracing. The result is a `RestResult<T>`
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

## Links

- [Documentation](https://github.com/fmazzant/RestClient#readme)
- [Migrating to 3.0](https://github.com/fmazzant/RestClient#migrating-to-30) (from `Mafe.RestClient`)
- [Changelog](https://github.com/fmazzant/RestClient/blob/main/CHANGELOG.md)
- [Issues](https://github.com/fmazzant/RestClient/issues)

Licensed under the [MIT License](https://github.com/fmazzant/RestClient/blob/main/LICENSE).
