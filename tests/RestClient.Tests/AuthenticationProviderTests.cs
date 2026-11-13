namespace Mafesoft.RestClient.Tests
{
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Headers;

    public class AuthenticationProviderTests
    {
        private static RestBuilder Root(FakeHttpMessageHandler handler)
            => Rest.Build(handler.CreateClient()).Url("https://api.example.com");

        [Fact]
        public async Task Async_authentication_sets_the_authorization_header()
        {
            var handler = FakeHttpMessageHandler.Returns(HttpStatusCode.OK);

            await Root(handler)
                .Authentication(async ct =>
                {
                    await Task.Yield();
                    return new AuthenticationHeaderValue("Basic", "dXNlcjpwd2Q=");
                })
                .GetAsync();

            Assert.Equal("Basic dXNlcjpwd2Q=", handler.Requests[0].Headers.Authorization.ToString());
        }

        [Fact]
        public async Task Bearer_token_sets_the_authorization_header()
        {
            var handler = FakeHttpMessageHandler.Returns(HttpStatusCode.OK);

            await Root(handler).BearerToken(async ct => { await Task.Yield(); return "abc"; }).GetAsync();

            Assert.Equal("Bearer abc", handler.Requests[0].Headers.Authorization.ToString());
        }

        [Fact]
        public async Task Null_or_empty_token_sends_the_request_without_authorization()
        {
            var handler = FakeHttpMessageHandler.Returns(HttpStatusCode.OK);

            await Root(handler).BearerToken(ct => Task.FromResult<string>(null)).GetAsync();
            await Root(handler).BearerToken(ct => Task.FromResult(string.Empty)).GetAsync();
            await Root(handler).Authentication(ct => Task.FromResult<AuthenticationHeaderValue>(null)).GetAsync();

            Assert.Equal(3, handler.Requests.Count);
            Assert.All(handler.Requests, r => Assert.Null(r.Headers.Authorization));
        }

        [Fact]
        public async Task Provider_is_called_on_every_attempt_of_the_retry()
        {
            var handler = new FakeHttpMessageHandler((r, i) =>
                new HttpResponseMessage(i < 2 ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.OK));
            int calls = 0;

            RestResult<string> result = await Root(handler)
                .Retry(3, TimeSpan.Zero)
                .BearerToken(ct => Task.FromResult("token-" + Interlocked.Increment(ref calls)))
                .GetAsync();

            Assert.Equal(HttpStatusCode.OK, result.StatusCode);
            Assert.Equal(3, calls);
            Assert.Equal(new[] { "token-1", "token-2", "token-3" }, handler.Requests.Select(r => r.Headers.Authorization.Parameter));
        }

        [Fact]
        public async Task Provider_is_called_again_after_a_refresh_token()
        {
            string token = "expired";
            var handler = new FakeHttpMessageHandler((r, i) =>
                new HttpResponseMessage(r.Headers.Authorization?.Parameter == "valid" ? HttpStatusCode.OK : HttpStatusCode.Unauthorized));
            var refreshHandler = FakeHttpMessageHandler.Returns(HttpStatusCode.OK);
            int calls = 0;

            RestResult<string> result = await Root(handler)
                .BearerToken(async ct =>
                {
                    Interlocked.Increment(ref calls);
                    await Task.Yield();
                    return token;
                })
                .RefreshToken(async ct =>
                {
                    token = "valid";
                    return await Root(refreshHandler).PostAsync(ct);
                })
                .GetAsync();

            Assert.Equal(HttpStatusCode.OK, result.StatusCode);
            Assert.Equal(2, calls);
            Assert.Equal(new[] { "expired", "valid" }, handler.Requests.Select(r => r.Headers.Authorization.Parameter));
        }

        [Fact]
        public async Task Provider_receives_the_cancellation_token_of_the_request()
        {
            var handler = FakeHttpMessageHandler.Returns(HttpStatusCode.OK);
            using var cts = new CancellationTokenSource();
            bool canBeCanceled = false;
            bool canceled = false;

            RestResult<string> result = await Root(handler)
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
            var handler = FakeHttpMessageHandler.Returns(HttpStatusCode.OK);
            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

            RestResult<string> result = await Root(handler)
                .Retry(3, TimeSpan.Zero)
                .BearerToken(async ct =>
                {
                    await Task.Delay(Timeout.Infinite, ct);
                    return "abc";
                })
                .GetAsync(cts.Token);

            Assert.True(result.IsCancellationRequested);
            Assert.Equal(0, result.RetryCount);
            Assert.Empty(handler.Requests);
        }

        [Fact]
        public async Task Timeout_cancels_the_provider_and_the_attempt_is_retried()
        {
            var handler = FakeHttpMessageHandler.Returns(HttpStatusCode.OK);
            int calls = 0;

            RestResult<string> result = await Root(handler)
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
            Assert.Single(handler.Requests);
        }

        [Fact]
        public async Task Provider_exception_is_reported_by_the_result()
        {
            var handler = FakeHttpMessageHandler.Returns(HttpStatusCode.OK);
            var error = new InvalidOperationException("no token");
            Exception raised = null;

            RestResult<string> result = await Root(handler)
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
            Assert.Empty(handler.Requests);
        }

        [Fact]
        public async Task Provider_returning_a_null_task_is_reported_by_the_result()
        {
            var handler = FakeHttpMessageHandler.Returns(HttpStatusCode.OK);

            RestResult<string> result = await Root(handler).Authentication(ct => null).GetAsync();
            RestResult<string> bearer = await Root(handler).BearerToken(ct => null).GetAsync();

            Assert.IsType<InvalidOperationException>(result.Exception);
            Assert.IsType<InvalidOperationException>(bearer.Exception);
            Assert.Empty(handler.Requests);
        }

        [Fact]
        public void Provider_works_with_synchronous_requests()
        {
            var handler = FakeHttpMessageHandler.Returns(HttpStatusCode.OK);

            RestResult<string> result = Root(handler)
                .BearerToken(async ct => { await Task.Delay(10, ct); return "abc"; })
                .Get();

            Assert.Equal(HttpStatusCode.OK, result.StatusCode);
            Assert.Equal("Bearer abc", handler.Requests[0].Headers.Authorization.ToString());
        }

        [Fact]
        public async Task Last_authentication_wins_and_null_removes_it()
        {
            var handler = FakeHttpMessageHandler.Returns(HttpStatusCode.OK);
            RestBuilder bearer = Root(handler).BearerToken(ct => Task.FromResult("async"));

            await bearer.Authentication("Basic", "sync").GetAsync();
            await bearer.Authentication("Basic", "sync").BearerToken(ct => Task.FromResult("async-again")).GetAsync();
            await bearer.BearerToken(null).GetAsync();
            await bearer.Authentication((Func<AuthenticationHeaderValue>)null).GetAsync();
            await bearer.Authentication(authenticationProvider: null).GetAsync();
            await bearer.GetAsync();

            Assert.Equal("Basic sync", handler.Requests[0].Headers.Authorization.ToString());
            Assert.Equal("Bearer async-again", handler.Requests[1].Headers.Authorization.ToString());
            Assert.Null(handler.Requests[2].Headers.Authorization);
            Assert.Null(handler.Requests[3].Headers.Authorization);
            Assert.Null(handler.Requests[4].Headers.Authorization);
            // the builder is immutable: the base builder keeps its provider
            Assert.Equal("Bearer async", handler.Requests[5].Headers.Authorization.ToString());
        }

        [Fact]
        public async Task Existing_overloads_keep_binding_as_before()
        {
            var handler = FakeHttpMessageHandler.Returns(HttpStatusCode.OK);
            static AuthenticationHeaderValue Header() => new AuthenticationHeaderValue("Bearer", "method-group");

            await Root(handler).Authentication(() => new AuthenticationHeaderValue("Bearer", "lambda")).GetAsync();
            await Root(handler).Authentication(Header).GetAsync();
            await Root(handler).Authentication(authentication: null).GetAsync();
            await Root(handler).Authentication("Bearer").GetAsync();

            Assert.Equal("Bearer lambda", handler.Requests[0].Headers.Authorization.ToString());
            Assert.Equal("Bearer method-group", handler.Requests[1].Headers.Authorization.ToString());
            Assert.Null(handler.Requests[2].Headers.Authorization);
            Assert.Equal("Bearer", handler.Requests[3].Headers.Authorization.ToString());
        }
    }
}
