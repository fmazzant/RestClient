# Mafesoft.RestClient.Extensions.DependencyInjection

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
```

`IRestClientFactory` is in the `Mafesoft.RestClient` namespace, `AddRestClient` in `Microsoft.Extensions.DependencyInjection`.
The builders log with the category `Mafesoft.RestClient.{name}`.

## Links

- [Documentation](https://github.com/fmazzant/RestClient#dependency-injection)
- [Migrating to 3.0](https://github.com/fmazzant/RestClient#migrating-to-30) (from `Mafe.RestClient.Extensions.DependencyInjection`)
- [Changelog](https://github.com/fmazzant/RestClient/blob/main/CHANGELOG.md)
- [Issues](https://github.com/fmazzant/RestClient/issues)

Licensed under the [MIT License](https://github.com/fmazzant/RestClient/blob/main/LICENSE).
