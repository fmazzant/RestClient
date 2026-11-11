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

namespace Mafesoft.RestClient.Http
{
    using System;
    using System.IO;
    using System.Net;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// An HTTP content that wraps another content and reports the upload progress.
    /// </summary>
    internal sealed class ProgressHttpContent : HttpContent
    {
        /// <summary>
        /// Http entity body and content headers
        /// </summary>
        private readonly HttpContent content;

        /// <summary>
        /// Buffer size
        /// </summary>
        private readonly int bufferSize;

        /// <summary>
        /// Progress callback (bytes uploaded, total bytes)
        /// </summary>
        private readonly Action<long, long> progress;

        /// <summary>
        /// Token used to cancel the upload
        /// </summary>
        private readonly CancellationToken cancellationToken;

        /// <summary>
        /// Initializes a new instance of the Mafesoft.RestClient.Http.ProgressHttpContent class.
        /// </summary>
        /// <param name="content">Http entity body and content headers</param>
        /// <param name="bufferSize">Buffer size</param>
        /// <param name="progress">Progress callback</param>
        /// <param name="cancellationToken">Token used to cancel the upload</param>
        public ProgressHttpContent(HttpContent content, int bufferSize, Action<long, long> progress, CancellationToken cancellationToken = default)
        {
            this.content = content ?? throw new ArgumentNullException(nameof(content));

            if (bufferSize <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(bufferSize), "the bufferSize cannot be <= 0");
            }

            this.bufferSize = bufferSize;
            this.progress = progress;
            this.cancellationToken = cancellationToken;

            foreach (var h in content.Headers)
            {
                this.Headers.TryAddWithoutValidation(h.Key, h.Value);
            }
        }

        /// <summary>
        /// Serialize the HTTP content to a stream as an asynchronous operation.
        /// </summary>
        /// <param name="stream">The target stream.</param>
        /// <param name="context">Information about the transport. This parameter may be null.</param>
        /// <returns>The task object representing the asynchronous operation</returns>
        protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context)
        {
            long total = content.Headers.ContentLength ?? 0;
            long uploaded = 0;
            byte[] buffer = new byte[bufferSize];

            using (Stream source = await content.ReadAsStreamAsync().ConfigureAwait(false))
            {
                int length;
                while ((length = await source.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false)) > 0)
                {
                    await stream.WriteAsync(buffer, 0, length, cancellationToken).ConfigureAwait(false);
                    uploaded += length;
                    progress?.Invoke(uploaded, total);
                }
            }

            await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        ///  Determines whether the HTTP content has a valid length in bytes.
        /// </summary>
        /// <param name="length">The length in bytes of the HTTP content.</param>
        /// <returns>true if length is a valid length; otherwise, false.</returns>
        protected override bool TryComputeLength(out long length)
        {
            long? contentLength = content.Headers.ContentLength;
            length = contentLength ?? -1;
            return contentLength.HasValue;
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
