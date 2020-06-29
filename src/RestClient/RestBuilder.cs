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

namespace RestClient
{
    using RestClient.Generic;
    using RestClient.IO;
    using RestClient.Serialization;
    using RestClient.Serialization.Json;
    using RestClient.Serialization.Xml;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
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
    /// Provides a set of methods for building the requests
    /// </summary>
    public sealed class RestBuilder
    {
        /// <summary>
        /// Provides a base class for sending HTTP requests and receiving HTTP responses from a resource identified by a URI
        /// </summary>
        internal HttpClient HttpClient { get; private set; }
            = new HttpClient();

        /// <summary>
        /// Rest Properties
        /// </summary>
        public RestProperties Properties { get; internal set; }
            = new RestProperties();

        /// <summary>
        /// List of commands
        /// </summary>
        List<string> Commands { get; set; }
            = new List<string>();

        /// <summary>
        /// List of parameters
        /// </summary>
        Dictionary<string, string> Parameters { get; set; }
            = new Dictionary<string, string>();

        /// <summary>
        /// if true writes log into System.Debug (section is temporary)
        /// </summary>
        bool Logger { get; set; }
            = false;

        /// <summary>
        /// Serialization kind. JSON is default value
        /// </summary>
        ISerializerContent Serializer { get; set; }
            = new JSON();

        /// <summary>
        /// Initializes a new instance
        /// </summary>
        internal RestBuilder()
        {
            ServicePointManager.ServerCertificateValidationCallback = (object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
                => true;
        }

        #region [ Certificate Validation ]

        /// <summary>
        /// Certificate Callback
        /// </summary>
        Func<object, X509Certificate, X509Chain, SslPolicyErrors, bool> CertificateCallback;

        /// <summary>
        /// The callback to validate a server certificate
        /// </summary>
        /// <param name="callback"></param>
        /// <returns></returns>
        public RestBuilder CertificateValidation(Func<object, X509Certificate, X509Chain, SslPolicyErrors, bool> callback)
        {
            var result = (RestBuilder)this.MemberwiseClone();
            result.CertificateCallback = callback;
            result.Credentials = Credentials;
            ServicePointManager.ServerCertificateValidationCallback = new RemoteCertificateValidationCallback(callback);
            this.CreateNewHttpClientInstance(result);
            return result;
        }

        #endregion

        #region [ Network Credential ]

        /// <summary>
        /// Current Credentials
        /// </summary>
        ICredentials Credentials { get; set; }
            = null;

        /// <summary>
        /// Authentication information used by this func
        /// </summary>
        /// <param name="credential"></param>
        /// <returns></returns>
        public RestBuilder NetworkCredential(Func<NetworkCredential> credential)
        {
            var result = (RestBuilder)this.MemberwiseClone();
            result.Credentials = credential();
            this.CreateNewHttpClientInstance(result);
            return result;
        }

        /// <summary>
        /// Authentication information used by username and password
        /// </summary>
        /// <param name="username"></param>
        /// <param name="password"></param>
        /// <returns></returns>
        public RestBuilder NetworkCredential(string username, string password)
        {
            var result = (RestBuilder)this.MemberwiseClone();
            result.Credentials = new NetworkCredential(username, password);
            this.CreateNewHttpClientInstance(result);
            return result;
        }

        /// <summary>
        /// Authentication information used by username, password and domain
        /// </summary>
        /// <param name="username"></param>
        /// <param name="password"></param>
        /// <param name="domain"></param>
        /// <returns></returns>
        public RestBuilder NetworkCredential(string username, string password, string domain)
        {
            var result = (RestBuilder)this.MemberwiseClone();
            result.Credentials = new NetworkCredential(username, password, domain);
            this.CreateNewHttpClientInstance(result);
            return result;
        }

        /// <summary>
        /// Authentication information used by username and secure string password
        /// </summary>
        /// <param name="username"></param>
        /// <param name="password"></param>
        /// <returns></returns>
        public RestBuilder NetworkCredential(string username, System.Security.SecureString password)
        {
            var result = (RestBuilder)this.MemberwiseClone();
            result.Credentials = new NetworkCredential(username, password);
            this.CreateNewHttpClientInstance(result);
            return result;
        }

        /// <summary>
        /// Authentication information used by username, secure string password and domain
        /// </summary>
        /// <param name="username"></param>
        /// <param name="password"></param>
        /// <param name="domain"></param>
        /// <returns></returns>
        public RestBuilder NetworkCredential(string username, System.Security.SecureString password, string domain)
        {
            var result = (RestBuilder)this.MemberwiseClone();
            result.Credentials = new NetworkCredential(username, password, domain);
            this.CreateNewHttpClientInstance(result);
            return result;
        }

        #endregion

        #region [ Create New HttpClient Instance and set into HttpClient ]

        /// <summary>
        /// Create new HttpClient instance and set into result.HttpClient
        /// </summary>
        /// <param name="result"></param>
        private void CreateNewHttpClientInstance(RestBuilder result)
        {
            var client = new HttpClient(new HttpClientHandler()
            {
                Credentials = result.Credentials,
                ClientCertificateOptions = Properties.CertificateOption,
                ServerCertificateCustomValidationCallback = result.CertificateCallback
            })
            {
                Timeout = Properties.Timeout
            };

            foreach (var h in result.HttpClient.DefaultRequestHeaders)
                client.DefaultRequestHeaders.Add(h.Key, h.Value);

            result.HttpClient = client;
        }

        #endregion

        #region [ Authentication ]

        /// <summary>
        /// The Authorization header for an HTTP request
        /// </summary>
        /// <param name="authentication"></param>
        /// <returns></returns>
        public RestBuilder Authentication(Func<AuthenticationHeaderValue> authentication)
        {
            var result = (RestBuilder)this.MemberwiseClone();
            result.HttpClient.DefaultRequestHeaders.Authorization = authentication();
            return result;
        }

        /// <summary>
        /// The Authorization header for an HTTP request (This method will be removed)
        /// </summary>
        /// <param name="authentication"></param>
        /// <returns></returns>
        [Obsolete("OnAuthentication: this method will be removed soon", false)]
        public RestBuilder OnAuthentication(Func<AuthenticationHeaderValue> authentication) => Authentication(authentication);

        /// <summary>
        /// The Authorization header for an HTTP request
        /// </summary>
        /// <param name="scheme"></param>
        /// <returns></returns>
        public RestBuilder Authentication(string scheme)
        {
            var result = (RestBuilder)this.MemberwiseClone();
            result.HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(scheme);
            return result;
        }

        /// <summary>
        /// The Authorization header for an HTTP request (This method will be removed)
        /// </summary>
        /// <param name="scheme"></param>
        /// <returns></returns>
        [Obsolete("OnAuthentication: this method will be removed soon", false)]
        public RestBuilder OnAuthentication(string scheme) => Authentication(scheme);

        /// <summary>
        /// The Authorization header for an HTTP request
        /// </summary>
        /// <param name="scheme"></param>
        /// <param name="parameter"></param>
        /// <returns></returns>
        public RestBuilder Authentication(string scheme, string parameter)
        {
            var result = (RestBuilder)this.MemberwiseClone();
            result.HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(scheme, parameter);
            return result;
        }

        /// <summary>
        /// The Authorization header for an HTTP request (This method will be removed)
        /// </summary>
        /// <param name="scheme"></param>
        /// <param name="parameter"></param>
        /// <returns></returns>
        [Obsolete("OnAuthentication: this method will be removed soon", false)]
        public RestBuilder OnAuthentication(string scheme, string parameter) => Authentication(scheme, parameter);

        #endregion

        #region [ Timeout ]

        /// <summary>
        /// Sets the timespan to wait before the request times out
        /// </summary>
        /// <param name="timeOut"></param>
        /// <returns></returns>
        public RestBuilder Timeout(TimeSpan timeOut)
        {
            var result = (RestBuilder)this.MemberwiseClone();
            result.HttpClient.Timeout
                = result.Properties.Timeout
                = timeOut;
            return result;
        }

        /// <summary>
        /// Sets the timespan to wait before the request times out
        /// </summary>
        /// <param name="milliseconds"></param>
        /// <returns></returns>
        public RestBuilder Timeout(double milliseconds)
        {
            var result = (RestBuilder)this.MemberwiseClone();
            result.HttpClient.Timeout
                = result.Properties.Timeout
                = TimeSpan.FromMilliseconds(milliseconds);
            return result;
        }

        #endregion

        #region [ Buffer Size ]

        /// <summary>
        /// Sets the buffer size. 
        /// </summary>
        /// <param name="bufferSize"></param>
        /// <returns></returns>
        public RestBuilder BufferSize(int bufferSize)
        {
            if (bufferSize <= 0) throw new ArgumentOutOfRangeException(nameof(bufferSize));
            var result = (RestBuilder)this.MemberwiseClone();
            result.Properties.BufferSize = bufferSize;
            return result;
        }

        #endregion

        #region [ Header ]

        /// <summary>
        ///  Sets the headers which should be sent with this o children requests
        /// </summary>
        /// <param name="defaultRequestHeaders"></param>
        /// <returns></returns>
        public RestBuilder Header(Action<HttpRequestHeaders> defaultRequestHeaders)
        {
            var result = (RestBuilder)this.MemberwiseClone();
            defaultRequestHeaders(result.HttpClient.DefaultRequestHeaders);
            return result;
        }

        #endregion

        #region [ Command ]

        /// <summary>
        /// Adds the command to request
        /// </summary>
        /// <param name="command"></param>
        /// <returns></returns>
        public RestBuilder Command(string command)
        {
            var result = (RestBuilder)this.MemberwiseClone();
            string prefix = !command.StartsWith("/") ? "/" : string.Empty;
            result.Commands.Add($"{prefix}{command}");
            return result;
        }

        /// <summary>
        /// Adds the command to request
        /// </summary>
        /// <param name="command"></param>
        /// <returns></returns>
        public RestBuilder Command(int command)
        {
            var result = (RestBuilder)this.MemberwiseClone();
            result.Commands.Add($"/{command}");
            return result;
        }

        /// <summary>
        /// Adds the command to request
        /// </summary>
        /// <param name="command"></param>
        /// <returns></returns>
        public RestBuilder Command(Guid command)
        {
            var result = (RestBuilder)this.MemberwiseClone();
            result.Commands.Add($"/{command}");
            return result;
        }

        #endregion

        #region [ Refresh Token Invoke ]

        /// <summary>
        /// if true the refresh token execution is enabled
        /// </summary>
        bool RefreshTokenExecution = true;

        /// <summary>
        /// Func refresh token
        /// </summary>
        Func<RestResult> RefreshTokenApi = null;

        /// <summary>
        /// Func refresh token async
        /// </summary>
        Func<Task<RestResult>> RefreshTokenApiAsync = null;

        /// <summary>
        /// Enables refresh token: true if enabled, false when disabled
        /// </summary>
        /// <param name="refreshToken"></param>
        /// <returns></returns>
        public RestBuilder RefreshToken(bool refreshToken = true)
        {
            var result = (RestBuilder)this.MemberwiseClone();
            result.RefreshTokenExecution = refreshToken;
            return result;
        }

        /// <summary>
        /// Sets the func when the refresh token is invoked
        /// </summary>
        /// <param name="refreshTokenApi"></param>
        /// <returns></returns>
        public RestBuilder RefreshTokenInvoke(Func<RestResult> refreshTokenApi)
        {
            var result = (RestBuilder)this.MemberwiseClone();
            result.RefreshTokenApi = refreshTokenApi;
            return result;
        }

        /// <summary>
        /// Sets the func when the refresh token is invoked
        /// </summary>
        /// <param name="refreshTokenApi"></param>
        /// <returns></returns>
        public RestBuilder RefreshTokenInvoke(Func<Task<RestResult>> refreshTokenApi)
        {
            var result = (RestBuilder)this.MemberwiseClone();
            result.RefreshTokenApiAsync = refreshTokenApi;
            return result;
        }

        #endregion

        #region [ Serialization ]

        /// <summary>
        /// Sets xml as serialization
        /// </summary>
        /// <returns></returns>
        public RestBuilder Xml()
        {
            var result = (RestBuilder)this.MemberwiseClone();
            result.Serializer = new XML();
            return result;
        }

        /// <summary>
        /// Sets json as serialization
        /// </summary>
        /// <returns></returns>
        public RestBuilder Json()
        {
            var result = (RestBuilder)this.MemberwiseClone();
            result.Serializer = new JSON();
            return result;
        }

        /// <summary>
        /// Sets custom serialization
        /// </summary>
        /// <param name="serializer"></param>
        /// <returns></returns>
        public RestBuilder CustomSerializer(ISerializerContent serializer)
        {
            var result = (RestBuilder)this.MemberwiseClone();
            result.Serializer = serializer;
            return result;
        }

        #endregion

        #region [ Logger ]

        /// <summary>
        /// Sets log, true is enabled, when false is disabled.
        /// Currently this function is temporary. 
        /// </summary>
        /// <param name="logger"></param>
        /// <returns></returns>
        [Obsolete("Currently this function is temporary!", false)]
        public RestBuilder Log(bool logger = true)
        {
            var result = (RestBuilder)this.MemberwiseClone();
            result.Logger = logger;
            return result;
        }

        #endregion

        #region [ Parameter ]

        /// <summary>
        /// Adds parameter to querystring
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public RestBuilder Parameter(string key, object value)
        {
            var result = (RestBuilder)this.MemberwiseClone();
            if (Parameters.ContainsKey(key))
                result.Parameters[key] = value.ToString();
            else
                result.Parameters.Add(key, value.ToString());
            return result;
        }

        /// <summary>
        /// Adds list of rest parameter to querystring
        /// </summary>
        /// <param name="parameter"></param>
        /// <param name="others"></param>
        /// <returns></returns>
        public RestBuilder Parameter(RestParameter parameter, params RestParameter[] others)
        {
            var result = (RestBuilder)this.MemberwiseClone();
            result.Parameter(parameter.Key, parameter.Value);
            foreach (RestParameter p in others) result.Parameter(p.Key, p.Value);
            return result;
        }

        #endregion

        #region [ Url ]

        /// <summary>
        /// Sets url
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        public RestBuilder Url(string url)
        {
            return this.Url(new Uri(url));
        }

        /// <summary>
        /// Sets Uri
        /// </summary>
        /// <param name="uri"></param>
        /// <returns></returns>
        public RestBuilder Url(Uri uri)
        {
            var result = (RestBuilder)this.MemberwiseClone();
            result.Properties.EndPoint = uri;
            return result;
        }

        #endregion

        #region [ Payload & FormUrlEncoded ]

        /// <summary>
        /// Payload content
        /// </summary>
        object PayloadContent { get; set; }

        /// <summary>
        /// Payload content's type
        /// </summary>
        Type PayloadContentType { get; set; }

        /// <summary>
        /// Sets payload as generic type
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="payload"></param>
        /// <returns></returns>
        public RestBuilder Payload<T>(T payload)
        {
            var result = (RestBuilder)this.MemberwiseClone();
            result.PayloadContent = payload;
            result.PayloadContentType = typeof(T);
            return result;
        }

        /// <summary>
        /// Defines is enabled x-form-urlencoded
        /// </summary>
        bool IsEnabledFormUrlEncoded { get; set; } = false;

        /// <summary>
        /// Params for x-www-form-urlencoded
        /// </summary>
        Dictionary<string, string> FormUrlEncodedKeyValues { get; set; }

        /// <summary>
        /// Enable form Url Encoding
        /// </summary>
        /// <param name="enableFormUrlEncoded"></param>
        /// <returns></returns>
        public RestBuilder EnableFormUrlEncoded(bool enableFormUrlEncoded = true)
        {
            var result = (RestBuilder)this.MemberwiseClone();
            result.IsEnabledFormUrlEncoded = enableFormUrlEncoded;
            return result;
        }

        /// <summary>
        /// x-www-form-urlencoded key values
        /// </summary>
        /// <param name="keyValues"></param>
        /// <returns></returns>
        public RestBuilder FormUrlEncoded(Dictionary<string, string> keyValues)
        {
            var result = (RestBuilder)this.MemberwiseClone();
            result.FormUrlEncodedKeyValues = keyValues;
            return result;
        }

        /// <summary>
        /// x-www-form-urlencoded key values
        /// </summary>
        /// <param name="kesValues"></param>
        /// <returns></returns>
        public RestBuilder FormUrlEncoded(Action<Dictionary<string, string>> kesValues)
        {
            var result = (RestBuilder)this.MemberwiseClone();
            result.FormUrlEncodedKeyValues = new Dictionary<string, string>();
            kesValues(result.FormUrlEncodedKeyValues);
            return result;
        }

        #endregion

        #region [ OnStart ]

        /// <summary>
        /// On Start Action
        /// </summary>
        Action<StartEventArgs> OnStartAction = null;

        /// <summary>
        /// Sets onStart action and occurs when the request is started
        /// </summary>
        /// <param name="onStart"></param>
        /// <returns></returns>
        public RestBuilder OnStart(Action<StartEventArgs> onStart)
        {
            var result = (RestBuilder)this.MemberwiseClone();
            result.OnStartAction = onStart;
            return result;
        }

        #endregion

        #region [ Upload & Download Progress ]

        /// <summary>
        /// On Upload Progress Action
        /// </summary>
        Action<ProgressEventArgs> OnUploadProgressAction = null;

        /// <summary>
        /// Sets onUploadProgress action, occurs when the request is processing 
        /// </summary>
        /// <param name="onProgress"></param>
        /// <returns></returns>
        public RestBuilder OnUploadProgress(Action<ProgressEventArgs> onProgress)
        {
            var result = (RestBuilder)this.MemberwiseClone();
            result.OnUploadProgressAction = onProgress;
            return result;
        }

        /// <summary>
        /// On Download Progress
        /// </summary>
        Action<ProgressEventArgs> OnDownloadProgressAction = null;

        /// <summary>
        /// Sets onDownloadProgress action, occurs when the response is processing 
        /// </summary>
        /// <param name="onProgress"></param>
        /// <returns></returns>
        public RestBuilder OnDownloadProgress(Action<ProgressEventArgs> onProgress)
        {
            var result = (RestBuilder)this.MemberwiseClone();
            result.OnDownloadProgressAction = onProgress;
            return result;
        }

        #endregion

        #region [ PreResult ]

        /// <summary>
        /// On Pre Result Action
        /// </summary>
        Action<RestResult> OnPreResultAction = null;

        /// <summary>
        /// Sets onPreResult, occurs when the result of request il builded and it isn't completed yet
        /// </summary>
        /// <param name="restResult"></param>
        /// <returns></returns>
        [Obsolete("OnPreResult method will be removed soon, use: .OnPreCompleted((e)=> { var result = e.Result; })", false)]
        public RestBuilder OnPreResult(Action<RestResult> restResult)
        {
            var result = (RestBuilder)this.MemberwiseClone();
            result.OnPreResultAction = restResult;
            return result;
        }

        /// <summary>
        /// On Pre-Completed Action
        /// </summary>
        Action<PreCompletedEventArgs> OnPreCompletedAction = null;

        /// <summary>
        /// Sets OnPreCompleted, occurs when the result of request il builded and it isn't completed yet
        /// </summary>
        /// <param name="restResult"></param>
        /// <returns></returns>
        public RestBuilder OnPreCompleted(Action<PreCompletedEventArgs> onPreCompleted)
        {
            var result = (RestBuilder)this.MemberwiseClone();
            result.OnPreCompletedAction = onPreCompleted;
            return result;
        }

        #endregion

        #region [ OnPreviewContentAsString ]

        /// <summary>
        /// On Preview Content As String Action
        /// </summary>
        Action<PreviewContentAsStringEventArgs> OnPreviewContentAsStringAction = null;

        /// <summary>
        ///  Sets OnPreviewContentAsString, displays the response as string
        /// </summary>
        /// <param name="onPreviewContent"></param>
        /// <returns></returns>
        public RestBuilder OnPreviewContentAsString(Action<PreviewContentAsStringEventArgs> onPreviewContent)
        {
            var result = (RestBuilder)this.MemberwiseClone();
            result.OnPreviewContentAsStringAction = onPreviewContent;
            return result;
        }

        #endregion

        #region [ Completed ]

        /// <summary>
        /// On Completed Action
        /// </summary>
        Action<EventArgs> OnCompletedActionEA = null;

        /// <summary>
        /// Sets onCompleted action, occurs when the call is completed.
        /// </summary>
        /// <param name="onCompleted"></param>
        /// <returns></returns>
        //[Obsolete("OnCompleted(Action<EventArgs> onCompleted) method will be removed soon, use: .OnPreCompleted((e)=> { var result = e.Result; })", false)]
        //public RestBuilder OnCompleted(Action<EventArgs> onCompleted)
        //{
        //    var result = (RestBuilder)this.MemberwiseClone();
        //    result.OnCompletedActionEA = onCompleted;
        //    return result;
        //}

        /// <summary>
        /// On Completed Action
        /// </summary>
        Action<CompletedEventArgs> OnCompletedAction = null;

        /// <summary>
        /// Sets onCompleted action, occurs when the call is completed.
        /// </summary>
        /// <param name="onCompleted"></param>
        /// <returns></returns>
        public RestBuilder OnCompleted(Action<CompletedEventArgs> onCompleted)
        {
            var result = (RestBuilder)this.MemberwiseClone();
            result.OnCompletedAction = onCompleted;
            return result;
        }

        #endregion

        #region [ Exception ]

        /// <summary>
        /// On Exception Action
        /// </summary>
        Action<Exception> OnExceptionAction = null;

        /// <summary>
        /// Sets onException, occurs when there is an exception during the call
        /// </summary>
        /// <param name="exception"></param>
        /// <returns></returns>
        public RestBuilder OnException(Action<Exception> exception)
        {
            var result = (RestBuilder)this.MemberwiseClone();
            result.OnExceptionAction = exception;
            return result;
        }

        #endregion

        #region [ Http Methods ]

        #region [ Get ]
        /// <summary>
        ///  Send a GET request to the specified Uri as an asynchronous operation
        /// </summary>
        /// <returns></returns>
        public async Task<RestResult<string>> GetAsync(CancellationToken token = new CancellationToken())
            => await SendAsStringAsync(HttpMethod.Get, PayloadContent, PayloadContentType, token);

        /// <summary>
        ///  Send a GET request to the specified Uri as an asynchronous operation
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public async Task<RestResult<T>> GetAsync<T>(CancellationToken token = new CancellationToken()) where T : new()
            => await SendAsync<T>(HttpMethod.Get, PayloadContent, PayloadContentType, token);

        /// <summary>
        ///  Send a GET request to the specified Uri as an asynchronous operation
        /// </summary>
        /// <returns></returns>
        public async Task<RestResult<byte[]>> GetAsByteArrayAsync(CancellationToken token = new CancellationToken())
            => await SendAsByteArrayAsync(HttpMethod.Get, PayloadContent, PayloadContentType, token);

        /// <summary>
        ///  Send a GET request to the specified Uri as an asynchronous operation
        /// </summary>
        /// <returns></returns>
        public async Task<RestResult<Stream>> GetAsStreamAsync(CancellationToken token = new CancellationToken())
            => await SendAsStreamAsync(HttpMethod.Get, PayloadContent, PayloadContentType, token);

        /// <summary>
        /// Send a GET request to the specified Uri as an synchronous operation
        /// </summary>
        /// <returns></returns>
        public RestResult<string> Get()
            => GetAsync().Result;

        /// <summary>
        /// Send a GET request to the specified Uri as an synchronous operation
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        public RestResult<string> Get(string url)
            => Url(url).Get();

        /// <summary>
        /// Send a GET request to the specified Uri as an synchronous operation
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        public RestResult<string> Get(Uri url)
            => Url(url).Get();

        /// <summary>
        /// Send a GET request to the specified Uri as an synchronous operation
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public RestResult<T> Get<T>() where T : new()
            => this.GetAsync<T>().Result;

        /// <summary>
        /// Send a GET request to the specified Uri as an synchronous operation
        /// </summary>
        /// <returns></returns>
        public RestResult<byte[]> GetAsByteArray()
            => GetAsByteArrayAsync().Result;

        /// <summary>
        /// Send a GET request to the specified Uri as an synchronous operation
        /// </summary>
        /// <returns></returns>
        public RestResult<Stream> GetAsStream()
            => GetAsStreamAsync().Result;
        #endregion

        #region [ Post ]

        /// <summary>
        /// Send a POST request to the specified Uri as an asynchronous operation
        /// </summary>
        /// <returns></returns>
        public async Task<RestResult<string>> PostAsync(CancellationToken token = new CancellationToken())
            => await SendAsStringAsync(HttpMethod.Post, PayloadContent, PayloadContentType, token);

        /// <summary>
        /// Send a POST request to the specified Uri as an asynchronous operation
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public async Task<RestResult<T>> PostAsync<T>(CancellationToken token = new CancellationToken()) where T : new()
            => await SendAsync<T>(HttpMethod.Post, PayloadContent, PayloadContentType, token);

        /// <summary>
        /// Send a POST request to the specified Uri as an asynchronous operation
        /// </summary>
        /// <returns></returns>
        public async Task<RestResult<byte[]>> PostAsByteArrayAsync(CancellationToken token = new CancellationToken())
            => await SendAsByteArrayAsync(HttpMethod.Post, PayloadContent, PayloadContentType, token);

        /// <summary>
        /// Send a POST request to the specified Uri as an asynchronous operation
        /// </summary>
        /// <returns></returns>
        public async Task<RestResult<Stream>> PostAsStreamAsync(CancellationToken token = new CancellationToken())
            => await SendAsStreamAsync(HttpMethod.Post, PayloadContent, PayloadContentType, token);

        /// <summary>
        /// Send a POST request to the specified Uri as a synchronous operation
        /// </summary>
        /// <returns></returns>
        public RestResult<string> Post()
            => PostAsync().Result;

        /// <summary>
        /// Send a POST request to the specified Uri as a synchronous operation
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public RestResult<T> Post<T>() where T : new()
            => PostAsync<T>().Result;

        /// <summary>
        /// Send a POST request to the specified Uri as a synchronous operation
        /// </summary>
        /// <returns></returns>
        public RestResult<byte[]> PostAsByteArray()
            => PostAsByteArrayAsync().Result;

        /// <summary>
        /// Send a POST request to the specified Uri as a synchronous operation
        /// </summary>
        /// <returns></returns>
        public RestResult<Stream> PostAsStream()
            => PostAsStreamAsync().Result;
        #endregion

        #region [ Put ]

        /// <summary>
        /// Send a PUT request to the specified Uri as an asynchronous operation.
        /// </summary>
        /// <returns></returns>
        public async Task<RestResult<string>> PutAsync(CancellationToken token = new CancellationToken())
            => await SendAsStringAsync(HttpMethod.Put, PayloadContent, PayloadContentType, token);

        /// <summary>
        /// Send a PUT request to the specified Uri as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public async Task<RestResult<T>> PutAsync<T>(CancellationToken token = new CancellationToken()) where T : new()
            => await SendAsync<T>(HttpMethod.Put, PayloadContent, PayloadContentType, token);

        /// <summary>
        /// Send a PUT request to the specified Uri as an asynchronous operation.
        /// </summary>
        /// <returns></returns>
        public async Task<RestResult<byte[]>> PutAsByteArrayAsync(CancellationToken token = new CancellationToken())
            => await SendAsByteArrayAsync(HttpMethod.Put, PayloadContent, PayloadContentType, token);

        /// <summary>
        /// Send a PUT request to the specified Uri as an asynchronous operation.
        /// </summary>
        /// <returns></returns>
        public async Task<RestResult<Stream>> PutAsStreamAsync(CancellationToken token = new CancellationToken())
            => await SendAsStreamAsync(HttpMethod.Put, PayloadContent, PayloadContentType, token);

        /// <summary>
        /// Send a PUT request to the specified Uri as a synchronous operation.
        /// </summary>
        /// <returns></returns>
        public RestResult<string> Put()
            => PutAsync().Result;

        /// <summary>
        /// Send a PUT request to the specified Uri as a synchronous operation.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public RestResult<T> Put<T>() where T : new()
            => PutAsync<T>().Result;

        /// <summary>
        /// Send a PUT request to the specified Uri as a synchronous operation.
        /// </summary>
        /// <returns></returns>
        public RestResult<byte[]> PutAsByteArray()
            => PutAsByteArrayAsync().Result;

        /// <summary>
        /// Send a PUT request to the specified Uri as a synchronous operation.
        /// </summary>
        /// <returns></returns>
        public RestResult<Stream> PutAsStream()
            => PutAsStreamAsync().Result;
        #endregion

        #region [ Delete ]

        /// <summary>
        ///  Send a DELETE request to the specified Uri as an asynchronous operation.
        /// </summary>
        /// <returns></returns>
        public async Task<RestResult<string>> DeleteAsync(CancellationToken token = new CancellationToken())
            => await SendAsStringAsync(HttpMethod.Delete, PayloadContent, PayloadContentType, token);

        /// <summary>
        ///  Send a DELETE request to the specified Uri as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public async Task<RestResult<T>> DeleteAsync<T>(CancellationToken token = new CancellationToken()) where T : new()
            => await SendAsync<T>(HttpMethod.Delete, PayloadContent, PayloadContentType, token);

        /// <summary>
        ///  Send a DELETE request to the specified Uri as an asynchronous operation.
        /// </summary>
        /// <returns></returns>
        public async Task<RestResult<byte[]>> DeleteAsByteArrayAsync(CancellationToken token = new CancellationToken())
            => await SendAsByteArrayAsync(HttpMethod.Delete, PayloadContent, PayloadContentType, token);

        /// <summary>
        ///  Send a DELETE request to the specified Uri as an asynchronous operation.
        /// </summary>
        /// <returns></returns>
        public async Task<RestResult<Stream>> DeleteAsStreamAsync(CancellationToken token = new CancellationToken())
            => await SendAsStreamAsync(HttpMethod.Delete, PayloadContent, PayloadContentType, token);

        /// <summary>
        /// Send a DELETE request to the specified Uri as a synchronous operation.
        /// </summary>
        /// <returns></returns>
        public RestResult<string> Delete()
            => DeleteAsync().Result;

        /// <summary>
        /// Send a DELETE request to the specified Uri as a synchronous operation.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public RestResult<T> Delete<T>() where T : new()
            => DeleteAsync<T>().Result;

        /// <summary>
        /// Send a DELETE request to the specified Uri as a synchronous operation.
        /// </summary>
        /// <returns></returns>
        public RestResult<byte[]> DeleteAsByteArray()
            => DeleteAsByteArrayAsync().Result;

        /// <summary>
        /// Send a DELETE request to the specified Uri as a synchronous operation.
        /// </summary>
        /// <returns></returns>
        public RestResult<Stream> DeleteAsStream()
            => DeleteAsStreamAsync().Result;
        #endregion

        #region [ Patch ]
        HttpMethod PATCH = new HttpMethod("PATCH");

        /// <summary>
        /// Send a PATCH request to the specified Uri as an asynchronous operation
        /// </summary>
        /// <returns></returns>
        public async Task<RestResult<string>> PatchAsync(CancellationToken token = new CancellationToken())
            => await SendAsStringAsync(PATCH, PayloadContent, PayloadContentType, token);

        /// <summary>
        /// Send a PATCH request to the specified Uri as an asynchronous operation
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public async Task<RestResult<T>> PatchAsync<T>(CancellationToken token = new CancellationToken()) where T : new()
            => await SendAsync<T>(PATCH, PayloadContent, PayloadContentType, token);

        /// <summary>
        /// Send a PATCH request to the specified Uri as an asynchronous operation
        /// </summary>
        /// <returns></returns>
        public async Task<RestResult<byte[]>> PatchAsByteArrayAsync(CancellationToken token = new CancellationToken())
            => await SendAsByteArrayAsync(PATCH, PayloadContent, PayloadContentType, token);

        /// <summary>
        /// Send a PATCH request to the specified Uri as an asynchronous operation
        /// </summary>
        /// <returns></returns>
        public async Task<RestResult<Stream>> PatchAsStreamAsync(CancellationToken token = new CancellationToken())
            => await SendAsStreamAsync(PATCH, PayloadContent, PayloadContentType, token);

        /// <summary>
        /// Send a PATCH request to the specified Uri as a synchronous operation
        /// </summary>
        /// <returns></returns>
        public RestResult<string> Patch()
            => PatchAsync().Result;

        /// <summary>
        /// Send a PATCH request to the specified Uri as a synchronous operation
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public RestResult<T> Patch<T>() where T : new()
            => PatchAsync<T>().Result;

        /// <summary>
        /// Send a PATCH request to the specified Uri as a synchronous operation
        /// </summary>
        /// <returns></returns>
        public RestResult<byte[]> PatchAsByteArray()
            => PatchAsByteArrayAsync().Result;

        /// <summary>
        /// Send a PATCH request to the specified Uri as a synchronous operation
        /// </summary>
        /// <returns></returns>
        public RestResult<Stream> PatchAsStream()
            => PatchAsStreamAsync().Result;

        #endregion

        #region [ Custom Call ]

        /// <summary>
        /// Send a CUSTOM CALL request to the specified Uri as an asynchronous operation
        /// </summary>
        /// <returns></returns>
        public async Task<RestResult<string>> CustomCallAsync(HttpMethod method, CancellationToken token = new CancellationToken())
            => await SendAsStringAsync(method, PayloadContent, PayloadContentType, token);

        /// <summary>
        /// Send a CUSTOM CALL request to the specified Uri as an asynchronous operation
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public async Task<RestResult<T>> CustomCallAsync<T>(HttpMethod method, CancellationToken token = new CancellationToken()) where T : new()
            => await SendAsync<T>(method, PayloadContent, PayloadContentType, token);

        /// <summary>
        /// Send a CUSTOM CALL request to the specified Uri as an asynchronous operation
        /// </summary>
        /// <returns></returns>
        public async Task<RestResult<byte[]>> CustomCallAsByteArrayAsync(HttpMethod method, CancellationToken token = new CancellationToken())
            => await SendAsByteArrayAsync(method, PayloadContent, PayloadContentType, token);

        /// <summary>
        /// Send a CUSTOM CALL request to the specified Uri as an asynchronous operation
        /// </summary>
        /// <returns></returns>
        public async Task<RestResult<Stream>> CustomCallAsStreamAsync(HttpMethod method, CancellationToken token = new CancellationToken())
            => await SendAsStreamAsync(method, PayloadContent, PayloadContentType, token);

        /// <summary>
        /// Send a CUSTOM CALL request to the specified Uri as a synchronous operation
        /// </summary>
        /// <returns></returns>
        public RestResult<string> CustomCall(HttpMethod method)
            => CustomCallAsync(method).Result;

        /// <summary>
        /// Send a CUSTOM CALL request to the specified Uri as a synchronous operation
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public RestResult<T> CustomCall<T>(HttpMethod method) where T : new()
            => CustomCallAsync<T>(method).Result;

        /// <summary>
        /// Send a CUSTOM CALL request to the specified Uri as a synchronous operation
        /// </summary>
        /// <returns></returns>
        public RestResult<byte[]> CustomCallAsByteArray(HttpMethod method)
            => CustomCallAsByteArrayAsync(method).Result;

        /// <summary>
        /// Send a CUSTOM CALL request to the specified Uri as a synchronous operation
        /// </summary>
        /// <returns></returns>
        public RestResult<Stream> CustomCallAsStream(HttpMethod method)
            => CustomCallAsStreamAsync(method).Result;

        #endregion

        #region [ Download ]

        /// <summary>
        /// Send a GET request to download file from the specified Uri as an asynchronous operation
        /// </summary>
        /// <param name="url"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        public Task<RestResult<byte[]>> DownloadAsync(string url, CancellationToken token = new CancellationToken())
            => Url(url).GetAsByteArrayAsync(token);

        /// <summary>
        /// Send a GET request to download file from the specified Uri as an asynchronous operation
        /// </summary>
        /// <param name="url"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        public Task<RestResult<byte[]>> DownloadAsync(Uri url, CancellationToken token = new CancellationToken())
            => Url(url).GetAsByteArrayAsync(token);

        /// <summary>
        /// Send a GET request to download file from the specified Uri as a synchronous operation
        /// </summary>
        /// <param name="url"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        public RestResult<byte[]> Download(string url)
            => DownloadAsync(url).Result;

        /// <summary>
        /// Send a GET request to download file from the specified Uri as a synchronous operation
        /// </summary>
        /// <param name="url"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        public RestResult<byte[]> Download(Uri url)
            => DownloadAsync(url).Result;

        #endregion

        #endregion

        #region [ Fetch ]

        /// <summary>
        /// isAfterRefreshTokenCalled
        /// </summary>
        bool isAfterRefreshTokenCalled = false;

        /// <summary>
        ///  Send an HTTP request as an asynchronous operation.
        /// </summary>
        /// <param name="method"></param>
        /// <param name="payloadContent"></param>
        /// <param name="payloadContentType"></param>
        /// <returns></returns>
        private async Task<RestResult<string>> SendAsStringAsync(HttpMethod method,
            object payloadContent = null,
            Type payloadContentType = null,
            CancellationToken cancellationToken = new CancellationToken())
        {
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            RestResult<string> response = null;
            try
            {
                string url = BuildFinalUrl();

                if (Logger) Console.WriteLine(url);

                StartEventArgs startEventArgs = new StartEventArgs()
                {
                    Cancel = false,
                    Payload = payloadContent,
                    Url = url
                };
                OnStartAction?.Invoke(startEventArgs);

                if (startEventArgs.Cancel) throw new OperationCanceledException();

                HttpRequestMessage request = new HttpRequestMessage(method, url);
                HttpResponseMessage responseMessage = null;

                #region [ Sending ]

                if (!IsEnabledFormUrlEncoded)
                {
                    if (payloadContent != null)
                    {
                        string json = Serializer.SerializeObject(payloadContent, payloadContentType);
                        HttpContent hc = new StringContent(json, Encoding.UTF8, Serializer.MediaTypeAsString);
                        request.Content = hc;

                        using (HttpContentStream streamContent = new HttpContentStream(request.Content, Properties.BufferSize))
                        {
                            streamContent.ProgressChanged += (s, e) => OnUploadProgressAction?.Invoke(e);
                            responseMessage = await streamContent.WriteStringAsStreamAsync(HttpClient, request, cancellationToken);
                        }
                    }
                    else
                    {
                        responseMessage = await HttpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                    }
                }
                else
                {
                    request.Content = new FormUrlEncodedContent(FormUrlEncodedKeyValues);
                    responseMessage = await HttpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                }

                if (!isAfterRefreshTokenCalled && RefreshTokenExecution && responseMessage.StatusCode == HttpStatusCode.Unauthorized)
                {
                    isAfterRefreshTokenCalled = true;

                    if (RefreshTokenApi != null && RefreshTokenApi().StatusCode == HttpStatusCode.OK)
                        return await SendAsStringAsync(method, payloadContent, payloadContentType, cancellationToken);

                    if (RefreshTokenApiAsync != null && (await RefreshTokenApiAsync()).StatusCode == HttpStatusCode.OK)
                        return await SendAsStringAsync(method, payloadContent, payloadContentType, cancellationToken);
                }

                using (HttpContentStream streamContent = new HttpContentStream(responseMessage.Content, Properties.BufferSize))
                {
                    streamContent.ProgressChanged += (s, e) => OnDownloadProgressAction?.Invoke(e);
                    response = RestResult<string>.CreateInstanceFrom<string>(responseMessage);
                    var result = await streamContent.ReadStringAsStreamAsync(cancellationToken);
                    OnPreviewContentAsStringAction?.Invoke(new PreviewContentAsStringEventArgs { ContentAsString = result });
                    //response.StringContent = result;
                    response.Content = result;
                }

                #endregion

                OnPreResultAction?.Invoke(response);
                OnPreCompletedAction?.Invoke(new PreCompletedEventArgs { IsCompleted = true, Result = response });
            }
            catch (Exception ex)
            {
                OnExceptionAction?.Invoke(ex);
                response = RestResult<string>.CreateFromException(ex);
            }

            stopwatch.Stop();

            OnCompletedAction?.Invoke(new CompletedEventArgs { Result = response, ExecutionTime = stopwatch.Elapsed });
            OnCompletedActionEA?.Invoke(new EventArgs());

            return response;
        }

        /// <summary>
        ///  Send an HTTP request as an asynchronous operation.
        /// </summary>
        /// <param name="method"></param>
        /// <param name="payloadContent"></param>
        /// <param name="payloadContentType"></param>
        /// <returns></returns>
        private async Task<RestResult<Stream>> SendAsStreamAsync(HttpMethod method,
            object payloadContent = null,
            Type payloadContentType = null,
            CancellationToken cancellationToken = new CancellationToken())
        {
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            RestResult<Stream> response = null;
            try
            {
                string url = BuildFinalUrl();

                if (Logger) Console.WriteLine(url);

                StartEventArgs startEventArgs = new StartEventArgs()
                {
                    Cancel = false,
                    Payload = payloadContent,
                    Url = url
                };
                OnStartAction?.Invoke(startEventArgs);
                if (startEventArgs.Cancel) throw new OperationCanceledException();

                HttpRequestMessage request = new HttpRequestMessage(method, url);
                HttpResponseMessage responseMessage = null;

                #region [ Sending ]

                if (!IsEnabledFormUrlEncoded)
                {
                    if (payloadContent != null)
                    {
                        string json = Serializer.SerializeObject(payloadContent, payloadContentType);
                        HttpContent hc = new StringContent(json, Encoding.UTF8, Serializer.MediaTypeAsString);
                        request.Content = hc;

                        using (HttpContentStream streamContent = new HttpContentStream(request.Content, Properties.BufferSize))
                        {
                            streamContent.ProgressChanged += (s, e) => OnUploadProgressAction?.Invoke(e);
                            responseMessage = await streamContent.WriteStringAsStreamAsync(HttpClient, request, cancellationToken);
                        }
                    }
                    else
                    {
                        responseMessage = await HttpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                    }
                }
                else
                {
                    request.Content = new FormUrlEncodedContent(FormUrlEncodedKeyValues);
                    responseMessage = await HttpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                }

                if (!isAfterRefreshTokenCalled && RefreshTokenExecution && responseMessage.StatusCode == HttpStatusCode.Unauthorized)
                {
                    isAfterRefreshTokenCalled = true;

                    if (RefreshTokenApi != null && RefreshTokenApi().StatusCode == HttpStatusCode.OK)
                        return await SendAsStreamAsync(method, payloadContent, payloadContentType, cancellationToken);

                    if (RefreshTokenApiAsync != null && (await RefreshTokenApiAsync()).StatusCode == HttpStatusCode.OK)
                        return await SendAsStreamAsync(method, payloadContent, payloadContentType, cancellationToken);
                }

                using (HttpContentStream streamContent = new HttpContentStream(responseMessage.Content, Properties.BufferSize))
                {
                    streamContent.ProgressChanged += (s, e) => OnDownloadProgressAction?.Invoke(e);
                    response = RestResult<Stream>.CreateInstanceFrom<Stream>(responseMessage);
                    OnPreviewContentAsStringAction?.Invoke(new PreviewContentAsStringEventArgs { ContentAsString = string.Empty });
                    response.Content = await responseMessage.Content.ReadAsStreamAsync();
                }

                #endregion

                OnPreResultAction?.Invoke(response);
                OnPreCompletedAction?.Invoke(new PreCompletedEventArgs { IsCompleted = true, Result = response });
            }
            catch (Exception ex)
            {
                OnExceptionAction?.Invoke(ex);
                response = RestResult<Stream>.CreateFromException(ex);
            }

            stopwatch.Stop();

            OnCompletedAction?.Invoke(new CompletedEventArgs { Result = response, ExecutionTime = stopwatch.Elapsed });
            OnCompletedActionEA?.Invoke(new EventArgs());

            return response;
        }

        /// <summary>
        ///  Send an HTTP request as an asynchronous operation.
        /// </summary>
        /// <param name="method"></param>
        /// <param name="payloadContent"></param>
        /// <param name="payloadContentType"></param>
        /// <returns></returns>
        private async Task<RestResult<byte[]>> SendAsByteArrayAsync(HttpMethod method,
            object payloadContent = null,
            Type payloadContentType = null,
            CancellationToken cancellationToken = new CancellationToken())
        {
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            RestResult<byte[]> response = null;
            try
            {
                string url = BuildFinalUrl();

                if (Logger) Console.WriteLine(url);

                StartEventArgs startEventArgs = new StartEventArgs()
                {
                    Cancel = false,
                    Payload = payloadContent,
                    Url = url
                };

                OnStartAction?.Invoke(startEventArgs);
                if (startEventArgs.Cancel) throw new OperationCanceledException();

                HttpRequestMessage request = new HttpRequestMessage(method, url);
                HttpResponseMessage responseMessage = null;

                #region [ Sending ]

                if (!IsEnabledFormUrlEncoded)
                {
                    if (payloadContent != null)
                    {
                        string json = Serializer.SerializeObject(payloadContent, payloadContentType);
                        HttpContent hc = new StringContent(json, Encoding.UTF8, Serializer.MediaTypeAsString);
                        request.Content = hc;

                        using (HttpContentStream streamContent = new HttpContentStream(request.Content, Properties.BufferSize))
                        {
                            streamContent.ProgressChanged += (s, e) => OnUploadProgressAction?.Invoke(e);
                            responseMessage = await streamContent.WriteStringAsStreamAsync(HttpClient, request, cancellationToken);
                        }
                    }
                    else
                    {
                        responseMessage = await HttpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                    }
                }
                else
                {
                    request.Content = new FormUrlEncodedContent(FormUrlEncodedKeyValues);
                    responseMessage = await HttpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                }

                if (!isAfterRefreshTokenCalled && RefreshTokenExecution && responseMessage.StatusCode == HttpStatusCode.Unauthorized)
                {
                    isAfterRefreshTokenCalled = true;

                    if (RefreshTokenApi != null && RefreshTokenApi().StatusCode == HttpStatusCode.OK)
                        return await SendAsByteArrayAsync(method, payloadContent, payloadContentType, cancellationToken);

                    if (RefreshTokenApiAsync != null && (await RefreshTokenApiAsync()).StatusCode == HttpStatusCode.OK)
                        return await SendAsByteArrayAsync(method, payloadContent, payloadContentType, cancellationToken);
                }

                using (HttpContentStream streamContent = new HttpContentStream(responseMessage.Content, Properties.BufferSize))
                {
                    streamContent.ProgressChanged += (s, e) => OnDownloadProgressAction?.Invoke(e);
                    response = RestResult<byte[]>.CreateInstanceFrom<byte[]>(responseMessage);
                    response.Content = await streamContent.ReadBytesAsStreamAsync(cancellationToken);
                    //response.StringContent = BitConverter.ToString(response.Content);
                    OnPreviewContentAsStringAction?.Invoke(new PreviewContentAsStringEventArgs { ContentAsString = BitConverter.ToString(response.Content) });
                }

                #endregion

                OnPreResultAction?.Invoke(response);
                OnPreCompletedAction?.Invoke(new PreCompletedEventArgs { IsCompleted = true, Result = response });
            }
            catch (Exception ex)
            {
                OnExceptionAction?.Invoke(ex);
                response = RestResult<byte[]>.CreateFromException(ex);
            }

            stopwatch.Stop();

            OnCompletedAction?.Invoke(new CompletedEventArgs { Result = response, ExecutionTime = stopwatch.Elapsed });
            OnCompletedActionEA?.Invoke(new EventArgs());

            return response;
        }

        /// <summary>
        ///  Send an HTTP request as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="method"></param>
        /// <param name="payloadContent"></param>
        /// <param name="payloadContentType"></param>
        /// <returns></returns>
        private async Task<RestResult<T>> SendAsync<T>(HttpMethod method,
            object payloadContent = null,
            Type payloadContentType = null,
            CancellationToken cancellationToken = new CancellationToken())
            where T : new()
        {
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            RestResult<T> response = null;
            try
            {
                string url = BuildFinalUrl();

                if (Logger) Console.WriteLine(url);

                StartEventArgs startEventArgs = new StartEventArgs()
                {
                    Cancel = false,
                    Payload = payloadContent,
                    Url = url
                };

                OnStartAction?.Invoke(startEventArgs);
                if (startEventArgs.Cancel) throw new OperationCanceledException();

                HttpRequestMessage request = new HttpRequestMessage(method, url);
                HttpResponseMessage responseMessage = null;

                #region [ Sending ]

                if (!IsEnabledFormUrlEncoded)
                {
                    if (payloadContent != null)
                    {
                        string json = Serializer.SerializeObject(payloadContent, payloadContentType);
                        HttpContent hc = new StringContent(json, Encoding.UTF8, Serializer.MediaTypeAsString);
                        request.Content = hc;

                        using (HttpContentStream streamContent = new HttpContentStream(request.Content, Properties.BufferSize))
                        {
                            streamContent.ProgressChanged += (s, e) => OnUploadProgressAction?.Invoke(e);
                            responseMessage = await streamContent.WriteStringAsStreamAsync(HttpClient, request, cancellationToken);
                        }
                    }
                    else
                    {
                        responseMessage = await HttpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                    }
                }
                else
                {
                    request.Content = new FormUrlEncodedContent(FormUrlEncodedKeyValues);
                    responseMessage = await HttpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                }

                if (!isAfterRefreshTokenCalled && RefreshTokenExecution && responseMessage.StatusCode == HttpStatusCode.Unauthorized)
                {
                    isAfterRefreshTokenCalled = true;

                    if (RefreshTokenApi != null && RefreshTokenApi().StatusCode == HttpStatusCode.OK)
                        return await SendAsync<T>(method, payloadContent, payloadContentType, cancellationToken);

                    if (RefreshTokenApiAsync != null && (await RefreshTokenApiAsync()).StatusCode == HttpStatusCode.OK)
                        return await SendAsync<T>(method, payloadContent, payloadContentType, cancellationToken);
                }

                using (HttpContentStream streamContent = new HttpContentStream(responseMessage.Content, Properties.BufferSize))
                {
                    streamContent.ProgressChanged += (s, e) => OnDownloadProgressAction?.Invoke(e);
                    response = Generic.RestResult<T>.CreateInstanceFrom<T>(responseMessage);
                    response.Content = (T)Serializer.DeserializeObject(await streamContent.ReadStringAsStreamAsync(cancellationToken), typeof(T));
                    OnPreviewContentAsStringAction?.Invoke(new PreviewContentAsStringEventArgs { ContentAsString = await streamContent.ReadStringAsStreamAsync(cancellationToken) });
                }

                #endregion

                OnPreResultAction?.Invoke(response);
                OnPreCompletedAction?.Invoke(new PreCompletedEventArgs { IsCompleted = true, Result = response });
            }
            catch (Exception ex)
            {
                OnExceptionAction?.Invoke(ex);
                response = RestResult<T>.CreateFromException(ex);
            }

            stopwatch.Stop();

            OnCompletedAction?.Invoke(new CompletedEventArgs { Result = response, ExecutionTime = stopwatch.Elapsed });
            OnCompletedActionEA?.Invoke(new EventArgs());

            return response;
        }

        /// <summary>
        /// Builder final url
        /// </summary>
        /// <returns></returns>
        private string BuildFinalUrl()
        {
            StringBuilder urlBuilder = new StringBuilder();

            urlBuilder.Append(Properties.EndPoint.OriginalString);

            urlBuilder.Append(string.Join("", Commands.Select(c => $"{c}")));

            if (Parameters.Count > 0)
            {
                urlBuilder.Append($"?");
                urlBuilder.Append(string.Join("&", Parameters.Select(q => $"{q.Key}={q.Value}")));
            }

            return urlBuilder.ToString();
        }

        #endregion
    }
}