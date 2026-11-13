namespace Mafesoft.RestClient.Tests
{
    using Microsoft.Extensions.Logging;
    using System.Collections.Concurrent;
    using System.Diagnostics;
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Headers;

    public class ResilienceAndDiagnosticsTests
    {
        private static RestBuilder Root(FakeHttpMessageHandler handler)
            => Rest.Build(handler.CreateClient()).Url("https://api.example.com");

        [Fact]
        public async Task Retry_transient_status_codes_for_idempotent_methods()
        {
            var statuses = new Queue<HttpStatusCode>(new[] { HttpStatusCode.ServiceUnavailable, HttpStatusCode.BadGateway, HttpStatusCode.OK });
            var handler = new FakeHttpMessageHandler((r, i) => new HttpResponseMessage(statuses.Dequeue()) { Content = new StringContent("ok") });

            RestResult<string> result = await Root(handler).Retry(3, TimeSpan.Zero).GetAsync();

            Assert.Equal(HttpStatusCode.OK, result.StatusCode);
            Assert.Equal(2, result.RetryCount);
            Assert.Equal(3, handler.Requests.Count);
        }

        [Fact]
        public async Task Retry_gives_up_after_max_retries_and_returns_the_last_response()
        {
            var handler = FakeHttpMessageHandler.Returns(HttpStatusCode.ServiceUnavailable);

            RestResult<string> result = await Root(handler).Retry(2, TimeSpan.Zero).GetAsync();

            Assert.Equal(HttpStatusCode.ServiceUnavailable, result.StatusCode);
            Assert.Equal(2, result.RetryCount);
            Assert.Equal(3, handler.Requests.Count);
        }

        [Fact]
        public async Task Retry_does_not_repeat_non_idempotent_methods_by_default()
        {
            var handler = FakeHttpMessageHandler.Returns(HttpStatusCode.ServiceUnavailable);

            await Root(handler).Retry(3, TimeSpan.Zero).PostAsync();

            Assert.Single(handler.Requests);
        }

        [Fact]
        public async Task Retry_network_errors_and_timeouts()
        {
            var handler = new FakeHttpMessageHandler(async (r, i) =>
            {
                if (i == 0) { throw new HttpRequestException("connection refused"); }
                if (i == 1) { await Task.Delay(TimeSpan.FromSeconds(30)); }
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("ok") };
            });

            RestResult<string> result = await Root(handler).Timeout(TimeSpan.FromMilliseconds(200)).Retry(2, TimeSpan.Zero).GetAsync();

            Assert.Equal("ok", result.Content);
            Assert.Equal(2, result.RetryCount);
        }

        [Fact]
        public async Task Retry_honors_retry_after()
        {
            int calls = 0;
            var handler = new FakeHttpMessageHandler((r, i) =>
            {
                var response = new HttpResponseMessage(++calls == 1 ? (HttpStatusCode)429 : HttpStatusCode.OK);
                response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromMilliseconds(300));
                return response;
            });

            Stopwatch stopwatch = Stopwatch.StartNew();
            RestResult<string> result = await Root(handler).Retry(1, TimeSpan.Zero).GetAsync();

            Assert.Equal(HttpStatusCode.OK, result.StatusCode);
            Assert.True(stopwatch.ElapsedMilliseconds >= 250, $"elapsed {stopwatch.ElapsedMilliseconds}ms");
        }

        [Fact]
        public async Task Concurrent_unauthorized_requests_refresh_the_token_once()
        {
            string token = "expired";
            var handler = new FakeHttpMessageHandler(async (r, i) =>
            {
                await Task.Delay(20);
                return new HttpResponseMessage(r.Headers.Authorization.Parameter == "valid" ? HttpStatusCode.OK : HttpStatusCode.Unauthorized);
            });
            var refreshHandler = FakeHttpMessageHandler.Returns(HttpStatusCode.OK);
            int refreshCalls = 0;

            var api = Root(handler)
                .Authentication(() => new AuthenticationHeaderValue("Bearer", token))
                .RefreshToken(async ct =>
                {
                    Interlocked.Increment(ref refreshCalls);
                    await Task.Delay(50, ct);
                    token = "valid";
                    return await Root(refreshHandler).PostAsync(ct);
                });

            RestResult<string>[] results = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => api.Command("data").GetAsync()));

            Assert.All(results, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
            Assert.Equal(1, refreshCalls);
        }

        [Fact]
        public async Task Refresh_token_retries_once_and_the_builder_keeps_working()
        {
            string token = "expired";
            var handler = new FakeHttpMessageHandler((r, i) =>
                new HttpResponseMessage(r.Headers.Authorization.Parameter == "valid" ? HttpStatusCode.OK : HttpStatusCode.Unauthorized));
            int refreshCalls = 0;

            var api = Root(handler)
                .Authentication(() => new AuthenticationHeaderValue("Bearer", token))
                .RefreshToken(async ct =>
                {
                    refreshCalls++;
                    token = "valid";
                    return await Root(handler).Authentication("Bearer", token).GetAsync(ct);
                });

            Assert.Equal(HttpStatusCode.OK, (await api.GetAsync()).StatusCode);
            token = "expired-again";
            Assert.Equal(HttpStatusCode.OK, (await api.GetAsync()).StatusCode);
            Assert.Equal(2, refreshCalls);
        }

        [Fact]
        public void Refresh_token_works_with_synchronous_requests()
        {
            string token = "expired";
            var handler = new FakeHttpMessageHandler((r, i) =>
                new HttpResponseMessage(r.Headers.Authorization.Parameter == "valid" ? HttpStatusCode.OK : HttpStatusCode.Unauthorized));

            var api = Root(handler)
                .Authentication(() => new AuthenticationHeaderValue("Bearer", token))
                .RefreshToken(async ct =>
                {
                    token = "valid";
                    return await Root(handler).Authentication("Bearer", token).GetAsync(ct);
                });

            Assert.Equal(HttpStatusCode.OK, api.Get().StatusCode);
        }

        [Fact]
        public async Task Refresh_func_receives_the_cancellation_token_of_the_request()
        {
            var handler = FakeHttpMessageHandler.Returns(HttpStatusCode.Unauthorized);
            using var cts = new CancellationTokenSource();
            CancellationToken received = default;

            await Root(handler)
                .RefreshToken(async ct =>
                {
                    received = ct;
                    return await Root(handler).PostAsync(ct);   // 401: the refresh fails
                })
                .GetAsync(cts.Token);

            Assert.True(received.CanBeCanceled);
            cts.Cancel();
            Assert.True(received.IsCancellationRequested);
        }

        [Fact]
        public async Task Refresh_token_null_disables_the_refresh_on_the_derived_builder()
        {
            var handler = FakeHttpMessageHandler.Returns(HttpStatusCode.Unauthorized);
            int refreshCalls = 0;
            var api = Root(handler).RefreshToken(async ct =>
            {
                refreshCalls++;
                return await Root(handler).PostAsync(ct);   // 401: the refresh fails
            });

            RestResult<string> result = await api.RefreshToken(null).GetAsync();

            Assert.Equal(HttpStatusCode.Unauthorized, result.StatusCode);
            Assert.Equal(0, refreshCalls);
            await api.GetAsync();
            Assert.Equal(1, refreshCalls);
        }

        [Fact]
        public async Task Refresh_func_requests_never_trigger_a_nested_refresh()
        {
            var handler = FakeHttpMessageHandler.Returns(HttpStatusCode.Unauthorized);
            int refreshCalls = 0;
            RestBuilder api = null;
            api = Root(handler).RefreshToken(async ct =>
            {
                refreshCalls++;
                return await api.Command("refresh").PostAsync(ct);   // same coordinator, receives 401 too
            });

            RestResult<string> result = await api.GetAsync();

            Assert.Equal(HttpStatusCode.Unauthorized, result.StatusCode);
            Assert.Equal(1, refreshCalls);
        }

        [Fact]
        public async Task Logger_writes_requests_without_query_string()
        {
            var logger = new ListLogger();
            var handler = FakeHttpMessageHandler.Returns(HttpStatusCode.OK);

            await Root(handler).Logger(logger).Command("users").Parameter("apiKey", "secret").GetAsync();

            Assert.Contains(logger.Entries, e => e.Level == LogLevel.Debug && e.Message == "Sending HTTP GET https://api.example.com/users");
            Assert.Contains(logger.Entries, e => e.Level == LogLevel.Information && e.Message.StartsWith("Received HTTP 200 for GET https://api.example.com/users in "));
            Assert.DoesNotContain(logger.Entries, e => e.Message.Contains("secret"));
        }

        [Fact]
        public async Task Logger_writes_the_url_with_the_route_values()
        {
            var logger = new ListLogger();
            var handler = FakeHttpMessageHandler.Returns(HttpStatusCode.OK);

            await Root(handler).Logger(logger).Command("users/{id}").Route("id", "a b").Parameter("apiKey", "secret").GetAsync();

            Assert.Contains(logger.Entries, e => e.Level == LogLevel.Debug && e.Message == "Sending HTTP GET https://api.example.com/users/a%20b");
            Assert.DoesNotContain(logger.Entries, e => e.Message.Contains("secret"));
        }

        [Fact]
        public async Task Logger_writes_retries_and_errors()
        {
            var logger = new ListLogger();
            var handler = new FakeHttpMessageHandler((r, i) => Task.FromException<HttpResponseMessage>(new HttpRequestException("down")));

            await Root(handler).Logger(logger).Retry(1, TimeSpan.Zero).GetAsync();

            Assert.Contains(logger.Entries, e => e.Level == LogLevel.Warning && e.Message.Contains("Retrying HTTP GET") && e.Message.Contains("down"));
            Assert.Contains(logger.Entries, e => e.Level == LogLevel.Error && e.Exception is HttpRequestException);
        }

        [Fact]
        public async Task Activities_are_created()
        {
            var activities = new ConcurrentBag<Activity>();
            using var listener = new ActivityListener
            {
                ShouldListenTo = source => source.Name == RestClientTelemetry.ActivitySourceName,
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
                ActivityStopped = activities.Add
            };
            ActivitySource.AddActivityListener(listener);

            var handler = FakeHttpMessageHandler.Returns(HttpStatusCode.NotFound);
            await Root(handler).Command("tracing-test").Parameter("q", "x").GetAsync();

            Activity activity = activities.Single(a => (string)a.GetTagItem("url.full") == "https://api.example.com/tracing-test");
            Assert.Equal("RestClient GET", activity.DisplayName);
            Assert.Equal("GET", activity.GetTagItem("http.request.method"));
            Assert.Equal(404, activity.GetTagItem("http.response.status_code"));
            Assert.Equal(ActivityStatusCode.Error, activity.Status);
        }

        private sealed class ListLogger : ILogger
        {
            public ConcurrentQueue<(LogLevel Level, string Message, Exception Exception)> Entries { get; } = new();

            public IDisposable BeginScope<TState>(TState state) => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
                => Entries.Enqueue((logLevel, formatter(state, exception), exception));
        }
    }
}
