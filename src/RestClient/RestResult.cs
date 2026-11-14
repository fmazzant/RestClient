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
    using Mafesoft.RestClient.Serialization;
    using System;
    using System.IO;
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Threading;

    /// <summary>
    /// Provides custom formatting for generic action result.
    /// </summary>
    public interface IRestResult
    {
        /// <summary>
        /// Gets content
        /// </summary>
        object? Content { get; }
    }

    /// <summary>
    /// Provides custom formatting for generic action result with type.
    /// </summary>
    /// <typeparam name="T">The type of the content.</typeparam>
    public interface IRestResult<out T>
    {
        /// <summary>
        /// Gets content typed
        /// </summary>
        T? Content { get; }
    }

    /// <summary>
    /// The result of a request. Requests never throw: network errors, timeouts and cancellations are reported by the result.
    /// Dispose the result to release the HTTP response (mandatory for Stream results, which keep the connection open).
    /// </summary>
    public abstract class RestResult : IDisposable, IRestResult
    {
        #region [ Constructors ]

        private protected RestResult(HttpResponseMessage response)
        {
            this.Response = response;
        }

        private protected RestResult(Exception exception)
        {
            this.Exception = exception;
        }

        #endregion

        #region [ Public Properties ]

        /// <summary>
        /// Gets HTTP response. It is null when the request failed before receiving a response.
        /// </summary>
        public HttpResponseMessage? Response { get; }

        /// <summary>
        /// Gets the exception raised during the request or during the deserialization of the response.
        /// When the exception is raised by the deserialization, Response is still available.
        /// </summary>
        public Exception? Exception { get; internal set; }

        /// <summary>
        /// Gets HTTP response's headers
        /// </summary>
        public HttpResponseHeaders? Headers => Response?.Headers;

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
        public string? ReasonPhrase => Response != null ? Response.ReasonPhrase : Exception?.Message;

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
        public Version? Version => Response?.Version;

        /// <summary>
        /// Gets the number of retries executed by the retry policy (0 when the first attempt succeeded).
        /// </summary>
        public int RetryCount { get; internal set; }

        /// <summary>
        /// Gets HTTP response's content
        /// </summary>
        public object? Content => ContentAsObject;

        /// <summary>
        /// Gets HTTP response's content as object
        /// </summary>
        private protected abstract object? ContentAsObject { get; }

        /// <summary>
        /// Resources released with the result (the HttpClient of a streamed response)
        /// </summary>
        internal IDisposable? Owner { get; set; }

        /// <summary>
        /// Gets the Problem Details (RFC 9457) of an error response: it is read from the body when the status code
        /// is not a success status code and the content type is <c>application/problem+json</c>.
        /// It is null for a success response, when no response has been received, for another content type, or when
        /// the body is not a valid JSON object. It is read once, on first access, without reflection.
        /// For a <c>Stream</c> result see <see cref="ErrorAs{TError}"/>.
        /// </summary>
        public RestProblemDetails? Problem
        {
            get
            {
                if (Response == null || Response.IsSuccessStatusCode)
                {
                    return null;
                }

                lock (ErrorLock)
                {
                    if (!problemRead)
                    {
                        if (RestProblemDetails.IsProblemMediaType(Response.Content?.Headers.ContentType))
                        {
                            byte[]? body = GetErrorBody();
                            problem = body == null ? null : RestProblemDetails.Parse(body);
                        }
                        problemRead = true;
                    }
                    return problem;
                }
            }
        }

        #endregion

        #region [ Error body ]

        private object? errorLock;
        private byte[]? errorBody;
        private bool errorBodyRead;
        private IRestSerializer? errorSerializer;
        private RestProblemDetails? problem;
        private bool problemRead;

        private object ErrorLock
        {
            get
            {
                if (errorLock == null)
                {
                    Interlocked.CompareExchange(ref errorLock, new object(), null);
                }
                return errorLock!;
            }
        }

        /// <summary>
        /// Sets the serializer used by ErrorAs and, for a buffered response, the error body (only for error responses)
        /// </summary>
        internal void SetError(IRestSerializer serializer, byte[]? body)
        {
            errorSerializer = serializer;
            if (body != null)
            {
                errorBody = body;
                errorBodyRead = true;
            }
        }

        /// <summary>
        /// Gets the body of an error response, reading it once (a Stream content is read here, on first use)
        /// </summary>
        private byte[]? GetErrorBody()
        {
            if (!errorBodyRead)
            {
                errorBody = ReadErrorBody();
                errorBodyRead = true;
            }
            return errorBody;
        }

        /// <summary>
        /// Reads the error body that has not been buffered (Stream result)
        /// </summary>
        private protected abstract byte[]? ReadErrorBody();

        /// <summary>
        /// Gets the body of an error response as TError: <c>string</c> and <c>byte[]</c> are the raw body,
        /// <c>Stream</c> is a read-only stream over the body, any other type is deserialized with the serializer
        /// configured on the builder (JSON by default: pass a JsonSerializerContext that contains TError to be
        /// Native AOT and trimming safe), for example a custom error DTO.
        /// It returns the default value of TError for a success response, when no response has been received,
        /// when the body is empty or when it cannot be deserialized; it never throws.
        /// The body is read only once, also when it is used by both Problem and ErrorAs. For a <c>Stream</c> result,
        /// Problem and ErrorAs read the error body from Content and then replace Content with a buffered stream with
        /// the same bytes: use them before reading Content.
        /// </summary>
        /// <typeparam name="TError">The type of the error body.</typeparam>
        /// <returns>The error body as TError, or the default value of TError.</returns>
        public TError? ErrorAs<TError>()
        {
            if (Response == null || Response.IsSuccessStatusCode)
            {
                return default;
            }

            byte[]? body;
            lock (ErrorLock)
            {
                body = GetErrorBody();
            }

            if (body == null)
            {
                return default;
            }

            try
            {
                Type type = typeof(TError);
                if (type == typeof(byte[]))
                {
                    return (TError)(object)body.Clone();
                }
                if (type == typeof(Stream))
                {
                    return (TError)(object)new MemoryStream(body, writable: false);
                }

                string text = DecodeErrorBody(body);
                if (type == typeof(string))
                {
                    return (TError)(object)text;
                }

                if (errorSerializer == null || text.Length == 0)
                {
                    return default;
                }

                object? value = errorSerializer.Deserialize(text, type);
                return value is null ? default : (TError)value;
            }
            catch (Exception)
            {
                return default;
            }
        }

        /// <summary>
        /// Decodes the error body as HttpContent.ReadAsStringAsync does (charset of the content type, BOM, UTF-8)
        /// </summary>
        private string DecodeErrorBody(byte[] body)
        {
            using (ByteArrayContent content = new ByteArrayContent(body))
            {
                MediaTypeHeaderValue? contentType = Response?.Content?.Headers.ContentType;
                if (contentType != null)
                {
                    content.Headers.ContentType = contentType;
                }
                // a ByteArrayContent completes synchronously
                return content.ReadAsStringAsync().ConfigureAwait(false).GetAwaiter().GetResult();
            }
        }

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
                throw CreateRequestException(Exception);
            }

            if (!Response.IsSuccessStatusCode)
            {
                throw CreateStatusCodeException(Response);
            }

            if (Exception != null)
            {
                throw CreateRequestException(Exception);
            }

            return this;
        }

        /// <summary>
        /// The exception of EnsureSuccessStatusCode for a request that failed (no response or unreadable content)
        /// </summary>
        internal static HttpRequestException CreateRequestException(Exception? exception)
            => new HttpRequestException(exception?.Message ?? "No response received.", exception);

        /// <summary>
        /// The exception of EnsureSuccessStatusCode for a response with a status code that is not a success status code
        /// </summary>
        internal static HttpRequestException CreateStatusCodeException(HttpResponseMessage response)
            => new HttpRequestException($"Response status code does not indicate success: {(int)response.StatusCode} ({response.ReasonPhrase}).");

        #endregion

        #region [ IDisposable ]

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
                    Owner?.Dispose();
                }
                disposed = true;
            }
        }

        #endregion
    }

    /// <summary>
    /// The result of a request with a content of type T.
    /// </summary>
    /// <typeparam name="T">The type of the content.</typeparam>
    public sealed class RestResult<T> : RestResult, IRestResult<T>
    {
        internal RestResult(HttpResponseMessage response) : base(response) { }

        internal RestResult(Exception exception) : base(exception) { }

        /// <summary>
        /// Gets HTTP response's content. It is the default value of T when the request failed
        /// or the response could not be read (see Exception).
        /// </summary>
        public new T? Content { get; internal set; }

        private protected override object? ContentAsObject => Content;

        private protected override byte[]? ReadErrorBody()
        {
            // the error body of a Stream result is not buffered: it is read here, once, and Content is replaced
            // with a buffered stream with the same bytes
            if (typeof(T) != typeof(Stream) || !(Content is Stream stream))
            {
                return null;
            }

            try
            {
                using (MemoryStream memory = new MemoryStream())
                {
                    stream.CopyTo(memory);
                    byte[] body = memory.ToArray();
                    Content = (T)(object)new MemoryStream(body, writable: false);
                    stream.Dispose();
                    return body;
                }
            }
            catch (Exception)
            {
                return null;
            }
        }

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
