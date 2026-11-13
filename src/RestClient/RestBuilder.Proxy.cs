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
    /// The proxy of the requests
    /// </summary>
    public sealed partial class RestBuilder
    {
        /// <summary>
        /// Sends the requests through the proxy, for example
        /// Proxy(new WebProxy("http://proxy:8080") { Credentials = CredentialCache.DefaultCredentials }).
        /// Null disables the proxy: the requests go directly to the server, ignoring the system proxy settings.
        /// The proxy is part of the pooled handler configuration: builders that use the same IWebProxy instance
        /// share the handler and its connections, a different instance gets its own handler, so define the proxy once.
        /// It is not applied to an external HttpClient (see UseHttpClient): configure it on your handler.
        /// </summary>
        /// <param name="proxy">The proxy, null to send the requests without a proxy</param>
        /// <returns></returns>
        public RestBuilder Proxy(IWebProxy? proxy)
            => With(r =>
            {
                r.useProxy = proxy != null;
                r.proxy = proxy;
            });

        /// <summary>
        /// Uses the system proxy settings (HttpClient.DefaultProxy on .NET): this is the default.
        /// It undoes a previous call to Proxy. It is not applied to an external HttpClient (see UseHttpClient).
        /// </summary>
        /// <returns></returns>
        public RestBuilder SystemProxy()
            => With(r =>
            {
                r.useProxy = true;
                r.proxy = null;
            });
    }
}
