using Mafesoft.RestClient;

/// <summary>
/// Typed client registered with AddRestClient&lt;PostsClient&gt;(): it receives a RestBuilder configured in Program.cs,
/// that sends the requests through IHttpClientFactory (pooled handlers, resilience handler, logging).
/// </summary>
public sealed class PostsClient(RestBuilder rest)
{
    public Task<RestResult<Post>> GetAsync(int id, CancellationToken cancellationToken)
        => rest.Command("posts").Command(id).GetAsync<Post>(cancellationToken);

    public Task<RestResult<List<Post>>> GetByUserAsync(int userId, CancellationToken cancellationToken)
        => rest.Command("posts").Parameter("userId", userId).GetAsync<List<Post>>(cancellationToken);

    public Task<RestResult<Post>> CreateAsync(Post post, CancellationToken cancellationToken)
        => rest.Command("posts").Payload(post).PostAsync<Post>(cancellationToken);
}

public sealed class Post
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public string? Title { get; set; }

    public string? Body { get; set; }
}
