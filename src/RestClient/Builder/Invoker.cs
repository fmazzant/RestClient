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
    using RestClient.IO;
    using System;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Provides a set of methods for building the requests
    /// </summary>
    public class Invoker : HttpClient
    {
        /// <summary>
        /// Lets keep buffer of 20kb
        /// </summary>
        private const int DefaultBufferSize = 5 * 4096 * 4; //80kb

        /// <summary>
        /// Buffer size
        /// </summary>
        private int BufferSize { get; set; } = DefaultBufferSize;

        /// <summary>
        /// Occurs when the request starts.
        /// </summary>
        public event ProgressBytesChangedEventHandler ProgressChanged;

        /// <summary>
        /// Initializes a new instance
        /// </summary>
        public Invoker(int bufferSize = DefaultBufferSize)
            : base()
        {
            BufferSize = bufferSize;
        }

        /// <summary>
        /// Initializes a new instance of the System.Net.Http.HttpClient class with a specific handler.
        /// </summary>
        /// <param name="handler"></param>
        public Invoker(HttpMessageHandler handler, int bufferSize = DefaultBufferSize)
            : base(handler)
        {
            BufferSize = bufferSize;
        }

        /// <summary>
        /// Initializes a new instance of the System.Net.Http.HttpClient class with a specific handler.
        /// </summary>
        /// <param name="handler"></param>
        /// <param name="disposeHandler"></param>
        public Invoker(HttpMessageHandler handler, bool disposeHandler, int bufferSize = DefaultBufferSize)
            : base(handler, disposeHandler)
        {
            BufferSize = bufferSize;
        }

        /// <summary>
        /// Send an HTTP request as an asynchronous operation.
        /// </summary>
        /// <param name="request"></param>
        /// <param name="content"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task<HttpResponseMessage> SendWithProgressAsync(HttpRequestMessage request, HttpContent content, ProgressBytesChangedEventHandler handler = null, CancellationToken cancellationToken = new CancellationToken())
        {
            HttpContent httpContent = content ?? request.Content;
            if (handler != null)
            {
                ProgressChanged += handler;
            }

            if (httpContent != null && httpContent.GetType() != typeof(ProgressHttpContent))
            {
                request.Content = new ProgressHttpContent(httpContent, BufferSize, (current, total) => ProgressChanged?.Invoke(this, new ProgressEventArgs
                {
                    CurrentBytes = current,
                    TotalBytes = total
                }));
            }
            return await this.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        }

        /// <summary>
        /// Send an HTTP request as an asynchronous operation.
        /// </summary>
        /// <param name="request"></param>
        /// <param name="content"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task<HttpResponseMessage> SendWithProgressAsync(HttpRequestMessage request, HttpContent content, CancellationToken cancellationToken = new CancellationToken())
            => await SendWithProgressAsync(request, content, null, cancellationToken);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="request"></param>
        /// <param name="handler"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task<HttpResponseMessage> SendWithProgressAsync(HttpRequestMessage request, ProgressBytesChangedEventHandler handler = null, CancellationToken cancellationToken = new CancellationToken())
           => await SendWithProgressAsync(request, null, handler, cancellationToken);

        /// <summary>
        /// Send an HTTP request as an asynchronous operation.
        /// </summary>
        /// <param name="request"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task<HttpResponseMessage> SendWithProgressAsync(HttpRequestMessage request, CancellationToken cancellationToken = new CancellationToken())
            => await SendWithProgressAsync(request, null, null, cancellationToken);

        /// <summary>
        /// Releases the unmanaged resources used by the Invoker and optionally disposes of the managed resources.
        /// </summary>
        /// <param name="disposing"></param>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (ProgressChanged != null)
                {
                    foreach (Delegate d in ProgressChanged.GetInvocationList())
                    {
                        ProgressChanged -= (ProgressBytesChangedEventHandler)d;
                    }
                }
            }
            base.Dispose(disposing);
        }
    }
}