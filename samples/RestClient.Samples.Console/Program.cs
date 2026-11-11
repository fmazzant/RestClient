using Mafesoft.RestClient;
using System.Net;

// End-to-end example against JSONPlaceholder (https://jsonplaceholder.typicode.com), a free fake REST API.

// 1. Build: configure the root once. The builder is immutable, every call returns a new instance,
//    so the root can be reused (also concurrently) as a base for all the requests.
RestBuilder api = Rest.Build()
    .Url("https://jsonplaceholder.typicode.com")
    .Header("User-Agent", "Mafesoft.RestClient.Samples.Console")
    .Timeout(TimeSpan.FromSeconds(10))
    .Retry(3);   // network errors, timeouts, 408, 429, 5xx: retried with backoff (GET, PUT, DELETE, ...)

using var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cancellation.Cancel(); };   // Ctrl+C cancels the requests
CancellationToken cancellationToken = cancellation.Token;

// 2. GET a DTO: the result never throws, it reports status code, content and error
RestResult<Post> post = await api.Command("posts").Command(1).GetAsync<Post>(cancellationToken);
if (post.IsSuccessStatusCode)
{
    Console.WriteLine($"GET /posts/1 -> {(int)post.StatusCode}: {post.Content!.Title}");
}

// GET a list, with query string parameters (?userId=1)
RestResult<List<Post>> posts = await api.Command("posts").Parameter("userId", 1).GetAsync<List<Post>>(cancellationToken);
Console.WriteLine($"GET /posts?userId=1 -> {(int)posts.StatusCode}: {posts.Content?.Count} posts, {posts.RetryCount} retries");

// 3. POST a DTO as JSON and read the created resource
var newPost = new Post { UserId = 1, Title = "Hello", Body = "Sent with Mafesoft.RestClient" };
RestResult<Post> created = await api.Command("posts").Payload(newPost).PostAsync<Post>(cancellationToken);
Console.WriteLine($"POST /posts -> {(int)created.StatusCode}: id {created.Content?.Id}");

// 4. Error handling: an HTTP error is a result, not an exception
RestResult<Post> missing = await api.Command("posts").Command(0).GetAsync<Post>(cancellationToken);
if (missing.StatusCode == HttpStatusCode.NotFound)
{
    Console.WriteLine("GET /posts/0 -> 404 Not Found");
}

// network errors, timeouts and cancellation are reported by Exception (StatusCode is 0)
RestResult<string> unreachable = await Rest.Build()
    .Url("https://unreachable.invalid")
    .Timeout(TimeSpan.FromSeconds(5))
    .GetAsync(cancellationToken);
Console.WriteLine($"GET https://unreachable.invalid -> {unreachable.Exception?.GetType().Name}: {unreachable.Exception?.Message}");

// or turn any failure into an exception
try
{
    (await api.Command("posts").Command(0).GetAsync<Post>(cancellationToken)).EnsureSuccessStatusCode();
}
catch (HttpRequestException ex)
{
    Console.WriteLine($"EnsureSuccessStatusCode -> {ex.Message}");
}

// 5. Cancellation: the token cancels the request (and the retries); the result reports an OperationCanceledException
using (var canceledSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
{
    canceledSource.Cancel();
    RestResult<List<Post>> canceled = await api.Command("posts").GetAsync<List<Post>>(canceledSource.Token);
    Console.WriteLine($"Canceled request -> {canceled.Exception?.GetType().Name}");
}

public sealed class Post
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public string? Title { get; set; }

    public string? Body { get; set; }
}
