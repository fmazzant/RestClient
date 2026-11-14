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
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// The HttpMessageHandler of the fake server: records the request and answers with the configured response
    /// </summary>
    internal sealed class RestTestHandler : HttpMessageHandler
    {
        private readonly RestTest test;

        public RestTestHandler(RestTest test)
        {
            this.test = test;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            string? body = null;
            string? contentType = null;
            if (request.Content != null)
            {
                body = await request.Content.ReadAsStringAsync().ConfigureAwait(false);
                contentType = request.Content.Headers.ContentType?.MediaType;
            }

            var call = new RestTestCall(request, body, contentType);
            RestTestResponse? setup = test.Receive(call);

            cancellationToken.ThrowIfCancellationRequested();
            HttpResponseMessage response = setup == null
                ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(string.Empty, Encoding.UTF8, "text/plain") }
                : await setup.CreateAsync(request, cancellationToken).ConfigureAwait(false)
                    ?? throw new InvalidOperationException("The RestTest responder returned null.");

            response.RequestMessage ??= request;
            call.StatusCode = response.StatusCode;
            return response;
        }

        protected override void Dispose(bool disposing)
        {
            // shared by the HttpClient instances created for every request: never disposed
        }
    }

    /// <summary>
    /// Wildcard patterns: <c>*</c> matches any text, the match is case-insensitive and covers the whole text
    /// </summary>
    internal static class UrlPattern
    {
        public static bool IsMatch(string pattern, string text)
            => Regex.IsMatch(text, "^" + Regex.Escape(pattern).Replace("\\*", ".*") + "$", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant);

        /// <summary>
        /// Matches the escaped (AbsoluteUri) or the unescaped form of the URL
        /// </summary>
        public static bool IsMatch(string pattern, Uri url)
            => IsMatch(pattern, url.AbsoluteUri) || IsMatch(pattern, Uri.UnescapeDataString(url.AbsoluteUri));
    }
}
