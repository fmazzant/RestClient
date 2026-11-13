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
    using Mafesoft.RestClient.Http;
    using Mafesoft.RestClient.Internal;
    using Mafesoft.RestClient.Serialization;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    public sealed partial class RestBuilder
    {
        #region [ Send ]

        /// <summary>
        /// Sends an HTTP request as an asynchronous operation. It never throws: errors are reported by the result.
        /// </summary>
        /// <typeparam name="T">
        /// Type of the content: <c>string</c> (raw body), <c>byte[]</c> (raw body), <c>Stream</c> (not buffered,
        /// dispose the result when done) or any other type, deserialized with the configured serializer.
        /// </typeparam>
        /// <param name="method">The HTTP method</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The result of the request</returns>
        public Task<RestResult<T>> SendAsync<T>(HttpMethod method, CancellationToken cancellationToken = default)
        {
            if (method == null) { throw new ArgumentNullException(nameof(method)); }
            return SendCoreAsync<T>(method, cancellationToken);
        }

        /// <summary>
        /// Sends an HTTP request as a synchronous operation. It never throws: errors are reported by the result.
        /// Prefer SendAsync: this method blocks the calling thread until the response is received (sync-over-async).
        /// </summary>
        /// <typeparam name="T">Type of the content, see SendAsync</typeparam>
        /// <param name="method">The HTTP method</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The result of the request</returns>
        public RestResult<T> Send<T>(HttpMethod method, CancellationToken cancellationToken = default)
            => SendAsync<T>(method, cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult();

        private async Task<RestResult<T>> SendCoreAsync<T>(HttpMethod method, CancellationToken cancellationToken)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            string logUrl = string.Empty;
            using Activity? activity = RestClientDiagnostics.StartActivity(method);

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

                RestResult<T> result = await ExecuteAsync<T>(method, url, logUrl, cancellationToken).ConfigureAwait(false);

                stopwatch.Stop();
                RestClientDiagnostics.Completed(activity, logger, method, logUrl, result, stopwatch.Elapsed);

                onPreCompletedAction?.Invoke(new PreCompletedEventArgs { IsCompleted = true, Result = result });
                onCompletedAction?.Invoke(new CompletedEventArgs { Result = result, ExecutionTime = stopwatch.Elapsed });

                return result;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                RestClientDiagnostics.Failed(activity, logger, method, logUrl, ex, stopwatch.Elapsed);
                onExceptionAction?.Invoke(ex);
                return new RestResult<T>(ex);
            }
        }

        /// <summary>
        /// Sends the request with the retry policy and the refresh token
        /// </summary>
        private async Task<RestResult<T>> ExecuteAsync<T>(HttpMethod method, string url, string logUrl, CancellationToken cancellationToken)
        {
            int retries = 0;
            bool refreshAttempted = false;

            while (true)
            {
                TimeSpan? retryAfter = null;
                string retryReason;

                using (CancellationTokenSource attemptSource = CreateAttemptTokenSource(cancellationToken))
                {
                    ResponseLease? lease = null;
                    try
                    {
                        int generation = refreshTokenCoordinator?.Generation ?? 0;
                        lease = await SendOnceAsync(method, url, attemptSource.Token).ConfigureAwait(false);
                        HttpResponseMessage response = lease.Response;

                        if (response.StatusCode == HttpStatusCode.Unauthorized
                            && !refreshAttempted
                            && refreshTokenCoordinator != null
                            && !RefreshTokenCoordinator.IsRefreshing)
                        {
                            refreshAttempted = true;
                            RestClientDiagnostics.RefreshingToken(logger, method, logUrl);
                            if (await refreshTokenCoordinator.RefreshAsync(generation, cancellationToken).ConfigureAwait(false))
                            {
                                lease.Dispose();
                                continue;
                            }
                        }

                        if (!IsTransient(response.StatusCode) || !CanRetry(method, retries))
                        {
                            RestResult<T> result = await MaterializeAsync<T>(method, logUrl, lease, attemptSource.Token).ConfigureAwait(false);
                            result.RetryCount = retries;
                            return result;
                        }

                        retryAfter = GetRetryAfter(response);
                        retryReason = $"HTTP {(int)response.StatusCode}";
                        lease.Dispose();
                    }
                    catch (OperationCanceledException ex) when (attemptSource.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
                    {
                        lease?.Dispose();
                        TimeoutException timeout = new TimeoutException(
                            $"The request was canceled due to the configured Timeout of {properties.Timeout.TotalSeconds} seconds elapsing.", ex);
                        if (!CanRetry(method, retries))
                        {
                            throw timeout;
                        }
                        retryReason = timeout.Message;
                    }
                    catch (HttpRequestException ex) when (CanRetry(method, retries))
                    {
                        lease?.Dispose();
                        retryReason = ex.Message;
                    }
                    catch
                    {
                        lease?.Dispose();
                        throw;
                    }
                }

                TimeSpan delay = retrySettings!.GetDelay(retries, retryAfter);
                retries++;
                RestClientDiagnostics.Retrying(logger, method, logUrl, delay, retries, retrySettings.MaxRetries, retryReason);
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            }
        }

        private CancellationTokenSource CreateAttemptTokenSource(CancellationToken cancellationToken)
        {
            CancellationTokenSource source = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            TimeSpan timeout = properties.Timeout;
            if (timeout != System.Threading.Timeout.InfiniteTimeSpan && timeout > TimeSpan.Zero)
            {
                source.CancelAfter(timeout);
            }
            return source;
        }

        private bool CanRetry(HttpMethod method, int retries)
            => retrySettings != null
            && retries < retrySettings.MaxRetries
            && (retrySettings.RetryNonIdempotentMethods || RetrySettings.IsIdempotent(method));

        private static bool IsTransient(HttpStatusCode statusCode)
        {
            switch ((int)statusCode)
            {
                case 408:
                case 429:
                case 500:
                case 502:
                case 503:
                case 504:
                    return true;
                default:
                    return false;
            }
        }

        private static TimeSpan? GetRetryAfter(HttpResponseMessage response)
        {
            var retryAfter = response.Headers.RetryAfter;
            if (retryAfter?.Delta is TimeSpan delta)
            {
                return delta;
            }
            if (retryAfter?.Date is DateTimeOffset date)
            {
                TimeSpan wait = date - DateTimeOffset.UtcNow;
                return wait > TimeSpan.Zero ? wait : TimeSpan.Zero;
            }
            return null;
        }

        #endregion

        #region [ Single attempt ]

        /// <summary>
        /// Maximum number of redirects followed by RestClient when a cookie container is set
        /// (the same default as HttpClientHandler.MaxAutomaticRedirections)
        /// </summary>
        internal const int MaxRedirects = 50;

        /// <summary>
        /// Sends a single HTTP request and returns as soon as the response headers are received
        /// </summary>
        private async Task<ResponseLease> SendOnceAsync(HttpMethod method, string url, CancellationToken cancellationToken)
        {
            HttpClient client;
            IDisposable? owner;
            CookieContainer? cookieContainer = null;

            if (externalHttpClient != null)
            {
                client = externalHttpClient;
                owner = null;
            }
            else
            {
                // the cookies belong to the builder, not to the pooled handler (UseCookies = false): they are handled
                // here for every request and, so that the Set-Cookie of the redirects are stored too, the redirects are
                // followed here instead of by the handler
                cookieContainer = cookies;
                client = HttpHandlerPool.CreateClient(new HttpHandlerPool.HandlerKey(
                    enabledCompression, properties.CertificateOption, credentials, certificateCallback, useProxy, proxy,
                    allowAutoRedirect: cookieContainer == null));
                // the timeout is managed by RestClient (see CreateAttemptTokenSource)
                client.Timeout = System.Threading.Timeout.InfiniteTimeSpan;
                owner = client;
                onConfigureHttpClientAction?.Invoke(client);
            }

            try
            {
                // the authorization is computed once per attempt: the redirected requests have no Authorization header
                AuthenticationHeaderValue? authorization = onAuthenticationAsync != null
                    ? await GetAuthorizationAsync(onAuthenticationAsync, cancellationToken).ConfigureAwait(false)
                    : onAuthentication?.Invoke();

                HttpMethod requestMethod = method;
                Uri requestUri = new Uri(url, UriKind.RelativeOrAbsolute);
                bool redirected = false;
                bool withContent = true;
                int redirects = 0;

                while (true)
                {
                    HttpResponseMessage response;
                    using (HttpRequestMessage request = CreateRequestMessage(requestMethod, requestUri, authorization, redirected, withContent, cancellationToken))
                    {
                        AddCookieHeader(cookieContainer, request);
                        response = await client
                            .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                            .ConfigureAwait(false);
                    }
#if !NET
                    if (response.Content == null)
                    {
                        response.Content = new ByteArrayContent(Array.Empty<byte>());
                    }
#endif
                    StoreCookies(cookieContainer, requestUri, response);

                    if (cookieContainer != null
                        && redirects < MaxRedirects
                        && TryGetRedirect(requestMethod, requestUri, response, out HttpMethod redirectMethod, out Uri? location))
                    {
                        response.Dispose();
                        redirects++;
                        redirected = true;
                        withContent = withContent && redirectMethod == requestMethod;
                        requestMethod = redirectMethod;
                        requestUri = location!;
                        continue;
                    }

                    return new ResponseLease(response, owner);
                }
            }
            catch
            {
                owner?.Dispose();
                throw;
            }
        }

        /// <summary>
        /// Invokes the asynchronous authentication provider of the attempt
        /// </summary>
        private static Task<AuthenticationHeaderValue> GetAuthorizationAsync(Func<CancellationToken, Task<AuthenticationHeaderValue>> provider, CancellationToken cancellationToken)
            => provider(cancellationToken) ?? throw new InvalidOperationException("The authentication provider returned a null task.");

        /// <summary>
        /// Adds the cookies of the container for the request URI (Cookie header)
        /// </summary>
        private static void AddCookieHeader(CookieContainer? container, HttpRequestMessage request)
        {
            Uri? uri = request.RequestUri;
            if (container == null || uri == null || !uri.IsAbsoluteUri)
            {
                return;
            }

            string header = container.GetCookieHeader(uri);
            if (!string.IsNullOrEmpty(header))
            {
                request.Headers.TryAddWithoutValidation("Cookie", header);
            }
        }

        /// <summary>
        /// Stores the cookies of the response (Set-Cookie headers) in the container
        /// </summary>
        private static void StoreCookies(CookieContainer? container, Uri requestUri, HttpResponseMessage response)
        {
            if (container == null
                || !requestUri.IsAbsoluteUri
                || !response.Headers.TryGetValues("Set-Cookie", out IEnumerable<string>? values))
            {
                return;
            }

            foreach (string value in values)
            {
                try
                {
                    container.SetCookies(requestUri, value);
                }
                catch (CookieException)
                {
                    // an invalid cookie is ignored, as HttpClientHandler does
                }
            }
        }

        /// <summary>
        /// Gets the next request of a redirect, with the rules of HttpClientHandler: 300, 301, 302, 303, 307 and 308
        /// with a Location header; POST becomes GET for 300-302, every method except GET and HEAD becomes GET for 303;
        /// a redirect from HTTPS to HTTP or to a scheme other than HTTP(S) is not followed.
        /// </summary>
        private static bool TryGetRedirect(HttpMethod method, Uri requestUri, HttpResponseMessage response, out HttpMethod redirectMethod, out Uri? location)
        {
            redirectMethod = method;
            location = null;

            int status = (int)response.StatusCode;
            switch (status)
            {
                case 300:
                case 301:
                case 302:
                case 303:
                case 307:
                case 308:
                    break;
                default:
                    return false;
            }

            Uri? header = response.Headers.Location;
            if (header == null || !requestUri.IsAbsoluteUri)
            {
                return false;
            }

            // a path like /next can be parsed as an absolute file URI on Unix: resolve it against the request URI
            Uri target = header.IsAbsoluteUri && !header.IsFile ? header : new Uri(requestUri, header.OriginalString);
            if (target.Scheme != Uri.UriSchemeHttp && target.Scheme != Uri.UriSchemeHttps)
            {
                return false;
            }
            if (requestUri.Scheme == Uri.UriSchemeHttps && target.Scheme == Uri.UriSchemeHttp)
            {
                return false;
            }

            if ((status <= 302 && method == HttpMethod.Post)
                || (status == 303 && method != HttpMethod.Get && method != HttpMethod.Head))
            {
                redirectMethod = HttpMethod.Get;
            }

            location = target;
            return true;
        }

        /// <summary>
        /// Creates the request message with headers, authentication and content.
        /// A redirected request has no Authorization header (as with HttpClientHandler), no content when the method
        /// has become GET, and it does not raise OnPreviewContentRequestAsString again.
        /// </summary>
        private HttpRequestMessage CreateRequestMessage(HttpMethod method, Uri requestUri, AuthenticationHeaderValue? authorization, bool redirected, bool withContent, CancellationToken cancellationToken)
        {
            HttpRequestMessage request = new HttpRequestMessage(method, requestUri);
            try
            {
                if (httpVersion != null)
                {
                    request.Version = httpVersion;
                }
#if NET
                if (httpVersionPolicy != null)
                {
                    request.VersionPolicy = httpVersionPolicy.Value;
                }
#endif

                if (authorization != null && !redirected)
                {
                    request.Headers.Authorization = authorization;
                }

                onRequestHeaders?.Invoke(request.Headers);

                if (redirected)
                {
                    request.Headers.Authorization = null;
                }

                HttpContent? content = withContent ? MakeHttpContent(preview: !redirected) : null;
                if (content != null && onUploadProgressAction != null)
                {
                    var onProgress = onUploadProgressAction;
                    content = new ProgressHttpContent(
                        content,
                        properties.BufferSize,
                        (current, total) => onProgress(new ProgressEventArgs { CurrentBytes = current, TotalBytes = total }),
                        cancellationToken);
                }
                request.Content = content;

                return request;
            }
            catch
            {
                request.Dispose();
                throw;
            }
        }

        /// <summary>
        /// Creates the request content
        /// </summary>
        private HttpContent? MakeHttpContent(bool preview)
        {
            Action<PreviewContentAsStringEventArgs>? onPreview = preview ? onPreviewContentRequestAsStringAction : null;

            if (multipartFormData != null)
            {
                onPreview?.Invoke(new PreviewContentAsStringEventArgs
                {
                    ContentAsString = multipartFormData.Describe(),
                    ContentType = typeof(MultipartFormData)
                });
                return multipartFormData.CreateContent();
            }

            if (isEnabledFormUrlEncoded)
            {
                if (formUrlEncodedKeyValues == null)
                {
                    return null;
                }

                onPreview?.Invoke(new PreviewContentAsStringEventArgs
                {
                    ContentAsString = string.Join("&", formUrlEncodedKeyValues.Select(q => $"{Uri.EscapeDataString(q.Key)}={Uri.EscapeDataString(q.Value ?? string.Empty)}")),
                    ContentType = formUrlEncodedKeyValues.GetType()
                });

#if NET
                return new FormUrlEncodedContent(formUrlEncodedKeyValues!);
#else
                return new FormUrlEncodedContent(formUrlEncodedKeyValues);
#endif
            }

            if (payloadContent != null)
            {
                Type payloadType = payloadContent.GetType();
                string serializedObject = serializer.SerializeObject(payloadContent, payloadType, serializeOptions);
                onPreview?.Invoke(new PreviewContentAsStringEventArgs
                {
                    ContentAsString = serializedObject,
                    ContentType = payloadType
                });
                return new StringContent(serializedObject, Encoding.UTF8, serializer.MediaTypeAsString);
            }

            return null;
        }

        #endregion

        #region [ Response ]

        /// <summary>
        /// Reads the response content as T. The lease is owned by the returned result (or disposed on error).
        /// </summary>
        private async Task<RestResult<T>> MaterializeAsync<T>(HttpMethod method, string logUrl, ResponseLease lease, CancellationToken cancellationToken)
        {
            HttpResponseMessage response = lease.Response;
            RestResult<T> result = new RestResult<T>(response);
            Type type = typeof(T);

            try
            {
                // Stream: not buffered, the connection stays open until the result is disposed
                if (type == typeof(Stream))
                {
                    Stream stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
                    if (onDownloadProgressAction != null)
                    {
                        stream = CreateProgressStream(stream, response);
                    }
                    result.Content = (T)(object)stream;
                    result.Owner = lease.Owner;
                    return result;
                }

                // successful response of a serializer that supports streams: deserialize while downloading
                if (type != typeof(string)
                    && type != typeof(byte[])
                    && serializer is IAsyncSerializerContent asyncSerializer
                    && response.IsSuccessStatusCode
                    && onPreviewContentResponseAsStringAction == null)
                {
                    using (ProgressStream stream = CreateProgressStream(await response.Content.ReadAsStreamAsync().ConfigureAwait(false), response))
                    {
                        try
                        {
                            object? value = await asyncSerializer.DeserializeObjectAsync(stream, type, deserializeOptions, cancellationToken).ConfigureAwait(false);
                            result.Content = value is null ? default : (T)value;
                        }
                        catch (Exception ex) when (!(ex is OperationCanceledException))
                        {
                            // an empty body is a null content, not an error
                            if (stream.BytesRead > 0)
                            {
                                SetContentError(result, method, logUrl, ex);
                            }
                        }
                    }
                    lease.Owner?.Dispose();
                    return result;
                }

                await BufferContentAsync(response, cancellationToken).ConfigureAwait(false);
                lease.Owner?.Dispose();

                HttpContent content = response.Content;
                if (onPreviewContentResponseAsStringAction != null && type != typeof(byte[]))
                {
                    onPreviewContentResponseAsStringAction(new PreviewContentAsStringEventArgs
                    {
                        ContentAsString = await content.ReadAsStringAsync().ConfigureAwait(false),
                        ContentType = type
                    });
                }

                try
                {
                    if (type == typeof(string))
                    {
                        result.Content = (T)(object)await content.ReadAsStringAsync().ConfigureAwait(false);
                    }
                    else if (type == typeof(byte[]))
                    {
                        result.Content = (T)(object)await content.ReadAsByteArrayAsync().ConfigureAwait(false);
                    }
                    else
                    {
                        string serializedObject = await content.ReadAsStringAsync().ConfigureAwait(false);
                        object? value = serializer.DeserializeObject(serializedObject, type, deserializeOptions);
                        result.Content = value is null ? default : (T)value;
                    }
                }
                catch (Exception ex)
                {
                    SetContentError(result, method, logUrl, ex);
                }

                return result;
            }
            catch
            {
                lease.Dispose();
                throw;
            }
        }

        /// <summary>
        /// The response has been received but its content cannot be read: status code and headers remain available
        /// </summary>
        private void SetContentError<T>(RestResult<T> result, HttpMethod method, string logUrl, Exception exception)
        {
            result.Exception = exception;
            RestClientDiagnostics.ContentError(logger, method, logUrl, typeof(T), exception);
            onExceptionAction?.Invoke(exception);
        }

        private ProgressStream CreateProgressStream(Stream stream, HttpResponseMessage response)
        {
            var onProgress = onDownloadProgressAction;
            return new ProgressStream(
                stream,
                response.Content.Headers.ContentLength ?? 0,
                onProgress == null ? null : (current, total) => onProgress(new ProgressEventArgs { CurrentBytes = current, TotalBytes = total }));
        }

        /// <summary>
        /// Reads the whole response content (raising OnDownloadProgress) and replaces it with a buffered content,
        /// so that it can be read more than once and outlives the HttpClient.
        /// </summary>
        private async Task BufferContentAsync(HttpResponseMessage response, CancellationToken cancellationToken)
        {
            HttpContent original = response.Content;
            byte[] buffer = new byte[properties.BufferSize];

            using (Stream source = CreateProgressStream(await original.ReadAsStreamAsync().ConfigureAwait(false), response))
            using (MemoryStream memory = new MemoryStream())
            {
                int read;
                while ((read = await source.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false)) > 0)
                {
                    memory.Write(buffer, 0, read);
                }

                ByteArrayContent buffered = new ByteArrayContent(memory.ToArray());
                foreach (var header in original.Headers)
                {
                    buffered.Headers.TryAddWithoutValidation(header.Key, header.Value);
                }
                buffered.Headers.ContentLength = memory.Length;

                original.Dispose();
                response.Content = buffered;
            }
        }

        /// <summary>
        /// Builds the final url: endpoint + commands (with the route values) + url-encoded query string
        /// </summary>
        private string BuildFinalUrl()
        {
            if (properties.EndPoint == null)
            {
                throw new InvalidOperationException("The EndPoint is not set: use Url(...) or Rest.Build(p => p.EndPoint = ...).");
            }

            StringBuilder urlBuilder = new StringBuilder(properties.EndPoint.OriginalString);

            if (commands.Count > 0)
            {
                while (urlBuilder.Length > 0 && urlBuilder[urlBuilder.Length - 1] == '/')
                {
                    urlBuilder.Length--;
                }
                foreach (string command in commands)
                {
                    urlBuilder.Append(command);
                }
            }

            // the {name} placeholders of the endpoint and of the commands (not of the query string parameters),
            // only when Route was used: without route values the url is sent untouched, as before templates existed
            string expanded = urlBuilder.ToString();
            if (routeValues.Count > 0)
            {
                expanded = UriTemplate.Expand(expanded, routeValues);
                urlBuilder.Clear().Append(expanded);
            }

            if (parameters.Count > 0)
            {
                urlBuilder.Append(expanded.IndexOf('?') >= 0 ? "&" : "?");
                urlBuilder.Append(string.Join("&", parameters.Select(q => $"{Uri.EscapeDataString(q.Key)}={Uri.EscapeDataString(q.Value)}")));
            }

            return urlBuilder.ToString();
        }

        #endregion

        /// <summary>
        /// An HTTP response and the HttpClient that must be disposed with it
        /// </summary>
        private sealed class ResponseLease : IDisposable
        {
            public ResponseLease(HttpResponseMessage response, IDisposable? owner)
            {
                Response = response;
                Owner = owner;
            }

            public HttpResponseMessage Response { get; }

            public IDisposable? Owner { get; }

            public void Dispose()
            {
                Response.Dispose();
                Owner?.Dispose();
            }
        }
    }
}
