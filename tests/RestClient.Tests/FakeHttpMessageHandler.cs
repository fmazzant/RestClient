namespace RestClient.Tests
{
    using System.Net;
    using System.Net.Http;
    using System.Text;

    /// <summary>
    /// Records the requests and returns the configured responses (thread-safe)
    /// </summary>
    internal sealed class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, int, Task<HttpResponseMessage>> responder;
        private readonly object sync = new object();

        public List<HttpRequestMessage> Requests { get; } = new List<HttpRequestMessage>();

        public List<string> RequestBodies { get; } = new List<string>();

        public FakeHttpMessageHandler(Func<HttpRequestMessage, int, Task<HttpResponseMessage>> responder)
        {
            this.responder = responder;
        }

        public FakeHttpMessageHandler(Func<HttpRequestMessage, int, HttpResponseMessage> responder)
            : this((r, i) => Task.FromResult(responder(r, i)))
        {
        }

        public static FakeHttpMessageHandler Returns(HttpStatusCode status, string body = "", string mediaType = "application/json")
            => new FakeHttpMessageHandler((r, i) => new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, mediaType)
            });

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            string body = request.Content == null ? null : await request.Content.ReadAsStringAsync();
            int index;
            lock (sync)
            {
                Requests.Add(request);
                RequestBodies.Add(body);
                index = Requests.Count - 1;
            }

            cancellationToken.ThrowIfCancellationRequested();
            Task<HttpResponseMessage> response = responder(request, index);
            Task cancelled = Task.Delay(Timeout.Infinite, cancellationToken);
            await Task.WhenAny(response, cancelled);
            cancellationToken.ThrowIfCancellationRequested();
            return await response;
        }

        public HttpClient CreateClient() => new HttpClient(this, disposeHandler: false);

        protected override void Dispose(bool disposing)
        {
            // shared by many HttpClient instances in the tests: never disposed
        }
    }
}
