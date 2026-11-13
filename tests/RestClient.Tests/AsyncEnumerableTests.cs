namespace Mafesoft.RestClient.Tests
{
    using System.Collections.Concurrent;
    using System.Diagnostics;
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Text;
    using System.Text.Json;

    /// <summary>
    /// GetAsyncEnumerable / SendAsyncEnumerable against an in-process handler (every target framework, .NET Framework included)
    /// </summary>
    public class AsyncEnumerableTests
    {
        private const string TwoUsers = "[{\"id\":1,\"name\":\"a\"},{\"id\":2,\"name\":\"b\"}]";

        private static RestBuilder Root(FakeHttpMessageHandler handler)
            => Rest.Build(handler.CreateClient()).Url("https://api.example.com/v1/");

        private static async Task<List<T>> ToListAsync<T>(IAsyncEnumerable<T> items)
        {
            var list = new List<T>();
            await foreach (T item in items)
            {
                list.Add(item);
            }
            return list;
        }

        private static HttpResponseMessage StreamResponse(Stream stream)
            => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StreamContent(stream) { Headers = { ContentType = new MediaTypeHeaderValue("application/json") } }
            };

        [Fact]
        public async Task Items_of_a_json_array_are_returned()
        {
            var handler = FakeHttpMessageHandler.Returns(HttpStatusCode.OK, TwoUsers);

            List<User> users = await ToListAsync(Root(handler).Command("users").GetAsyncEnumerable<User>());

            Assert.Equal(new[] { 1, 2 }, users.Select(u => u.Id));
            Assert.Equal(new[] { "a", "b" }, users.Select(u => u.Name));
            Assert.Equal(HttpMethod.Get, handler.Requests.Single().Method);
            Assert.Equal("https://api.example.com/v1/users", handler.Requests.Single().RequestUri.ToString());
        }

        [Fact]
        public async Task Nothing_is_sent_until_the_enumeration_starts_and_every_enumeration_sends_a_request()
        {
            var handler = FakeHttpMessageHandler.Returns(HttpStatusCode.OK, TwoUsers);

            IAsyncEnumerable<User> users = Root(handler).GetAsyncEnumerable<User>();
            Assert.Empty(handler.Requests);

            Assert.Equal(2, (await ToListAsync(users)).Count);
            Assert.Equal(2, (await ToListAsync(users)).Count);
            Assert.Equal(2, handler.Requests.Count);
        }

        [Fact]
        public async Task Empty_array_and_null_items()
        {
            var handler = new FakeHttpMessageHandler((r, i) => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(i == 0 ? "[]" : "[null,{\"id\":3}]", Encoding.UTF8, "application/json")
            });

            Assert.Empty(await ToListAsync(Root(handler).GetAsyncEnumerable<User>()));
            List<User> users = await ToListAsync(Root(handler).GetAsyncEnumerable<User>());
            Assert.Null(users[0]);
            Assert.Equal(3, users[1].Id);
        }

        [Fact]
        public async Task Error_status_throws_the_exception_of_EnsureSuccessStatusCode()
        {
            var handler = FakeHttpMessageHandler.Returns(HttpStatusCode.NotFound, "{\"error\":\"missing\"}");
            RestBuilder api = Root(handler).Command("missing");

            RestResult<List<User>> result = await api.GetAsync<List<User>>();
            HttpRequestException expected = Assert.Throws<HttpRequestException>(() => result.EnsureSuccessStatusCode());
            HttpRequestException actual = await Assert.ThrowsAsync<HttpRequestException>(() => ToListAsync(api.GetAsyncEnumerable<User>()));

            Assert.Equal(expected.Message, actual.Message);
            Assert.Contains("404", actual.Message);
        }

        [Fact]
        public async Task Request_failure_throws_the_exception_of_EnsureSuccessStatusCode()
        {
            var handler = new FakeHttpMessageHandler((Func<HttpRequestMessage, int, HttpResponseMessage>)((r, i) => throw new HttpRequestException("connection refused")));
            var exceptions = new ConcurrentQueue<Exception>();
            RestBuilder api = Root(handler).OnException(exceptions.Enqueue);

            RestResult<List<User>> result = await api.GetAsync<List<User>>();
            HttpRequestException expected = Assert.Throws<HttpRequestException>(() => result.EnsureSuccessStatusCode());
            HttpRequestException actual = await Assert.ThrowsAsync<HttpRequestException>(() => ToListAsync(api.GetAsyncEnumerable<User>()));

            Assert.Equal(expected.Message, actual.Message);
            Assert.Equal("connection refused", actual.InnerException.Message);
            Assert.Equal(2, exceptions.Count);
        }

        [Fact]
        public async Task Timeout_throws_HttpRequestException_with_an_inner_TimeoutException()
        {
            var handler = new FakeHttpMessageHandler((r, i) => new TaskCompletionSource<HttpResponseMessage>().Task);

            HttpRequestException exception = await Assert.ThrowsAsync<HttpRequestException>(
                () => ToListAsync(Root(handler).Timeout(TimeSpan.FromMilliseconds(100)).GetAsyncEnumerable<User>()));

            Assert.IsType<TimeoutException>(exception.InnerException);
        }

        [Fact]
        public async Task Transient_errors_are_retried_before_the_headers()
        {
            var handler = new FakeHttpMessageHandler((r, i) => i == 0
                ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
                : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(TwoUsers, Encoding.UTF8, "application/json") });

            List<User> users = await ToListAsync(Root(handler).Retry(2, TimeSpan.Zero).GetAsyncEnumerable<User>());

            Assert.Equal(2, users.Count);
            Assert.Equal(2, handler.Requests.Count);
        }

        [Fact]
        public async Task Unauthorized_refreshes_the_token_and_sends_the_request_again()
        {
            string token = "expired";
            var handler = new FakeHttpMessageHandler((r, i) => r.Headers.Authorization.Parameter == "valid"
                ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(TwoUsers, Encoding.UTF8, "application/json") }
                : new HttpResponseMessage(HttpStatusCode.Unauthorized));
            var refreshHandler = FakeHttpMessageHandler.Returns(HttpStatusCode.OK);

            RestBuilder api = Root(handler)
                .BearerToken(ct => Task.FromResult(token))
                .RefreshToken(async ct =>
                {
                    token = "valid";
                    return await Root(refreshHandler).PostAsync(ct);
                });

            Assert.Equal(2, (await ToListAsync(api.GetAsyncEnumerable<User>())).Count);
            Assert.Equal(2, handler.Requests.Count);
        }

        [Fact]
        public async Task Post_sends_the_payload()
        {
            var handler = FakeHttpMessageHandler.Returns(HttpStatusCode.OK, TwoUsers);

            List<User> users = await ToListAsync(Root(handler).Payload(new User { Id = 7 }).PostAsyncEnumerable<User>());

            Assert.Equal(2, users.Count);
            Assert.Equal(HttpMethod.Post, handler.Requests.Single().Method);
            Assert.Contains("\"id\":7", handler.RequestBodies.Single());
        }

        [Fact]
        public async Task Json_context_is_used_and_a_missing_type_is_reported_before_sending()
        {
            var handler = FakeHttpMessageHandler.Returns(HttpStatusCode.OK, TwoUsers);
            RestBuilder api = Root(handler).Json(TestJsonContext.Default);

            List<User> users = await ToListAsync(api.GetAsyncEnumerable<User>());
            Assert.Equal(new[] { "a", "b" }, users.Select(u => u.Name));

            Assert.Throws<NotSupportedException>(() => api.GetAsyncEnumerable<Uri>());
            Assert.Single(handler.Requests);
        }

        [Fact]
        public void Xml_and_custom_serializers_are_not_supported()
        {
            var handler = FakeHttpMessageHandler.Returns(HttpStatusCode.OK, TwoUsers);

            NotSupportedException exception = Assert.Throws<NotSupportedException>(() => Root(handler).Xml().GetAsyncEnumerable<User>());
            Assert.Contains("JSON", exception.Message);
            Assert.Throws<ArgumentNullException>(() => Root(handler).SendAsyncEnumerable<User>(null));
            Assert.Empty(handler.Requests);
        }

        [Fact]
        public async Task Cancellation_before_the_enumeration_and_OnStart_throw_OperationCanceledException()
        {
            var handler = FakeHttpMessageHandler.Returns(HttpStatusCode.OK, TwoUsers);
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => ToListAsync(Root(handler).GetAsyncEnumerable<User>(cts.Token)));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => ToListAsync(Root(handler).OnStart(e => e.Cancel = true).GetAsyncEnumerable<User>()));
        }

        [Fact]
        public async Task Cancellation_while_reading_aborts_the_response()
        {
            // the stream ignores the cancellation token (as the response stream of .NET Framework): the read is
            // aborted by disposing the response
            var stream = new GatedStream("[{\"id\":1},");
            var handler = new FakeHttpMessageHandler((r, i) => StreamResponse(stream));
            using var cts = new CancellationTokenSource();
            var received = new List<User>();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            {
                await foreach (User user in Root(handler).GetAsyncEnumerable<User>().WithCancellation(cts.Token))
                {
                    received.Add(user);
                    cts.CancelAfter(50);
                }
            });

            Assert.Single(received);
            Assert.True(stream.Disposed);
        }

        [Fact]
        public async Task Break_disposes_the_response()
        {
            var stream = new GatedStream("[{\"id\":1},{\"id\":2},");
            var handler = new FakeHttpMessageHandler((r, i) => StreamResponse(stream));

            await foreach (User user in Root(handler).GetAsyncEnumerable<User>())
            {
                Assert.False(stream.Disposed);
                break;
            }

            Assert.True(stream.Disposed);
        }

        [Fact]
        public async Task Invalid_json_throws_JsonException_and_disposes_the_response()
        {
            var stream = new GatedStream("[{\"id\":1},{\"id\":", ended: true);
            var handler = new FakeHttpMessageHandler((r, i) => StreamResponse(stream));
            var exceptions = new ConcurrentQueue<Exception>();

            await Assert.ThrowsAnyAsync<JsonException>(() => ToListAsync(Root(handler).OnException(exceptions.Enqueue).GetAsyncEnumerable<User>()));

            Assert.True(stream.Disposed);
            Assert.IsAssignableFrom<JsonException>(Assert.Single(exceptions));
        }

        [Fact]
        public async Task Download_progress_is_raised_and_completed_events_are_not()
        {
            var handler = FakeHttpMessageHandler.Returns(HttpStatusCode.OK, TwoUsers);
            long downloaded = 0;
            bool completed = false;

            await ToListAsync(Root(handler)
                .OnDownloadProgress(e => downloaded = e.CurrentBytes)
                .OnCompleted(e => completed = true)
                .GetAsyncEnumerable<User>());

            Assert.Equal(Encoding.UTF8.GetByteCount(TwoUsers), downloaded);
            Assert.False(completed);
        }

        [Fact]
        public async Task The_activity_lasts_until_the_end_of_the_enumeration()
        {
            var stopped = new ConcurrentBag<Activity>();
            using var listener = new ActivityListener
            {
                ShouldListenTo = source => source.Name == RestClientTelemetry.ActivitySourceName,
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
                ActivityStopped = stopped.Add
            };
            ActivitySource.AddActivityListener(listener);
            const string url = "https://api.example.com/v1/activity-stream";

            var handler = FakeHttpMessageHandler.Returns(HttpStatusCode.OK, TwoUsers);
            await foreach (User user in Root(handler).Command("activity-stream").GetAsyncEnumerable<User>())
            {
                Assert.DoesNotContain(stopped, a => (string)a.GetTagItem("url.full") == url);
            }

            Activity activity = stopped.Single(a => (string)a.GetTagItem("url.full") == url);
            Assert.Equal(200, activity.GetTagItem("http.response.status_code"));
            Assert.NotEqual(ActivityStatusCode.Error, activity.Status);
        }

        /// <summary>
        /// A response stream that returns its content and then waits, ignoring the cancellation token, until it is
        /// disposed (or ends, when ended is true)
        /// </summary>
        private sealed class GatedStream : Stream
        {
            private readonly byte[] content;
            private readonly bool ended;
            private readonly TaskCompletionSource<int> disposed = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
            private int position;

            public GatedStream(string content, bool ended = false)
            {
                this.content = Encoding.UTF8.GetBytes(content);
                this.ended = ended;
            }

            public bool Disposed => disposed.Task.IsCompleted;

            public override bool CanRead => true;
            public override bool CanSeek => false;
            public override bool CanWrite => false;
            public override long Length => throw new NotSupportedException();
            public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

            public override int Read(byte[] buffer, int offset, int count) => ReadAsync(buffer, offset, count, CancellationToken.None).GetAwaiter().GetResult();

            public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            {
                if (Disposed)
                {
                    throw new ObjectDisposedException(nameof(GatedStream));
                }
                if (position < content.Length)
                {
                    int read = Math.Min(count, content.Length - position);
                    Array.Copy(content, position, buffer, offset, read);
                    position += read;
                    return read;
                }
                if (ended)
                {
                    return 0;
                }
                await disposed.Task;
                throw new ObjectDisposedException(nameof(GatedStream));
            }

            public override void Flush() { }
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

            protected override void Dispose(bool disposing)
            {
                disposed.TrySetResult(0);
                base.Dispose(disposing);
            }
        }
    }
}
