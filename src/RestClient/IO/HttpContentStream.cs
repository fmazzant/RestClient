﻿/// <summary>
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
        /// Occurs when the request starts.
        /// </summary>
        public event ProgressBytesChangedEventHandler ProgressChanged;
        #endregion

        #region [ Public Properties ]
        /// <summary>
        /// Gets HTTP content
        /// </summary>
        public HttpContent Content { get; private set; }
        #endregion

        #region [ Constructors ]
        /// <summary>
        /// Initializes a new instance of the VarGroup.Mobile.Core.IO.HttpContentStream class.
        /// </summary>
        /// <param name="httpContent">HTTP content</param>
        public HttpContentStream(HttpContent httpContent)
            : base()
        {
            this.Content = httpContent;
        }
        #endregion

        #region [ Read ]
        /// <summary>
        /// Reads content as stream
        /// </summary>
        /// <param name="cancellationToken">Propagates notification that operations should be canceled.</param>
        /// <param name="progress">Proveder for progress update</param>
        /// <returns>content as string</returns>
        public async Task<string> ReadStringAsStreamAsync(CancellationToken cancellationToken = new CancellationToken())
        {
            long totalBytesToReceive = this.Content.Headers.ContentLength != null ? (int)this.Content.Headers.ContentLength : 0;
            long bytesReceived = 0;

            string result = string.Empty;
            using (Stream stream = await this.Content.ReadAsStreamAsync())
            {
                byte[] data = new byte[1024];
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
        public async Task<byte[]> ReadBytesAsStreamAsync(CancellationToken cancellationToken = new CancellationToken())
        {
            long totalBytesToReceive = this.Content.Headers.ContentLength != null ? (int)this.Content.Headers.ContentLength : 0;
            long bytesReceived = 0;

            byte[] result = new byte[0];
            using (Stream stream = await this.Content.ReadAsStreamAsync())
            {
                byte[] data = new byte[1024];
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
        public async Task<HttpResponseMessage> WriteStringAsStreamAsync(HttpClient client, HttpRequestMessage request, CancellationToken cancellationToken)
        {
            HttpResponseMessage response = null;
            long totalBytesToSend = this.Content != null ? this.Content.Headers.ContentLength.Value : 1;

            string payload = this.Content != null ? await this.Content.ReadAsStringAsync() : "";

            try
            {
                if (this.Content != null)
                {
                    var streamContent = new ProgressableStreamContent(
                       request.Content,
                       4096,
                       (sent, total) =>
                       {
                           ProgressChanged?.Invoke(this, new ProgressEventArgs
                           {
                               TotalBytes = total,
                               CurrentBytes = sent
                           });
                       });
                    request.Content = streamContent;
                }
                response = client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).Result;
            }
            catch (Exception)
            {
                try
                {
                    response = client.SendAsync(request, cancellationToken).Result;
                    ProgressChanged?.Invoke(this, new ProgressEventArgs
                    {
                        TotalBytes = 1,
                        CurrentBytes = 1
                    });
                }
                catch (Exception)
                {
                    response = null;
                }
            }
            return response;
        }

        #endregion

        #region [ IDisposable ]
        /// <summary>
        /// True when the object was disposed.
        /// </summary>
        private bool disposed = false;

        /// <summary>
        /// Releases all resources used by the VarGroup.Mobile.Core.IO.HttpContentStream.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        /// <summary>
        /// Releases the unmanaged resources used by the VarGroup.Mobile.Core.IO.HttpContentStream and optionally releases the managed resources.
        /// </summary>
        /// <param name="disposing">true to release both managed and unmanaged resources; false to release only unmanaged resources.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!this.disposed)
            {
                if (disposing)
                {
                    if (Content != null) Content.Dispose();
                }
                disposed = true;
            }
        }
        #endregion
    }
}