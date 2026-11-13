namespace Mafesoft.RestClient.Tests
{
    using System.Collections.Concurrent;
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Net.ServerSentEvents;
    using System.Text;
    using System.Text.Json;
    using GatedStream = AsyncEnumerableTests.GatedStream;

    /// <summary>
    /// GetEventsAsync / SendEventsAsync against an in-process handler (every target framework, .NET Framework included)
    /// </summary>
    public class ServerSentEventsTests
    {
        private const string TwoEvents = "data: {\"id\":1,\"name\":\"a\"}\n\ndata: {\"id\":2,\"name\":\"b\"}\n\n";

        private static RestBuilder Root(FakeHttpMessageHandler handler)
            => Rest.Build(handler.CreateClient()).Url("https://api.example.com/v1/");

        private static FakeHttpMessageHandler Events(string events)
            => FakeHttpMessageHandler.Returns(HttpStatusCode.OK, events, "text/event-stream");

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
                Content = new StreamContent(stream) { Headers = { ContentType = new MediaTypeHeaderValue("text/event-stream") } }
            };

        [Fact]
        public async Task Data_of_the_events_is_deserialized()
        {
            var handler = Events(TwoEvents);

            List<SseItem<User>> events = await ToListAsync(Root(handler).Command("stream").GetEventsAsync<User>());

            Assert.Equal(new[] { 1, 2 }, events.Select(e => e.Data.Id));
            Assert.Equal(new[] { "a", "b" }, events.Select(e => e.Data.Name));
            Assert.All(events, e => Assert.Equal("message", e.EventType));
            HttpRequestMessage request = handler.Requests.Single();
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Equal("https://api.example.com/v1/stream", request.RequestUri.ToString());
            Assert.Equal("text/event-stream", request.Headers.Accept.Single().MediaType);
        }

        [Fact]
        public async Task Event_type_id_retry_and_multi_line_data_are_returned()
        {
            var handler = Events(
                ": comment\n" +
                "event: delta\nid: 7\nretry: 3000\ndata: {\"id\":1,\ndata: \"name\":\"a\"}\n\n" +
                "data: {\"id\":2}\n\n");

            List<SseItem<User>> events = await ToListAsync(Root(handler).GetEventsAsync<User>());

            Assert.Equal(2, events.Count);
            Assert.Equal("delta", events[0].EventType);
            Assert.Equal("7", events[0].EventId);
            Assert.Equal(TimeSpan.FromSeconds(3), events[0].ReconnectionInterval);
            Assert.Equal("a", events[0].Data.Name);
            Assert.Equal("message", events[1].EventType);
            Assert.Equal(2, events[1].Data.Id);
        }

        [Fact]
        public async Task String_events_return_the_raw_data()
        {
            var handler = Events("event: start\ndata: line 1\ndata: line 2\n\ndata: [DONE]\n\n");

            List<SseItem<string>> events = await ToListAsync(Root(handler).GetEventsAsync());

            Assert.Equal(new[] { "line 1\nline 2", "[DONE]" }, events.Select(e => e.Data));
            Assert.Equal(new[] { "start", "message" }, events.Select(e => e.EventType));
            Assert.Equal("text/event-stream", handler.Requests.Single().Headers.Accept.Single().MediaType);
        }

        [Fact]
        public async Task Filter_skips_the_events_that_do_not_carry_T()
        {
            var handler = Events("event: ping\ndata: \n\n" + TwoEvents + "data: [DONE]\n\n");
            var seen = new List<string>();

            List<SseItem<User>> events = await ToListAsync(Root(handler).GetEventsAsync<User>((type, data) =>
            {
                seen.Add(type);
                return type == "message" && data != "[DONE]";
            }));

            Assert.Equal(new[] { 1, 2 }, events.Select(e => e.Data.Id));
            Assert.Equal(new[] { "ping", "message", "message", "message" }, seen);
        }

        [Fact]
        public async Task Data_that_is_not_json_throws_JsonException_and_disposes_the_response()
        {
            var stream = new GatedStream(TwoEvents + "data: [DONE]\n\n", ended: true);
            var handler = new FakeHttpMessageHandler((r, i) => StreamResponse(stream));
            var exceptions = new ConcurrentQueue<Exception>();
            var received = new List<int>();

            await Assert.ThrowsAnyAsync<JsonException>(async () =>
            {
                await foreach (SseItem<User> item in Root(handler).OnException(exceptions.Enqueue).GetEventsAsync<User>())
                {
                    received.Add(item.Data.Id);
                }
            });

            Assert.Equal(new[] { 1, 2 }, received);
            Assert.True(stream.Disposed);
            Assert.IsAssignableFrom<JsonException>(Assert.Single(exceptions));
        }

        [Fact]
        public async Task Accept_header_set_by_the_caller_is_kept()
        {
            var handler = Events(TwoEvents);

            await ToListAsync(Root(handler).Header(h => h.Accept.ParseAdd("application/x-ndjson")).GetEventsAsync());
            await ToListAsync(Root(handler).Header("Accept", "*/*").GetEventsAsync<User>());

            Assert.Equal("application/x-ndjson", handler.Requests[0].Headers.Accept.Single().MediaType);
            Assert.Equal("*/*", handler.Requests[1].Headers.Accept.Single().MediaType);
        }

        [Fact]
        public async Task Accept_header_is_not_added_to_the_other_requests_of_the_builder()
        {
            var handler = Events(TwoEvents);
            RestBuilder api = Root(handler);

            await ToListAsync(api.GetEventsAsync());
            await api.GetAsync();

            Assert.Single(handler.Requests[0].Headers.Accept);
            Assert.Empty(handler.Requests[1].Headers.Accept);
        }

        [Fact]
        public async Task Nothing_is_sent_until_the_enumeration_starts_and_every_enumeration_sends_a_request()
        {
            var handler = Events(TwoEvents);

            IAsyncEnumerable<SseItem<User>> events = Root(handler).GetEventsAsync<User>();
            Assert.Empty(handler.Requests);

            Assert.Equal(2, (await ToListAsync(events)).Count);
            Assert.Equal(2, (await ToListAsync(events)).Count);
            Assert.Equal(2, handler.Requests.Count);
        }

        [Fact]
        public async Task Post_sends_the_payload()
        {
            var handler = Events(TwoEvents);

            List<SseItem<User>> events = await ToListAsync(Root(handler).Payload(new User { Id = 7 }).PostEventsAsync<User>());
            List<SseItem<string>> raw = await ToListAsync(Root(handler).Payload(new User { Id = 8 }).PostEventsAsync());

            Assert.Equal(2, events.Count);
            Assert.Equal(2, raw.Count);
            Assert.All(handler.Requests, r => Assert.Equal(HttpMethod.Post, r.Method));
            Assert.Contains("\"id\":7", handler.RequestBodies[0]);
            Assert.Contains("\"id\":8", handler.RequestBodies[1]);
        }

        [Fact]
        public async Task Error_status_throws_the_exception_of_EnsureSuccessStatusCode()
        {
            var handler = FakeHttpMessageHandler.Returns(HttpStatusCode.NotFound, "{\"error\":\"missing\"}");
            RestBuilder api = Root(handler).Command("missing");

            RestResult<User> result = await api.GetAsync<User>();
            HttpRequestException expected = Assert.Throws<HttpRequestException>(() => result.EnsureSuccessStatusCode());
            HttpRequestException typed = await Assert.ThrowsAsync<HttpRequestException>(() => ToListAsync(api.GetEventsAsync<User>()));
            HttpRequestException raw = await Assert.ThrowsAsync<HttpRequestException>(() => ToListAsync(api.GetEventsAsync()));

            Assert.Equal(expected.Message, typed.Message);
            Assert.Equal(expected.Message, raw.Message);
            Assert.Contains("404", typed.Message);
        }

        [Fact]
        public async Task Transient_errors_are_retried_before_the_headers()
        {
            var handler = new FakeHttpMessageHandler((r, i) => i == 0
                ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
                : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(TwoEvents, Encoding.UTF8, "text/event-stream") });

            List<SseItem<User>> events = await ToListAsync(Root(handler).Retry(2, TimeSpan.Zero).GetEventsAsync<User>());

            Assert.Equal(2, events.Count);
            Assert.Equal(2, handler.Requests.Count);
        }

        [Fact]
        public async Task Json_context_is_used_and_a_missing_type_is_reported_before_sending()
        {
            var handler = Events(TwoEvents);
            RestBuilder api = Root(handler).Json(TestJsonContext.Default);

            List<SseItem<User>> events = await ToListAsync(api.GetEventsAsync<User>());
            Assert.Equal(new[] { "a", "b" }, events.Select(e => e.Data.Name));

            Assert.Throws<NotSupportedException>(() => api.GetEventsAsync<Uri>());
            Assert.Single(handler.Requests);
        }

        [Fact]
        public async Task Xml_and_custom_serializers_are_not_supported_for_typed_events()
        {
            var handler = Events(TwoEvents);

            NotSupportedException exception = Assert.Throws<NotSupportedException>(() => Root(handler).Xml().GetEventsAsync<User>());
            Assert.Contains("JSON", exception.Message);
            Assert.Throws<ArgumentNullException>(() => Root(handler).SendEventsAsync<User>(null));
            Assert.Throws<ArgumentNullException>(() => Root(handler).SendEventsAsync(null));
            Assert.Empty(handler.Requests);

            // the raw data does not need the serializer
            Assert.Equal(2, (await ToListAsync(Root(handler).Xml().GetEventsAsync())).Count);
        }

        [Fact]
        public async Task Cancellation_before_the_enumeration_throws_OperationCanceledException()
        {
            var handler = Events(TwoEvents);
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => ToListAsync(Root(handler).GetEventsAsync<User>(cts.Token)));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => ToListAsync(Root(handler).GetEventsAsync(cts.Token)));
        }

        [Fact]
        public async Task Cancellation_while_reading_aborts_the_response()
        {
            // the stream ignores the cancellation token (as the response stream of .NET Framework): the read is
            // aborted by disposing the response
            var stream = new GatedStream("data: {\"id\":1}\n\n");
            var handler = new FakeHttpMessageHandler((r, i) => StreamResponse(stream));
            using var cts = new CancellationTokenSource();
            var received = new List<User>();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            {
                await foreach (SseItem<User> item in Root(handler).GetEventsAsync<User>().WithCancellation(cts.Token))
                {
                    received.Add(item.Data);
                    cts.CancelAfter(50);
                }
            });

            Assert.Single(received);
            Assert.True(stream.Disposed);
        }

        [Fact]
        public async Task Break_disposes_the_response()
        {
            var stream = new GatedStream("data: one\n\ndata: two\n\n");
            var handler = new FakeHttpMessageHandler((r, i) => StreamResponse(stream));

            await foreach (SseItem<string> item in Root(handler).GetEventsAsync())
            {
                Assert.Equal("one", item.Data);
                Assert.False(stream.Disposed);
                break;
            }

            Assert.True(stream.Disposed);
        }
    }
}
