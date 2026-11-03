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

namespace RestClient.Internal
{
    using Microsoft.Extensions.Logging;
    using System;
    using System.Diagnostics;
    using System.Net.Http;

    /// <summary>
    /// Logging and tracing helpers
    /// </summary>
    internal static class RestClientDiagnostics
    {
        internal static readonly ActivitySource ActivitySource = new ActivitySource(
            RestClientTelemetry.ActivitySourceName,
            typeof(RestClientDiagnostics).Assembly.GetName().Version?.ToString());

        private static readonly Action<ILogger, string, string, Exception?> requestStarting = LoggerMessage.Define<string, string>(
            LogLevel.Debug, new EventId(1, "RequestStarting"), "Sending HTTP {Method} {Url}");

        private static readonly Action<ILogger, int, string, string, double, Exception?> requestCompleted = LoggerMessage.Define<int, string, string, double>(
            LogLevel.Information, new EventId(2, "RequestCompleted"), "Received HTTP {StatusCode} for {Method} {Url} in {ElapsedMilliseconds}ms");

        private static readonly Action<ILogger, string, string, double, Exception?> requestFailed = LoggerMessage.Define<string, string, double>(
            LogLevel.Error, new EventId(3, "RequestFailed"), "HTTP {Method} {Url} failed after {ElapsedMilliseconds}ms");

        private static readonly Action<ILogger, string, string, double, int, int, string, Exception?> retrying = LoggerMessage.Define<string, string, double, int, int, string>(
            LogLevel.Warning, new EventId(4, "Retrying"), "Retrying HTTP {Method} {Url} in {DelayMilliseconds}ms (retry {RetryCount} of {MaxRetries}): {Reason}");

        private static readonly Action<ILogger, string, string, Exception?> refreshingToken = LoggerMessage.Define<string, string>(
            LogLevel.Information, new EventId(5, "RefreshingToken"), "HTTP {Method} {Url} returned 401 Unauthorized, refreshing the token");

        private static readonly Action<ILogger, string, string, string, Exception?> contentError = LoggerMessage.Define<string, string, string>(
            LogLevel.Warning, new EventId(6, "ContentError"), "Cannot read the response of HTTP {Method} {Url} as {Type}");

        /// <summary>
        /// Removes the query string (it can contain secrets) and the user info from the url
        /// </summary>
        internal static string RedactUrl(string url)
        {
            int query = url.IndexOf('?');
            string withoutQuery = query >= 0 ? url.Substring(0, query) : url;
            if (Uri.TryCreate(withoutQuery, UriKind.Absolute, out Uri? uri) && !string.IsNullOrEmpty(uri.UserInfo))
            {
                return withoutQuery.Replace(uri.UserInfo + "@", string.Empty);
            }
            return withoutQuery;
        }

        internal static Activity? StartActivity(HttpMethod method)
        {
            Activity? activity = ActivitySource.StartActivity($"RestClient {method.Method}", ActivityKind.Internal);
            activity?.SetTag("http.request.method", method.Method);
            return activity;
        }

        internal static void Starting(Activity? activity, ILogger? logger, HttpMethod method, string url)
        {
            activity?.SetTag("url.full", url);
            if (logger != null && logger.IsEnabled(LogLevel.Debug))
            {
                requestStarting(logger, method.Method, url, null);
            }
        }

        internal static void Completed(Activity? activity, ILogger? logger, HttpMethod method, string url, RestResult result, TimeSpan elapsed)
        {
            if (activity != null)
            {
                activity.SetTag("http.response.status_code", (int)result.StatusCode);
                activity.SetTag("rest_client.retry_count", result.RetryCount);
                if (!result.IsSuccessStatusCode)
                {
                    activity.SetTag("error.type", result.Exception?.GetType().FullName ?? ((int)result.StatusCode).ToString());
                    activity.SetStatus(ActivityStatusCode.Error);
                }
            }

            if (logger != null && logger.IsEnabled(LogLevel.Information))
            {
                requestCompleted(logger, (int)result.StatusCode, method.Method, url, elapsed.TotalMilliseconds, null);
            }
        }

        internal static void Failed(Activity? activity, ILogger? logger, HttpMethod method, string url, Exception exception, TimeSpan elapsed)
        {
            if (activity != null)
            {
                activity.SetTag("error.type", exception.GetType().FullName);
                activity.SetStatus(ActivityStatusCode.Error, exception.Message);
            }

            if (logger != null && logger.IsEnabled(LogLevel.Error))
            {
                requestFailed(logger, method.Method, url, elapsed.TotalMilliseconds, exception);
            }
        }

        internal static void Retrying(ILogger? logger, HttpMethod method, string url, TimeSpan delay, int retryCount, int maxRetries, string reason)
        {
            Activity.Current?.AddEvent(new ActivityEvent("retry"));
            if (logger != null && logger.IsEnabled(LogLevel.Warning))
            {
                retrying(logger, method.Method, url, delay.TotalMilliseconds, retryCount, maxRetries, reason, null);
            }
        }

        internal static void RefreshingToken(ILogger? logger, HttpMethod method, string url)
        {
            Activity.Current?.AddEvent(new ActivityEvent("refresh_token"));
            if (logger != null && logger.IsEnabled(LogLevel.Information))
            {
                refreshingToken(logger, method.Method, url, null);
            }
        }

        internal static void ContentError(ILogger? logger, HttpMethod method, string url, Type type, Exception exception)
        {
            if (logger != null && logger.IsEnabled(LogLevel.Warning))
            {
                contentError(logger, method.Method, url, type.Name, exception);
            }
        }
    }
}
