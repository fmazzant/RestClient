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
    /// The timeout and the buffer size of the requests
    /// </summary>
    public sealed partial class RestBuilder
    {
        /// <summary>
        /// Sets the timespan to wait before an attempt times out (System.Threading.Timeout.InfiniteTimeSpan to disable it).
        /// For Stream results the timeout covers the response headers, not the reading of the stream.
        /// </summary>
        /// <param name="timeOut"></param>
        /// <returns></returns>
        public RestBuilder Timeout(TimeSpan timeOut)
            => With(r => r.properties.Timeout = timeOut);

        /// <summary>
        /// Sets the timespan to wait before an attempt times out
        /// </summary>
        /// <param name="milliseconds"></param>
        /// <returns></returns>
        public RestBuilder Timeout(double milliseconds)
            => Timeout(TimeSpan.FromMilliseconds(milliseconds));

        /// <summary>
        /// Sets the buffer size used to upload and download.
        /// </summary>
        /// <param name="bufferSize"></param>
        /// <returns></returns>
        public RestBuilder BufferSize(int bufferSize)
        {
            if (bufferSize <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(bufferSize));
            }

            return With(r => r.properties.BufferSize = bufferSize);
        }

        /// <summary>
        /// Specifies how the client certificates are provided (default Manual).
        /// It is not applied when an external HttpClient is used (see UseHttpClient).
        /// </summary>
        /// <param name="certificateOption"></param>
        /// <returns></returns>
        public RestBuilder CertificateOption(ClientCertificateOption certificateOption)
            => With(r => r.properties.CertificateOption = certificateOption);
    }
}
