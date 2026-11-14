namespace Mafesoft.RestClient.Tests
{
    using Mafesoft.RestClient.Testing;
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Headers;

    public class AuthenticationProviderTests
    {
        // Rest.Build() without an HttpClient: RestTest (Mafesoft.RestClient.Testing) intercepts the requests
        private static RestBuilder Root()
            => Rest.Build().Url("https://api.example.com");

        [Fact]
        public async Task Async_authentication_sets_the_authorization_header()
        {
            using var http = new RestTest();

            await Root()
                .Authentication(async ct =>
                {
                    await Task.Yield();
                    return new AuthenticationHeaderValue("Basic", "dXNlcjpwd2Q=");
                })
                .GetAsync();

            Assert.Equal("Basic dXNlcjpwd2Q=", http.CallLog[0].Request.Headers.Authorization.ToString());
        }

        [Fact]
        public async Task Bearer_token_sets_the_authorization_header()
        {
            using var http = new RestTest();

            await Root().BearerToken(async ct => { await Task.Yield(); return "abc"; }).GetAsync();

            Assert.Equal("Bearer abc", http.CallLog[0].Request.Headers.Authorization.ToString());
        }

        [Fact]
        public async Task Null_or_empty_token_sends_the_request_without_authorization()
        {
            using var http = new RestTest();

            await Root().BearerToken(ct => Task.FromResult<string>(null)).GetAsync();
            await Root().BearerToken(ct => Task.FromResult(string.Empty)).GetAsync();
            await Root().Authentication(ct => Task.FromResult<AuthenticationHeaderValue>(null)).GetAsync();

            http.ShouldHaveMadeACall().WithoutHeader("Authorization").Times(3);
        }

        [Fact]
        public async Task Provider_is_called_on_every_attempt_of_the_retry()
        {
            using var http = new RestTest();
            http.RespondWith(HttpStatusCode.ServiceUnavailable);
            http.RespondWith(HttpStatusCode.ServiceUnavailable);
            http.RespondWith(HttpStatusCode.OK);
            int calls = 0;

            RestResult<string> result = await Root()
                .Retry(3, TimeSpan.Zero)
                .BearerToken(ct => Task.FromResult("token-" + Interlocked.Increment(ref calls)))
                .GetAsync();

            Assert.Equal(HttpStatusCode.OK, result.StatusCode);
            Assert.Equal(3, calls);
            Assert.Equal(new[] { "token-1", "token-2", "token-3" }, http.CallLog.Select(c => c.Request.Headers.Authorization.Parameter));
        }

        [Fact]
        public async Task Provider_is_called_again_after_a_refresh_token()
        {
            string token = "expired";
            using var http = new RestTest();
            http.RespondWith(HttpStatusCode.OK).For(HttpMethod.Post, "*/refresh");
            http.RespondWith(r => new HttpResponseMessage(r.Headers.Authorization?.Parameter == "valid" ? HttpStatusCode.OK : HttpStatusCode.Unauthorized))
                .For(HttpMethod.Get, "*");
            int calls = 0;

            RestResult<string> result = await Root()
                .BearerToken(async ct =>
                {
                    Interlocked.Increment(ref calls);
                    await Task.Yield();
                    return token;
                })
                .RefreshToken(async ct =>
                {
                    token = "valid";
                    return await Root().Command("refresh").PostAsync(ct);
                })
                .GetAsync();

            Assert.Equal(HttpStatusCode.OK, result.StatusCode);
            Assert.Equal(2, calls);
            http.ShouldHaveCalled("*/refresh").WithVerb(HttpMethod.Post).Times(1);
            Assert.Equal(new[] { "expired", "valid" }, http.CallLog
                .Where(c => c.Method == HttpMethod.Get)
                .Select(c => c.Request.Headers.Authorization.Parameter));
        }

        [Fact]
        public async Task Provider_receives_the_cancellation_token_of_the_request()
        {
            using var http = new RestTest();
            using var cts = new CancellationTokenSource();
            bool canBeCanceled = false;
            bool canceled = false;

            RestResult<string> result = await Root()
                .BearerToken(ct =>
                {
                    canBeCanceled = ct.CanBeCanceled;
                    cts.Cancel();
                    canceled = ct.IsCancellationRequested;
                    return Task.FromResult("abc");
                })
                .GetAsync(cts.Token);

            Assert.True(canBeCanceled);
            Assert.True(canceled);
            Assert.True(result.IsCancellationRequested);
        }

        [Fact]
        public async Task Canceling_the_request_cancels_the_provider()
        {
            using var http = new RestTest();
            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

            RestResult<string> result = await Root()
                .Retry(3, TimeSpan.Zero)
                .BearerToken(async ct =>
                {
                    await Task.Delay(Timeout.Infinite, ct);
                    return "abc";
                })
                .GetAsync(cts.Token);

            Assert.True(result.IsCancellationRequested);
            Assert.Equal(0, result.RetryCount);
            http.ShouldNotHaveMadeACall();
        }

        [Fact]
        public async Task Timeout_cancels_the_provider_and_the_attempt_is_retried()
        {
            using var http = new RestTest();
            int calls = 0;

            RestResult<string> result = await Root()
                .Timeout(TimeSpan.FromMilliseconds(100))
                .Retry(1, TimeSpan.Zero)
                .BearerToken(async ct =>
                {
                    if (Interlocked.Increment(ref calls) == 1)
                    {
                        await Task.Delay(Timeout.Infinite, ct);
                    }
                    return "abc";
                })
                .GetAsync();

            Assert.Equal(HttpStatusCode.OK, result.StatusCode);
            Assert.Equal(1, result.RetryCount);
            Assert.Equal(2, calls);
            http.ShouldHaveMadeACall().Times(1);
        }

        [Fact]
        public async Task Provider_exception_is_reported_by_the_result()
        {
            using var http = new RestTest();
            var error = new InvalidOperationException("no token");
            Exception raised = null;

            RestResult<string> result = await Root()
                .Retry(3, TimeSpan.Zero)
                .BearerToken(async ct =>
                {
                    await Task.Yield();
                    throw error;
                })
                .OnException(e => raised = e)
                .GetAsync();

            Assert.False(result.IsSuccessStatusCode);
            Assert.Same(error, result.Exception);
            Assert.Same(error, raised);
            http.ShouldNotHaveMadeACall();
        }

        [Fact]
        public async Task Provider_returning_a_null_task_is_reported_by_the_result()
        {
            using var http = new RestTest();

            RestResult<string> result = await Root().Authentication(ct => null).GetAsync();
            RestResult<string> bearer = await Root().BearerToken(ct => null).GetAsync();

            Assert.IsType<InvalidOperationException>(result.Exception);
            Assert.IsType<InvalidOperationException>(bearer.Exception);
            http.ShouldNotHaveMadeACall();
        }

        [Fact]
        public void Provider_works_with_synchronous_requests()
        {
            using var http = new RestTest();

            RestResult<string> result = Root()
                .BearerToken(async ct => { await Task.Delay(10, ct); return "abc"; })
                .Get();

            Assert.Equal(HttpStatusCode.OK, result.StatusCode);
            Assert.Equal("Bearer abc", http.CallLog[0].Request.Headers.Authorization.ToString());
        }

        [Fact]
        public async Task Last_authentication_wins_and_null_removes_it()
        {
            using var http = new RestTest();
            RestBuilder bearer = Root().BearerToken(ct => Task.FromResult("async"));

            await bearer.Authentication("Basic", "sync").GetAsync();
            await bearer.Authentication("Basic", "sync").BearerToken(ct => Task.FromResult("async-again")).GetAsync();
            await bearer.BearerToken(null).GetAsync();
            await bearer.Authentication((Func<AuthenticationHeaderValue>)null).GetAsync();
            await bearer.Authentication(authenticationProvider: null).GetAsync();
            await bearer.GetAsync();

            Assert.Equal("Basic sync", http.CallLog[0].Request.Headers.Authorization.ToString());
            Assert.Equal("Bearer async-again", http.CallLog[1].Request.Headers.Authorization.ToString());
            Assert.Null(http.CallLog[2].Request.Headers.Authorization);
            Assert.Null(http.CallLog[3].Request.Headers.Authorization);
            Assert.Null(http.CallLog[4].Request.Headers.Authorization);
            // the builder is immutable: the base builder keeps its provider
            Assert.Equal("Bearer async", http.CallLog[5].Request.Headers.Authorization.ToString());
        }

        [Fact]
        public async Task Existing_overloads_keep_binding_as_before()
        {
            using var http = new RestTest();
            static AuthenticationHeaderValue Header() => new AuthenticationHeaderValue("Bearer", "method-group");

            await Root().Authentication(() => new AuthenticationHeaderValue("Bearer", "lambda")).GetAsync();
            await Root().Authentication(Header).GetAsync();
            await Root().Authentication(authentication: null).GetAsync();
            await Root().Authentication("Bearer").GetAsync();

            Assert.Equal("Bearer lambda", http.CallLog[0].Request.Headers.Authorization.ToString());
            Assert.Equal("Bearer method-group", http.CallLog[1].Request.Headers.Authorization.ToString());
            Assert.Null(http.CallLog[2].Request.Headers.Authorization);
            Assert.Equal("Bearer", http.CallLog[3].Request.Headers.Authorization.ToString());
        }
    }
}
