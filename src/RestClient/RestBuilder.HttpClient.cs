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
    /// The HttpClient used to send the requests
    /// </summary>
    public sealed partial class RestBuilder
    {
        /// <summary>
        /// Configure the HttpClient created by RestClient before sending a request.
        /// It is not invoked when an external HttpClient is used (see UseHttpClient).
        /// </summary>
        /// <param name="onHttpClient"></param>
        /// <returns></returns>
        public RestBuilder OnConfigureHttpClient(Action<HttpClient>? onHttpClient)
            => With(r => r.onConfigureHttpClientAction = onHttpClient);

        /// <summary>
        /// Sends the requests using an existing HttpClient (for example created by IHttpClientFactory).
        /// The HttpClient is never disposed by RestClient. Credentials, CertificateValidation, CertificateOption, Proxy, Cookies,
        /// compression and OnConfigureHttpClient are not applied: configure them on your HttpClient/handler.
        /// </summary>
        /// <param name="httpClient">The HttpClient, null to go back to the HttpClient managed by RestClient.</param>
        /// <returns></returns>
        public RestBuilder UseHttpClient(HttpClient? httpClient)
            => With(r => r.externalHttpClient = httpClient);
    }
}
