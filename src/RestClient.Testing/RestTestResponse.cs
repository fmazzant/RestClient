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
    using System;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// A response of the fake server (RestTest). By default it answers any request: For and When restrict it.
    /// </summary>
    public sealed class RestTestResponse
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> factory;
        private HttpMethod? method;
        private string? urlPattern;
        private Func<HttpRequestMessage, bool>? predicate;

        internal RestTestResponse(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> factory)
        {
            this.factory = factory;
        }

        /// <summary>
        /// true once the response has been used by a request (accessed under the lock of RestTest)
        /// </summary>
        internal bool Used { get; set; }

        /// <summary>
        /// Uses the response only for the requests whose URL matches the pattern (<c>*</c> matches any text,
        /// case-insensitive), e.g. <c>"*/users/*"</c>.
        /// </summary>
        /// <param name="urlPattern">The URL pattern</param>
        /// <returns>This response</returns>
        public RestTestResponse For(string urlPattern)
        {
            this.urlPattern = urlPattern ?? throw new ArgumentNullException(nameof(urlPattern));
            return this;
        }

        /// <summary>
        /// Uses the response only for the requests with the method and a URL that matches the pattern (see For(string))
        /// </summary>
        /// <param name="method">The HTTP method</param>
        /// <param name="urlPattern">The URL pattern</param>
        /// <returns>This response</returns>
        public RestTestResponse For(HttpMethod method, string urlPattern)
        {
            this.method = method ?? throw new ArgumentNullException(nameof(method));
            return For(urlPattern);
        }

        /// <summary>
        /// Uses the response only for the requests that satisfy the predicate (in addition to For)
        /// </summary>
        /// <param name="predicate">The condition on the request; the content of the request must not be read</param>
        /// <returns>This response</returns>
        public RestTestResponse When(Func<HttpRequestMessage, bool> predicate)
        {
            this.predicate = predicate ?? throw new ArgumentNullException(nameof(predicate));
            return this;
        }

        internal bool Matches(HttpRequestMessage request)
            => (method == null || method == request.Method)
            && (urlPattern == null || (request.RequestUri != null && UrlPattern.IsMatch(urlPattern, request.RequestUri)))
            && (predicate == null || predicate(request));

        internal Task<HttpResponseMessage> CreateAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => factory(request, cancellationToken);
    }
}
