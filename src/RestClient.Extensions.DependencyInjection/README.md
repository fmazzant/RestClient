# Mafesoft.RestClient.Extensions.DependencyInjection

[![NuGet](https://img.shields.io/nuget/v/Mafesoft.RestClient.Extensions.DependencyInjection?style=flat-square&label=nuget)](https://www.nuget.org/packages/Mafesoft.RestClient.Extensions.DependencyInjection)
[![Downloads](https://img.shields.io/nuget/dt/Mafesoft.RestClient.Extensions.DependencyInjection?style=flat-square&label=downloads)](https://www.nuget.org/packages/Mafesoft.RestClient.Extensions.DependencyInjection)
[![Build](https://img.shields.io/github/actions/workflow/status/fmazzant/RestClient/ci.yml?branch=main&style=flat-square&label=build)](https://github.com/fmazzant/RestClient/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue?style=flat-square)](https://github.com/fmazzant/RestClient/blob/main/LICENSE)

[![Mafe.RestClient.Extensions.DependencyInjection downloads](https://img.shields.io/nuget/dt/Mafe.RestClient.Extensions.DependencyInjection?style=flat-square&label=Mafe.RestClient.Extensions.DependencyInjection%20downloads)](https://www.nuget.org/packages/Mafe.RestClient.Extensions.DependencyInjection)

Registers the [Mafesoft.RestClient](https://www.nuget.org/packages/Mafesoft.RestClient) builders in the dependency
injection container, on top of `IHttpClientFactory`: pooled handlers, delegating handlers and resilience
(`AddStandardResilienceHandler()`) work as with any other `HttpClient`.

Target frameworks: .NET 10, .NET 8, .NET Standard 2.0.

## Quick start

```
dotnet add package Mafesoft.RestClient.Extensions.DependencyInjection
```

```c#
// named client: AddRestClient returns the IHttpClientBuilder
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

// asynchronous token provider registered in the container (resolve singletons: the provider is the root one)
builder.Services.AddRestClient("graph", (provider, rest) => rest
    .Url("https://graph.microsoft.com/v1.0")
    .BearerToken(provider.GetRequiredService<ITokenProvider>().GetTokenAsync));
```

`IRestClientFactory` is in the `Mafesoft.RestClient` namespace, `AddRestClient` in `Microsoft.Extensions.DependencyInjection`.
The builders log with the category `Mafesoft.RestClient.{name}`.

## Support

If Mafesoft.RestClient.Extensions.DependencyInjection saves you time, you can say thanks with a small donation. It is never required, but it helps me keep the project maintained and up to date.

[![Donate](https://img.shields.io/badge/donate-PayPal-00457C?style=flat-square&logo=paypal&logoColor=white)](https://paypal.me/fmazzant)

## Links

- [Documentation](https://github.com/fmazzant/RestClient#dependency-injection)
- [Migrating to 3.0](https://github.com/fmazzant/RestClient#migrating-to-30) (from `Mafe.RestClient.Extensions.DependencyInjection`)
- [Changelog](https://github.com/fmazzant/RestClient/blob/main/CHANGELOG.md)
- [Issues](https://github.com/fmazzant/RestClient/issues)

Licensed under the [MIT License](https://github.com/fmazzant/RestClient/blob/main/LICENSE).
