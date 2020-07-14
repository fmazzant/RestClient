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

namespace RestClient.Http
{
    using System.IO;
    using System.IO.Compression;
    using System.Linq;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// The message compression handler by HttpClient 
    /// </summary>
    public class HttpClientCompressionHandler : HttpClientHandler
    {
        /// <summary>
        /// Enabled GZip Compression
        /// </summary>
        public bool EnabledGZipCompression { get; internal set; }

        /// <summary>
        /// Occurs when the uploading starts.
        /// </summary>
        public ProgressBytesChangedEventHandler UploadingProgressChanged { get; internal set; }

        /// <summary>
        /// Occurs when the download starts.
        /// </summary>
        public ProgressBytesChangedEventHandler DownloadingProgressChanged { get; internal set; }

        /// <summary>
        /// Buffer size
        /// </summary>
        public int BufferSize { get; set; } = 4096 * 5;

        /// <summary>
        /// Creates an instance of System.Net.Http.HttpResponseMessage based on the information
        /// provided in the System.Net.Http.HttpRequestMessage as an operation that will
        /// not block.
        /// </summary>
        /// <param name="request"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            HttpResponseMessage response = null;
            if (request != null)
            {
                if (request.Content != null)
                {
                    request.Content = new ProgressHttpContent(
                        request.Content,
                        (current, total) => UploadingProgressChanged?.Invoke(this, new ProgressEventArgs
                        {
                            CurrentBytes = current,
                            TotalBytes = total
                        }));
                }

                response = await base.SendAsync(request, cancellationToken);

                if (response.Content != null)
                {
                    long totalBytesToReceive = response.Content.Headers.ContentLength ?? 0;
                    bool isGZipContentEncoding = response.Content.Headers.ContentEncoding.Any(x => x == "gzip");

                    using (Stream stream = isGZipContentEncoding
                        ? new GZipStream(await response.Content.ReadAsStreamAsync(), CompressionMode.Decompress)
                        : await response.Content.ReadAsStreamAsync())
                    {
                        long bytesReceived = 0;
                        byte[] data = new byte[BufferSize];
                        using (MemoryStream ms = new MemoryStream())
                        {
                            int numBytesRead;
                            while ((numBytesRead = await stream.ReadAsync(data, 0, data.Length)) > 0)
                            {
                                bytesReceived += numBytesRead;
                                ms.Write(data, 0, numBytesRead);

                                DownloadingProgressChanged?.Invoke(this, new ProgressEventArgs
                                {
                                    TotalBytes = totalBytesToReceive,
                                    CurrentBytes = bytesReceived
                                });

                                if (cancellationToken.IsCancellationRequested)
                                {
                                    throw new TaskCanceledException();
                                }
                            }
                            response.Content = new ByteArrayContent(ms.ToArray());
                        }
                    }
                }
            }
            return response;
        }

        /// <summary>
        /// Releases the unmanaged resources used by the System.Net.Http.HttpClientHandler and optionally disposes of the managed resources.
        /// </summary>
        /// <param name="disposing">
        /// true to release both managed and unmanaged resources; false to releases only unmanaged resources.
        /// </param>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                UploadingProgressChanged = null;
                DownloadingProgressChanged = null;
            }
            base.Dispose(disposing);
        }
    }
}
