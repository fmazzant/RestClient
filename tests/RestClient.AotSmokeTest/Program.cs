using RestClient;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

// Runs the main features of RestClient in a Native AOT executable, against an in-process HTTP handler.
// Exit code 0 when every check passes.

var failures = new List<string>();

void Check(bool condition, string name)
{
    Console.WriteLine($"{(condition ? "PASS" : "FAIL")} {name}");
    if (!condition)
    {
        failures.Add(name);
    }
}

using var handler = new EchoHandler();
using var httpClient = new HttpClient(handler);

RestBuilder root = Rest.Build(httpClient)
    .Url("https://aot.example.com/api")
    .Json(AppJsonContext.Default)
    .Authentication(() => new AuthenticationHeaderValue("Bearer", "token"))
    .Retry(2, TimeSpan.Zero);

// JSON payload and response through the source generated context
RestResult<User> created = await root.Command("users").Payload(new User { Id = 1, Name = "Mario" }).PostAsync<User>();
Check(created.IsSuccessStatusCode && created.Content?.Name == "Mario" && created.Content.Id == 1, "json round trip");

// collections, query string and retry of a transient error
RestResult<List<User>> users = await root.Command("flaky").Parameter("id", new[] { 1, 2 }).GetAsync<List<User>>();
Check(users.IsSuccessStatusCode && users.Content?.Count == 2 && users.RetryCount == 1, "retry and collections");
Check(handler.LastQuery == "?id=1&id=2", "repeated query keys");

// raw string, bytes and streaming
RestResult<string> text = await root.Command("text").GetAsync();
Check(text.Content == "plain text", "string result");

RestResult<byte[]> bytes = await root.Command("text").GetAsync<byte[]>();
Check(bytes.Content is not null && Encoding.UTF8.GetString(bytes.Content) == "plain text", "byte[] result");

using (RestResult<Stream> stream = await root.Command("text").GetAsync<Stream>())
using (var reader = new StreamReader(stream.Content!))
{
    Check(await reader.ReadToEndAsync() == "plain text", "stream result");
}

// multipart upload
RestResult<string> upload = await root.Command("upload")
    .Multipart(m => m.Add("title", "aot").AddFile("file", Encoding.UTF8.GetBytes("content"), "a.txt", "text/plain"))
    .PostAsync();
Check(upload.Content?.Contains("filename=a.txt") == true, "multipart upload");

// errors are reported, not thrown
RestResult<User> notFound = await root.Command("missing").GetAsync<User>();
Check(notFound.StatusCode == HttpStatusCode.NotFound && !notFound.IsSuccessStatusCode, "error status");

Console.WriteLine(failures.Count == 0 ? "All checks passed." : $"{failures.Count} check(s) failed.");
return failures.Count == 0 ? 0 : 1;

public sealed class User
{
    public int Id { get; set; }
    public string? Name { get; set; }
}

[JsonSerializable(typeof(User))]
[JsonSerializable(typeof(List<User>))]
[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
public partial class AppJsonContext : JsonSerializerContext
{
}

/// <summary>
/// In-process server: echoes JSON bodies, fails "flaky" once, returns text and 404.
/// </summary>
internal sealed class EchoHandler : HttpMessageHandler
{
    private int flakyCalls;

    public string? LastQuery { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        string path = request.RequestUri!.AbsolutePath;
        LastQuery = request.RequestUri.Query;
        string body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);

        if (request.Headers.Authorization?.Parameter != "token")
        {
            return new HttpResponseMessage(HttpStatusCode.Unauthorized);
        }

        return path switch
        {
            "/api/users" => Json(body),
            "/api/flaky" when Interlocked.Increment(ref flakyCalls) == 1 => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable),
            "/api/flaky" => Json("[{\"id\":1,\"name\":\"a\"},{\"id\":2,\"name\":\"b\"}]"),
            "/api/text" => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("plain text") },
            "/api/upload" => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) },
            _ => new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StringContent("{\"error\":\"not found\"}") },
        };
    }

    private static HttpResponseMessage Json(string json)
        => new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
}
