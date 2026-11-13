#if NET
namespace Mafesoft.RestClient.Tests
{
    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.AspNetCore.Http;
    using Microsoft.Extensions.Logging;
    using System.Net;
    using System.Net.ServerSentEvents;
    using System.Text.Json;

    /// <summary>
    /// GetEventsAsync / PostEventsAsync against a real Kestrel server that writes the events one at a time
    /// </summary>
    public sealed class ServerSentEventsKestrelTests : IAsyncLifetime
    {
        private WebApplication app;
        private string url;
        private int flakyCalls;
        private readonly TaskCompletionSource gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource serverFinished = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource serverAborted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private string accept;

        private static async Task WriteEventAsync(HttpContext context, string data, string type = null, CancellationToken cancellationToken = default)
        {
            string text = (type == null ? "" : $"event: {type}\n") + $"data: {data}\n\n";
            await context.Response.WriteAsync(text, cancellationToken);
            await context.Response.Body.FlushAsync(cancellationToken);
        }

        public async Task InitializeAsync()
        {
            WebApplicationBuilder builder = WebApplication.CreateBuilder();
            builder.Logging.ClearProviders();
            builder.WebHost.ConfigureKestrel(kestrel => kestrel.Listen(IPAddress.Loopback, 0));
            app = builder.Build();

            // an LLM-like stream: the request is echoed in the chunks, the first chunk is sent, the rest only when
            // the test opens the gate, then the [DONE] sentinel
            app.MapPost("/events/chat", async (HttpContext context) =>
            {
                accept = context.Request.Headers.Accept;
                User request = await JsonSerializer.DeserializeAsync<User>(context.Request.Body, new JsonSerializerOptions(JsonSerializerDefaults.Web));
                context.Response.ContentType = "text/event-stream";
                await WriteEventAsync(context, $"{{\"id\":0,\"name\":\"{request.Name}\"}}");
                await gate.Task;
                for (int i = 1; i < 3; i++)
                {
                    await Task.Delay(20);
                    await WriteEventAsync(context, $"{{\"id\":{i},\"name\":\"chunk-{i}\"}}");
                }
                await WriteEventAsync(context, "[DONE]");
                serverFinished.SetResult();
            });

            // events until the client goes away
            app.MapGet("/events/endless", async (HttpContext context) =>
            {
                context.Response.ContentType = "text/event-stream";
                try
                {
                    for (int i = 0; ; i++)
                    {
                        await WriteEventAsync(context, $"{{\"id\":{i}}}", "tick", context.RequestAborted);
                        await Task.Delay(10, context.RequestAborted);
                    }
                }
                catch (Exception) when (context.RequestAborted.IsCancellationRequested)
                {
                    serverAborted.TrySetResult();
                }
            });

            app.MapGet("/events/flaky", async (HttpContext context) =>
            {
                if (Interlocked.Increment(ref flakyCalls) == 1)
                {
                    context.Response.StatusCode = 503;
                    return;
                }
                context.Response.ContentType = "text/event-stream";
                await WriteEventAsync(context, "first");
                await WriteEventAsync(context, "second");
            });

            app.MapGet("/events/missing", () => Results.Problem(statusCode: 404, title: "Not found"));

            await app.StartAsync();
            url = app.Urls.Single();
        }

        public async Task DisposeAsync()
        {
            gate.TrySetResult();
            await app.DisposeAsync();
        }

        private RestBuilder Root => Rest.Build().Url(url).Command("events");

        [Fact]
        public async Task Events_are_received_before_the_response_ends()
        {
            var names = new List<string>();
            await foreach (SseItem<User> item in Root.Command("chat").Payload(new User { Name = "hello" }).PostEventsAsync<User>((type, data) => data != "[DONE]"))
            {
                if (names.Count == 0)
                {
                    // the server is still waiting: the first event has been read from an incomplete response
                    Assert.False(serverFinished.Task.IsCompleted);
                    gate.SetResult();
                }
                names.Add(item.Data.Name);
            }

            Assert.Equal(new[] { "hello", "chunk-1", "chunk-2" }, names);
            Assert.True(serverFinished.Task.IsCompleted);
            Assert.Equal("text/event-stream", accept);
        }

        [Fact]
        public async Task Cancellation_while_reading_throws_OperationCanceledException_and_closes_the_response()
        {
            using var cts = new CancellationTokenSource();
            int received = 0;

            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            {
                await foreach (SseItem<User> item in Root.Command("endless").GetEventsAsync<User>(cts.Token))
                {
                    Assert.Equal("tick", item.EventType);
                    Assert.Equal(received, item.Data.Id);
                    if (++received == 5)
                    {
                        cts.Cancel();
                    }
                }
            });

            Assert.Equal(5, received);
            await serverAborted.Task.WaitAsync(TimeSpan.FromSeconds(10));
        }

        [Fact]
        public async Task Break_closes_the_response()
        {
            int received = 0;
            await foreach (SseItem<string> item in Root.Command("endless").GetEventsAsync())
            {
                if (++received == 3)
                {
                    break;
                }
            }

            await serverAborted.Task.WaitAsync(TimeSpan.FromSeconds(10));
        }

        [Fact]
        public async Task Transient_errors_are_retried_before_the_headers()
        {
            var data = new List<string>();
            await foreach (SseItem<string> item in Root.Command("flaky").Retry(1, TimeSpan.Zero).GetEventsAsync())
            {
                data.Add(item.Data);
            }

            Assert.Equal(new[] { "first", "second" }, data);
            Assert.Equal(2, flakyCalls);
        }

        [Fact]
        public async Task Error_status_throws_the_exception_of_EnsureSuccessStatusCode()
        {
            RestResult<string> result = await Root.Command("missing").GetAsync();
            HttpRequestException expected = Assert.Throws<HttpRequestException>(() => result.EnsureSuccessStatusCode());

            HttpRequestException actual = await Assert.ThrowsAsync<HttpRequestException>(async () =>
            {
                await foreach (SseItem<User> item in Root.Command("missing").GetEventsAsync<User>())
                {
                }
            });

            Assert.Equal(expected.Message, actual.Message);
        }
    }
}
#endif
