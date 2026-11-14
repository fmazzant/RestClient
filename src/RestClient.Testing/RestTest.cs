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

namespace Mafesoft.RestClient.Testing
{
    using Mafesoft.RestClient.Internal;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Text;
    using System.Text.Json;
    using System.Text.Json.Serialization.Metadata;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// A fake server for the tests. While it is active (from the constructor to Dispose), every request sent by
    /// Mafesoft.RestClient in the current async flow (the test method and the code it calls) is answered by it,
    /// without reaching the network: with <c>Rest.Build()</c>, with <c>Rest.Build(httpClient)</c> and with the builders of
    /// <c>IRestClientFactory</c>. The responses are configured with the RespondWith methods, the requests are recorded
    /// in CallLog and checked with ShouldHaveCalled. Test framework agnostic: failed assertions throw
    /// RestCallAssertionException. Thread safe.
    /// </summary>
    /// <example>
    /// <code>
    /// using var http = new RestTest();
    /// http.RespondWithJson(new User { Name = "fede" }).For(HttpMethod.Get, "*/users/*");
    ///
    /// var result = await sut.GetUserAsync("fede");
    ///
    /// http.ShouldHaveCalled("*/users/fede").WithVerb(HttpMethod.Get).Times(1);
    /// </code>
    /// </example>
    public sealed class RestTest : IDisposable
    {
        private const string ReflectionMessage = "JSON serialization of an object uses reflection: use RespondWithJson(body, JsonTypeInfo) to be trimming and Native AOT safe.";

        private static readonly JsonSerializerOptions defaultJsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);

        private readonly object sync = new object();
        private readonly List<RestTestResponse> responses = new List<RestTestResponse>();
        private readonly List<RestTestCall> calls = new List<RestTestCall>();
        private readonly RestTestHandler handler;
        private readonly HttpMessageHandler? previous;
        private bool disposed;

        /// <summary>
        /// Starts the fake server for the current async flow. Dispose it at the end of the test.
        /// </summary>
        public RestTest()
        {
            handler = new RestTestHandler(this);
            previous = TestInterception.Handler;
            TestInterception.Handler = handler;
        }

        /// <summary>
        /// The handler of the fake server, e.g. for an HttpClient used outside the async flow of the test
        /// (it is not needed with Rest.Build() and IRestClientFactory in the test method).
        /// </summary>
        public HttpMessageHandler Handler => handler;

        /// <summary>
        /// Creates an HttpClient over the fake server (see Handler)
        /// </summary>
        public HttpClient CreateClient() => new HttpClient(handler, disposeHandler: false);

        /// <summary>
        /// The requests received so far, in order
        /// </summary>
        public IReadOnlyList<RestTestCall> CallLog
        {
            get
            {
                lock (sync)
                {
                    return calls.ToArray();
                }
            }
        }

        #region [ Responses ]

        /// <summary>
        /// Adds a response with a string body. The responses are used in the order they are added; the last one
        /// that matches a request (see RestTestResponse.For) is used again for all the following matching requests.
        /// A request with no matching response gets 200 OK with an empty body.
        /// </summary>
        /// <param name="body">The body</param>
        /// <param name="status">The status code</param>
        /// <param name="mediaType">The media type of the body</param>
        public RestTestResponse RespondWith(string body = "", HttpStatusCode status = HttpStatusCode.OK, string mediaType = "text/plain")
            => RespondWith(request => CreateResponse(status, body ?? string.Empty, mediaType));

        /// <summary>
        /// Adds a response with the status code and an empty body (see RespondWith(string, HttpStatusCode, string))
        /// </summary>
        /// <param name="status">The status code</param>
        public RestTestResponse RespondWith(HttpStatusCode status)
            => RespondWith(string.Empty, status);

        /// <summary>
        /// Adds a response created for every request by the func (see RespondWith(string, HttpStatusCode, string))
        /// </summary>
        /// <param name="responder">Creates the response of the request</param>
        public RestTestResponse RespondWith(Func<HttpRequestMessage, HttpResponseMessage> responder)
        {
            if (responder == null) { throw new ArgumentNullException(nameof(responder)); }
            return Add(new RestTestResponse((request, cancellationToken) => Task.FromResult(responder(request))));
        }

        /// <summary>
        /// Adds a JSON response, serialized with the source generated metadata (Native AOT safe)
        /// (see RespondWith(string, HttpStatusCode, string))
        /// </summary>
        /// <param name="body">The value serialized as JSON</param>
        /// <param name="typeInfo">The metadata of T, e.g. MyJsonContext.Default.User</param>
        /// <param name="status">The status code</param>
        public RestTestResponse RespondWithJson<T>(T body, JsonTypeInfo<T> typeInfo, HttpStatusCode status = HttpStatusCode.OK)
        {
            if (typeInfo == null) { throw new ArgumentNullException(nameof(typeInfo)); }
            return RespondWith(JsonSerializer.Serialize(body, typeInfo), status, "application/json");
        }

        /// <summary>
        /// Adds a JSON response (see RespondWith(string, HttpStatusCode, string)).
        /// Without options the web defaults are used (camelCase), as the default serializer of RestClient.
        /// </summary>
        /// <param name="body">The value serialized as JSON</param>
        /// <param name="status">The status code</param>
        /// <param name="options">The System.Text.Json options</param>
#if NET
        [RequiresUnreferencedCode(ReflectionMessage)]
        [RequiresDynamicCode(ReflectionMessage)]
#endif
        public RestTestResponse RespondWithJson(object? body, HttpStatusCode status = HttpStatusCode.OK, JsonSerializerOptions? options = null)
        {
            string json = body == null ? "null" : JsonSerializer.Serialize(body, body.GetType(), options ?? defaultJsonOptions);
            return RespondWith(json, status, "application/json");
        }

        /// <summary>
        /// Adds a response that fails the request with the exception, as a network error would
        /// (see RespondWith(string, HttpStatusCode, string))
        /// </summary>
        /// <param name="exception">The exception, e.g. an HttpRequestException</param>
        public RestTestResponse SimulateException(Exception exception)
        {
            if (exception == null) { throw new ArgumentNullException(nameof(exception)); }
            return Add(new RestTestResponse((request, cancellationToken) => Task.FromException<HttpResponseMessage>(exception)));
        }

        /// <summary>
        /// Adds a response that never arrives: the request ends when the Timeout of the builder elapses
        /// (or when it is canceled). Set a short Timeout on the builder.
        /// </summary>
        public RestTestResponse SimulateTimeout()
            => Add(new RestTestResponse(async (request, cancellationToken) =>
            {
                await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);
                throw new OperationCanceledException(cancellationToken);
            }));

        private RestTestResponse Add(RestTestResponse response)
        {
            lock (sync)
            {
                responses.Add(response);
            }
            return response;
        }

        private static HttpResponseMessage CreateResponse(HttpStatusCode status, string body, string mediaType)
            => new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, mediaType) };

        #endregion

        #region [ Assertions ]

        /// <summary>
        /// Asserts that at least one request matched the URL pattern (<c>*</c> matches any text, case-insensitive),
        /// e.g. <c>"*/users/*"</c>. The returned assertion checks more details of the matching calls.
        /// </summary>
        /// <param name="urlPattern">The URL pattern</param>
        /// <exception cref="RestCallAssertionException">No request matched</exception>
        public RestCallAssertion ShouldHaveCalled(string urlPattern)
        {
            if (urlPattern == null) { throw new ArgumentNullException(nameof(urlPattern)); }
            return new RestCallAssertion(CallLog, Array.Empty<string>()).Where(call => UrlPattern.IsMatch(urlPattern, call.Url), $"URL {urlPattern}");
        }

        /// <summary>
        /// Asserts that no request matched the URL pattern (see ShouldHaveCalled)
        /// </summary>
        /// <param name="urlPattern">The URL pattern</param>
        /// <exception cref="RestCallAssertionException">A request matched</exception>
        public void ShouldNotHaveCalled(string urlPattern)
        {
            if (urlPattern == null) { throw new ArgumentNullException(nameof(urlPattern)); }
            IReadOnlyList<RestTestCall> log = CallLog;
            if (log.Any(call => UrlPattern.IsMatch(urlPattern, call.Url)))
            {
                throw new RestCallAssertionException(RestCallAssertion.Describe($"Expected no call to URL {urlPattern}, but there were some.", log));
            }
        }

        /// <summary>
        /// Asserts that at least one request has been sent. The returned assertion checks more details of the calls.
        /// </summary>
        /// <exception cref="RestCallAssertionException">No request has been sent</exception>
        public RestCallAssertion ShouldHaveMadeACall()
            => new RestCallAssertion(CallLog, Array.Empty<string>()).Where(call => true, "any URL");

        /// <summary>
        /// Asserts that no request has been sent
        /// </summary>
        /// <exception cref="RestCallAssertionException">A request has been sent</exception>
        public void ShouldNotHaveMadeACall()
        {
            IReadOnlyList<RestTestCall> log = CallLog;
            if (log.Count > 0)
            {
                throw new RestCallAssertionException(RestCallAssertion.Describe("Expected no calls, but there were some.", log));
            }
        }

        #endregion

        /// <summary>
        /// Stops the fake server: the following requests of the async flow use the network again
        /// </summary>
        public void Dispose()
        {
            if (disposed)
            {
                return;
            }
            disposed = true;
            if (ReferenceEquals(TestInterception.Handler, handler))
            {
                TestInterception.Handler = previous;
            }
        }

        /// <summary>
        /// Records the request and returns the response to use (null: the default 200 OK)
        /// </summary>
        internal RestTestResponse? Receive(RestTestCall call)
        {
            lock (sync)
            {
                calls.Add(call);

                RestTestResponse? last = null;
                foreach (RestTestResponse response in responses)
                {
                    if (!response.Matches(call.Request))
                    {
                        continue;
                    }
                    if (!response.Used)
                    {
                        response.Used = true;
                        return response;
                    }
                    last = response;
                }
                return last;
            }
        }
    }
}
