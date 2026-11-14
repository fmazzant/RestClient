// <summary>
//
// The MIT License (MIT)
//
// Copyright (c) 2020 Federico Mazzanti
//
// Permission is hereby granted, free of charge, to any person
// obtaining a copy of this software and associated documentation
// files (the "Software"), to deal in the Software without
// restriction, including without limitation the rights to use,
// copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the
// Software is furnished to do so, subject to the following
// conditions:
//
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES
// OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT
// HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY,
// WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
// FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR
// OTHER DEALINGS IN THE SOFTWARE.
//
// </summary>


namespace Mafesoft.RestClient
{
    using Mafesoft.RestClient.Serialization.Json;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Net.ServerSentEvents;
    using System.Runtime.CompilerServices;
    using System.Text;
    using System.Text.Json;
    using System.Text.Json.Serialization.Metadata;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Server-sent events: the events of a <c>text/event-stream</c> response are read while they are received.
    /// </summary>
    public sealed partial class RestBuilder
    {
        #region [ Server-sent events ]

        /// <summary>
        /// The media type of a server-sent events stream
        /// </summary>
        private const string EventStreamMediaType = "text/event-stream";

        /// <summary>
        /// Sends a GET request and streams the server-sent events of the response, with the data deserialized as T.
        /// See <see cref="SendEventsAsync{T}(HttpMethod, Func{string, string, bool}, CancellationToken)"/>.
        /// </summary>
        /// <typeparam name="T">The type of the JSON data of the events</typeparam>
        /// <param name="cancellationToken">Cancellation token, also combined with the token passed to WithCancellation</param>
        /// <returns>The events; the request is sent when the enumeration starts</returns>
        public IAsyncEnumerable<SseItem<T>> GetEventsAsync<T>(CancellationToken cancellationToken = default)
            => SendEventsAsync<T>(HttpMethod.Get, cancellationToken);

        /// <summary>
        /// Sends a GET request and streams the server-sent events of the response accepted by filter, with the data
        /// deserialized as T. See <see cref="SendEventsAsync{T}(HttpMethod, Func{string, string, bool}, CancellationToken)"/>.
        /// </summary>
        /// <typeparam name="T">The type of the JSON data of the events</typeparam>
        /// <param name="filter">Receives the event type and the data of an event: true to deserialize and return it, false to skip it</param>
        /// <param name="cancellationToken">Cancellation token, also combined with the token passed to WithCancellation</param>
        /// <returns>The events; the request is sent when the enumeration starts</returns>
        public IAsyncEnumerable<SseItem<T>> GetEventsAsync<T>(Func<string, string, bool> filter, CancellationToken cancellationToken = default)
            => SendEventsAsync<T>(HttpMethod.Get, filter, cancellationToken);

        /// <summary>
        /// Sends a GET request and streams the server-sent events of the response, with the data as a string.
        /// See <see cref="SendEventsAsync(HttpMethod, CancellationToken)"/>.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token, also combined with the token passed to WithCancellation</param>
        /// <returns>The events; the request is sent when the enumeration starts</returns>
        public IAsyncEnumerable<SseItem<string>> GetEventsAsync(CancellationToken cancellationToken = default)
            => SendEventsAsync(HttpMethod.Get, cancellationToken);

        /// <summary>
        /// Sends a POST request (with the configured payload) and streams the server-sent events of the response, with
        /// the data deserialized as T. See <see cref="SendEventsAsync{T}(HttpMethod, Func{string, string, bool}, CancellationToken)"/>.
        /// </summary>
        /// <typeparam name="T">The type of the JSON data of the events</typeparam>
        /// <param name="cancellationToken">Cancellation token, also combined with the token passed to WithCancellation</param>
        /// <returns>The events; the request is sent when the enumeration starts</returns>
        public IAsyncEnumerable<SseItem<T>> PostEventsAsync<T>(CancellationToken cancellationToken = default)
            => SendEventsAsync<T>(HttpMethod.Post, cancellationToken);

        /// <summary>
        /// Sends a POST request (with the configured payload) and streams the server-sent events of the response
        /// accepted by filter, with the data deserialized as T.
        /// See <see cref="SendEventsAsync{T}(HttpMethod, Func{string, string, bool}, CancellationToken)"/>.
        /// </summary>
        /// <typeparam name="T">The type of the JSON data of the events</typeparam>
        /// <param name="filter">Receives the event type and the data of an event: true to deserialize and return it, false to skip it</param>
        /// <param name="cancellationToken">Cancellation token, also combined with the token passed to WithCancellation</param>
        /// <returns>The events; the request is sent when the enumeration starts</returns>
        public IAsyncEnumerable<SseItem<T>> PostEventsAsync<T>(Func<string, string, bool> filter, CancellationToken cancellationToken = default)
            => SendEventsAsync<T>(HttpMethod.Post, filter, cancellationToken);

        /// <summary>
        /// Sends a POST request (with the configured payload) and streams the server-sent events of the response, with
        /// the data as a string. See <see cref="SendEventsAsync(HttpMethod, CancellationToken)"/>.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token, also combined with the token passed to WithCancellation</param>
        /// <returns>The events; the request is sent when the enumeration starts</returns>
        public IAsyncEnumerable<SseItem<string>> PostEventsAsync(CancellationToken cancellationToken = default)
            => SendEventsAsync(HttpMethod.Post, cancellationToken);

        /// <summary>
        /// Sends an HTTP request and streams the server-sent events of the response, with the data deserialized as T.
        /// See <see cref="SendEventsAsync{T}(HttpMethod, Func{string, string, bool}, CancellationToken)"/>.
        /// </summary>
        /// <typeparam name="T">The type of the JSON data of the events</typeparam>
        /// <param name="method">The HTTP method</param>
        /// <param name="cancellationToken">Cancellation token, also combined with the token passed to WithCancellation</param>
        /// <returns>The events; the request is sent when the enumeration starts</returns>
        /// <exception cref="ArgumentNullException">method is null</exception>
        /// <exception cref="NotSupportedException">The configured serializer is not SystemTextJsonSerializer (e.g. Xml or a custom serializer), or the configured JsonSerializerContext does not contain T</exception>
        public IAsyncEnumerable<SseItem<T>> SendEventsAsync<T>(HttpMethod method, CancellationToken cancellationToken = default)
            => SendEventsAsync<T>(method, null, cancellationToken);

        /// <summary>
        /// Sends an HTTP request and streams the server-sent events (<c>text/event-stream</c>) of the response while they
        /// are received, parsed by <c>System.Net.ServerSentEvents.SseParser</c>: the data of every event accepted by
        /// filter is deserialized as T (an event whose data spans more lines has the lines joined by '\n'), the event
        /// type, the event id and the reconnection interval are those of the event. Every enumeration sends a new request.
        /// <para>
        /// The request has the header <c>Accept: text/event-stream</c>, unless an Accept header is set with Header.
        /// It goes through the same pipeline as SendAsync until the headers of a successful response are received, and
        /// it throws the same exceptions as <see cref="SendAsyncEnumerable{T}(HttpMethod, CancellationToken)"/> when the
        /// enumeration starts; once the events are being read nothing is retried and only the cancellation token stops
        /// the stream (the Last-Event-ID reconnection is not automatic). The response is released when the enumeration
        /// ends, also with break or an exception. Events and diagnostics are those of SendAsyncEnumerable.
        /// </para>
        /// <para>
        /// The data is deserialized with the JSON configuration of the builder: with <c>Json(JsonSerializerContext)</c>
        /// the metadata of T comes from the context (Native AOT and trimming safe). Data that is not valid JSON throws
        /// JsonException and stops the enumeration: use filter to skip the events that do not carry T, such as the
        /// <c>data: [DONE]</c> sentinel of some APIs (<c>(type, data) => data != "[DONE]"</c>) or the event types that
        /// are not needed, or SendEventsAsync without T to read the data as a string.
        /// </para>
        /// </summary>
        /// <typeparam name="T">The type of the JSON data of the events</typeparam>
        /// <param name="method">The HTTP method</param>
        /// <param name="filter">Receives the event type and the data of an event: true to deserialize and return it, false to skip it. Null returns every event.</param>
        /// <param name="cancellationToken">Cancellation token, also combined with the token passed to WithCancellation</param>
        /// <returns>The events; the request is sent when the enumeration starts</returns>
        /// <exception cref="ArgumentNullException">method is null</exception>
        /// <exception cref="NotSupportedException">The configured serializer is not SystemTextJsonSerializer (e.g. Xml or a custom serializer), or the configured JsonSerializerContext does not contain T</exception>
        public IAsyncEnumerable<SseItem<T>> SendEventsAsync<T>(HttpMethod method, Func<string, string, bool>? filter, CancellationToken cancellationToken = default)
        {
            if (method == null) { throw new ArgumentNullException(nameof(method)); }
            if (!(serializer is SystemTextJsonSerializer jsonSerializer))
            {
                throw new NotSupportedException(
                    $"Deserializing server-sent events requires the JSON serializer (SystemTextJsonSerializer), but the builder uses {serializer.GetType().FullName}: call Json(...) or WithSerializer(new SystemTextJsonSerializer(...)) before SendEventsAsync<T>, or use SendEventsAsync to read the data as a string.");
            }

            // resolved here, so that a type missing from the JsonSerializerContext is reported before sending the request
            JsonTypeInfo<T> typeInfo = jsonSerializer.GetTypeInfo<T>();
            return AcceptEventStream().StreamAsync(method, (stream, token) => ReadEventsAsync(stream, typeInfo, filter, token), cancellationToken);
        }

        /// <summary>
        /// Sends an HTTP request and streams the server-sent events (<c>text/event-stream</c>) of the response while they
        /// are received, with the data as a string (no deserialization, so any serializer can be configured).
        /// Same behaviour as <see cref="SendEventsAsync{T}(HttpMethod, Func{string, string, bool}, CancellationToken)"/>.
        /// </summary>
        /// <param name="method">The HTTP method</param>
        /// <param name="cancellationToken">Cancellation token, also combined with the token passed to WithCancellation</param>
        /// <returns>The events; the request is sent when the enumeration starts</returns>
        /// <exception cref="ArgumentNullException">method is null</exception>
        public IAsyncEnumerable<SseItem<string>> SendEventsAsync(HttpMethod method, CancellationToken cancellationToken = default)
        {
            if (method == null) { throw new ArgumentNullException(nameof(method)); }
            return AcceptEventStream().StreamAsync(method, (stream, token) => SseParser.Create(stream).EnumerateAsync(token), cancellationToken);
        }

        /// <summary>
        /// A copy of the builder that sends Accept: text/event-stream when no Accept header has been set
        /// </summary>
        private RestBuilder AcceptEventStream()
            => With(r => r.onRequestHeaders = (Action<HttpRequestHeaders>?)Delegate.Combine(onRequestHeaders, (Action<HttpRequestHeaders>)(headers =>
            {
                if (!headers.Contains("Accept"))
                {
                    headers.Accept.Add(new MediaTypeWithQualityHeaderValue(EventStreamMediaType));
                }
            })));

        /// <summary>
        /// Parses the events of the stream and deserializes the data of those accepted by filter
        /// </summary>
        private static async IAsyncEnumerable<SseItem<T>> ReadEventsAsync<T>(Stream stream, JsonTypeInfo<T> typeInfo, Func<string, string, bool>? filter, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            SseParser<EventData<T>> parser = SseParser.Create(stream, (string eventType, ReadOnlySpan<byte> data) =>
            {
                if (filter != null && !filter(eventType, GetString(data)))
                {
                    return default;
                }
                return new EventData<T>(JsonSerializer.Deserialize(data, typeInfo)!);
            });

            await foreach (SseItem<EventData<T>> item in parser.EnumerateAsync(cancellationToken).ConfigureAwait(false))
            {
                if (item.Data.Accepted)
                {
                    yield return new SseItem<T>(item.Data.Value, item.EventType)
                    {
                        EventId = item.EventId,
                        ReconnectionInterval = item.ReconnectionInterval
                    };
                }
            }
        }

        /// <summary>
        /// Decodes the UTF-8 data of an event
        /// </summary>
        private static string GetString(ReadOnlySpan<byte> data)
        {
#if NETSTANDARD2_0
            return Encoding.UTF8.GetString(data.ToArray());
#else
            return Encoding.UTF8.GetString(data);
#endif
        }

        /// <summary>
        /// The deserialized data of an event, or default for an event skipped by the filter
        /// </summary>
        private readonly struct EventData<T>
        {
            public EventData(T value)
            {
                Value = value;
                Accepted = true;
            }

            public T Value { get; }

            public bool Accepted { get; }
        }

        #endregion
    }
}
