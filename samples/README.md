# Samples

The samples call [JSONPlaceholder](https://jsonplaceholder.typicode.com), a free fake REST API, and reference the
libraries in `src/` as projects. Build them all with `dotnet build samples/RestClient.Samples.sln`.

| Sample | What it shows | Run |
|---|---|---|
| [RestClient.Samples.Console](RestClient.Samples.Console) | Build a root builder, GET/POST DTOs, query string, error handling (`RestResult`, `EnsureSuccessStatusCode`), retry, cancellation | `dotnet run --project samples/RestClient.Samples.Console` |
| [RestClient.Samples.AspNetCore](RestClient.Samples.AspNetCore) | Minimal API with a typed client: `AddRestClient<TClient>()` + `AddStandardResilienceHandler()`, logging, OpenTelemetry tracing | `dotnet run --project samples/RestClient.Samples.AspNetCore`, then open http://localhost:5000/posts/1 |
| [RestClient.Samples.NativeAot](RestClient.Samples.NativeAot) | `Json(JsonSerializerContext)` with reflection-based JSON disabled, published with `PublishAot=true` | `dotnet publish -c Release -r <rid> samples/RestClient.Samples.NativeAot` (needs the platform C++ toolchain) |
