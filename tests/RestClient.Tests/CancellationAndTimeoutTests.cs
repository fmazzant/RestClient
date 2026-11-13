namespace Mafesoft.RestClient.Tests
{
    using System.Net;
    using System.Net.Http;

    /// <summary>
    /// Cancellation and timeout on every verb, synchronous and asynchronous
    /// (also run on net48, where the netstandard2.0 build is used)
    /// </summary>
    public class CancellationAndTimeoutTests
    {
        private static readonly string[] Verbs = { "GET", "POST", "PUT", "PATCH", "DELETE", "HEAD", "OPTIONS" };

        public static IEnumerable<object[]> VerbsAndModes()
            => from verb in Verbs
               from synchronous in new[] { false, true }
               select new object[] { verb, synchronous };

        private static RestBuilder Root(FakeHttpMessageHandler handler)
            => Rest.Build(handler.CreateClient()).Url("https://api.example.com/v1/");

        /// <summary>
        /// A server that never answers: only the cancellation or the timeout end the request
        /// </summary>
        private static FakeHttpMessageHandler NeverAnswers()
            => new FakeHttpMessageHandler((r, i) => new TaskCompletionSource<HttpResponseMessage>().Task);

        private static Task<RestResult<string>> Send(RestBuilder builder, string verb, bool synchronous, CancellationToken cancellationToken)
        {
            if (synchronous)
            {
                // the blocking call runs on a thread pool thread, as it would in a synchronous caller
                return Task.Run(() => verb switch
                {
                    "GET" => builder.Get(cancellationToken),
                    "POST" => builder.Post(cancellationToken),
                    "PUT" => builder.Put(cancellationToken),
                    "PATCH" => builder.Patch(cancellationToken),
                    "DELETE" => builder.Delete(cancellationToken),
                    "HEAD" => builder.Head(cancellationToken),
                    "OPTIONS" => builder.Options(cancellationToken),
                    _ => throw new ArgumentOutOfRangeException(nameof(verb)),
                });
            }

            return verb switch
            {
                "GET" => builder.GetAsync(cancellationToken),
                "POST" => builder.PostAsync(cancellationToken),
                "PUT" => builder.PutAsync(cancellationToken),
                "PATCH" => builder.PatchAsync(cancellationToken),
                "DELETE" => builder.DeleteAsync(cancellationToken),
                "HEAD" => builder.HeadAsync(cancellationToken),
                "OPTIONS" => builder.OptionsAsync(cancellationToken),
                _ => throw new ArgumentOutOfRangeException(nameof(verb)),
            };
        }

        [Theory]
        [MemberData(nameof(VerbsAndModes))]
        public async Task Verb_completes_with_the_expected_method(string verb, bool synchronous)
        {
            var handler = FakeHttpMessageHandler.Returns(HttpStatusCode.OK);

            RestResult<string> result = await Send(Root(handler), verb, synchronous, CancellationToken.None);

            Assert.Equal(HttpStatusCode.OK, result.StatusCode);
            Assert.Equal(verb, handler.Requests.Single().Method.Method);
        }

        [Theory]
        [MemberData(nameof(VerbsAndModes))]
        public async Task Already_cancelled_token(string verb, bool synchronous)
        {
            var handler = NeverAnswers();
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            RestResult<string> result = await Send(Root(handler), verb, synchronous, cts.Token);

            Assert.True(result.IsCancellationRequested);
            Assert.False(result.IsTimedOut);
            Assert.Null(result.Response);
        }

        [Theory]
        [MemberData(nameof(VerbsAndModes))]
        public async Task Token_cancelled_while_waiting_for_the_response(string verb, bool synchronous)
        {
            var handler = NeverAnswers();
            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

            RestResult<string> result = await Send(Root(handler).Timeout(TimeSpan.FromSeconds(30)), verb, synchronous, cts.Token);

            Assert.True(result.IsCancellationRequested);
            Assert.False(result.IsTimedOut);
            Assert.Null(result.Response);
        }

        [Theory]
        [MemberData(nameof(VerbsAndModes))]
        public async Task Timeout_while_waiting_for_the_response(string verb, bool synchronous)
        {
            var handler = NeverAnswers();

            RestResult<string> result = await Send(Root(handler).Timeout(TimeSpan.FromMilliseconds(100)), verb, synchronous, CancellationToken.None);

            Assert.True(result.IsTimedOut);
            Assert.False(result.IsCancellationRequested);
            Assert.IsType<TimeoutException>(result.Exception);
            Assert.Null(result.Response);
        }

        [Fact]
        public async Task Timeout_is_not_reported_when_the_response_arrives_in_time()
        {
            var handler = FakeHttpMessageHandler.Returns(HttpStatusCode.OK, "ok", "text/plain");

            RestResult<string> result = await Root(handler).Timeout(TimeSpan.FromSeconds(30)).GetAsync();

            Assert.Null(result.Exception);
            Assert.Equal("ok", result.Content);
        }
    }
}
