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

namespace RestClient.IO
{
    using System;
    using System.IO;
    using System.Net;
    using System.Net.Http;
    using System.Threading.Tasks;

    /// <summary>
    /// A  class representing an HTTP entity body and content headers.
    /// </summary>
    internal class HttpContentStreamProgressable : HttpContent
    {
        /// <summary>
        /// Lets keep buffer of 20kb
        /// </summary>
        private const int defaultBufferSize = 5 * 4096; //20kb

        /// <summary>
        /// Http entity body and content headers
        /// </summary>
        private HttpContent content;

        /// <summary>
        /// Buffer size
        /// </summary>
        private int bufferSize;

        /// <summary>
        /// Progress value
        /// </summary>
        private Action<long, long> progress;

        /// <summary>
        /// Initializes a new instance of the VarGroup.Mobile.Core.Net.ProgressableStreamContent class.
        /// </summary>
        /// <param name="content">Http entity body and content headers</param>
        /// <param name="progress">Progress value</param>
        public HttpContentStreamProgressable(HttpContent content, Action<long, long> progress)
            : this(content, defaultBufferSize, progress)
        {
        }

        /// <summary>
        /// Initializes a new instance of the VarGroup.Mobile.Core.Net.ProgressableStreamContent class.
        /// </summary>
        /// <param name="content">Http entity body and content headers</param>
        /// <param name="bufferSize">Buffer size</param>
        /// <param name="progress">Progress value</param>
        public HttpContentStreamProgressable(HttpContent content, int bufferSize, Action<long, long> progress)
        {
            if (content == null)
            {
                throw new ArgumentNullException("content");
            }
            if (bufferSize <= 0)
            {
                throw new ArgumentOutOfRangeException("bufferSize");
            }

            this.content = content;
            this.bufferSize = bufferSize;
            this.progress = progress;

            foreach (var h in content.Headers)
            {
                this.Headers.Add(h.Key, h.Value);
            }
        }

        /// <summary>
        /// Serialize the HTTP content to a stream as an asynchronous operation.
        /// </summary>
        /// <param name="stream">The target stream.</param>
        /// <param name="context">Information about the transport (channel binding token, for example). This parameter may be null.</param>
        /// <returns>Returns System.Threading.Tasks.Task.The task object representing the asynchronous operation</returns>
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext context)
        {
            return Task.Run(async () =>
            {
                var buffer = new Byte[this.bufferSize];
                long size;
                TryComputeLength(out size);
                var uploaded = 0;


                using (var sinput = await content.ReadAsStreamAsync())
                {
                    while (true)
                    {
                        var length = sinput.Read(buffer, 0, buffer.Length);
                        if (length <= 0) break;

                        uploaded += length;
                        progress?.Invoke(uploaded, size);

                        stream.Write(buffer, 0, length);
                        stream.Flush();
                    }
                }
                stream.Flush();
            });
        }

        /// <summary>
        /// Serialize the HTTP content to a memory stream as an asynchronous operation.
        /// </summary>
        /// <returns></returns>
        protected override Task<Stream> CreateContentReadStreamAsync()
        {
            return base.CreateContentReadStreamAsync();
        }

        /// <summary>
        ///  Determines whether the HTTP content has a valid length in bytes.
        /// </summary>
        /// <param name="length">The length in bytes of the HHTP content.</param>
        /// <returns>Returns System.Boolean.true if length is a valid length; otherwise, false.</returns>
        protected override bool TryComputeLength(out long length)
        {
            length = content.Headers.ContentLength.GetValueOrDefault();
            return true;
        }

        /// <summary>
        /// Releases the unmanaged resources used by the System.Net.Http.HttpContent and
        /// optionally disposes of the managed resources.
        /// </summary>
        /// <param name="disposing">true to release both managed and unmanaged resources; false to releases only unmanaged resources.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                content.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
