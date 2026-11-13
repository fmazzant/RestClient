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
    using System;
    using System.Net.Http;

    /// <summary>
    /// The HTTP version of the requests (HTTP/1.1, HTTP/2, HTTP/3)
    /// </summary>
    public sealed partial class RestBuilder
    {
        /// <summary>
        /// Sets the HTTP version of the requests (for example System.Net.HttpVersion.Version20).
        /// The version is set on every request, so it is applied also to an external HttpClient (see UseHttpClient).
        /// On .NET the default version policy (RequestVersionOrLower) is used: over https the version is negotiated
        /// with the server (ALPN), over http HTTP/1.1 is used. RestResult.Version reports the negotiated version.
        /// On .NET Framework only HTTP/1.0 and HTTP/1.1 are supported.
        /// </summary>
        /// <param name="version">The HTTP version, null to go back to the default (HTTP/1.1)</param>
        /// <returns></returns>
        public RestBuilder HttpVersion(Version? version)
            => With(r =>
            {
                r.httpVersion = version;
#if NET
                r.httpVersionPolicy = null;
#endif
            });

#if NET
        /// <summary>
        /// Sets the HTTP version of the requests and how it is negotiated with the server,
        /// for example HttpVersion(System.Net.HttpVersion.Version30, HttpVersionPolicy.RequestVersionOrLower).
        /// The version is set on every request, so it is applied also to an external HttpClient (see UseHttpClient).
        /// RequestVersionExact or RequestVersionOrHigher with HTTP/2 over http uses HTTP/2 without TLS (prior knowledge, h2c).
        /// RestResult.Version reports the negotiated version. Available on .NET 8+ only.
        /// </summary>
        /// <param name="version">The HTTP version</param>
        /// <param name="policy">The version policy</param>
        /// <returns></returns>
        public RestBuilder HttpVersion(Version version, HttpVersionPolicy policy)
        {
            if (version == null) { throw new ArgumentNullException(nameof(version)); }
            return With(r =>
            {
                r.httpVersion = version;
                r.httpVersionPolicy = policy;
            });
        }
#endif
    }
}
