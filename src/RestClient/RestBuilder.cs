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
    using Microsoft.Extensions.Logging;
    using Mafesoft.RestClient.Internal;
    using Mafesoft.RestClient.Serialization;
    using Mafesoft.RestClient.Serialization.Json;
    using Mafesoft.RestClient.Serialization.Xml;
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Globalization;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Net.Security;
    using System.Security.Cryptography.X509Certificates;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Provides a set of methods for building the requests.
    /// The builder is immutable: every method returns a new independent instance,
    /// so a configured builder can be safely reused (also concurrently) as a base for many requests.
    /// </summary>
    public sealed partial class RestBuilder
    {
        #region [ State ]

        /// <summary>
        /// The connection properties of the builder (read-only: use Url, Timeout, BufferSize and CertificateOption to change them)
        /// </summary>
        public RestProperties Properties => properties.Clone();

        private RestProperties properties;
        private List<string> commands = new List<string>();
        private List<KeyValuePair<string, string>> parameters = new List<KeyValuePair<string, string>>();
        private ISerializerContent serializer = new JSON();
        private object? serializeOptions;
        private object? deserializeOptions;
        private HttpClient? externalHttpClient;
        private Func<object, X509Certificate?, X509Chain?, SslPolicyErrors, bool>? certificateCallback;
        private ICredentials? credentials;
        private Func<AuthenticationHeaderValue>? onAuthentication;
        private Action<HttpRequestHeaders>? onRequestHeaders;
        private bool enabledCompression;
        private RefreshTokenCoordinator? refreshTokenCoordinator;
        private RetrySettings? retrySettings;
        private ILogger? logger;
        private object? payloadContent;
        private bool isEnabledFormUrlEncoded;
        private Dictionary<string, string>? formUrlEncodedKeyValues;
        private MultipartFormData? multipartFormData;

        private Action<HttpClient>? onConfigureHttpClientAction;
        private Action<StartEventArgs>? onStartAction;
        private Action<PreviewContentAsStringEventArgs>? onPreviewContentRequestAsStringAction;
        private Action<ProgressEventArgs>? onUploadProgressAction;
        private Action<ProgressEventArgs>? onDownloadProgressAction;
        private Action<PreCompletedEventArgs>? onPreCompletedAction;
        private Action<PreviewContentAsStringEventArgs>? onPreviewContentResponseAsStringAction;
        private Action<CompletedEventArgs>? onCompletedAction;
        private Action<Exception>? onExceptionAction;

        /// <summary>
        /// Initializes a new instance
        /// </summary>
        internal RestBuilder() : this(new RestProperties()) { }

        /// <summary>
        /// Initializes a new instance
        /// </summary>
        internal RestBuilder(RestProperties properties)
        {
            this.properties = properties ?? new RestProperties();
        }

        /// <summary>
        /// Creates a copy of the builder and applies the changes
        /// </summary>
        private RestBuilder With(Action<RestBuilder> change)
        {
            var result = (RestBuilder)MemberwiseClone();
            result.properties = properties.Clone();
            result.commands = new List<string>(commands);
            result.parameters = new List<KeyValuePair<string, string>>(parameters);
            change(result);
            return result;
        }

        #endregion

        #region [ HttpClient ]

        /// <summary>
        /// Configure the HttpClient created by RestClient before sending a request.
        /// It is not invoked when an external HttpClient is used (see UseHttpClient).
        /// </summary>
        /// <param name="onHttpClient"></param>
        /// <returns></returns>
        public RestBuilder OnConfigureHttpClient(Action<HttpClient>? onHttpClient)
            => With(r => r.onConfigureHttpClientAction = onHttpClient);

        /// <summary>
        /// Sends the requests using an existing HttpClient (for example created by IHttpClientFactory).
        /// The HttpClient is never disposed by RestClient. Credentials, CertificateValidation, CertificateOption,
        /// compression and OnConfigureHttpClient are not applied: configure them on your HttpClient/handler.
        /// </summary>
        /// <param name="httpClient">The HttpClient, null to go back to the HttpClient managed by RestClient.</param>
        /// <returns></returns>
        public RestBuilder UseHttpClient(HttpClient? httpClient)
            => With(r => r.externalHttpClient = httpClient);

        #endregion

        #region [ Certificate Validation ]

        /// <summary>
        /// The callback to validate a server certificate. It is applied only to the requests sent by this builder.
        /// Define the callback once (static method or field): RestClient pools the HTTP handlers by configuration,
        /// so a new closure for every request prevents connection reuse.
        /// </summary>
        /// <param name="callback"></param>
        /// <returns></returns>
        public RestBuilder CertificateValidation(Func<object, X509Certificate?, X509Chain?, SslPolicyErrors, bool>? callback)
            => With(r => r.certificateCallback = callback);

        #endregion

        #region [ Network Credential ]

        /// <summary>
        /// Authentication information used by this func
        /// </summary>
        /// <param name="credential"></param>
        /// <returns></returns>
        public RestBuilder NetworkCredential(Func<ICredentials?> credential)
        {
            if (credential == null) { throw new ArgumentNullException(nameof(credential)); }
            return With(r => r.credentials = credential());
        }

        /// <summary>
        /// Authentication information used by username and password
        /// </summary>
        /// <param name="username"></param>
        /// <param name="password"></param>
        /// <returns></returns>
        public RestBuilder NetworkCredential(string username, string password)
            => With(r => r.credentials = new NetworkCredential(username, password));

        /// <summary>
        /// Authentication information used by username, password and domain
        /// </summary>
        /// <param name="username"></param>
        /// <param name="password"></param>
        /// <param name="domain"></param>
        /// <returns></returns>
        public RestBuilder NetworkCredential(string username, string password, string domain)
            => With(r => r.credentials = new NetworkCredential(username, password, domain));

        /// <summary>
        /// Authentication information used by username and secure string password
        /// </summary>
        /// <param name="username"></param>
        /// <param name="password"></param>
        /// <returns></returns>
        public RestBuilder NetworkCredential(string username, System.Security.SecureString password)
            => With(r => r.credentials = new NetworkCredential(username, password));

        /// <summary>
        /// Authentication information used by username, secure string password and domain
        /// </summary>
        /// <param name="username"></param>
        /// <param name="password"></param>
        /// <param name="domain"></param>
        /// <returns></returns>
        public RestBuilder NetworkCredential(string username, System.Security.SecureString password, string domain)
            => With(r => r.credentials = new NetworkCredential(username, password, domain));

        #endregion

        #region [ Authentication ]

        /// <summary>
        /// The Authorization header for an HTTP request. The func is invoked for every request,
        /// so after a refresh token the new value is used.
        /// </summary>
        /// <param name="authentication"></param>
        /// <returns></returns>
        public RestBuilder Authentication(Func<AuthenticationHeaderValue>? authentication)
            => With(r => r.onAuthentication = authentication);

        /// <summary>
        /// The Authorization header for an HTTP request
        /// </summary>
        /// <param name="scheme"></param>
        /// <returns></returns>
        public RestBuilder Authentication(string scheme)
            => Authentication(() => new AuthenticationHeaderValue(scheme));

        /// <summary>
        /// The Authorization header for an HTTP request
        /// </summary>
        /// <param name="scheme"></param>
        /// <param name="parameter"></param>
        /// <returns></returns>
        public RestBuilder Authentication(string scheme, string? parameter)
            => Authentication(() => new AuthenticationHeaderValue(scheme, parameter));

        #endregion

        #region [ Timeout & Buffer Size ]

        /// <summary>
        /// Sets the timespan to wait before an attempt times out (System.Threading.Timeout.InfiniteTimeSpan to disable it).
        /// For Stream results the timeout covers the response headers, not the reading of the stream.
        /// </summary>
        /// <param name="timeOut"></param>
        /// <returns></returns>
        public RestBuilder Timeout(TimeSpan timeOut)
            => With(r => r.properties.Timeout = timeOut);

        /// <summary>
        /// Sets the timespan to wait before an attempt times out
        /// </summary>
        /// <param name="milliseconds"></param>
        /// <returns></returns>
        public RestBuilder Timeout(double milliseconds)
            => Timeout(TimeSpan.FromMilliseconds(milliseconds));

        /// <summary>
        /// Sets the buffer size used to upload and download.
        /// </summary>
        /// <param name="bufferSize"></param>
        /// <returns></returns>
        public RestBuilder BufferSize(int bufferSize)
        {
            if (bufferSize <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(bufferSize));
            }

            return With(r => r.properties.BufferSize = bufferSize);
        }

        /// <summary>
        /// Specifies how the client certificates are provided (default Manual).
        /// It is not applied when an external HttpClient is used (see UseHttpClient).
        /// </summary>
        /// <param name="certificateOption"></param>
        /// <returns></returns>
        public RestBuilder CertificateOption(ClientCertificateOption certificateOption)
            => With(r => r.properties.CertificateOption = certificateOption);

        #endregion

        #region [ Header ]

        /// <summary>
        /// Sets the headers which should be sent with this or children requests.
        /// Calling Header more than once combines the actions.
        /// </summary>
        /// <param name="requestHeaders"></param>
        /// <returns></returns>
        public RestBuilder Header(Action<HttpRequestHeaders> requestHeaders)
            => With(r => r.onRequestHeaders = (Action<HttpRequestHeaders>?)Delegate.Combine(onRequestHeaders, requestHeaders));

        /// <summary>
        /// Adds a header which should be sent with this or children requests.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public RestBuilder Header(string name, string? value)
            => Header(h => h.TryAddWithoutValidation(name, value));

        #endregion

        #region [ Compression ]

        /// <summary>
        /// Enables automatic response decompression (gzip, deflate and, on .NET 8+, brotli)
        /// </summary>
        /// <returns></returns>
        public RestBuilder EnableGZipCompression()
            => With(r => r.enabledCompression = true);

        #endregion

        #region [ Command ]

        /// <summary>
        /// Adds the command (url segment) to request
        /// </summary>
        /// <param name="command"></param>
        /// <returns></returns>
        public RestBuilder Command(string command)
        {
            if (command == null) { throw new ArgumentNullException(nameof(command)); }
            string prefix = !command.StartsWith("/", StringComparison.Ordinal) ? "/" : string.Empty;
            return With(r => r.commands.Add($"{prefix}{command}"));
        }

        /// <summary>
        /// Adds the command (url segment) to request
        /// </summary>
        /// <param name="command"></param>
        /// <returns></returns>
        public RestBuilder Command(int command)
            => Command(command.ToString(CultureInfo.InvariantCulture));

        /// <summary>
        /// Adds the command (url segment) to request
        /// </summary>
        /// <param name="command"></param>
        /// <returns></returns>
        public RestBuilder Command(long command)
            => Command(command.ToString(CultureInfo.InvariantCulture));

        /// <summary>
        /// Adds the command (url segment) to request
        /// </summary>
        /// <param name="command"></param>
        /// <returns></returns>
        public RestBuilder Command(Guid command)
            => Command(command.ToString());

        #endregion

        #region [ Refresh Token & Retry ]

        /// <summary>
        /// Enables the refresh token: when the response is 401 Unauthorized the func is invoked and,
        /// if it returns a success status code, the request is sent again (only once).
        /// When many concurrent requests of this builder (and of the builders derived from it) receive 401
        /// the func is invoked only once. The requests sent from inside the func never trigger another refresh.
        /// </summary>
        /// <param name="refreshToken">
        /// The func that refreshes the token; it receives the cancellation token of the request.
        /// null disables the refresh token for this builder and the builders derived from it.
        /// </param>
        /// <returns></returns>
        public RestBuilder RefreshToken(Func<CancellationToken, Task<RestResult>>? refreshToken)
            => With(r => r.refreshTokenCoordinator = refreshToken == null ? null : new RefreshTokenCoordinator(refreshToken));

        /// <summary>
        /// Retries the request when it fails with a transient error: network error, timeout,
        /// 408 Request Timeout, 429 Too Many Requests, 500, 502, 503, 504.
        /// The delay is exponential with jitter (delay, 2*delay, 4*delay, ...) or the one requested by the Retry-After header.
        /// </summary>
        /// <param name="maxRetries">Maximum number of retries (0 disables the retry)</param>
        /// <param name="delay">Base delay, 200 milliseconds when null</param>
        /// <param name="retryNonIdempotentMethods">
        /// false (default) retries only idempotent methods (GET, HEAD, OPTIONS, PUT, DELETE, TRACE): a POST/PATCH could be executed twice.
        /// </param>
        /// <returns></returns>
        public RestBuilder Retry(int maxRetries, TimeSpan? delay = null, bool retryNonIdempotentMethods = false)
        {
            if (maxRetries < 0) { throw new ArgumentOutOfRangeException(nameof(maxRetries)); }
            if (delay < TimeSpan.Zero) { throw new ArgumentOutOfRangeException(nameof(delay)); }
            return With(r => r.retrySettings = maxRetries == 0
                ? null
                : new RetrySettings(maxRetries, delay ?? TimeSpan.FromMilliseconds(200), retryNonIdempotentMethods));
        }

        #endregion

        #region [ Logging ]

        /// <summary>
        /// Logs the requests (Debug), the responses (Information), the retries and the errors.
        /// The query string is never logged because it can contain secrets.
        /// </summary>
        /// <param name="logger">The logger, null to disable logging</param>
        /// <returns></returns>
        public RestBuilder Logger(ILogger? logger)
            => With(r => r.logger = logger);

        #endregion

        #region [ Serialization ]

        /// <summary>
        /// Sets xml as serialization. XmlSerializer is not compatible with trimming and Native AOT.
        /// </summary>
        /// <param name="readerOptions">Settings used to read (deserialize) the response</param>
        /// <param name="writerOptions">Settings used to write (serialize) the payload</param>
        /// <returns></returns>
#if NET
        [RequiresUnreferencedCode(XML.ReflectionMessage)]
        [RequiresDynamicCode(XML.ReflectionMessage)]
#endif
        public RestBuilder Xml(System.Xml.XmlReaderSettings? readerOptions = null, System.Xml.XmlWriterSettings? writerOptions = null)
        {
            XML xml = new XML();
            return With(r =>
            {
                r.serializer = xml;
                r.serializeOptions = writerOptions;
                r.deserializeOptions = readerOptions;
            });
        }

        /// <summary>
        /// Sets json as serialization (System.Text.Json). When options is null JSON.DefaultOptions (web defaults) are used.
        /// </summary>
        /// <returns></returns>
        public RestBuilder Json(JsonSerializerOptions? options = null)
            => With(r =>
            {
                r.serializer = new JSON();
                r.serializeOptions = options;
                r.deserializeOptions = options;
            });

        /// <summary>
        /// Sets json as serialization using a System.Text.Json source generated context:
        /// no reflection is used, so it is compatible with trimming and Native AOT.
        /// </summary>
        /// <param name="context">The JsonSerializerContext that contains all the types sent and received</param>
        /// <returns></returns>
        public RestBuilder Json(JsonSerializerContext context)
        {
            if (context == null) { throw new ArgumentNullException(nameof(context)); }
            return Json(context.Options);
        }

        /// <summary>
        /// Sets custom serialization. Implement IAsyncSerializerContent to deserialize directly from the response stream.
        /// </summary>
        /// <param name="serializer"></param>
        /// <param name="options">Options passed to the serializer</param>
        /// <returns></returns>
        public RestBuilder CustomSerializer(ISerializerContent serializer, object? options = null)
        {
            if (serializer == null) { throw new ArgumentNullException(nameof(serializer)); }
            return With(r =>
            {
                r.serializer = serializer;
                r.serializeOptions = options;
                r.deserializeOptions = options;
            });
        }

        #endregion

        #region [ Parameter ]

        /// <summary>
        /// Sets a query string parameter, replacing the previous values of the key.
        /// Key and value are url-encoded and the value is formatted with the invariant culture.
        /// When the value is a collection (not a string) the key is repeated for each item: ?id=1&amp;id=2
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public RestBuilder Parameter(string key, object? value)
            => Parameter(new RestParameter(key, value));

        /// <summary>
        /// Sets a list of query string parameters, replacing the previous values of the keys.
        /// Parameters with the same key in the same call are all added: ?id=1&amp;id=2
        /// </summary>
        /// <param name="parameter"></param>
        /// <param name="others"></param>
        /// <returns></returns>
        public RestBuilder Parameter(RestParameter parameter, params RestParameter[] others)
            => SetParameters(new[] { parameter }.Concat(others ?? Array.Empty<RestParameter>()).ToList());

        /// <summary>
        /// Sets a list of query string parameters, replacing the previous values of the keys.
        /// Parameters with the same key in the same call are all added: ?id=1&amp;id=2
        /// </summary>
        /// <param name="parameters"></param>
        /// <returns></returns>
        public RestBuilder Parameter(Action<List<RestParameter>> parameters)
        {
            if (parameters == null) { throw new ArgumentNullException(nameof(parameters)); }
            List<RestParameter> list = new List<RestParameter>();
            parameters(list);
            return SetParameters(list);
        }

        private RestBuilder SetParameters(List<RestParameter> list)
        {
            if (list.Any(p => !p.HasKey)) { throw new ArgumentException("The parameter key cannot be null."); }

            var expanded = list.SelectMany(p => ExpandValues(p.Value).Select(v => new KeyValuePair<string, string>(p.Key!, v))).ToList();
            var keys = new HashSet<string>(list.Select(p => p.Key!), StringComparer.Ordinal);

            return With(r =>
            {
                r.parameters.RemoveAll(p => keys.Contains(p.Key));
                r.parameters.AddRange(expanded);
            });
        }

        private static IEnumerable<string> ExpandValues(object? value)
        {
            if (value is IEnumerable enumerable && !(value is string))
            {
                foreach (object? item in enumerable)
                {
                    yield return FormatValue(item);
                }
            }
            else
            {
                yield return FormatValue(value);
            }
        }

        /// <summary>
        /// Formats a value using the invariant culture (dates as ISO 8601, booleans lowercase)
        /// </summary>
        private static string FormatValue(object? value)
        {
            switch (value)
            {
                case null: return string.Empty;
                case string s: return s;
                case bool b: return b ? "true" : "false";
                case DateTime dt: return dt.ToString("O", CultureInfo.InvariantCulture);
                case DateTimeOffset dto: return dto.ToString("O", CultureInfo.InvariantCulture);
                case IFormattable f: return f.ToString(null, CultureInfo.InvariantCulture);
                default: return value.ToString() ?? string.Empty;
            }
        }

        #endregion

        #region [ Url ]

        /// <summary>
        /// Sets url
        /// </summary>
        /// <param name="url">The URL represents a base url</param>
        /// <returns></returns>
        public RestBuilder Url(string url)
            => Url(new Uri(url));

        /// <summary>
        /// Sets Uri
        /// </summary>
        /// <param name="uri">The URI represents a base url</param>
        /// <returns></returns>
        public RestBuilder Url(Uri uri)
        {
            if (uri == null) { throw new ArgumentNullException(nameof(uri)); }
            return With(r => r.properties.EndPoint = uri);
        }

        #endregion

        #region [ Payload, FormUrlEncoded & Multipart ]

        /// <summary>
        /// Sets the payload, serialized with the configured serializer (JSON by default)
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="payload"></param>
        /// <returns></returns>
        public RestBuilder Payload<T>(T payload)
            => With(r =>
            {
                r.payloadContent = payload;
                r.multipartFormData = null;
            });

        /// <summary>
        /// Enable form Url Encoding
        /// </summary>
        /// <param name="enableFormUrlEncoded"></param>
        /// <returns></returns>
        public RestBuilder EnableFormUrlEncoded(bool enableFormUrlEncoded = true)
            => With(r => r.isEnabledFormUrlEncoded = enableFormUrlEncoded);

        /// <summary>
        /// Sets x-www-form-urlencoded key values and enables form url encoding
        /// </summary>
        /// <param name="keyValues"></param>
        /// <returns></returns>
        public RestBuilder FormUrlEncoded(Dictionary<string, string> keyValues)
            => FormUrlEncoded(true, keyValues);

        /// <summary>
        /// Sets x-www-form-urlencoded key values
        /// </summary>
        /// <param name="enableFormUrlEncoded"></param>
        /// <param name="keyValues"></param>
        /// <returns></returns>
        public RestBuilder FormUrlEncoded(bool enableFormUrlEncoded, Dictionary<string, string>? keyValues)
        {
            var copy = keyValues == null ? null : new Dictionary<string, string>(keyValues);
            return With(r =>
            {
                r.isEnabledFormUrlEncoded = enableFormUrlEncoded;
                r.formUrlEncodedKeyValues = copy;
                r.multipartFormData = null;
            });
        }

        /// <summary>
        /// Sets x-www-form-urlencoded key values and enables form url encoding
        /// </summary>
        /// <param name="keyValues"></param>
        /// <returns></returns>
        public RestBuilder FormUrlEncoded(Action<Dictionary<string, string>> keyValues)
            => FormUrlEncoded(true, keyValues);

        /// <summary>
        /// Sets x-www-form-urlencoded key values
        /// </summary>
        /// <param name="enableFormUrlEncoded"></param>
        /// <param name="keyValues"></param>
        /// <returns></returns>
        public RestBuilder FormUrlEncoded(bool enableFormUrlEncoded, Action<Dictionary<string, string>> keyValues)
        {
            if (keyValues == null) { throw new ArgumentNullException(nameof(keyValues)); }
            var values = new Dictionary<string, string>();
            keyValues(values);
            return FormUrlEncoded(enableFormUrlEncoded, values);
        }

        /// <summary>
        /// Sends a multipart/form-data content (fields and files).
        /// </summary>
        /// <param name="configure">Adds the fields and the files</param>
        /// <returns></returns>
        public RestBuilder Multipart(Action<MultipartFormData> configure)
        {
            if (configure == null) { throw new ArgumentNullException(nameof(configure)); }
            MultipartFormData multipart = new MultipartFormData();
            configure(multipart);
            return With(r =>
            {
                r.multipartFormData = multipart;
                r.isEnabledFormUrlEncoded = false;
                r.payloadContent = null;
            });
        }

        #endregion

        #region [ Events ]

        /// <summary>
        /// Sets onStart action and occurs when the request is started
        /// </summary>
        /// <param name="onStart"></param>
        /// <returns></returns>
        public RestBuilder OnStart(Action<StartEventArgs>? onStart)
            => With(r => r.onStartAction = onStart);

        /// <summary>
        ///  Sets OnPreviewContentRequestAsString, displays the request content as string
        /// </summary>
        /// <param name="onPreviewContent"></param>
        /// <returns></returns>
        public RestBuilder OnPreviewContentRequestAsString(Action<PreviewContentAsStringEventArgs>? onPreviewContent)
            => With(r => r.onPreviewContentRequestAsStringAction = onPreviewContent);

        /// <summary>
        /// Sets onUploadProgress action, occurs when the request is processing
        /// </summary>
        /// <param name="onProgress"></param>
        /// <returns></returns>
        public RestBuilder OnUploadProgress(Action<ProgressEventArgs>? onProgress)
            => With(r => r.onUploadProgressAction = onProgress);

        /// <summary>
        /// Sets onDownloadProgress action, occurs when the response is processing (also while reading a Stream result)
        /// </summary>
        /// <param name="onProgress"></param>
        /// <returns></returns>
        public RestBuilder OnDownloadProgress(Action<ProgressEventArgs>? onProgress)
            => With(r => r.onDownloadProgressAction = onProgress);

        /// <summary>
        /// Sets OnPreCompleted, occurs when the result of request il builded and it isn't completed yet
        /// </summary>
        /// <param name="onPreCompleted"></param>
        /// <returns></returns>
        public RestBuilder OnPreCompleted(Action<PreCompletedEventArgs>? onPreCompleted)
            => With(r => r.onPreCompletedAction = onPreCompleted);

        /// <summary>
        /// Sets OnPreviewContentResponseAsString, displays the response as string.
        /// When it is set the response is buffered as string before being deserialized.
        /// </summary>
        /// <param name="onPreviewContent"></param>
        /// <returns></returns>
        public RestBuilder OnPreviewContentResponseAsString(Action<PreviewContentAsStringEventArgs>? onPreviewContent)
            => With(r => r.onPreviewContentResponseAsStringAction = onPreviewContent);

        /// <summary>
        /// Sets onCompleted action, occurs when the call is completed.
        /// </summary>
        /// <param name="onCompleted"></param>
        /// <returns></returns>
        public RestBuilder OnCompleted(Action<CompletedEventArgs>? onCompleted)
            => With(r => r.onCompletedAction = onCompleted);

        /// <summary>
        /// Sets onException, occurs when there is an exception during the call or while reading the response
        /// </summary>
        /// <param name="exception"></param>
        /// <returns></returns>
        public RestBuilder OnException(Action<Exception>? exception)
            => With(r => r.onExceptionAction = exception);

        #endregion
    }
}
