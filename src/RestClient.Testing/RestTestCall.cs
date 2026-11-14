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
    using System.Net;
    using System.Net.Http;

    /// <summary>
    /// A request received by the fake server (RestTest)
    /// </summary>
    public sealed class RestTestCall
    {
        internal RestTestCall(HttpRequestMessage request, string? requestBody, string? contentType)
        {
            Request = request;
            Method = request.Method;
            Url = request.RequestUri!;
            RequestBody = requestBody;
            ContentType = contentType;
        }

        /// <summary>
        /// The request. Its headers can be read; its content has been disposed by the client: use RequestBody.
        /// </summary>
        public HttpRequestMessage Request { get; }

        /// <summary>
        /// The HTTP method
        /// </summary>
        public HttpMethod Method { get; }

        /// <summary>
        /// The absolute URL, with the query string
        /// </summary>
        public Uri Url { get; }

        /// <summary>
        /// The body of the request as a string, null when the request has no content
        /// </summary>
        public string? RequestBody { get; }

        /// <summary>
        /// The media type of the request content (e.g. application/json), null when the request has no content
        /// </summary>
        public string? ContentType { get; }

        /// <summary>
        /// The status code of the response, null when the response was a simulated exception or timeout
        /// </summary>
        public HttpStatusCode? StatusCode { get; internal set; }

        /// <summary>
        /// The method and the URL
        /// </summary>
        public override string ToString() => $"{Method} {Url}";
    }
}
