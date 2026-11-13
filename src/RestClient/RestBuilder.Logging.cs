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
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// The logging of the requests
    /// </summary>
    public sealed partial class RestBuilder
    {
        /// <summary>
        /// Logs the requests (Debug), the responses (Information), the retries and the errors.
        /// The query string is never logged because it can contain secrets.
        /// </summary>
        /// <param name="logger">The logger, null to disable logging</param>
        /// <returns></returns>
        public RestBuilder Logger(ILogger? logger)
            => With(r => r.logger = logger);

        /// <summary>
        /// Sets the logical name of the client, used as the <c>restclient.client.name</c> tag on the metrics
        /// (see <see cref="RestClientTelemetry.MeterName"/>). It is set automatically by <c>IRestClientFactory</c> /
        /// <c>AddRestClient</c> (the name of the client); set it yourself when you build the client without the factory.
        /// </summary>
        /// <param name="name">The name of the client, null to remove it (no tag)</param>
        /// <returns></returns>
        public RestBuilder ClientName(string? name)
            => With(r => r.clientName = name);
    }
}
