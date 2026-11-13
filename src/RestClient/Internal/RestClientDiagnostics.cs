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

namespace Mafesoft.RestClient.Internal
{
    using Microsoft.Extensions.Logging;
    using System;
    using System.Diagnostics;
    using System.Diagnostics.Metrics;
    using System.Net.Http;

    /// <summary>
    /// Logging, tracing and metrics helpers
    /// </summary>
    internal static class RestClientDiagnostics
    {
        private static readonly string? version = typeof(RestClientDiagnostics).Assembly.GetName().Version?.ToString();

        internal static readonly ActivitySource ActivitySource = new ActivitySource(RestClientTelemetry.ActivitySourceName, version);

        internal static readonly Meter Meter = new Meter(RestClientTelemetry.MeterName, version);

        // tag names: the same as the trace tags and the OpenTelemetry HTTP semantic conventions
        private const string MethodTag = "http.request.method";
        private const string StatusCodeTag = "http.response.status_code";
        private const string ErrorTypeTag = "error.type";
        private const string ClientNameTag = "restclient.client.name";

        // duration of a request as seen by the caller: one measurement per SendAsync, retries, retry delays,
        // refresh token and reading of the body included (not of a Stream content, read after SendAsync)
#if NET9_0_OR_GREATER || NETSTANDARD
        // with the bucket boundaries recommended by OpenTelemetry for http.client.request.duration
        // (the default boundaries of the OpenTelemetry SDK are for milliseconds); InstrumentAdvice is not available on net8.0
        private static readonly Histogram<double> requestDuration = Meter.CreateHistogram<double>(
            "restclient.request.duration",
            unit: "s",
            description: RequestDurationDescription,
            tags: null,
            advice: new InstrumentAdvice<double>
            {
                HistogramBucketBoundaries = new[] { 0.005, 0.01, 0.025, 0.05, 0.075, 0.1, 0.25, 0.5, 0.75, 1, 2.5, 5, 7.5, 10 }
            });
#else
        private static readonly Histogram<double> requestDuration = Meter.CreateHistogram<double>(
            "restclient.request.duration",
            unit: "s",
            description: RequestDurationDescription);
#endif

        private const string RequestDurationDescription = "Duration of the requests sent by RestClient, retries and refresh token included.";

        private static readonly Counter<long> retries = Meter.CreateCounter<long>(
            "restclient.retries",
            unit: "{retry}",
            description: "Number of requests sent again by the RestClient retry policy.");

        private static readonly Counter<long> refreshTokens = Meter.CreateCounter<long>(
            "restclient.refresh_token",
            unit: "{refresh}",
            description: "Number of invocations of the RestClient refresh token func.");

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

        internal static void Completed(Activity? activity, ILogger? logger, HttpMethod method, string url, RestResult result, TimeSpan elapsed, string? clientName)
        {
            bool recordDuration = requestDuration.Enabled;
            string? errorType = result.IsSuccessStatusCode || (activity == null && !recordDuration)
                ? null
                : result.Exception?.GetType().FullName ?? ((int)result.StatusCode).ToString();

            if (activity != null)
            {
                activity.SetTag("http.response.status_code", (int)result.StatusCode);
                activity.SetTag("rest_client.retry_count", result.RetryCount);
                if (errorType != null)
                {
                    activity.SetTag("error.type", errorType);
                    activity.SetStatus(ActivityStatusCode.Error);
                }
            }

            if (recordDuration)
            {
                TagList tags = CreateTags(method, clientName);
                tags.Add(StatusCodeTag, (int)result.StatusCode);
                if (errorType != null)
                {
                    tags.Add(ErrorTypeTag, errorType);
                }
                requestDuration.Record(elapsed.TotalSeconds, tags);
            }

            if (logger != null && logger.IsEnabled(LogLevel.Information))
            {
                requestCompleted(logger, (int)result.StatusCode, method.Method, url, elapsed.TotalMilliseconds, null);
            }
        }

        internal static void Failed(Activity? activity, ILogger? logger, HttpMethod method, string url, Exception exception, TimeSpan elapsed, string? clientName)
        {
            if (activity != null)
            {
                activity.SetTag("error.type", exception.GetType().FullName);
                activity.SetStatus(ActivityStatusCode.Error, exception.Message);
            }

            if (requestDuration.Enabled)
            {
                TagList tags = CreateTags(method, clientName);
                tags.Add(ErrorTypeTag, exception.GetType().FullName);
                requestDuration.Record(elapsed.TotalSeconds, tags);
            }

            if (logger != null && logger.IsEnabled(LogLevel.Error))
            {
                requestFailed(logger, method.Method, url, elapsed.TotalMilliseconds, exception);
            }
        }

        internal static void Retrying(ILogger? logger, HttpMethod method, string url, TimeSpan delay, int retryCount, int maxRetries, string reason, string? clientName)
        {
            Activity.Current?.AddEvent(new ActivityEvent("retry"));
            if (retries.Enabled)
            {
                retries.Add(1, CreateTags(method, clientName));
            }
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

        /// <summary>
        /// The refresh token func is being invoked (once for all the concurrent requests that received 401)
        /// </summary>
        internal static void RefreshTokenInvoked(HttpMethod method, string? clientName)
        {
            if (refreshTokens.Enabled)
            {
                refreshTokens.Add(1, CreateTags(method, clientName));
            }
        }

        private static TagList CreateTags(HttpMethod method, string? clientName)
        {
            TagList tags = new TagList { { MethodTag, method.Method } };
            if (clientName != null)
            {
                tags.Add(ClientNameTag, clientName);
            }
            return tags;
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
