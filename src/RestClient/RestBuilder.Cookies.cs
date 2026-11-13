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
    using System.Net;

    /// <summary>
    /// The cookies of the requests
    /// </summary>
    public sealed partial class RestBuilder
    {
        /// <summary>
        /// Stores the cookies of the responses (Set-Cookie) in the container and sends them back with the requests (Cookie),
        /// for example for an API with a cookie-based session: Cookies(new CookieContainer()).
        /// The container belongs to the builder, not to the pooled handler: the builders derived from this one share
        /// the same container (it is the session), builders with different containers never see each other's cookies,
        /// and without Cookies no cookie is stored. The cookies set by the redirects are stored too: RestClient follows
        /// the redirects itself when a container is set. CookieContainer is thread-safe.
        /// It is not applied to an external HttpClient (see UseHttpClient): configure CookieContainer on your handler.
        /// </summary>
        /// <param name="container">The cookie container, null to stop storing and sending cookies</param>
        /// <returns></returns>
        public RestBuilder Cookies(CookieContainer? container)
            => With(r => r.cookies = container);
    }
}
