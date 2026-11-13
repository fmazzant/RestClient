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
    /// <summary>
    /// Names used by RestClient for tracing and metrics.
    /// </summary>
    public static class RestClientTelemetry
    {
        /// <summary>
        /// Name of the ActivitySource used by RestClient. Add it to OpenTelemetry with
        /// <c>tracing.AddSource(RestClientTelemetry.ActivitySourceName)</c>.
        /// Every request creates an activity "RestClient {METHOD}" that contains the HttpClient activities
        /// of the attempts (retries and refresh token included).
        /// </summary>
        public const string ActivitySourceName = "Mafesoft.RestClient";

        /// <summary>
        /// Name of the Meter used by RestClient (the same as <see cref="ActivitySourceName"/>). Add it to OpenTelemetry with
        /// <c>metrics.AddMeter(RestClientTelemetry.MeterName)</c>.
        /// Instruments: <c>restclient.request.duration</c> (histogram, seconds, one measurement per request, retries and
        /// refresh token included), <c>restclient.retries</c> and <c>restclient.refresh_token</c> (counters).
        /// </summary>
        public const string MeterName = "Mafesoft.RestClient";
    }
}
