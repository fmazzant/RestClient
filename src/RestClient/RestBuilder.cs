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
    using RestClient.Generic;
    using RestClient.Http;
    using RestClient.Serialization;
    using RestClient.Serialization.Json;
    using RestClient.Serialization.Xml;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Net.Security;
    using System.Security.Cryptography.X509Certificates;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Provides a set of methods for building the requests.
    /// The builder is immutable: every method returns a new independent instance,
    /// so a configured builder can be safely reused as a base for many requests.
    /// </summary>
    public sealed class RestBuilder
    {
        #region [ State ]

        /// <summary>
        /// PATCH http method
        /// </summary>
        private static readonly HttpMethod PATCH = new HttpMethod("PATCH");

        /// <summary>
        /// Rest Properties
        /// </summary>
        public RestProperties Properties { get; private set; }

        /// <summary>
        /// List of commands
        /// </summary>
        private List<string> commands = new List<string>();

        /// <summary>
        /// List of parameters
        /// </summary>
        private Dictionary<string, string> parameters = new Dictionary<string, string>();

        /// <summary>
        /// Serialization kind. JSON is default value
        /// </summary>
        private ISerializerContent serializer = new JSON();

        /// <summary>
        /// Serialization options
        /// </summary>
        private object serializeOptions;

        /// <summary>
        /// Deserialization options
        /// </summary>
        private object deserializeOptions;

        /// <summary>
        /// External HttpClient: when set it is used (and never disposed) instead of creating a new one per request
        /// </summary>
        private HttpClient externalHttpClient;

        /// <summary>
        /// Certificate Callback
        /// </summary>
        private Func<object, X509Certificate, X509Chain, SslPolicyErrors, bool> certificateCallback;

        /// <summary>
        /// Current Credentials
        /// </summary>
        private ICredentials credentials;

        /// <summary>
        /// Authentication header factory, invoked for every request
        /// </summary>
        private Func<AuthenticationHeaderValue> onAuthentication;

        /// <summary>
        /// Request headers configuration
        /// </summary>
        private Action<HttpRequestHeaders> onRequestHeaders;

        /// <summary>
        /// true if compression is enabled, false otherwise
        /// </summary>
        private bool enabledCompression;

        /// <summary>
        /// if true the refresh token execution is enabled
        /// </summary>
        private bool refreshTokenExecution = true;

        /// <summary>
        /// Func refresh token
        /// </summary>
        private Func<RestResult> refreshTokenApi;

        /// <summary>
        /// Func refresh token async
        /// </summary>
        private Func<Task<RestResult>> refreshTokenApiAsync;

        /// <summary>
        /// Payload content
        /// </summary>
        private object payloadContent;

        /// <summary>
        /// Defines is enabled x-form-urlencoded
        /// </summary>
        private bool isEnabledFormUrlEncoded;

        /// <summary>
        /// Params for x-www-form-urlencoded
        /// </summary>
        private Dictionary<string, string> formUrlEncodedKeyValues;

        private Action<HttpClient> onConfigureHttpClientAction;
        private Action<StartEventArgs> onStartAction;
        private Action<PreviewContentAsStringEventArgs> onPreviewContentRequestAsStringAction;
        private Action<ProgressEventArgs> onUploadProgressAction;
        private Action<ProgressEventArgs> onDownloadProgressAction;
        private Action<PreCompletedEventArgs> onPreCompletedAction;
        private Action<PreviewContentAsStringEventArgs> onPreviewContentResponseAsStringAction;
        private Action<CompletedEventArgs> onCompletedAction;
        private Action<Exception> onExceptionAction;

        /// <summary>
        /// Initializes a new instance
        /// </summary>
        internal RestBuilder() : this(new RestProperties()) { }

        /// <summary>
        /// Initializes a new instance
        /// </summary>
        internal RestBuilder(RestProperties properties)
        {
            Properties = properties ?? new RestProperties();
        }

        /// <summary>
        /// Creates an independent copy of the builder
        /// </summary>
        /// <returns></returns>
        private RestBuilder Clone()
        {
            var result = (RestBuilder)MemberwiseClone();
            result.Properties = Properties.Clone();
            result.commands = new List<string>(commands);
            result.parameters = new Dictionary<string, string>(parameters);
            result.formUrlEncodedKeyValues = formUrlEncodedKeyValues == null
                ? null
                : new Dictionary<string, string>(formUrlEncodedKeyValues);
            return result;
        }

        /// <summary>
        /// Creates a copy of the builder and applies the changes
        /// </summary>
        private RestBuilder With(Action<RestBuilder> change)
        {
            var result = Clone();
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
        public RestBuilder OnConfigureHttpClient(Action<HttpClient> onHttpClient)
            => With(r => r.onConfigureHttpClientAction = onHttpClient);

        /// <summary>
        /// Sends the requests using an existing HttpClient (for example created by IHttpClientFactory).
        /// The HttpClient is never disposed by RestClient. Credentials, CertificateValidation, CertificateOption,
        /// compression and OnConfigureHttpClient are not applied: configure them on your HttpClient/handler.
        /// </summary>
        /// <param name="httpClient">The HttpClient, null to go back to an HttpClient created per request.</param>
        /// <returns></returns>
        public RestBuilder UseHttpClient(HttpClient httpClient)
            => With(r => r.externalHttpClient = httpClient);

        #endregion

        #region [ Certificate Validation ]

        /// <summary>
        /// The callback to validate a server certificate. It is applied only to the requests sent by this builder.
        /// </summary>
        /// <param name="callback"></param>
        /// <returns></returns>
        public RestBuilder CertificateValidation(Func<object, X509Certificate, X509Chain, SslPolicyErrors, bool> callback)
            => With(r => r.certificateCallback = callback);

        #endregion

        #region [ Network Credential ]

        /// <summary>
        /// Authentication information used by this func
        /// </summary>
        /// <param name="credential"></param>
        /// <returns></returns>
        public RestBuilder NetworkCredential(Func<NetworkCredential> credential)
            => With(r => r.credentials = credential?.Invoke());

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
        public RestBuilder Authentication(Func<AuthenticationHeaderValue> authentication)
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
        public RestBuilder Authentication(string scheme, string parameter)
            => Authentication(() => new AuthenticationHeaderValue(scheme, parameter));

        #endregion

        #region [ Timeout ]

        /// <summary>
        /// Sets the timespan to wait before the request times out
        /// </summary>
        /// <param name="timeOut"></param>
        /// <returns></returns>
        public RestBuilder Timeout(TimeSpan timeOut)
            => With(r => r.Properties.Timeout = timeOut);

        /// <summary>
        /// Sets the timespan to wait before the request times out
        /// </summary>
        /// <param name="milliseconds"></param>
        /// <returns></returns>
        public RestBuilder Timeout(double milliseconds)
            => Timeout(TimeSpan.FromMilliseconds(milliseconds));

        #endregion

        #region [ Buffer Size ]

        /// <summary>
        /// Sets the buffer size.
        /// </summary>
        /// <param name="bufferSize"></param>
        /// <returns></returns>
        public RestBuilder BufferSize(int bufferSize)
        {
            if (bufferSize <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(bufferSize));
            }

            return With(r => r.Properties.BufferSize = bufferSize);
        }

        #endregion

        #region [ Header ]

        /// <summary>
        /// Sets the headers which should be sent with this or children requests.
        /// Calling Header more than once combines the actions.
        /// </summary>
        /// <param name="requestHeaders"></param>
        /// <returns></returns>
        public RestBuilder Header(Action<HttpRequestHeaders> requestHeaders)
            => With(r => r.onRequestHeaders = (Action<HttpRequestHeaders>)Delegate.Combine(onRequestHeaders, requestHeaders));

        /// <summary>
        /// Adds a header which should be sent with this or children requests.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public RestBuilder Header(string name, string value)
            => Header(h => h.TryAddWithoutValidation(name, value));

        #endregion

        #region [ Compression ]

        /// <summary>
        /// Enables automatic response decompression (gzip, deflate and, on .NET, brotli)
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
        public RestBuilder Command(uint command)
            => Command(command.ToString(CultureInfo.InvariantCulture));

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

        #region [ Refresh Token Invoke ]

        /// <summary>
        /// Enables refresh token: true if enabled, false when disabled
        /// </summary>
        /// <param name="refreshToken"></param>
        /// <returns></returns>
        public RestBuilder RefreshToken(bool refreshToken = true)
            => With(r => r.refreshTokenExecution = refreshToken);

        /// <summary>
        /// Sets the func invoked when the response is 401 Unauthorized.
        /// If the func returns a success status code the request is sent again (only once).
        /// </summary>
        /// <param name="refreshTokenApi"></param>
        /// <returns></returns>
        public RestBuilder RefreshTokenInvoke(Func<RestResult> refreshTokenApi)
            => With(r => r.refreshTokenApi = refreshTokenApi);

        /// <summary>
        /// Sets the func invoked when the response is 401 Unauthorized.
        /// If the func returns a success status code the request is sent again (only once).
        /// </summary>
        /// <param name="refreshTokenApi"></param>
        /// <returns></returns>
        public RestBuilder RefreshTokenInvoke(Func<Task<RestResult>> refreshTokenApi)
            => With(r => r.refreshTokenApiAsync = refreshTokenApi);

        #endregion

        #region [ Serialization ]

        /// <summary>
        /// Sets xml as serialization
        /// </summary>
        /// <param name="readerOptions">Settings used to read (deserialize) the response</param>
        /// <param name="writerOptions">Settings used to write (serialize) the payload</param>
        /// <returns></returns>
        public RestBuilder Xml(System.Xml.XmlReaderSettings readerOptions = null, System.Xml.XmlWriterSettings writerOptions = null)
            => With(r =>
            {
                r.serializer = new XML();
                r.serializeOptions = writerOptions;
                r.deserializeOptions = readerOptions;
            });

        /// <summary>
        /// Sets json as serialization (System.Text.Json). When options is null JSON.DefaultOptions (web defaults) are used.
        /// </summary>
        /// <returns></returns>
        public RestBuilder Json(System.Text.Json.JsonSerializerOptions options = null)
            => With(r =>
            {
                r.serializer = new JSON();
                r.serializeOptions = options;
                r.deserializeOptions = options;
            });

        /// <summary>
        /// Sets custom serialization
        /// </summary>
        /// <param name="serializer"></param>
        /// <param name="options">Options passed to the serializer</param>
        /// <returns></returns>
        public RestBuilder CustomSerializer(ISerializerContent serializer, object options = null)
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
        /// Adds parameter to querystring. Key and value are url-encoded and the value is formatted with the invariant culture.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public RestBuilder Parameter(string key, object value)
        {
            if (key == null) { throw new ArgumentNullException(nameof(key)); }
            return With(r => r.parameters[key] = FormatValue(value));
        }

        /// <summary>
        /// Adds list of rest parameter to querystring
        /// </summary>
        /// <param name="parameter"></param>
        /// <param name="others"></param>
        /// <returns></returns>
        public RestBuilder Parameter(RestParameter parameter, params RestParameter[] others)
            => With(r =>
            {
                r.AddParameter(parameter);
                foreach (RestParameter p in others ?? Array.Empty<RestParameter>())
                {
                    r.AddParameter(p);
                }
            });

        /// <summary>
        /// Adds list of rest parameter to querystring
        /// </summary>
        /// <param name="parameters"></param>
        /// <returns></returns>
        public RestBuilder Parameter(Action<List<RestParameter>> parameters)
        {
            if (parameters == null) { throw new ArgumentNullException(nameof(parameters)); }
            List<RestParameter> list = new List<RestParameter>();
            parameters(list);
            return With(r => list.ForEach(r.AddParameter));
        }

        private void AddParameter(RestParameter parameter)
        {
            if (!parameter.HasKey) { throw new ArgumentException("The parameter key cannot be null.", nameof(parameter)); }
            parameters[parameter.Key] = FormatValue(parameter.Value);
        }

        /// <summary>
        /// Formats a value using the invariant culture (dates as ISO 8601, booleans lowercase)
        /// </summary>
        private static string FormatValue(object value)
        {
            switch (value)
            {
                case null: return string.Empty;
                case string s: return s;
                case bool b: return b ? "true" : "false";
                case DateTime dt: return dt.ToString("O", CultureInfo.InvariantCulture);
                case DateTimeOffset dto: return dto.ToString("O", CultureInfo.InvariantCulture);
                case IFormattable f: return f.ToString(null, CultureInfo.InvariantCulture);
                default: return value.ToString();
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
            => With(r => r.Properties.EndPoint = uri);

        #endregion

        #region [ Payload & FormUrlEncoded ]

        /// <summary>
        /// Sets payload as generic type
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="payload"></param>
        /// <returns></returns>
        public RestBuilder Payload<T>(T payload)
            => With(r => r.payloadContent = payload);

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
        public RestBuilder FormUrlEncoded(bool enableFormUrlEncoded, Dictionary<string, string> keyValues)
            => With(r =>
            {
                r.isEnabledFormUrlEncoded = enableFormUrlEncoded;
                r.formUrlEncodedKeyValues = keyValues == null ? null : new Dictionary<string, string>(keyValues);
            });

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

        #endregion

        #region [ Events ]

        /// <summary>
        /// Sets onStart action and occurs when the request is started
        /// </summary>
        /// <param name="onStart"></param>
        /// <returns></returns>
        public RestBuilder OnStart(Action<StartEventArgs> onStart)
            => With(r => r.onStartAction = onStart);

        /// <summary>
        ///  Sets OnPreviewContentRequestAsString, displays the request content as string
        /// </summary>
        /// <param name="onPreviewContent"></param>
        /// <returns></returns>
        public RestBuilder OnPreviewContentRequestAsString(Action<PreviewContentAsStringEventArgs> onPreviewContent)
            => With(r => r.onPreviewContentRequestAsStringAction = onPreviewContent);

        /// <summary>
        /// Sets onUploadProgress action, occurs when the request is processing
        /// </summary>
        /// <param name="onProgress"></param>
        /// <returns></returns>
        public RestBuilder OnUploadProgress(Action<ProgressEventArgs> onProgress)
            => With(r => r.onUploadProgressAction = onProgress);

        /// <summary>
        /// Sets onDownloadProgress action, occurs when the response is processing
        /// </summary>
        /// <param name="onProgress"></param>
        /// <returns></returns>
        public RestBuilder OnDownloadProgress(Action<ProgressEventArgs> onProgress)
            => With(r => r.onDownloadProgressAction = onProgress);

        /// <summary>
        /// Sets OnPreCompleted, occurs when the result of request il builded and it isn't completed yet
        /// </summary>
        /// <param name="onPreCompleted"></param>
        /// <returns></returns>
        public RestBuilder OnPreCompleted(Action<PreCompletedEventArgs> onPreCompleted)
            => With(r => r.onPreCompletedAction = onPreCompleted);

        /// <summary>
        ///  Sets OnPreviewContentResponseAsString, displays the response as string
        /// </summary>
        /// <param name="onPreviewContent"></param>
        /// <returns></returns>
        public RestBuilder OnPreviewContentResponseAsString(Action<PreviewContentAsStringEventArgs> onPreviewContent)
            => With(r => r.onPreviewContentResponseAsStringAction = onPreviewContent);

        /// <summary>
        /// Sets onCompleted action, occurs when the call is completed.
        /// </summary>
        /// <param name="onCompleted"></param>
        /// <returns></returns>
        public RestBuilder OnCompleted(Action<CompletedEventArgs> onCompleted)
            => With(r => r.onCompletedAction = onCompleted);

        /// <summary>
        /// Sets onException, occurs when there is an exception during the call
        /// </summary>
        /// <param name="exception"></param>
        /// <returns></returns>
        public RestBuilder OnException(Action<Exception> exception)
            => With(r => r.onExceptionAction = exception);

        #endregion

        #region [ Http Methods ]

        #region [ Get ]

        /// <summary>
        ///  Send a GET request to the specified Uri as an asynchronous operation
        /// </summary>
        public Task<RestResult<string>> GetAsync(CancellationToken token = default)
            => SendAsStringAsync(HttpMethod.Get, token);

        /// <summary>
        ///  Send a GET request to the specified Uri as an asynchronous operation
        /// </summary>
        public Task<RestResult<T>> GetAsync<T>(CancellationToken token = default)
            => SendAsync<T>(HttpMethod.Get, token);

        /// <summary>
        ///  Send a GET request to the specified Uri as an asynchronous operation
        /// </summary>
        public Task<RestResult<byte[]>> GetAsByteArrayAsync(CancellationToken token = default)
            => SendAsByteArrayAsync(HttpMethod.Get, token);

        /// <summary>
        ///  Send a GET request to the specified Uri as an asynchronous operation
        /// </summary>
        public Task<RestResult<Stream>> GetAsStreamAsync(CancellationToken token = default)
            => SendAsStreamAsync(HttpMethod.Get, token);

        /// <summary>
        /// Send a GET request to the specified Uri as an synchronous operation
        /// </summary>
        public RestResult<string> Get()
            => GetAsync().GetAwaiter().GetResult();

        /// <summary>
        /// Send a GET request to the specified Uri as an synchronous operation
        /// </summary>
        public RestResult<string> Get(string url)
            => Url(url).Get();

        /// <summary>
        /// Send a GET request to the specified Uri as an synchronous operation
        /// </summary>
        public RestResult<string> Get(Uri url)
            => Url(url).Get();

        /// <summary>
        /// Send a GET request to the specified Uri as an synchronous operation
        /// </summary>
        public RestResult<T> Get<T>()
            => GetAsync<T>().GetAwaiter().GetResult();

        /// <summary>
        /// Send a GET request to the specified Uri as an synchronous operation
        /// </summary>
        public RestResult<byte[]> GetAsByteArray()
            => GetAsByteArrayAsync().GetAwaiter().GetResult();

        /// <summary>
        /// Send a GET request to the specified Uri as an synchronous operation
        /// </summary>
        public RestResult<Stream> GetAsStream()
            => GetAsStreamAsync().GetAwaiter().GetResult();

        #endregion

        #region [ Post ]

        /// <summary>
        /// Send a POST request to the specified Uri as an asynchronous operation
        /// </summary>
        public Task<RestResult<string>> PostAsync(CancellationToken token = default)
            => SendAsStringAsync(HttpMethod.Post, token);

        /// <summary>
        /// Send a POST request to the specified Uri as an asynchronous operation
        /// </summary>
        public Task<RestResult<T>> PostAsync<T>(CancellationToken token = default)
            => SendAsync<T>(HttpMethod.Post, token);

        /// <summary>
        /// Send a POST request to the specified Uri as an asynchronous operation
        /// </summary>
        public Task<RestResult<byte[]>> PostAsByteArrayAsync(CancellationToken token = default)
            => SendAsByteArrayAsync(HttpMethod.Post, token);

        /// <summary>
        /// Send a POST request to the specified Uri as an asynchronous operation
        /// </summary>
        public Task<RestResult<Stream>> PostAsStreamAsync(CancellationToken token = default)
            => SendAsStreamAsync(HttpMethod.Post, token);

        /// <summary>
        /// Send a POST request to the specified Uri as a synchronous operation
        /// </summary>
        public RestResult<string> Post()
            => PostAsync().GetAwaiter().GetResult();

        /// <summary>
        /// Send a POST request to the specified Uri as a synchronous operation
        /// </summary>
        public RestResult<T> Post<T>()
            => PostAsync<T>().GetAwaiter().GetResult();

        /// <summary>
        /// Send a POST request to the specified Uri as a synchronous operation
        /// </summary>
        public RestResult<byte[]> PostAsByteArray()
            => PostAsByteArrayAsync().GetAwaiter().GetResult();

        /// <summary>
        /// Send a POST request to the specified Uri as a synchronous operation
        /// </summary>
        public RestResult<Stream> PostAsStream()
            => PostAsStreamAsync().GetAwaiter().GetResult();

        #endregion

        #region [ Put ]

        /// <summary>
        /// Send a PUT request to the specified Uri as an asynchronous operation.
        /// </summary>
        public Task<RestResult<string>> PutAsync(CancellationToken token = default)
            => SendAsStringAsync(HttpMethod.Put, token);

        /// <summary>
        /// Send a PUT request to the specified Uri as an asynchronous operation.
        /// </summary>
        public Task<RestResult<T>> PutAsync<T>(CancellationToken token = default)
            => SendAsync<T>(HttpMethod.Put, token);

        /// <summary>
        /// Send a PUT request to the specified Uri as an asynchronous operation.
        /// </summary>
        public Task<RestResult<byte[]>> PutAsByteArrayAsync(CancellationToken token = default)
            => SendAsByteArrayAsync(HttpMethod.Put, token);

        /// <summary>
        /// Send a PUT request to the specified Uri as an asynchronous operation.
        /// </summary>
        public Task<RestResult<Stream>> PutAsStreamAsync(CancellationToken token = default)
            => SendAsStreamAsync(HttpMethod.Put, token);

        /// <summary>
        /// Send a PUT request to the specified Uri as a synchronous operation.
        /// </summary>
        public RestResult<string> Put()
            => PutAsync().GetAwaiter().GetResult();

        /// <summary>
        /// Send a PUT request to the specified Uri as a synchronous operation.
        /// </summary>
        public RestResult<T> Put<T>()
            => PutAsync<T>().GetAwaiter().GetResult();

        /// <summary>
        /// Send a PUT request to the specified Uri as a synchronous operation.
        /// </summary>
        public RestResult<byte[]> PutAsByteArray()
            => PutAsByteArrayAsync().GetAwaiter().GetResult();

        /// <summary>
        /// Send a PUT request to the specified Uri as a synchronous operation.
        /// </summary>
        public RestResult<Stream> PutAsStream()
            => PutAsStreamAsync().GetAwaiter().GetResult();

        #endregion

        #region [ Delete ]

        /// <summary>
        ///  Send a DELETE request to the specified Uri as an asynchronous operation.
        /// </summary>
        public Task<RestResult<string>> DeleteAsync(CancellationToken token = default)
            => SendAsStringAsync(HttpMethod.Delete, token);

        /// <summary>
        ///  Send a DELETE request to the specified Uri as an asynchronous operation.
        /// </summary>
        public Task<RestResult<T>> DeleteAsync<T>(CancellationToken token = default)
            => SendAsync<T>(HttpMethod.Delete, token);

        /// <summary>
        ///  Send a DELETE request to the specified Uri as an asynchronous operation.
        /// </summary>
        public Task<RestResult<byte[]>> DeleteAsByteArrayAsync(CancellationToken token = default)
            => SendAsByteArrayAsync(HttpMethod.Delete, token);

        /// <summary>
        ///  Send a DELETE request to the specified Uri as an asynchronous operation.
        /// </summary>
        public Task<RestResult<Stream>> DeleteAsStreamAsync(CancellationToken token = default)
            => SendAsStreamAsync(HttpMethod.Delete, token);

        /// <summary>
        /// Send a DELETE request to the specified Uri as a synchronous operation.
        /// </summary>
        public RestResult<string> Delete()
            => DeleteAsync().GetAwaiter().GetResult();

        /// <summary>
        /// Send a DELETE request to the specified Uri as a synchronous operation.
        /// </summary>
        public RestResult<T> Delete<T>()
            => DeleteAsync<T>().GetAwaiter().GetResult();

        /// <summary>
        /// Send a DELETE request to the specified Uri as a synchronous operation.
        /// </summary>
        public RestResult<byte[]> DeleteAsByteArray()
            => DeleteAsByteArrayAsync().GetAwaiter().GetResult();

        /// <summary>
        /// Send a DELETE request to the specified Uri as a synchronous operation.
        /// </summary>
        public RestResult<Stream> DeleteAsStream()
            => DeleteAsStreamAsync().GetAwaiter().GetResult();

        #endregion

        #region [ Patch ]

        /// <summary>
        /// Send a PATCH request to the specified Uri as an asynchronous operation
        /// </summary>
        public Task<RestResult<string>> PatchAsync(CancellationToken token = default)
            => SendAsStringAsync(PATCH, token);

        /// <summary>
        /// Send a PATCH request to the specified Uri as an asynchronous operation
        /// </summary>
        public Task<RestResult<T>> PatchAsync<T>(CancellationToken token = default)
            => SendAsync<T>(PATCH, token);

        /// <summary>
        /// Send a PATCH request to the specified Uri as an asynchronous operation
        /// </summary>
        public Task<RestResult<byte[]>> PatchAsByteArrayAsync(CancellationToken token = default)
            => SendAsByteArrayAsync(PATCH, token);

        /// <summary>
        /// Send a PATCH request to the specified Uri as an asynchronous operation
        /// </summary>
        public Task<RestResult<Stream>> PatchAsStreamAsync(CancellationToken token = default)
            => SendAsStreamAsync(PATCH, token);

        /// <summary>
        /// Send a PATCH request to the specified Uri as a synchronous operation
        /// </summary>
        public RestResult<string> Patch()
            => PatchAsync().GetAwaiter().GetResult();

        /// <summary>
        /// Send a PATCH request to the specified Uri as a synchronous operation
        /// </summary>
        public RestResult<T> Patch<T>()
            => PatchAsync<T>().GetAwaiter().GetResult();

        /// <summary>
        /// Send a PATCH request to the specified Uri as a synchronous operation
        /// </summary>
        public RestResult<byte[]> PatchAsByteArray()
            => PatchAsByteArrayAsync().GetAwaiter().GetResult();

        /// <summary>
        /// Send a PATCH request to the specified Uri as a synchronous operation
        /// </summary>
        public RestResult<Stream> PatchAsStream()
            => PatchAsStreamAsync().GetAwaiter().GetResult();

        #endregion

        #region [ Custom Call ]

        /// <summary>
        /// Send a CUSTOM CALL request to the specified Uri as an asynchronous operation
        /// </summary>
        public Task<RestResult<string>> CustomCallAsync(HttpMethod method, CancellationToken token = default)
            => SendAsStringAsync(method, token);

        /// <summary>
        /// Send a CUSTOM CALL request to the specified Uri as an asynchronous operation
        /// </summary>
        public Task<RestResult<T>> CustomCallAsync<T>(HttpMethod method, CancellationToken token = default)
            => SendAsync<T>(method, token);

        /// <summary>
        /// Send a CUSTOM CALL request to the specified Uri as an asynchronous operation
        /// </summary>
        public Task<RestResult<byte[]>> CustomCallAsByteArrayAsync(HttpMethod method, CancellationToken token = default)
            => SendAsByteArrayAsync(method, token);

        /// <summary>
        /// Send a CUSTOM CALL request to the specified Uri as an asynchronous operation
        /// </summary>
        public Task<RestResult<Stream>> CustomCallAsStreamAsync(HttpMethod method, CancellationToken token = default)
            => SendAsStreamAsync(method, token);

        /// <summary>
        /// Send a CUSTOM CALL request to the specified Uri as a synchronous operation
        /// </summary>
        public RestResult<string> CustomCall(HttpMethod method)
            => CustomCallAsync(method).GetAwaiter().GetResult();

        /// <summary>
        /// Send a CUSTOM CALL request to the specified Uri as a synchronous operation
        /// </summary>
        public RestResult<T> CustomCall<T>(HttpMethod method)
            => CustomCallAsync<T>(method).GetAwaiter().GetResult();

        /// <summary>
        /// Send a CUSTOM CALL request to the specified Uri as a synchronous operation
        /// </summary>
        public RestResult<byte[]> CustomCallAsByteArray(HttpMethod method)
            => CustomCallAsByteArrayAsync(method).GetAwaiter().GetResult();

        /// <summary>
        /// Send a CUSTOM CALL request to the specified Uri as a synchronous operation
        /// </summary>
        public RestResult<Stream> CustomCallAsStream(HttpMethod method)
            => CustomCallAsStreamAsync(method).GetAwaiter().GetResult();

        #endregion

        #region [ Download ]

        /// <summary>
        /// Send a GET request to download file from the specified Uri as an asynchronous operation
        /// </summary>
        public Task<RestResult<byte[]>> DownloadAsync(string url, CancellationToken token = default)
            => Url(url).GetAsByteArrayAsync(token);

        /// <summary>
        /// Send a GET request to download file from the specified Uri as an asynchronous operation
        /// </summary>
        public Task<RestResult<byte[]>> DownloadAsync(Uri url, CancellationToken token = default)
            => Url(url).GetAsByteArrayAsync(token);

        /// <summary>
        /// Send a GET request to download file from the specified Uri as a synchronous operation
        /// </summary>
        public RestResult<byte[]> Download(string url)
            => DownloadAsync(url).GetAwaiter().GetResult();

        /// <summary>
        /// Send a GET request to download file from the specified Uri as a synchronous operation
        /// </summary>
        public RestResult<byte[]> Download(Uri url)
            => DownloadAsync(url).GetAwaiter().GetResult();

        #endregion

        #endregion

        #region [ Fetch ]

        private Task<RestResult<string>> SendAsStringAsync(HttpMethod method, CancellationToken cancellationToken)
            => SendCoreAsync(method, content => content.ReadAsStringAsync(), true, cancellationToken);

        private Task<RestResult<byte[]>> SendAsByteArrayAsync(HttpMethod method, CancellationToken cancellationToken)
            => SendCoreAsync(method, content => content.ReadAsByteArrayAsync(), false, cancellationToken);

        private Task<RestResult<Stream>> SendAsStreamAsync(HttpMethod method, CancellationToken cancellationToken)
            => SendCoreAsync(method, content => content.ReadAsStreamAsync(), false, cancellationToken);

        private Task<RestResult<T>> SendAsync<T>(HttpMethod method, CancellationToken cancellationToken)
            => SendCoreAsync(method, async content =>
            {
                string serializedObject = await content.ReadAsStringAsync().ConfigureAwait(false);
                object value = serializer.DeserializeObject(serializedObject, typeof(T), deserializeOptions);
                return value == null ? default : (T)value;
            }, true, cancellationToken);

        /// <summary>
        /// Sends an HTTP request as an asynchronous operation. Every Send* method goes through this pipeline.
        /// </summary>
        /// <typeparam name="T">Type of the content</typeparam>
        /// <param name="method">HTTP method</param>
        /// <param name="readContent">Converts the (buffered) response content to T</param>
        /// <param name="previewResponse">true if OnPreviewContentResponseAsString must be raised</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns></returns>
        private async Task<RestResult<T>> SendCoreAsync<T>(HttpMethod method, Func<HttpContent, Task<T>> readContent, bool previewResponse, CancellationToken cancellationToken)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();

            try
            {
                string url = BuildFinalUrl();

                StartEventArgs startEventArgs = new StartEventArgs()
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

                HttpResponseMessage responseMessage = await SendWithRefreshTokenAsync(method, url, cancellationToken).ConfigureAwait(false);

                RestResult<T> response = new RestResult<T>(responseMessage);

                if (previewResponse && onPreviewContentResponseAsStringAction != null)
                {
                    onPreviewContentResponseAsStringAction(new PreviewContentAsStringEventArgs
                    {
                        ContentAsString = await responseMessage.Content.ReadAsStringAsync().ConfigureAwait(false),
                        ContentType = typeof(T)
                    });
                }

                try
                {
                    response.Content = await readContent(responseMessage.Content).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    // the response is valid, only its content could not be read/deserialized:
                    // keep status code and headers available to the caller.
                    response.Exception = ex;
                    onExceptionAction?.Invoke(ex);
                }

                onPreCompletedAction?.Invoke(new PreCompletedEventArgs { IsCompleted = true, Result = response });

                stopwatch.Stop();

                onCompletedAction?.Invoke(new CompletedEventArgs { Result = response, ExecutionTime = stopwatch.Elapsed });

                return response;
            }
            catch (Exception ex)
            {
                onExceptionAction?.Invoke(ex);
                return new RestResult<T>(ex);
            }
        }

        /// <summary>
        /// Sends the request; when the response is 401 and a refresh token func is configured,
        /// the refresh token is invoked and the request is sent again (only once).
        /// </summary>
        private async Task<HttpResponseMessage> SendWithRefreshTokenAsync(HttpMethod method, string url, CancellationToken cancellationToken)
        {
            HttpResponseMessage responseMessage = await SendOnceAsync(method, url, cancellationToken).ConfigureAwait(false);

            if (responseMessage.StatusCode == HttpStatusCode.Unauthorized
                && refreshTokenExecution
                && (refreshTokenApi != null || refreshTokenApiAsync != null))
            {
                bool refreshed = (refreshTokenApi != null && IsSuccess(refreshTokenApi()))
                    || (refreshTokenApiAsync != null && IsSuccess(await refreshTokenApiAsync().ConfigureAwait(false)));

                if (refreshed)
                {
                    responseMessage.Dispose();
                    responseMessage = await SendOnceAsync(method, url, cancellationToken).ConfigureAwait(false);
                }
            }

            return responseMessage;
        }

        private static bool IsSuccess(RestResult result)
            => result != null && result.IsSuccessStatusCode;

        /// <summary>
        /// Sends a single HTTP request and buffers the response content
        /// </summary>
        private async Task<HttpResponseMessage> SendOnceAsync(HttpMethod method, string url, CancellationToken cancellationToken)
        {
            using (CancellationTokenSource timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                TimeSpan timeout = Properties.Timeout;
                if (timeout != System.Threading.Timeout.InfiniteTimeSpan && timeout > TimeSpan.Zero)
                {
                    timeoutSource.CancelAfter(timeout);
                }

                HttpClient client = externalHttpClient ?? CreateHttpClient();
                try
                {
                    using (HttpRequestMessage request = CreateRequestMessage(method, url, timeoutSource.Token))
                    {
                        HttpResponseMessage responseMessage = await client
                            .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeoutSource.Token)
                            .ConfigureAwait(false);

                        try
                        {
                            await BufferContentAsync(responseMessage, timeoutSource.Token).ConfigureAwait(false);
                        }
                        catch
                        {
                            responseMessage.Dispose();
                            throw;
                        }

                        return responseMessage;
                    }
                }
                catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested && timeoutSource.IsCancellationRequested)
                {
                    throw new TimeoutException($"The request was canceled due to the configured Timeout of {timeout.TotalSeconds} seconds elapsing.", ex);
                }
                finally
                {
                    if (externalHttpClient == null)
                    {
                        client.Dispose();
                    }
                }
            }
        }

        /// <summary>
        /// Creates the HttpClient used for a single request
        /// </summary>
        private HttpClient CreateHttpClient()
        {
            HttpClientHandler handler = new HttpClientHandler
            {
                ClientCertificateOptions = Properties.CertificateOption,
            };

            if (credentials != null)
            {
                handler.Credentials = credentials;
            }

            if (certificateCallback != null)
            {
                handler.ServerCertificateCustomValidationCallback = (message, certificate, chain, errors)
                    => certificateCallback(message, certificate, chain, errors);
            }

            if (enabledCompression && handler.SupportsAutomaticDecompression)
            {
#if NET
                handler.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli;
#else
                handler.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
#endif
            }

            HttpClient client = new HttpClient(handler, disposeHandler: true)
            {
                // the timeout is managed by RestClient (see SendOnceAsync)
                Timeout = System.Threading.Timeout.InfiniteTimeSpan
            };

            onConfigureHttpClientAction?.Invoke(client);

            return client;
        }

        /// <summary>
        /// Creates the request message with headers, authentication and content
        /// </summary>
        private HttpRequestMessage CreateRequestMessage(HttpMethod method, string url, CancellationToken cancellationToken)
        {
            HttpRequestMessage request = new HttpRequestMessage(method, url);
            try
            {
                if (onAuthentication != null)
                {
                    request.Headers.Authorization = onAuthentication();
                }

                onRequestHeaders?.Invoke(request.Headers);

                HttpContent content = MakeHttpContent();
                if (content != null && onUploadProgressAction != null)
                {
                    content = new ProgressHttpContent(
                        content,
                        Properties.BufferSize,
                        (current, total) => onUploadProgressAction(new ProgressEventArgs { CurrentBytes = current, TotalBytes = total }),
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
        /// Reads the whole response content (raising OnDownloadProgress) and replaces it with a buffered content,
        /// so that it can be read more than once and outlives the HttpClient.
        /// </summary>
        private async Task BufferContentAsync(HttpResponseMessage response, CancellationToken cancellationToken)
        {
            HttpContent original = response.Content;
            if (original == null)
            {
                response.Content = new ByteArrayContent(Array.Empty<byte>());
                return;
            }

            long totalBytes = original.Headers.ContentLength ?? 0;
            byte[] buffer = new byte[Properties.BufferSize];
            long bytesReceived = 0;

            using (Stream source = await original.ReadAsStreamAsync().ConfigureAwait(false))
            using (MemoryStream memory = new MemoryStream())
            {
                int read;
                while ((read = await source.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false)) > 0)
                {
                    memory.Write(buffer, 0, read);
                    bytesReceived += read;
                    onDownloadProgressAction?.Invoke(new ProgressEventArgs { CurrentBytes = bytesReceived, TotalBytes = totalBytes });
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
        /// Builds the final url: endpoint + commands + url-encoded query string
        /// </summary>
        /// <returns></returns>
        private string BuildFinalUrl()
        {
            if (Properties.EndPoint == null)
            {
                throw new InvalidOperationException("The EndPoint is not set: use Url(...) or Rest.Build(p => p.EndPoint = ...).");
            }

            StringBuilder urlBuilder = new StringBuilder(Properties.EndPoint.OriginalString);

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

            if (parameters.Count > 0)
            {
                urlBuilder.Append(urlBuilder.ToString().Contains("?") ? "&" : "?");
                urlBuilder.Append(string.Join("&", parameters.Select(q => $"{Uri.EscapeDataString(q.Key)}={Uri.EscapeDataString(q.Value)}")));
            }

            return urlBuilder.ToString();
        }

        #endregion

        #region [ Make Http Content ]

        /// <summary>
        /// Make Http Content
        /// </summary>
        /// <returns></returns>
        private HttpContent MakeHttpContent()
        {
            if (isEnabledFormUrlEncoded)
            {
                if (formUrlEncodedKeyValues == null)
                {
                    return null;
                }

                onPreviewContentRequestAsStringAction?.Invoke(new PreviewContentAsStringEventArgs
                {
                    ContentAsString = string.Join("&", formUrlEncodedKeyValues.Select(q => $"{Uri.EscapeDataString(q.Key)}={Uri.EscapeDataString(q.Value ?? string.Empty)}")),
                    ContentType = formUrlEncodedKeyValues.GetType()
                });

                return new FormUrlEncodedContent(formUrlEncodedKeyValues);
            }

            if (payloadContent != null)
            {
                var serializedObject = serializer.SerializeObject(payloadContent, payloadContent.GetType(), serializeOptions);
                onPreviewContentRequestAsStringAction?.Invoke(new PreviewContentAsStringEventArgs
                {
                    ContentAsString = serializedObject,
                    ContentType = payloadContent.GetType()
                });
                return new StringContent(serializedObject, Encoding.UTF8, serializer.MediaTypeAsString);
            }

            return null;
        }

        #endregion
    }
}
