/// <summary>
/// 
/// The MIT License (MIT)
/// 
/// Copyright (c) 2020 Federico Mazzanti
/// 
/// Permission is hereby granted, free of charge, to any person
/// obtaining a copy of this software and associated documentation
/// files (the "Software"), to deal in the Software without
/// restriction, including without limitation the rights to use,
/// copy, modify, merge, publish, distribute, sublicense, and/or sell
/// copies of the Software, and to permit persons to whom the
/// Software is furnished to do so, subject to the following
/// conditions:
/// 
/// The above copyright notice and this permission notice shall be
/// included in all copies or substantial portions of the Software.
/// 
/// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
/// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES
/// OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
/// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT
/// HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY,
/// WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
/// FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR
/// OTHER DEALINGS IN THE SOFTWARE.
/// 
/// </summary>

namespace RestClient.Builder
{
    using RestClient.Generic;
    using RestClient.IO;
    using RestClient.Serialization;
    using RestClient.Serialization.Json;
    using RestClient.Serialization.Xml;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Net.Security;
    using System.Runtime.InteropServices;
    using System.Security.Cryptography.X509Certificates;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Provides a set of methods for building the requests
    /// </summary>
    public class Invoker : HttpClient
    {
        /// <summary>
        /// Occurs when the request starts.
        /// </summary>
        public event ProgressBytesChangedEventHandler ProgressChanged;

        /// <summary>
        /// Initializes a new instance
        /// </summary>
        public Invoker()
            : base()
        {

        }

        /// <summary>
        /// Initializes a new instance of the System.Net.Http.HttpClient class with a specific handler.
        /// </summary>
        /// <param name="handler"></param>
        public Invoker(HttpMessageHandler handler)
            : base(handler)
        {
        }

        /// <summary>
        /// Initializes a new instance of the System.Net.Http.HttpClient class with a specific handler.
        /// </summary>
        /// <param name="handler"></param>
        /// <param name="disposeHandler"></param>
        public Invoker(HttpMessageHandler handler, bool disposeHandler)
            : base(handler, disposeHandler)
        {

        }

        /// <summary>
        /// Send an HTTP request as an asynchronous operation.
        /// </summary>
        /// <param name="request"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        protected async Task<RestResult<string>> SendProgressAsStringAsync(HttpRequestMessage request, HttpContent content, Func<RestResult<string>> refreshToken, CancellationToken cancellationToken = new CancellationToken())
        {
            RestResult<string> result = null;
            request.Content = new ProgressHttpContent(content, 81920, (current, total) => ProgressChanged?.Invoke(this, new ProgressEventArgs
            {
                CurrentBytes = current,
                TotalBytes = total
            }));

            HttpResponseMessage response = await this.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                return refreshToken();
            }

            string contentAsString = await ReadStringAsStreamAsync(response, 81920, cancellationToken);

            return result;
        }


        /// <summary>
        /// Reads content as stream
        /// </summary>
        /// <param name="cancellationToken">Propagates notification that operations should be canceled.</param>
        /// <param name="progress">Proveder for progress update</param>
        /// <returns>content as string</returns>
        private async Task<string> ReadStringAsStreamAsync(HttpResponseMessage response, int bufferSize, CancellationToken cancellationToken = new CancellationToken())
        {
            long totalBytesToReceive = response.Content.Headers.ContentLength != null ? (int)response.Content.Headers.ContentLength : 0;
            long bytesReceived = 0;

            string result = string.Empty;
            using (Stream stream = await response.Content.ReadAsStreamAsync())
            {
                byte[] data = new byte[bufferSize];
                using (MemoryStream ms = new MemoryStream())
                {
                    int numBytesRead;
                    while ((numBytesRead = stream.Read(data, 0, data.Length)) > 0)
                    {
                        bytesReceived += numBytesRead;
                        ms.Write(data, 0, numBytesRead);

                        ProgressChanged?.Invoke(this, new ProgressEventArgs
                        {
                            TotalBytes = totalBytesToReceive,
                            CurrentBytes = bytesReceived
                        });
                        if (cancellationToken.IsCancellationRequested) throw new TaskCanceledException();
                    }
                    result = Encoding.UTF8.GetString(ms.ToArray(), 0, (int)ms.Length);
                }
            }
            return result;
        }

        /// <summary>
        /// Reads content as stream
        /// </summary>
        /// <param name="cancellationToken">Propagates notification that operations should be canceled.</param>
        /// <param name="progress">Proveder for progress update</param>
        /// <returns>content as array bits</returns>
        private async Task<byte[]> ReadBytesAsStreamAsync(HttpResponseMessage response, int bufferSize, CancellationToken cancellationToken = new CancellationToken())
        {
            long totalBytesToReceive = response.Content.Headers.ContentLength != null ? (int)response.Content.Headers.ContentLength : 0;
            long bytesReceived = 0;

            byte[] result = new byte[0];
            using (Stream stream = await response.Content.ReadAsStreamAsync())
            {
                byte[] data = new byte[bufferSize];
                using (MemoryStream ms = new MemoryStream())
                {
                    int numBytesRead;
                    while ((numBytesRead = stream.Read(data, 0, data.Length)) > 0)
                    {
                        bytesReceived += numBytesRead;
                        ms.Write(data, 0, numBytesRead);

                        ProgressChanged?.Invoke(this, new ProgressEventArgs
                        {
                            TotalBytes = totalBytesToReceive,
                            CurrentBytes = bytesReceived
                        });
                        if (cancellationToken.IsCancellationRequested) throw new TaskCanceledException();
                    }
                    result = ms.ToArray();
                }
            }
            return result;
        }

    }
}