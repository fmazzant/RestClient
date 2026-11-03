namespace RestClient.Tests
{
    using System.Globalization;
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Text;
    using System.Text.Json.Serialization;

    public class User
    {
        public int Id { get; set; }
        public string Name { get; set; }
    }

    [JsonSerializable(typeof(User))]
    [JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
    public partial class TestJsonContext : JsonSerializerContext
    {
    }

    public class RestBuilderTests
    {
        private static RestBuilder Root(FakeHttpMessageHandler handler)
            => Rest.Build(handler.CreateClient()).Url("https://api.example.com/v1/");

        [Fact]
        public async Task Builder_is_immutable_and_reusable()
        {
            var handler = FakeHttpMessageHandler.Returns(HttpStatusCode.OK);
            var users = Root(handler).Command("users");

            await users.Command(1).Parameter("a", 1).GetAsync();
            await users.Command(2).GetAsync();
            await users.GetAsync();

            Assert.Equal("https://api.example.com/v1/users/1?a=1", handler.Requests[0].RequestUri.AbsoluteUri);
            Assert.Equal("https://api.example.com/v1/users/2", handler.Requests[1].RequestUri.AbsoluteUri);
            Assert.Equal("https://api.example.com/v1/users", handler.Requests[2].RequestUri.AbsoluteUri);
        }

        [Fact]
        public void Properties_changes_do_not_leak()
        {
            var handler = FakeHttpMessageHandler.Returns(HttpStatusCode.OK);
            var root = Root(handler);
            var child = root.Timeout(TimeSpan.FromSeconds(1));

            Assert.Equal(RestProperties.DefaultTimeout, root.Properties.Timeout);
            Assert.Equal(TimeSpan.FromSeconds(1), child.Properties.Timeout);

            root.Properties.Timeout = TimeSpan.FromSeconds(5);
            Assert.Equal(RestProperties.DefaultTimeout, root.Properties.Timeout);
        }

        [Fact]
        public async Task Query_string_is_encoded_and_culture_invariant()
        {
            var previous = CultureInfo.CurrentCulture;
            CultureInfo.CurrentCulture = new CultureInfo("it-IT");
            try
            {
                var handler = FakeHttpMessageHandler.Returns(HttpStatusCode.OK);
                await Root(handler)
                    .Parameter("q", "a b&c=d")
                    .Parameter("price", 1.5)
                    .Parameter("active", true)
                    .Parameter(new RestParameter("q", "override"))
                    .GetAsync();

                Assert.Equal("?price=1.5&active=true&q=override", handler.Requests[0].RequestUri.Query);

                await Root(handler).Parameter("q", "a b&c=d").GetAsync();
                Assert.Equal("?q=a%20b%26c%3Dd", handler.Requests[1].RequestUri.Query);
            }
            finally
            {
                CultureInfo.CurrentCulture = previous;
            }
        }

        [Fact]
        public async Task Query_string_supports_repeated_keys()
        {
            var handler = FakeHttpMessageHandler.Returns(HttpStatusCode.OK);

            await Root(handler).Parameter("id", new[] { 1, 2, 3 }).Parameter("x", "y").GetAsync();
            await Root(handler).Parameter(p => { p.Add(new RestParameter("tag", "a")); p.Add(new RestParameter("tag", "b")); }).GetAsync();
            await Root(handler).Parameter("id", new[] { 1, 2 }).Parameter("id", 9).GetAsync();

            Assert.Equal("?id=1&id=2&id=3&x=y", handler.Requests[0].RequestUri.Query);
            Assert.Equal("?tag=a&tag=b", handler.Requests[1].RequestUri.Query);
            Assert.Equal("?id=9", handler.Requests[2].RequestUri.Query);
        }

        [Fact]
        public async Task Json_uses_web_defaults()
        {
            var handler = FakeHttpMessageHandler.Returns(HttpStatusCode.OK, "{\"id\":7,\"name\":\"Federico\"}");

            RestResult<User> result = await Root(handler)
                .Payload(new User { Id = 1, Name = "Mario" })
                .PostAsync<User>();

            Assert.True(result.IsSuccessStatusCode);
            Assert.Equal(7, result.Content.Id);
            Assert.Equal("Federico", result.Content.Name);
            Assert.Equal("{\"id\":1,\"name\":\"Mario\"}", handler.RequestBodies[0]);
            Assert.Equal("application/json", handler.Requests[0].Content.Headers.ContentType.MediaType);
        }

        [Fact]
        public async Task Json_source_generated_context()
        {
            var handler = FakeHttpMessageHandler.Returns(HttpStatusCode.OK, "{\"id\":7,\"name\":\"Anna\"}");

            RestResult<User> result = await Root(handler)
                .Json(TestJsonContext.Default)
                .Payload(new User { Id = 1, Name = "Mario" })
                .PostAsync<User>();

            Assert.Equal("Anna", result.Content.Name);
            Assert.Equal("{\"id\":1,\"name\":\"Mario\"}", handler.RequestBodies[0]);
        }

        [Fact]
        public async Task Generic_methods_support_any_type()
        {
            var handler = FakeHttpMessageHandler.Returns(HttpStatusCode.OK, "[1,2,3]");

            RestResult<IReadOnlyList<int>> list = await Root(handler).GetAsync<IReadOnlyList<int>>();
            RestResult<string> text = await Root(handler).GetAsync<string>();
            RestResult<byte[]> bytes = await Root(handler).SendAsync<byte[]>(new HttpMethod("REPORT"));

            Assert.Equal(new[] { 1, 2, 3 }, list.Content);
            Assert.Equal("[1,2,3]", text.Content);
            Assert.Equal(Encoding.UTF8.GetBytes("[1,2,3]"), bytes.Content);
            Assert.Equal("REPORT", handler.Requests[2].Method.Method);
        }

        [Fact]
        public async Task Empty_body_is_a_null_content()
        {
            var handler = FakeHttpMessageHandler.Returns(HttpStatusCode.NoContent, "");
            RestResult<User> result = await Root(handler).GetAsync<User>();

            Assert.True(result.IsSuccessStatusCode);
            Assert.Null(result.Content);
            Assert.Null(result.Exception);
        }

        [Fact]
        public async Task Deserialization_error_keeps_the_http_response()
        {
            var handler = FakeHttpMessageHandler.Returns(HttpStatusCode.NotFound, "<html>not found</html>", "text/html");
            var successHandler = FakeHttpMessageHandler.Returns(HttpStatusCode.OK, "{ not json");

            RestResult<User> notFound = await Root(handler).GetAsync<User>();
            RestResult<User> invalid = await Root(successHandler).GetAsync<User>();

            Assert.Equal(HttpStatusCode.NotFound, notFound.StatusCode);
            Assert.NotNull(notFound.Response);
            Assert.NotNull(notFound.Exception);
            Assert.Equal("<html>not found</html>", await notFound.Response.Content.ReadAsStringAsync());

            Assert.Equal(HttpStatusCode.OK, invalid.StatusCode);
            Assert.NotNull(invalid.Exception);
            Assert.False(invalid.IsSuccessStatusCode);
        }

        [Fact]
        public async Task Network_error_is_reported_without_throwing()
        {
            var handler = new FakeHttpMessageHandler((r, i) => Task.FromException<HttpResponseMessage>(new HttpRequestException("boom")));
            Exception raised = null;

            RestResult<string> result = await Root(handler).OnException(e => raised = e).GetAsync();

            Assert.Null(result.Response);
            Assert.Equal(default(HttpStatusCode), result.StatusCode);
            Assert.Equal("boom", result.ReasonPhrase);
            Assert.Null(result.Version);
            Assert.IsType<HttpRequestException>(result.Exception);
            Assert.Same(result.Exception, raised);
            Assert.Throws<HttpRequestException>(() => result.EnsureSuccessStatusCode());
            result.Dispose();
        }

        [Fact]
        public async Task Timeout_is_reported_as_timeout()
        {
            var handler = new FakeHttpMessageHandler(async (r, i) =>
            {
                await Task.Delay(TimeSpan.FromSeconds(30));
                return new HttpResponseMessage(HttpStatusCode.OK);
            });

            RestResult<string> result = await Root(handler).Timeout(TimeSpan.FromMilliseconds(100)).GetAsync();

            Assert.True(result.IsTimedOut);
            Assert.False(result.IsCancellationRequested);
        }

        [Fact]
        public async Task Cancellation_token_is_reported_as_cancellation()
        {
            var handler = FakeHttpMessageHandler.Returns(HttpStatusCode.OK);
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            RestResult<string> result = await Root(handler).GetAsync(cts.Token);

            Assert.True(result.IsCancellationRequested);
            Assert.False(result.IsTimedOut);
        }

        [Fact]
        public async Task OnStart_can_cancel_the_request()
        {
            var handler = FakeHttpMessageHandler.Returns(HttpStatusCode.OK);
            RestResult<string> result = await Root(handler).OnStart(e => e.Cancel = true).GetAsync();

            Assert.True(result.IsCancellationRequested);
            Assert.Empty(handler.Requests);
        }

        [Fact]
        public async Task Headers_and_authentication_are_applied_to_the_request()
        {
            var handler = FakeHttpMessageHandler.Returns(HttpStatusCode.OK);
            string token = "abc";

            await Root(handler)
                .Authentication(() => new AuthenticationHeaderValue("Bearer", token))
                .Header("X-One", "1")
                .Header(h => h.Add("X-Two", "2"))
                .GetAsync();

            var request = handler.Requests[0];
            Assert.Equal("Bearer abc", request.Headers.Authorization.ToString());
            Assert.Equal("1", request.Headers.GetValues("X-One").Single());
            Assert.Equal("2", request.Headers.GetValues("X-Two").Single());
        }

        [Fact]
        public async Task Head_and_options()
        {
            var handler = FakeHttpMessageHandler.Returns(HttpStatusCode.OK);
            await Root(handler).HeadAsync();
            Root(handler).Options();

            Assert.Equal(HttpMethod.Head, handler.Requests[0].Method);
            Assert.Equal(HttpMethod.Options, handler.Requests[1].Method);
        }

        [Fact]
        public async Task Form_url_encoded_content()
        {
            var handler = FakeHttpMessageHandler.Returns(HttpStatusCode.OK);
            string preview = null;

            await Root(handler)
                .OnPreviewContentRequestAsString(e => preview = e.ContentAsString)
                .FormUrlEncoded(kv => { kv.Add("user name", "mario"); kv.Add("pwd", "a&b"); })
                .PostAsync();

            Assert.Equal("application/x-www-form-urlencoded", handler.Requests[0].Content.Headers.ContentType.MediaType);
            Assert.Equal("user+name=mario&pwd=a%26b", handler.RequestBodies[0]);
            Assert.Equal("user%20name=mario&pwd=a%26b", preview);
        }

        [Fact]
        public async Task Multipart_form_data_content_can_be_retried()
        {
            int calls = 0;
            var handler = new FakeHttpMessageHandler((r, i) =>
                new HttpResponseMessage(++calls == 1 ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.OK));
            using var file = new MemoryStream(Encoding.UTF8.GetBytes("file-content"));

            RestResult<string> result = await Root(handler)
                .Retry(1, TimeSpan.Zero, retryNonIdempotentMethods: true)
                .Multipart(m => m
                    .Add("title", "hello")
                    .AddFile("doc", file, "doc.txt", "text/plain")
                    .AddFile("bin", new byte[] { 1, 2, 3 }, "a.bin"))
                .PostAsync();

            Assert.True(result.IsSuccessStatusCode);
            Assert.Equal(1, result.RetryCount);
            Assert.Equal(2, handler.Requests.Count);
            Assert.Equal("multipart/form-data", handler.Requests[1].Content.Headers.ContentType.MediaType);
            string body = handler.RequestBodies[1];
            Assert.Contains("name=title", body);
            Assert.Contains("hello", body);
            Assert.Contains("filename=doc.txt", body);
            Assert.Contains("file-content", body);
            Assert.Contains("Content-Type: text/plain", body);
            Assert.True(file.CanRead); // the caller's stream is not disposed
        }

        [Fact]
        public async Task Xml_serialization_uses_reader_and_writer_settings()
        {
            var handler = FakeHttpMessageHandler.Returns(HttpStatusCode.OK, "<User><Id>3</Id><Name>Anna</Name></User>", "application/xml");

            RestResult<User> result = await Root(handler)
                .Xml(new System.Xml.XmlReaderSettings(), new System.Xml.XmlWriterSettings { OmitXmlDeclaration = true })
                .Payload(new User { Id = 1, Name = "Mario" })
                .PostAsync<User>();

            Assert.Equal(3, result.Content.Id);
            Assert.StartsWith("<User", handler.RequestBodies[0]);
            Assert.Equal("application/xml", handler.Requests[0].Content.Headers.ContentType.MediaType);
        }

        [Fact]
        public async Task Progress_events_are_raised()
        {
            byte[] payload = Enumerable.Range(0, 100_000).Select(i => (byte)i).ToArray();
            var handler = new FakeHttpMessageHandler((r, i) => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(payload)
            });
            var download = new List<ProgressEventArgs>();
            var upload = new List<ProgressEventArgs>();

            RestResult<byte[]> result = await Root(handler)
                .BufferSize(10_000)
                .Payload(new { text = new string('x', 50_000) })
                .OnUploadProgress(upload.Add)
                .OnDownloadProgress(download.Add)
                .PostAsync<byte[]>();

            Assert.Equal(payload, result.Content);
            Assert.Equal(10, download.Count);
            Assert.Equal(100, download.Last().ProgressPercentage);
            Assert.NotEmpty(upload);
            Assert.Equal(100, upload.Last().ProgressPercentage);
        }

        [Fact]
        public async Task Stream_result_is_not_buffered()
        {
            var release = new TaskCompletionSource<bool>();
            var handler = new FakeHttpMessageHandler((r, i) => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StreamContent(new SlowStream(Encoding.UTF8.GetBytes("first-"), Encoding.UTF8.GetBytes("second"), release.Task))
            });
            var progress = new List<long>();

            using RestResult<Stream> result = await Root(handler).OnDownloadProgress(p => progress.Add(p.CurrentBytes)).GetAsync<Stream>();

            // the result is available before the whole body has been produced
            Assert.False(release.Task.IsCompleted);
            release.SetResult(true);

            using var reader = new StreamReader(result.Content, Encoding.UTF8);
            Assert.Equal("first-second", await reader.ReadToEndAsync());
            Assert.Equal(new long[] { 6, 12 }, progress);
        }

        [Fact]
        public async Task String_result_and_events()
        {
            var handler = FakeHttpMessageHandler.Returns(HttpStatusCode.OK, "hello", "text/plain");
            string preview = null;
            CompletedEventArgs completed = null;
            PreCompletedEventArgs preCompleted = null;

            RestResult<string> text = await Root(handler)
                .OnPreviewContentResponseAsString(e => preview = e.ContentAsString)
                .OnPreCompleted(e => preCompleted = e)
                .OnCompleted(e => completed = e)
                .GetAsync();

            Assert.Equal("hello", text.Content);
            Assert.Equal("hello", ((IRestResult)text).Content);
            Assert.Equal("hello", preview);
            Assert.Same(text, preCompleted.Result);
            Assert.Same(text, completed.Result);
        }

        [Fact]
        public void Synchronous_methods_work()
        {
            var handler = FakeHttpMessageHandler.Returns(HttpStatusCode.OK, "{\"id\":1}");
            RestResult<User> result = Root(handler).Command("users").Command(1).Get<User>();
            Assert.Equal(1, result.Content.Id);
        }

        [Fact]
        public async Task Missing_endpoint_is_reported()
        {
            RestResult<string> result = await Rest.Build().GetAsync();
            Assert.IsType<InvalidOperationException>(result.Exception);
        }

        [Fact]
        public async Task Build_with_properties_copies_them()
        {
            var properties = new RestProperties { EndPoint = new Uri("https://a.example.com") };
            var builder = Rest.Build(properties);
            properties.EndPoint = new Uri("https://b.example.com");

            var handler = FakeHttpMessageHandler.Returns(HttpStatusCode.OK);
            await builder.UseHttpClient(handler.CreateClient()).GetAsync();
            Assert.Equal("a.example.com", handler.Requests[0].RequestUri.Host);
        }

        /// <summary>
        /// Returns the first chunk, then waits for the release before returning the second one
        /// </summary>
        private sealed class SlowStream : Stream
        {
            private readonly Queue<byte[]> chunks;
            private readonly Task release;

            public SlowStream(byte[] first, byte[] second, Task release)
            {
                chunks = new Queue<byte[]>(new[] { first, second });
                this.release = release;
            }

            public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            {
                if (chunks.Count == 0) { return 0; }
                if (chunks.Count == 1) { await release; }
                byte[] chunk = chunks.Dequeue();
                Array.Copy(chunk, 0, buffer, offset, chunk.Length);
                return chunk.Length;
            }

            public override int Read(byte[] buffer, int offset, int count) => ReadAsync(buffer, offset, count, default).GetAwaiter().GetResult();
            public override bool CanRead => true;
            public override bool CanSeek => false;
            public override bool CanWrite => false;
            public override long Length => throw new NotSupportedException();
            public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
            public override void Flush() { }
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        }
    }
}
