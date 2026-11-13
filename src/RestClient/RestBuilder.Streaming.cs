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
    using Mafesoft.RestClient.Internal;
    using Mafesoft.RestClient.Serialization.Json;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Net.Http;
    using System.Runtime.CompilerServices;
    using System.Text.Json;
    using System.Text.Json.Serialization.Metadata;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Streamed responses: the items are read while the response is downloaded, without buffering the body.
    /// </summary>
    public sealed partial class RestBuilder
    {
        #region [ JSON array as IAsyncEnumerable ]

        /// <summary>
        /// Sends a GET request and streams the items of the JSON array of the response while they are downloaded.
        /// See <see cref="SendAsyncEnumerable{T}(HttpMethod, CancellationToken)"/>.
        /// </summary>
        /// <typeparam name="T">The type of the items of the JSON array</typeparam>
        /// <param name="cancellationToken">Cancellation token, also combined with the token passed to WithCancellation</param>
        /// <returns>The items of the array; the request is sent when the enumeration starts</returns>
        public IAsyncEnumerable<T> GetAsyncEnumerable<T>(CancellationToken cancellationToken = default)
            => SendAsyncEnumerable<T>(HttpMethod.Get, cancellationToken);

        /// <summary>
        /// Sends a POST request (with the configured payload) and streams the items of the JSON array of the response
        /// while they are downloaded. See <see cref="SendAsyncEnumerable{T}(HttpMethod, CancellationToken)"/>.
        /// </summary>
        /// <typeparam name="T">The type of the items of the JSON array</typeparam>
        /// <param name="cancellationToken">Cancellation token, also combined with the token passed to WithCancellation</param>
        /// <returns>The items of the array; the request is sent when the enumeration starts</returns>
        public IAsyncEnumerable<T> PostAsyncEnumerable<T>(CancellationToken cancellationToken = default)
            => SendAsyncEnumerable<T>(HttpMethod.Post, cancellationToken);

        /// <summary>
        /// Sends an HTTP request and streams the items of the JSON array of the response while they are downloaded
        /// (<c>JsonSerializer.DeserializeAsyncEnumerable</c>): the body is never buffered, memory does not grow with
        /// the number of items. Every enumeration sends a new request.
        /// <para>
        /// The request goes through the same pipeline as SendAsync (headers, authentication, cookies and redirects,
        /// Timeout of each attempt, retry and refresh token) until the headers of a successful response are received;
        /// once the items are being read nothing is retried and only the cancellation token stops the download.
        /// </para>
        /// <para>
        /// Unlike SendAsync, errors are thrown when the enumeration starts: a status code that is not a success status
        /// code and a failed request throw the HttpRequestException of RestResult.EnsureSuccessStatusCode (a timeout is
        /// its inner TimeoutException); a cancellation throws OperationCanceledException; an invalid JSON throws
        /// JsonException. A JSON <c>null</c> item is returned as the default value of T.
        /// The response is released when the enumeration ends, also with break or an exception.
        /// </para>
        /// <para>
        /// The items are deserialized with the JSON configuration of the builder: with <c>Json(JsonSerializerContext)</c>
        /// the metadata of T comes from the context (Native AOT and trimming safe). OnStart, OnPreviewContentRequestAsString,
        /// OnUploadProgress, OnDownloadProgress and OnException are raised; OnPreviewContentResponseAsString, OnPreCompleted
        /// and OnCompleted are not (there is no buffered body nor RestResult). The trace activity and the
        /// <c>restclient.request.duration</c> metric last until the end of the enumeration.
        /// </para>
        /// </summary>
        /// <typeparam name="T">The type of the items of the JSON array</typeparam>
        /// <param name="method">The HTTP method</param>
        /// <param name="cancellationToken">Cancellation token, also combined with the token passed to WithCancellation</param>
        /// <returns>The items of the array; the request is sent when the enumeration starts</returns>
        /// <exception cref="ArgumentNullException">method is null</exception>
        /// <exception cref="NotSupportedException">The configured serializer is not JSON (Xml or CustomSerializer), or the configured JsonSerializerContext does not contain T</exception>
        public IAsyncEnumerable<T> SendAsyncEnumerable<T>(HttpMethod method, CancellationToken cancellationToken = default)
        {
            if (method == null) { throw new ArgumentNullException(nameof(method)); }
            if (!(serializer is JSON))
            {
                throw new NotSupportedException(
                    $"Streaming a JSON array requires the JSON serializer, but the builder uses {serializer.GetType().FullName}: call Json(...) before SendAsyncEnumerable, or use SendAsync<Stream> to read the response yourself.");
            }

            // resolved here, so that a type missing from the JsonSerializerContext is reported before sending the request
            JsonTypeInfo<T> typeInfo = JSON.GetTypeInfo<T>(deserializeOptions);
            return StreamAsync(method, (stream, token) => (IAsyncEnumerable<T>)JsonSerializer.DeserializeAsyncEnumerable(stream, typeInfo, token), cancellationToken);
        }

        #endregion

        #region [ Streamed response ]

        /// <summary>
        /// Sends the request when the enumeration starts and returns the items read from the content of the successful
        /// response by read (JSON array, server-sent events, ...). Errors before the items are thrown as described
        /// in SendAsyncEnumerable; the response and the diagnostics are completed when the enumeration ends.
        /// </summary>
        private async IAsyncEnumerable<TItem> StreamAsync<TItem>(HttpMethod method, Func<Stream, CancellationToken, IAsyncEnumerable<TItem>> read, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            StreamingResponse response = await SendStreamingAsync(method, cancellationToken).ConfigureAwait(false);
            Exception? failure = null;
            try
            {
                IAsyncEnumerator<TItem> items;
                try
                {
                    Stream stream = await response.ReadContentAsync(cancellationToken).ConfigureAwait(false);
                    items = read(stream, cancellationToken).GetAsyncEnumerator(cancellationToken);
                }
                catch (Exception ex)
                {
                    failure = response.GetFailure(ex, cancellationToken);
                    if (failure == ex) { throw; }
                    throw failure;
                }

                try
                {
                    while (true)
                    {
                        bool next;
                        try
                        {
                            next = await items.MoveNextAsync().ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            failure = response.GetFailure(ex, cancellationToken);
                            if (failure == ex) { throw; }
                            throw failure;
                        }

                        if (!next)
                        {
                            break;
                        }
                        yield return items.Current;
                    }
                }
                finally
                {
                    await items.DisposeAsync().ConfigureAwait(false);
                }
            }
            finally
            {
                // completed, break of the caller (DisposeAsync) or exception
                response.Complete(failure, typeof(TItem));
            }
        }

        /// <summary>
        /// Sends the request through the pipeline (OnStart, tracing, logging, retry, refresh token, Timeout of each
        /// attempt) and returns the successful response with only the headers read: the caller reads the content and
        /// calls Complete. Throws the exception of RestResult.EnsureSuccessStatusCode for a failed request or an error
        /// status code, OperationCanceledException for a cancellation.
        /// </summary>
        private async Task<StreamingResponse> SendStreamingAsync(HttpMethod method, CancellationToken cancellationToken)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            string logUrl = string.Empty;
            Activity? activity = RestClientDiagnostics.StartActivity(method);
            StreamingResponse response;

            try
            {
                string url = BuildFinalUrl();
                logUrl = RestClientDiagnostics.RedactUrl(url);

                StartEventArgs startEventArgs = new StartEventArgs
                {
                    Cancel = false,
                    Payload = payloadContent,
                    Url = url
                };

                onStartAction?.Invoke(startEventArgs);

                if (startEventArgs.Cancel)
                {
                    throw new OperationCanceledException("The request has been cancelled by OnStart.");
                }

                RestClientDiagnostics.Starting(activity, logger, method, logUrl);

                // the per-attempt token is not used after the headers: the content is read with cancellationToken only
                response = await ExecuteAsync(
                    method,
                    url,
                    logUrl,
                    (lease, retries, attemptToken) => Task.FromResult(new StreamingResponse(this, method, logUrl, lease, retries, activity, stopwatch)),
                    cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                RestClientDiagnostics.Failed(activity, logger, method, logUrl, ex, stopwatch.Elapsed, clientName);
                activity?.Dispose();
                onExceptionAction?.Invoke(ex);
                if (ex is OperationCanceledException)
                {
                    throw;
                }
                throw RestResult.CreateRequestException(ex);
            }

            if (!response.Response.IsSuccessStatusCode)
            {
                HttpRequestException error = RestResult.CreateStatusCodeException(response.Response);
                response.Complete(null, null);
                throw error;
            }

            return response;
        }

        /// <summary>
        /// A successful response whose content is being read: it owns the response, the HttpClient, the activity
        /// and the stopwatch of the request until Complete
        /// </summary>
        private sealed class StreamingResponse
        {
            private readonly RestBuilder builder;
            private readonly HttpMethod method;
            private readonly string logUrl;
            private readonly ResponseLease lease;
            private readonly int retries;
            private readonly Activity? activity;
            private readonly Stopwatch stopwatch;
            private CancellationTokenRegistration abort;
            private int completed;

            public StreamingResponse(RestBuilder builder, HttpMethod method, string logUrl, ResponseLease lease, int retries, Activity? activity, Stopwatch stopwatch)
            {
                this.builder = builder;
                this.method = method;
                this.logUrl = logUrl;
                this.lease = lease;
                this.retries = retries;
                this.activity = activity;
                this.stopwatch = stopwatch;
            }

            public HttpResponseMessage Response => lease.Response;

            /// <summary>
            /// Gets the content stream (with OnDownloadProgress). A cancellation disposes the response, so that a
            /// pending read is aborted also where the response stream ignores the token (.NET Framework).
            /// </summary>
            public async Task<Stream> ReadContentAsync(CancellationToken cancellationToken)
            {
                Stream stream = await Response.Content.ReadAsStreamAsync().ConfigureAwait(false);
                abort = cancellationToken.Register(state => ((ResponseLease)state!).Dispose(), lease);
                return builder.onDownloadProgressAction != null ? builder.CreateProgressStream(stream, Response) : stream;
            }

            /// <summary>
            /// The exception thrown by the enumeration for an error while reading the content: an error caused by
            /// the cancellation (the response has been disposed) is an OperationCanceledException
            /// </summary>
            public Exception GetFailure(Exception exception, CancellationToken cancellationToken)
                => cancellationToken.IsCancellationRequested && !(exception is OperationCanceledException)
                    ? new OperationCanceledException("The enumeration has been cancelled.", exception, cancellationToken)
                    : exception;

            /// <summary>
            /// Records the request (duration until now) and releases the response. Only the first call has effect.
            /// failure is the exception thrown while reading the content, null when the content has been read or
            /// the enumeration has been stopped by the caller.
            /// </summary>
            public void Complete(Exception? failure, Type? itemType)
            {
                if (Interlocked.Exchange(ref completed, 1) != 0)
                {
                    return;
                }

                abort.Dispose();
                stopwatch.Stop();
                try
                {
                    if (failure is OperationCanceledException)
                    {
                        RestClientDiagnostics.Failed(activity, builder.logger, method, logUrl, failure, stopwatch.Elapsed, builder.clientName);
                    }
                    else
                    {
                        if (failure != null)
                        {
                            RestClientDiagnostics.ContentError(builder.logger, method, logUrl, itemType ?? typeof(Stream), failure);
                        }
                        RestClientDiagnostics.Completed(activity, builder.logger, method, logUrl, (int)Response.StatusCode,
                            Response.IsSuccessStatusCode && failure == null, failure, retries, stopwatch.Elapsed, builder.clientName);
                    }

                    if (failure != null)
                    {
                        builder.onExceptionAction?.Invoke(failure);
                    }
                }
                finally
                {
                    lease.Dispose();
                    activity?.Dispose();
                }
            }
        }

        #endregion
    }
}
