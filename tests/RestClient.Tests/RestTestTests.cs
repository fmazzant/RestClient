namespace Mafesoft.RestClient.Tests
{
    using Mafesoft.RestClient.Testing;
    using System.Net;
    using System.Net.Http;
#if NET
    using Microsoft.Extensions.DependencyInjection;
#endif

    /// <summary>
    /// Mafesoft.RestClient.Testing: RestTest fake server and call assertions
    /// </summary>
    public class RestTestTests
    {
        public class User
        {
            public string Name { get; set; }
        }

        /// <summary>
        /// The code under test of the README example: it builds its own client
        /// </summary>
        private sealed class UsersService
        {
            public async Task<User> GetUserAsync(string name)
                => (await Rest.Build().Url("https://api.example.com").Command("users").Command(name).GetAsync<User>()).Content;
        }

        [Fact]
        public async Task Readme_example()
        {
            using var http = new RestTest();
            http.RespondWithJson(new User { Name = "fede" }).For(HttpMethod.Get, "*/users/*");

            User user = await new UsersService().GetUserAsync("fede");

            Assert.Equal("fede", user.Name);
            http.ShouldHaveCalled("*/users/fede").WithVerb(HttpMethod.Get).Times(1);
        }

        [Fact]
        public async Task Intercepts_a_builder_with_its_own_HttpClient()
        {
            using var http = new RestTest();
            http.RespondWith("intercepted");
            using var client = new HttpClient { BaseAddress = new Uri("https://unreachable.invalid") };

            RestResult<string> result = await Rest.Build(client).Url("https://unreachable.invalid/ping").GetAsync();

            Assert.Equal("intercepted", result.Content);
            http.ShouldHaveCalled("https://unreachable.invalid/ping");
        }

#if NET
        [Fact]
        public async Task Intercepts_the_builders_of_IRestClientFactory()
        {
            using var http = new RestTest();
            http.RespondWithJson(new User { Name = "di" });
            using ServiceProvider provider = new ServiceCollection()
                .AddRestClient("users", builder => builder.Url("https://users.invalid").Header("X-Api", "1"))
                .Services
                .BuildServiceProvider();

            RestResult<User> result = await provider.GetRequiredService<IRestClientFactory>().CreateClient("users")
                .Command("me")
                .GetAsync<User>();

            Assert.Equal("di", result.Content.Name);
            http.ShouldHaveCalled("https://users.invalid/me").WithHeader("X-Api", "1");
        }
#endif

        [Fact]
        public async Task Without_responses_every_request_gets_200_with_an_empty_body()
        {
            using var http = new RestTest();

            RestResult<string> result = await Rest.Build().Url("https://api.example.com").GetAsync();

            Assert.Equal(HttpStatusCode.OK, result.StatusCode);
            Assert.Equal(string.Empty, result.Content);
            Assert.Equal(HttpStatusCode.OK, http.CallLog[0].StatusCode);
        }

        [Fact]
        public async Task Responses_are_used_in_order_and_the_last_one_is_reused()
        {
            using var http = new RestTest();
            http.RespondWith("first");
            http.RespondWith("second", HttpStatusCode.Created);
            RestBuilder api = Rest.Build().Url("https://api.example.com");

            RestResult<string> first = await api.GetAsync();
            RestResult<string> second = await api.GetAsync();
            RestResult<string> third = await api.GetAsync();

            Assert.Equal("first", first.Content);
            Assert.Equal("second", second.Content);
            Assert.Equal(HttpStatusCode.Created, second.StatusCode);
            Assert.Equal("second", third.Content);
        }

        [Fact]
        public async Task For_and_When_restrict_the_responses()
        {
            using var http = new RestTest();
            http.RespondWith("users").For(HttpMethod.Get, "*/users*");
            http.RespondWith("created", HttpStatusCode.Created).For(HttpMethod.Post, "*/users");
            http.RespondWith(HttpStatusCode.Forbidden).When(r => r.Headers.Contains("X-Admin"));
            RestBuilder api = Rest.Build().Url("https://api.example.com");

            RestResult<string> list = await api.Command("users").Parameter("page", 2).GetAsync();
            RestResult<string> created = await api.Command("users").Payload(new User { Name = "a" }).PostAsync();
            RestResult<string> forbidden = await api.Command("admin").Header("X-Admin", "1").DeleteAsync();
            RestResult<string> other = await api.Command("other").GetAsync();

            Assert.Equal("users", list.Content);
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
            Assert.Equal(HttpStatusCode.OK, other.StatusCode);
            Assert.Equal(string.Empty, other.Content);
        }

        [Fact]
        public async Task Dynamic_responses_receive_the_request()
        {
            using var http = new RestTest();
            http.RespondWith(request => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(request.RequestUri.AbsolutePath) });

            RestResult<string> result = await Rest.Build().Url("https://api.example.com").Command("echo").GetAsync();

            Assert.Equal("/echo", result.Content);
        }

        [Fact]
        public async Task Simulated_exception_and_timeout_are_reported_by_the_result()
        {
            using var http = new RestTest();
            var error = new HttpRequestException("connection refused");
            http.SimulateException(error);
            http.SimulateTimeout();
            RestBuilder api = Rest.Build().Url("https://api.example.com").Timeout(TimeSpan.FromMilliseconds(100));

            RestResult<string> failed = await api.GetAsync();
            RestResult<string> timedOut = await api.GetAsync();

            Assert.Same(error, failed.Exception);
            Assert.IsType<TimeoutException>(timedOut.Exception);
            Assert.All(http.CallLog, call => Assert.Null(call.StatusCode));
        }

        [Fact]
        public async Task Call_assertions_check_the_details_of_the_calls()
        {
            using var http = new RestTest();
            RestBuilder api = Rest.Build().Url("https://api.example.com").BearerToken(ct => Task.FromResult("abc"));

            await api.Command("users").Parameter("q", "a b").Parameter("page", 1).Header("X-Trace", "t-1").GetAsync();
            await api.Command("users").Payload(new User { Name = "fede" }).PostAsync();

            http.ShouldHaveCalled("*/users*").Times(2);
            http.ShouldHaveCalled("https://api.example.com/users?q=a b&page=1")
                .WithVerb(HttpMethod.Get)
                .WithQueryParam("q", "a b")
                .WithQueryParam("page")
                .WithHeader("X-Trace", "t-*")
                .WithOAuthBearerToken("abc")
                .Times(1);
            http.ShouldHaveCalled("*/users")
                .WithVerb(HttpMethod.Post)
                .WithContentType("application/json")
                .WithHeader("Content-Type", "application/json")
                .WithRequestBody("*\"name\":\"fede\"*")
                .WithoutHeader("X-Trace")
                .With(call => call.RequestBody.Length > 0, "a body")
                .Times(1);
            http.ShouldNotHaveCalled("*/orders*");
            http.ShouldHaveMadeACall().Times(2);
            Assert.Equal(2, http.CallLog.Count);
            Assert.Equal("POST https://api.example.com/users", http.CallLog[1].ToString());
        }

        [Fact]
        public async Task Failed_assertions_throw_with_the_calls_made()
        {
            using var http = new RestTest();
            http.ShouldNotHaveMadeACall();
            RestCallAssertionException none = Assert.Throws<RestCallAssertionException>(() => http.ShouldHaveCalled("*"));
            Assert.Contains("No calls were made", none.Message);

            await Rest.Build().Url("https://api.example.com").Command("users").GetAsync();

            RestCallAssertionException wrongUrl = Assert.Throws<RestCallAssertionException>(() => http.ShouldHaveCalled("*/orders"));
            RestCallAssertionException wrongVerb = Assert.Throws<RestCallAssertionException>(() => http.ShouldHaveCalled("*/users").WithVerb(HttpMethod.Post));
            RestCallAssertionException wrongTimes = Assert.Throws<RestCallAssertionException>(() => http.ShouldHaveCalled("*/users").Times(2));
            Assert.Throws<RestCallAssertionException>(() => http.ShouldNotHaveCalled("*/users"));
            Assert.Throws<RestCallAssertionException>(() => http.ShouldNotHaveMadeACall());

            Assert.Contains("GET https://api.example.com/users", wrongUrl.Message);
            Assert.Contains("method POST", wrongVerb.Message);
            Assert.Contains("Expected 2 call(s)", wrongTimes.Message);
        }

        [Fact]
        public async Task Dispose_stops_the_interception_and_restores_the_previous_one()
        {
            using var outer = new RestTest();
            outer.RespondWith("outer");
            RestBuilder api = Rest.Build().Url("https://api.example.com");

            using (var inner = new RestTest())
            {
                inner.RespondWith("inner");
                Assert.Equal("inner", (await api.GetAsync()).Content);
            }

            Assert.Equal("outer", (await api.GetAsync()).Content);
            Assert.Single(outer.CallLog);
        }

        [Fact]
        public async Task Parallel_flows_have_their_own_fake_server()
        {
            async Task<(string, int)> RunAsync(string name)
            {
                using var http = new RestTest();
                http.RespondWith(name);
                await Task.Delay(20);
                string content = (await Rest.Build().Url("https://api.example.com").GetAsync()).Content;
                return (content, http.CallLog.Count);
            }

            (string, int)[] results = await Task.WhenAll(Enumerable.Range(0, 10).Select(i => Task.Run(() => RunAsync("flow-" + i))));

            Assert.Equal(Enumerable.Range(0, 10).Select(i => ("flow-" + i, 1)), results);
        }

        [Fact]
        public async Task CreateClient_works_outside_the_async_flow()
        {
            var http = new RestTest();
            http.RespondWith("explicit");
            HttpClient client = http.CreateClient();
            http.Dispose();

            // the fake server is no longer installed in the flow, but the client still uses it
            RestResult<string> result = await Rest.Build(client).Url("https://api.example.com").GetAsync();

            Assert.Equal("explicit", result.Content);
            Assert.Single(http.CallLog);
        }

        [Fact]
        public async Task RespondWithJson_with_source_generated_metadata()
        {
            using var http = new RestTest();
            http.RespondWithJson(new Mafesoft.RestClient.Tests.User { Id = 4, Name = "ctx" }, TestJsonContext.Default.User, HttpStatusCode.Accepted);

            RestResult<Mafesoft.RestClient.Tests.User> result = await Rest.Build().Url("https://api.example.com").GetAsync<Mafesoft.RestClient.Tests.User>();

            Assert.Equal(HttpStatusCode.Accepted, result.StatusCode);
            Assert.Equal(4, result.Content.Id);
            Assert.Equal("application/json", result.Response.Content.Headers.ContentType.MediaType);
        }
    }
}
