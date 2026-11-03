namespace RestClient.Tests
{
    using RestClient.Generic;
    using System.Globalization;
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Text;

    public class RestBuilderTests
    {
        public class User
        {
            public int Id { get; set; }
            public string Name { get; set; }
        }

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
        public async Task Timeout_changes_do_not_leak_to_the_parent_builder()
        {
            var handler = FakeHttpMessageHandler.Returns(HttpStatusCode.OK);
            var root = Root(handler);
            var child = root.Timeout(TimeSpan.FromSeconds(1));

            Assert.Equal(RestProperties.DefaultTimeout, root.Properties.Timeout);
            Assert.Equal(TimeSpan.FromSeconds(1), child.Properties.Timeout);
            await Task.CompletedTask;
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

                Assert.Equal("?q=override&price=1.5&active=true", handler.Requests[0].RequestUri.Query);

                await Root(handler).Parameter("q", "a b&c=d").GetAsync();
                Assert.Equal("?q=a%20b%26c%3Dd", handler.Requests[1].RequestUri.Query);
            }
            finally
            {
                CultureInfo.CurrentCulture = previous;
            }
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
        public async Task Generic_methods_support_types_without_parameterless_constructor()
        {
            var handler = FakeHttpMessageHandler.Returns(HttpStatusCode.OK, "[1,2,3]");
            RestResult<IReadOnlyList<int>> result = await Root(handler).GetAsync<IReadOnlyList<int>>();
            Assert.Equal(new[] { 1, 2, 3 }, result.Content);
        }

        [Fact]
        public async Task Deserialization_error_keeps_the_http_response()
        {
            var handler = FakeHttpMessageHandler.Returns(HttpStatusCode.NotFound, "<html>not found</html>", "text/html");

            RestResult<User> result = await Root(handler).GetAsync<User>();

            Assert.Equal(HttpStatusCode.NotFound, result.StatusCode);
            Assert.NotNull(result.Response);
            Assert.NotNull(result.Exception);
            Assert.False(result.IsSuccessStatusCode);
            Assert.Null(result.Content);
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
        public async Task Refresh_token_retries_once_with_the_new_token()
        {
            string token = "expired";
            var handler = new FakeHttpMessageHandler((r, i) =>
                new HttpResponseMessage(r.Headers.Authorization.Parameter == "valid" ? HttpStatusCode.OK : HttpStatusCode.Unauthorized)
                {
                    Content = new StringContent("ok")
                });
            var refreshHandler = FakeHttpMessageHandler.Returns(HttpStatusCode.Created);
            int refreshCalls = 0;

            var api = Root(handler)
                .Authentication(() => new AuthenticationHeaderValue("Bearer", token))
                .RefreshTokenInvoke(async () =>
                {
                    refreshCalls++;
                    token = "valid";
                    return await Root(refreshHandler).PostAsync();
                });

            RestResult<string> first = await api.GetAsync();
            Assert.Equal(HttpStatusCode.OK, first.StatusCode);
            Assert.Equal(2, handler.Requests.Count);

            // the builder keeps working for the next requests
            token = "expired-again";
            RestResult<string> second = await api.GetAsync();
            Assert.Equal(HttpStatusCode.OK, second.StatusCode);
            Assert.Equal(2, refreshCalls);
        }

        [Fact]
        public async Task Refresh_token_is_not_retried_when_refresh_fails()
        {
            var handler = FakeHttpMessageHandler.Returns(HttpStatusCode.Unauthorized);
            var refreshHandler = FakeHttpMessageHandler.Returns(HttpStatusCode.BadRequest);

            RestResult<string> result = await Root(handler)
                .RefreshTokenInvoke(() => Root(refreshHandler).Post())
                .GetAsync();

            Assert.Equal(HttpStatusCode.Unauthorized, result.StatusCode);
            Assert.Single(handler.Requests);
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
                .PostAsByteArrayAsync();

            Assert.Equal(payload, result.Content);
            Assert.Equal(10, download.Count);
            Assert.Equal(100, download.Last().ProgressPercentage);
            Assert.NotEmpty(upload);
            Assert.Equal(100, upload.Last().ProgressPercentage);
        }

        [Fact]
        public async Task Stream_and_string_results_and_events()
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

            using RestResult<Stream> stream = await Root(handler).GetAsStreamAsync();
            using var reader = new StreamReader(stream.Content, Encoding.UTF8);
            Assert.Equal("hello", await reader.ReadToEndAsync());
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
    }
}
