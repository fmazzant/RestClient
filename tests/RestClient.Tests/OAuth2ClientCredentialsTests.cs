namespace Mafesoft.RestClient.Tests
{
    using Microsoft.Extensions.Logging;
    using System.Collections.Concurrent;
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Text;

    /// <summary>
    /// OAuth2 client credentials and refresh token grant with token caching
    /// </summary>
    public class OAuth2ClientCredentialsTests
    {
        private const string TokenEndpoint = "https://login.example.com/oauth2/token";

        /// <summary>
        /// A token endpoint and an API on the same fake handler. The API accepts only the last issued token.
        /// </summary>
        private sealed class OAuth2Server
        {
            private int issued;

            public OAuth2Server(int expiresIn = 3600, bool withRefreshToken = false)
            {
                ExpiresIn = expiresIn;
                WithRefreshToken = withRefreshToken;
                Handler = new FakeHttpMessageHandler(async (request, index) =>
                {
                    if (request.RequestUri.AbsoluteUri == TokenEndpoint)
                    {
                        await Task.Delay(TokenDelay);
                        return Token(request);
                    }
                    return Api(request);
                });
            }

            public FakeHttpMessageHandler Handler { get; }

            public int ExpiresIn { get; set; }

            public bool WithRefreshToken { get; set; }

            public TimeSpan TokenDelay { get; set; } = TimeSpan.Zero;

            /// <summary>
            /// When set, the token endpoint returns this status and body
            /// </summary>
            public Func<string, (HttpStatusCode Status, string Body)> TokenOverride { get; set; }

            /// <summary>
            /// When true, the API rejects every token issued so far once (the token was revoked)
            /// </summary>
            public string RevokedToken { get; set; }

            public string CurrentToken => "token-" + Volatile.Read(ref issued);

            public List<string> TokenRequestBodies => Handler.Requests
                .Select((r, i) => (r, i))
                .Where(x => x.r.RequestUri.AbsoluteUri == TokenEndpoint)
                .Select(x => Handler.RequestBodies[x.i])
                .ToList();

            public List<HttpRequestMessage> ApiRequests => Handler.Requests
                .Where(r => r.RequestUri.AbsoluteUri != TokenEndpoint)
                .ToList();

            private HttpResponseMessage Token(HttpRequestMessage request)
            {
                if (TokenOverride != null)
                {
                    string body = request.Content.ReadAsStringAsync().Result;
                    (HttpStatusCode status, string json) = TokenOverride(body);
                    if (json != null)
                    {
                        return new HttpResponseMessage(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
                    }
                }

                int n = Interlocked.Increment(ref issued);
                string refresh = WithRefreshToken ? $",\"refresh_token\":\"refresh-{n}\"" : string.Empty;
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        $"{{\"access_token\":\"token-{n}\",\"token_type\":\"bearer\",\"expires_in\":{ExpiresIn}{refresh}}}",
                        Encoding.UTF8,
                        "application/json"),
                };
            }

            private HttpResponseMessage Api(HttpRequestMessage request)
            {
                string token = request.Headers.Authorization?.Parameter;
                bool valid = request.Headers.Authorization?.Scheme == "Bearer" && token != null && token != RevokedToken;
                return new HttpResponseMessage(valid ? HttpStatusCode.OK : HttpStatusCode.Unauthorized)
                {
                    Content = new StringContent(valid ? token : string.Empty),
                };
            }
        }

        private static RestBuilder Client(OAuth2Server server, Action<OAuth2ClientCredentialsOptions> configure = null)
            => Rest.Build(server.Handler.CreateClient())
                .Url("https://api.example.com")
                .OAuth2ClientCredentials(o =>
                {
                    o.TokenEndpoint = new Uri(TokenEndpoint);
                    o.ClientId = "my-client";
                    o.ClientSecret = "s3cret";
                    o.Scopes = new[] { "api.read", "api.write" };
                    configure?.Invoke(o);
                });

        [Fact]
        public async Task Token_is_requested_before_the_first_call_and_sent_as_bearer()
        {
            var server = new OAuth2Server();

            RestResult<string> result = await Client(server).Command("orders").GetAsync();

            Assert.Equal(HttpStatusCode.OK, result.StatusCode);
            Assert.Equal("token-1", result.Content);
            Assert.Equal(2, server.Handler.Requests.Count);
            HttpRequestMessage tokenRequest = server.Handler.Requests[0];
            Assert.Equal(HttpMethod.Post, tokenRequest.Method);
            Assert.Equal("application/x-www-form-urlencoded", tokenRequest.Content.Headers.ContentType.MediaType);
            Assert.Contains(tokenRequest.Headers.Accept, a => a.MediaType == "application/json");
            Assert.Null(tokenRequest.Headers.Authorization);
            Assert.Equal(
                "grant_type=client_credentials&scope=api.read+api.write&client_id=my-client&client_secret=s3cret",
                server.TokenRequestBodies[0]);
            Assert.Equal("Bearer token-1", server.ApiRequests[0].Headers.Authorization.ToString());
        }

        [Fact]
        public async Task Token_is_cached_and_reused_by_the_derived_builders()
        {
            var server = new OAuth2Server();
            RestBuilder api = Client(server);

            await api.Command("a").GetAsync();
            await api.Command("b").GetAsync();
            await api.Command("c").Timeout(TimeSpan.FromSeconds(5)).GetAsync();

            Assert.Single(server.TokenRequestBodies);
            Assert.All(server.ApiRequests, r => Assert.Equal("Bearer token-1", r.Headers.Authorization.ToString()));
        }

        [Fact]
        public async Task Token_is_renewed_when_it_expires()
        {
            // expires_in shorter than RefreshBeforeExpiration: the token is expired as soon as it is received
            var server = new OAuth2Server(expiresIn: 10);
            RestBuilder api = Client(server);

            RestResult<string> first = await api.GetAsync();
            RestResult<string> second = await api.GetAsync();

            Assert.Equal("token-1", first.Content);
            Assert.Equal("token-2", second.Content);
            Assert.Equal(2, server.TokenRequestBodies.Count);
        }

        [Fact]
        public async Task Token_is_not_renewed_before_RefreshBeforeExpiration()
        {
            var server = new OAuth2Server(expiresIn: 10);
            RestBuilder api = Client(server, o => o.RefreshBeforeExpiration = TimeSpan.FromSeconds(1));

            await api.GetAsync();
            await api.GetAsync();

            Assert.Single(server.TokenRequestBodies);
        }

        [Fact]
        public async Task One_token_request_for_many_concurrent_calls()
        {
            var server = new OAuth2Server { TokenDelay = TimeSpan.FromMilliseconds(100) };
            RestBuilder api = Client(server);

            RestResult<string>[] results = await Task.WhenAll(Enumerable.Range(0, 20).Select(i => api.Command("item").Command(i).GetAsync()));

            Assert.Single(server.TokenRequestBodies);
            Assert.All(results, r => Assert.Equal("token-1", r.Content));
        }

        [Fact]
        public async Task Token_is_renewed_on_401_and_the_request_is_sent_again_once()
        {
            var server = new OAuth2Server();
            RestBuilder api = Client(server);
            await api.GetAsync();

            server.RevokedToken = "token-1";
            RestResult<string> result = await api.GetAsync();

            Assert.Equal(HttpStatusCode.OK, result.StatusCode);
            Assert.Equal("token-2", result.Content);
            Assert.Equal(2, server.TokenRequestBodies.Count);
        }

        [Fact]
        public async Task A_second_401_is_returned_without_another_token_request()
        {
            var server = new OAuth2Server();
            RestBuilder api = Client(server);
            server.TokenOverride = body => (HttpStatusCode.OK, "{\"access_token\":\"revoked\",\"token_type\":\"Bearer\"}");
            server.RevokedToken = "revoked";

            RestResult<string> result = await api.GetAsync();

            Assert.Equal(HttpStatusCode.Unauthorized, result.StatusCode);
            Assert.Equal(2, server.TokenRequestBodies.Count);
            Assert.Equal(2, server.ApiRequests.Count);
        }

        [Fact]
        public async Task Concurrent_401_renew_the_token_once()
        {
            var server = new OAuth2Server { TokenDelay = TimeSpan.FromMilliseconds(50) };
            RestBuilder api = Client(server);
            await api.GetAsync();
            server.RevokedToken = "token-1";

            RestResult<string>[] results = await Task.WhenAll(Enumerable.Range(0, 10).Select(i => api.GetAsync()));

            Assert.All(results, r => Assert.Equal("token-2", r.Content));
            Assert.Equal(2, server.TokenRequestBodies.Count);
        }

        [Fact]
        public async Task Refresh_token_grant_is_used_when_the_token_response_has_a_refresh_token()
        {
            var server = new OAuth2Server(expiresIn: 10, withRefreshToken: true);
            RestBuilder api = Client(server);

            await api.GetAsync();
            RestResult<string> second = await api.GetAsync();

            Assert.Equal("token-2", second.Content);
            Assert.Equal(
                "grant_type=refresh_token&refresh_token=refresh-1&scope=api.read+api.write&client_id=my-client&client_secret=s3cret",
                server.TokenRequestBodies[1]);
        }

        [Fact]
        public async Task Refresh_token_grant_falls_back_to_client_credentials_when_rejected()
        {
            var server = new OAuth2Server(expiresIn: 10, withRefreshToken: true);
            RestBuilder api = Client(server);
            await api.GetAsync();

            server.TokenOverride = body => body.Contains("grant_type=refresh_token")
                ? (HttpStatusCode.BadRequest, "{\"error\":\"invalid_grant\"}")
                : (HttpStatusCode.OK, null);
            RestResult<string> second = await api.GetAsync();

            Assert.Equal(HttpStatusCode.OK, second.StatusCode);
            Assert.Equal("token-2", second.Content);
            Assert.Equal(3, server.TokenRequestBodies.Count);
            Assert.StartsWith("grant_type=client_credentials", server.TokenRequestBodies[2]);
        }

        [Fact]
        public async Task Client_credentials_in_the_basic_header()
        {
            var server = new OAuth2Server();
            RestBuilder api = Client(server, o =>
            {
                o.ClientAuthentication = OAuth2ClientAuthentication.BasicHeader;
                o.ClientSecret = "a b:c";
                o.Scopes = null;
                o.Parameters["audience"] = "https://api.example.com";
            });

            await api.GetAsync();

            AuthenticationHeaderValue basic = server.Handler.Requests[0].Headers.Authorization;
            Assert.Equal("Basic", basic.Scheme);
            Assert.Equal("my-client:a+b%3Ac", Encoding.UTF8.GetString(Convert.FromBase64String(basic.Parameter)));
            Assert.Equal("grant_type=client_credentials&audience=https%3A%2F%2Fapi.example.com", server.TokenRequestBodies[0]);
        }

        [Fact]
        public async Task Token_error_is_reported_by_the_result()
        {
            var server = new OAuth2Server
            {
                TokenOverride = body => (HttpStatusCode.Unauthorized, "{\"error\":\"invalid_client\",\"error_description\":\"Bad secret\"}"),
            };
            Exception raised = null;

            RestResult<string> result = await Client(server).OnException(e => raised = e).GetAsync();

            InvalidOperationException error = Assert.IsType<InvalidOperationException>(result.Exception);
            Assert.Same(error, raised);
            Assert.Contains("HTTP 401: invalid_client - Bad secret", error.Message);
            Assert.DoesNotContain("s3cret", error.Message);
            Assert.Empty(server.ApiRequests);
        }

        [Fact]
        public async Task Token_response_without_access_token_is_an_error()
        {
            var server = new OAuth2Server { TokenOverride = body => (HttpStatusCode.OK, "{\"token_type\":\"bearer\"}") };

            RestResult<string> result = await Client(server).GetAsync();

            Assert.Contains("no access_token", result.Exception.Message);
        }

        [Fact]
        public async Task Token_request_uses_the_retry_and_the_logger_but_not_the_events_and_headers()
        {
            int tokenCalls = 0;
            var handler = new FakeHttpMessageHandler((request, index) =>
            {
                if (request.RequestUri.AbsoluteUri == TokenEndpoint)
                {
                    return Interlocked.Increment(ref tokenCalls) == 1
                        ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
                        : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"access_token\":\"abc\",\"expires_in\":\"3600\"}") };
                }
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("ok") };
            });
            var logger = new ListLogger();
            var previews = new List<string>();
            int starts = 0;

            RestResult<string> result = await Rest.Build(handler.CreateClient())
                .Url("https://api.example.com")
                .Retry(2, TimeSpan.Zero, retryNonIdempotentMethods: true)
                .Logger(logger)
                .Header("X-Api-Key", "k")
                .OnStart(e => starts++)
                .OnPreviewContentRequestAsString(e => previews.Add(e.ContentAsString))
                .OnPreviewContentResponseAsString(e => previews.Add(e.ContentAsString))
                .OAuth2ClientCredentials(o =>
                {
                    o.TokenEndpoint = new Uri(TokenEndpoint);
                    o.ClientId = "my-client";
                    o.ClientSecret = "s3cret";
                })
                .Command("orders")
                .GetAsync();

            Assert.Equal("ok", result.Content);
            Assert.Equal(2, tokenCalls);
            Assert.Equal("Bearer abc", handler.Requests[2].Headers.Authorization.ToString());
            Assert.False(handler.Requests[0].Headers.Contains("X-Api-Key"));
            Assert.True(handler.Requests[2].Headers.Contains("X-Api-Key"));
            Assert.Equal(1, starts);
            Assert.Equal(new[] { "ok" }, previews);
            Assert.Contains(logger.Entries, e => e.Message.Contains("login.example.com/oauth2/token"));
            Assert.DoesNotContain(logger.Entries, e => e.Message.Contains("s3cret") || e.Message.Contains("abc"));
        }

        [Fact]
        public async Task Shared_cache_keeps_the_token_across_configurations()
        {
            var server = new OAuth2Server();
            var cache = new OAuth2TokenCache();

            await Client(server, o => o.Cache = cache).GetAsync();
            await Client(server, o => o.Cache = cache).GetAsync();
            cache.Clear();
            RestResult<string> afterClear = await Client(server, o => o.Cache = cache).GetAsync();

            Assert.Equal(2, server.TokenRequestBodies.Count);
            Assert.Equal("token-2", afterClear.Content);
        }

        [Fact]
        public async Task Authentication_replaces_OAuth2_and_OAuth2_replaces_Authentication()
        {
            var server = new OAuth2Server();

            await Client(server).Authentication("Bearer", "manual").GetAsync();
            await Rest.Build(server.Handler.CreateClient())
                .Url("https://api.example.com")
                .Authentication("Bearer", "manual")
                .OAuth2ClientCredentials(o =>
                {
                    o.TokenEndpoint = new Uri(TokenEndpoint);
                    o.ClientId = "my-client";
                })
                .GetAsync();

            Assert.Equal("Bearer manual", server.ApiRequests[0].Headers.Authorization.ToString());
            Assert.Equal("Bearer token-1", server.ApiRequests[1].Headers.Authorization.ToString());
            Assert.Equal("grant_type=client_credentials&client_id=my-client", server.TokenRequestBodies[0]);
        }

        [Fact]
        public void Options_are_validated()
        {
            RestBuilder root = Rest.Build();

            Assert.Throws<ArgumentNullException>(() => root.OAuth2ClientCredentials(null));
            Assert.Throws<ArgumentException>(() => root.OAuth2ClientCredentials(o => o.ClientId = "c"));
            Assert.Throws<ArgumentException>(() => root.OAuth2ClientCredentials(o =>
            {
                o.TokenEndpoint = new Uri("/token", UriKind.Relative);
                o.ClientId = "c";
            }));
            Assert.Throws<ArgumentException>(() => root.OAuth2ClientCredentials(o => o.TokenEndpoint = new Uri(TokenEndpoint)));
            Assert.Throws<ArgumentOutOfRangeException>(() => root.OAuth2ClientCredentials(o =>
            {
                o.TokenEndpoint = new Uri(TokenEndpoint);
                o.ClientId = "c";
                o.RefreshBeforeExpiration = TimeSpan.FromSeconds(-1);
            }));
        }

        [Fact]
        public async Task Cancellation_of_the_token_request_is_a_cancellation()
        {
            var server = new OAuth2Server { TokenDelay = TimeSpan.FromSeconds(10) };
            using var source = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

            RestResult<string> result = await Client(server).GetAsync(source.Token);

            Assert.IsAssignableFrom<OperationCanceledException>(result.Exception);
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
