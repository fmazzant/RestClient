#if NET
namespace Mafesoft.RestClient.Tests
{
    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.AspNetCore.Http;
    using Microsoft.Extensions.Logging;
    using System.Net;
    using System.Text;

    /// <summary>
    /// GetAsyncEnumerable against a real Kestrel server that writes the JSON array incrementally
    /// </summary>
    public sealed class AsyncEnumerableKestrelTests : IAsyncLifetime
    {
        private WebApplication app;
        private string url;
        private int flakyCalls;
        private readonly TaskCompletionSource gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource serverFinished = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource serverAborted = new(TaskCreationOptions.RunContinuationsAsynchronously);

        private static string Item(int id) => $"{{\"id\":{id},\"name\":\"item-{id}-{new string('x', 100)}\"}}";

        public async Task InitializeAsync()
        {
            WebApplicationBuilder builder = WebApplication.CreateBuilder();
            builder.Logging.ClearProviders();
            builder.WebHost.ConfigureKestrel(kestrel => kestrel.Listen(IPAddress.Loopback, 0));
            app = builder.Build();

            // the first item, then the rest only when the test opens the gate
            app.MapGet("/items/gated", async (HttpContext context) =>
            {
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync("[" + Item(0));
                await context.Response.Body.FlushAsync();
                await gate.Task;
                await context.Response.WriteAsync("," + Item(1) + "]");
                serverFinished.SetResult();
            });

            // count items written in batches
            app.MapGet("/items/many", async (HttpContext context) =>
            {
                int count = int.Parse(context.Request.Query["count"]);
                context.Response.ContentType = "application/json";
                var batch = new StringBuilder("[");
                for (int i = 0; i < count; i++)
                {
                    batch.Append(i == 0 ? "" : ",").Append(Item(i));
                    if (batch.Length > 64 * 1024)
                    {
                        await context.Response.WriteAsync(batch.ToString());
                        batch.Clear();
                    }
                }
                await context.Response.WriteAsync(batch.Append(']').ToString());
            });

            // items until the client goes away
            app.MapGet("/items/endless", async (HttpContext context) =>
            {
                context.Response.ContentType = "application/json";
                try
                {
                    await context.Response.WriteAsync("[" + Item(0), context.RequestAborted);
                    for (int i = 1; ; i++)
                    {
                        await context.Response.WriteAsync("," + Item(i), context.RequestAborted);
                        await context.Response.Body.FlushAsync(context.RequestAborted);
                        await Task.Delay(10, context.RequestAborted);
                    }
                }
                catch (Exception) when (context.RequestAborted.IsCancellationRequested)
                {
                    serverAborted.TrySetResult();
                }
            });

            app.MapGet("/items/flaky", (HttpContext context) => Interlocked.Increment(ref flakyCalls) == 1
                ? Results.StatusCode(503)
                : Results.Text("[" + Item(1) + "," + Item(2) + "]", "application/json"));

            app.MapGet("/items/missing", () => Results.Problem(statusCode: 404, title: "Not found"));

            await app.StartAsync();
            url = app.Urls.Single();
        }

        public async Task DisposeAsync()
        {
            gate.TrySetResult();
            await app.DisposeAsync();
        }

        private RestBuilder Root => Rest.Build().Url(url).Command("items");

        [Fact]
        public async Task Items_are_received_before_the_response_ends()
        {
            var ids = new List<int>();
            await foreach (User user in Root.Command("gated").GetAsyncEnumerable<User>())
            {
                if (ids.Count == 0)
                {
                    // the server is still waiting: the first item has been read from an incomplete response
                    Assert.False(serverFinished.Task.IsCompleted);
                    gate.SetResult();
                }
                ids.Add(user.Id);
            }

            Assert.Equal(new[] { 0, 1 }, ids);
            Assert.True(serverFinished.Task.IsCompleted);
        }

        [Fact]
        public async Task Memory_does_not_grow_with_the_number_of_items()
        {
            const int count = 200_000;
            WeakReference first = null;
            long baseline = 0;
            long growth = 0;
            int received = 0;

            await foreach (User user in Root.Command("many").Parameter("count", count).GetAsyncEnumerable<User>())
            {
                Assert.Equal(received, user.Id);
                if (received == 0)
                {
                    first = new WeakReference(user);
                }
                else if (received == 10_000)
                {
                    baseline = GC.GetTotalMemory(forceFullCollection: true);
                }
                else if (received == count - 1)
                {
                    growth = GC.GetTotalMemory(forceFullCollection: true) - baseline;
                }
                received++;
            }

            // ~25 MB of JSON, ~50 MB of objects if they were kept: the items are not retained
            Assert.Equal(count, received);
            Assert.False(first.IsAlive);
            Assert.True(growth < 16 * 1024 * 1024, $"memory grew by {growth} bytes");
        }

        [Fact]
        public async Task Cancellation_while_reading_throws_OperationCanceledException_and_closes_the_response()
        {
            using var cts = new CancellationTokenSource();
            int received = 0;

            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            {
                await foreach (User user in Root.Command("endless").GetAsyncEnumerable<User>(cts.Token))
                {
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
            await foreach (User user in Root.Command("endless").GetAsyncEnumerable<User>())
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
            var ids = new List<int>();
            await foreach (User user in Root.Command("flaky").Retry(1, TimeSpan.Zero).GetAsyncEnumerable<User>())
            {
                ids.Add(user.Id);
            }

            Assert.Equal(new[] { 1, 2 }, ids);
            Assert.Equal(2, flakyCalls);
        }

        [Fact]
        public async Task Error_status_throws_the_exception_of_EnsureSuccessStatusCode()
        {
            RestResult<string> result = await Root.Command("missing").GetAsync();
            HttpRequestException expected = Assert.Throws<HttpRequestException>(() => result.EnsureSuccessStatusCode());

            HttpRequestException actual = await Assert.ThrowsAsync<HttpRequestException>(async () =>
            {
                await foreach (User user in Root.Command("missing").GetAsyncEnumerable<User>())
                {
                }
            });

            Assert.Equal(expected.Message, actual.Message);
        }
    }
}
#endif
