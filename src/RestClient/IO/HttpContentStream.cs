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
    using RestClient;
    using System;
    using System.IO;
    using System.Net.Http;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Provides a specific stream from HttpContent instance.
    /// </summary>
    public class HttpContentStream : IDisposable
    {
        #region [ Public Events ]
        /// <summary>
        /// Occurs when the uploading starts.
        /// </summary>
        public event ProgressBytesChangedEventHandler UploadingProgressChanged;

        /// <summary>
        /// Occurs when the download starts.
        /// </summary>
        public event ProgressBytesChangedEventHandler DownloadingProgressChanged;
        #endregion

        #region [ Private ]

        /// <summary>
        /// Lets keep buffer of 20kb
        /// </summary>
        private const int DefaultBufferSize = 5 * 4096 * 4; //80kb

        /// <summary>
        /// Buffer size
        /// </summary>
        private int BufferSize { get; set; }

        #endregion

        #region [ Constructors ]
        /// <summary>
        /// Initializes a new instance of the RestClient.IO.HttpContentStream class.
        /// </summary>
        /// <param name="httpContent">HTTP content</param>
        public HttpContentStream()
            : this(DefaultBufferSize)
        {

        }

        /// <summary>
        /// Initializes a new instance of the RestClient.IO.HttpContentStream class.
        /// </summary>
        /// <param name="httpContent"></param>
        /// <param name="bufferSize"></param>
        public HttpContentStream(int bufferSize)
            : base()
        {
            //this.Content = httpContent;
            this.BufferSize = bufferSize;
        }
        #endregion

        #region [ Read ]
        /// <summary>
        /// Reads content as stream
        /// </summary>
        /// <param name="cancellationToken">Propagates notification that operations should be canceled.</param>
        /// <param name="progress">Proveder for progress update</param>
        /// <returns>content as string</returns>
        public async Task<string> ReadStringAsStreamAsync(HttpResponseMessage response, CancellationToken cancellationToken = new CancellationToken())
        {
            long totalBytesToReceive = response.Content.Headers.ContentLength != null ? (int)response.Content.Headers.ContentLength : 0;
            long bytesReceived = 0;

            string result = string.Empty;
            using (Stream stream = await response.Content.ReadAsStreamAsync())
            {
                byte[] data = new byte[BufferSize];
                using (MemoryStream ms = new MemoryStream())
                {
                    int numBytesRead;
                    while ((numBytesRead = stream.Read(data, 0, data.Length)) > 0)
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
        public async Task<byte[]> ReadBytesAsStreamAsync(HttpResponseMessage response, CancellationToken cancellationToken = new CancellationToken())
        {
            long totalBytesToReceive = response.Content.Headers.ContentLength != null ? (int)response.Content.Headers.ContentLength : 0;
            long bytesReceived = 0;

            byte[] result = new byte[0];
            using (Stream stream = await response.Content.ReadAsStreamAsync())
            {
                byte[] data = new byte[BufferSize];
                using (MemoryStream ms = new MemoryStream())
                {
                    int numBytesRead;
                    while ((numBytesRead = stream.Read(data, 0, data.Length)) > 0)
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
                    result = ms.ToArray();
                }
            }
            return result;
        }
        #endregion

        #region [ Write ]
        /// <summary>
        /// Write content
        /// </summary>
        /// <param name="client">Proveder to send a request</param>
        /// <param name="request">Represents a HTTP request message.</param>
        /// <param name="cancellationToken">Propagates notification that operations should be canceled.</param>
        /// <param name="progress">Proveder for progress update</param>
        /// <returns>HttpResponseMessage</returns>
        [Obsolete("Use: WriteStringAsStreamAsync(HttpRequestMessage request, CancellationToken cancellationToken)", true)]
        public async Task<HttpResponseMessage> WriteStringAsStreamAsync(HttpClient client, HttpRequestMessage request, CancellationToken cancellationToken)
        {
            HttpResponseMessage response = null;
            long totalBytesToSend = request.Content != null ? request.Content.Headers.ContentLength.Value : 1;

            string payload = request.Content != null ? await request.Content.ReadAsStringAsync() : "";

            try
            {
                if (request.Content != null)
                {
                    var streamContent = new ProgressHttpContent(
                       request.Content,
                       BufferSize,
                       (sent, total) =>
                       {
                           UploadingProgressChanged?.Invoke(this, new ProgressEventArgs
                           {
                               TotalBytes = total,
                               CurrentBytes = sent
                           });
                           if (cancellationToken.IsCancellationRequested)
                           {
                               throw new TaskCanceledException();
                           }
                       });
                    request.Content = streamContent;
                }
            }
            catch (TaskCanceledException)
            {
                throw new TaskCanceledException();
            }
            catch (Exception)
            {
                UploadingProgressChanged?.Invoke(this, new ProgressEventArgs
                {
                    TotalBytes = 1,
                    CurrentBytes = 1
                });
            }
            return response;
        }

        /// <summary>
        /// Write content
        /// </summary>
        /// <param name="request">Represents a HTTP request message.</param>
        /// <param name="cancellationToken">Propagates notification that operations should be canceled.</param>
        /// <returns></returns>
        [Obsolete("Use: RestClient.Builder.Invoker().SendWithProgressAsync()", true)]
        public void WriteStringAsStreamAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            long totalBytesToSend = request.Content != null ? request.Content.Headers.ContentLength.Value : 1;

            if (request.Content != null && request.Content.GetType() != typeof(ProgressHttpContent))
            {
                var streamContent = new ProgressHttpContent(
                   request.Content,
                   BufferSize,
                   (sent, total) =>
                   {
                       UploadingProgressChanged?.Invoke(this, new ProgressEventArgs
                       {
                           TotalBytes = total,
                           CurrentBytes = sent
                       });
                       if (cancellationToken.IsCancellationRequested)
                       {
                           throw new TaskCanceledException();
                       }
                   });
                request.Content = streamContent;
            }
        }

        #endregion

        #region [ IDisposable ]
        /// <summary>
        /// True when the object was disposed.
        /// </summary>
        private bool disposed = false;

        /// <summary>
        /// Releases all resources used by the object.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        /// <summary>
        /// Releases the unmanaged resources used by the objet and optionally releases the managed resources.
        /// </summary>
        /// <param name="disposing">true to release both managed and unmanaged resources; false to release only unmanaged resources.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!this.disposed)
            {
                if (disposing)
                {
                    //if (Content != null) Content.Dispose();
                    if (UploadingProgressChanged != null)
                    {
                        foreach (Delegate d in UploadingProgressChanged.GetInvocationList())
                        {
                            UploadingProgressChanged -= (ProgressBytesChangedEventHandler)d;
                        }
                    }

                    if (DownloadingProgressChanged != null)
                    {
                        foreach (Delegate d in DownloadingProgressChanged.GetInvocationList())
                        {
                            DownloadingProgressChanged -= (ProgressBytesChangedEventHandler)d;
                        }
                    }
                }
                disposed = true;
            }
        }
        #endregion
    }
}
