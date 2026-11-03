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

namespace RestClient
{
    using System;
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Headers;

    /// <summary>
    /// Provides custom formatting for generic action result.
    /// </summary>
    public interface IRestResult
    {
        /// <summary>
        /// Gets content
        /// </summary>
        object Content { get; }
    }

    /// <summary>
    /// Provides custom formatting for generic action result with type.
    /// </summary>
    /// <typeparam name="T">The type of the content.</typeparam>
    public interface IRestResult<T>
    {
        /// <summary>
        /// Gets content typed
        /// </summary>
        T Content { get; }
    }

    /// <summary>
    /// Provides a generic actionresult of a post/get/put/delete call request. This is an abstract class.
    /// </summary>
    public abstract class RestResult : IDisposable, IRestResult
    {
        #region [ Constructors ]

        /// <summary>
        /// Initializes a new instance of the RestClient.RestResult class.
        /// </summary>
        /// <param name="response">The HTTP response.</param>
        private protected RestResult(HttpResponseMessage response)
        {
            this.Response = response;
        }

        /// <summary>
        /// Initializes a new instance of the RestClient.RestResult class.
        /// </summary>
        /// <param name="exception">The exception raised during the request.</param>
        private protected RestResult(Exception exception)
        {
            this.Exception = exception;
        }

        #endregion

        #region [ Public Properties ]

        /// <summary>
        /// Gets HTTP response. It is null when the request failed before receiving a response.
        /// </summary>
        public HttpResponseMessage Response { get; }

        /// <summary>
        /// Gets the exception raised during the request or during the deserialization of the response.
        /// When the exception is raised by the deserialization, Response is still available.
        /// </summary>
        public Exception Exception { get; internal set; }

        /// <summary>
        /// Gets HTTP response's headers
        /// </summary>
        public HttpResponseHeaders Headers => Response?.Headers;

        /// <summary>
        /// Gets true if the HTTP response was received with a success status code and no exception was raised
        /// </summary>
        public bool IsSuccessStatusCode => Response != null && Response.IsSuccessStatusCode && Exception == null;

        /// <summary>
        /// Gets the status code of the HTTP response. It is 0 when no response has been received.
        /// </summary>
        public HttpStatusCode StatusCode => Response?.StatusCode ?? default;

        /// <summary>
        /// Gets HTTP response's reason phrase, or the exception message when no response has been received.
        /// </summary>
        public string ReasonPhrase => Response != null ? Response.ReasonPhrase : Exception?.Message;

        /// <summary>
        /// Get true if the request has been cancelled (by CancellationToken or by OnStart)
        /// </summary>
        public bool IsCancellationRequested => Exception is OperationCanceledException;

        /// <summary>
        /// Get true if the request has been aborted because the timeout elapsed
        /// </summary>
        public bool IsTimedOut => Exception is TimeoutException;

        /// <summary>
        /// Gets HTTP response's version. It is null when no response has been received.
        /// </summary>
        public Version Version => Response?.Version;

        /// <summary>
        /// Gets HTTP response's content
        /// </summary>
        public object Content => ContentAsObject;

        /// <summary>
        /// Gets HTTP response's content as object
        /// </summary>
        private protected abstract object ContentAsObject { get; }

        #endregion

        #region [ Methods ]

        /// <summary>
        /// Throws an exception if the request failed or the status code is not a success status code.
        /// </summary>
        /// <returns>The current instance</returns>
        public RestResult EnsureSuccessStatusCode()
        {
            if (Response == null)
            {
                throw new HttpRequestException(Exception?.Message ?? "No response received.", Exception);
            }

            if (!Response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"Response status code does not indicate success: {(int)Response.StatusCode} ({Response.ReasonPhrase}).");
            }

            if (Exception != null)
            {
                throw new HttpRequestException(Exception.Message, Exception);
            }

            return this;
        }

        #endregion

        #region [ IDisposable ]

        /// <summary>
        /// True when the object was disposed.
        /// </summary>
        private bool disposed = false;

        /// <summary>
        /// Releases all resources used by the RestClient.RestResult.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Releases the unmanaged resources used by the RestClient.RestResult and optionally releases the managed resources.
        /// </summary>
        /// <param name="disposing">true to release both managed and unmanaged resources; false to release only unmanaged resources.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!this.disposed)
            {
                if (disposing)
                {
                    (ContentAsObject as IDisposable)?.Dispose();
                    Response?.Dispose();
                }
                disposed = true;
            }
        }

        #endregion
    }

    namespace Generic
    {
        /// <summary>
        /// Provides a specific T type action result of a post/get/put/delete call request.
        /// </summary>
        /// <typeparam name="T">The type of the content.</typeparam>
        public class RestResult<T> : RestResult, IRestResult<T>
        {
            #region [ Constructors ]

            /// <summary>
            /// Initializes a new instance of the RestClient.Generic.RestResult class.
            /// </summary>
            /// <param name="response">The HTTP response.</param>
            internal RestResult(HttpResponseMessage response) : base(response) { }

            /// <summary>
            /// Initializes a new instance of the RestClient.Generic.RestResult class.
            /// </summary>
            /// <param name="exception">The exception raised during the request.</param>
            internal RestResult(Exception exception) : base(exception) { }

            #endregion

            /// <summary>
            /// Gets HTTP response's generic T object content
            /// </summary>
            public new T Content { get; internal set; }

            /// <summary>
            /// Gets HTTP response's content as object
            /// </summary>
            private protected override object ContentAsObject => Content;

            /// <summary>
            /// Throws an exception if the request failed or the status code is not a success status code.
            /// </summary>
            /// <returns>The current instance</returns>
            public new RestResult<T> EnsureSuccessStatusCode()
            {
                base.EnsureSuccessStatusCode();
                return this;
            }
        }
    }
}
