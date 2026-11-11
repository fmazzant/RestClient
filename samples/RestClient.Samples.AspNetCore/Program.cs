using Mafesoft.RestClient;
using OpenTelemetry.Trace;

// ASP.NET Core minimal API that calls JSONPlaceholder (https://jsonplaceholder.typicode.com) through a typed client.
// Run it and open http://localhost:5000/posts/1, /users/1/posts, or POST /posts.

var builder = WebApplication.CreateBuilder(args);

// Typed client: PostsClient receives a configured RestBuilder. AddRestClient returns the IHttpClientBuilder, so every
// IHttpClientFactory extension works: here the standard resilience pipeline (retry, circuit breaker, timeouts).
builder.Services.AddRestClient<PostsClient>((provider, rest) => rest
        .Url(provider.GetRequiredService<IConfiguration>()["Api:Url"] ?? "https://jsonplaceholder.typicode.com")
        .Header("User-Agent", "Mafesoft.RestClient.Samples.AspNetCore"))
    .AddStandardResilienceHandler();

// Logging: the builders created by AddRestClient log with the category "Mafesoft.RestClient.{name}"
// (see the "Logging" section of appsettings.json).

// Tracing: every request creates an Activity "RestClient {METHOD}" from the ActivitySource "Mafesoft.RestClient";
// the HttpClient activities (one for each attempt) are its children. Here they are written to the console.
// AddAspNetCoreInstrumentation is required: without it the activity of the incoming request is not recorded and
// the default sampler (parent based) drops its children, the RestClient activities included.
builder.Services.AddOpenTelemetry().WithTracing(tracing => tracing
    .AddAspNetCoreInstrumentation()
    .AddSource(RestClientTelemetry.ActivitySourceName)
    .AddHttpClientInstrumentation()
    .AddConsoleExporter());

var app = builder.Build();

app.MapGet("/posts/{id:int}", async (int id, PostsClient posts, CancellationToken cancellationToken) =>
{
    RestResult<Post> result = await posts.GetAsync(id, cancellationToken);
    return result.IsSuccessStatusCode
        ? Results.Ok(result.Content)
        : Results.Problem(result.Exception?.Message, statusCode: result.StatusCode == 0 ? 502 : (int)result.StatusCode);
});

app.MapGet("/users/{userId:int}/posts", async (int userId, PostsClient posts, CancellationToken cancellationToken) =>
{
    RestResult<List<Post>> result = await posts.GetByUserAsync(userId, cancellationToken);
    return result.IsSuccessStatusCode ? Results.Ok(result.Content) : Results.StatusCode(502);
});

app.MapPost("/posts", async (Post post, PostsClient posts, CancellationToken cancellationToken) =>
{
    RestResult<Post> result = await posts.CreateAsync(post, cancellationToken);
    return result.IsSuccessStatusCode ? Results.Created($"/posts/{result.Content?.Id}", result.Content) : Results.StatusCode(502);
});

app.Run();
