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
    using RestClient.Http;
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
        /// Certificate Callback
        /// </summary>
        Func<object, X509Certificate, X509Chain, SslPolicyErrors, bool> CertificateCallback;

        /// <summary>
        /// Current Credentials
        /// </summary>
        ICredentials Credentials { get; set; }
            = null;

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
        /// Payload content
        /// </summary>
        object PayloadContent { get; set; }

        /// <summary>
        /// Defines is enabled x-form-urlencoded
        /// </summary>
        bool IsEnabledFormUrlEncoded { get; set; } = false;

        /// <summary>
        /// Params for x-www-form-urlencoded
        /// </summary>
        Dictionary<string, string> FormUrlEncodedKeyValues { get; set; }

        /// <summary>
        /// On Start Action
        /// </summary>
        Action<StartEventArgs> OnStartAction = null;

        /// <summary>
        /// On Preview Content Request As String Action
        /// </summary>
        Action<PreviewContentAsStringEventArgs> OnPreviewContentRequestAsStringAction = null;

        /// <summary>
        /// On Upload Progress Action
        /// </summary>
        Action<ProgressEventArgs> OnUploadProgressAction = null;

        /// <summary>
        /// On Download Progress
        /// </summary>
        Action<ProgressEventArgs> OnDownloadProgressAction = null;

        /// <summary>
        /// On Pre Result Action
        /// </summary>
        Action<RestResult> OnPreResultAction = null;

        /// <summary>
        /// On Pre-Completed Action
        /// </summary>
        Action<PreCompletedEventArgs> OnPreCompletedAction = null;

        /// <summary>
        /// On Preview Content As String Action
        /// </summary>
        Action<PreviewContentAsStringEventArgs> OnPreviewContentAsStringAction = null;

        /// <summary>
        /// On Completed Action
        /// </summary>
        Action<EventArgs> OnCompletedActionEA = null;

        /// <summary>
        /// On Completed Action
        /// </summary>
        Action<CompletedEventArgs> OnCompletedAction = null;

        /// <summary>
        /// On Exception Action
        /// </summary>
        Action<Exception> OnExceptionAction = null;

        /// <summary>
        /// Is After Refresh Token Called
        /// </summary>
        bool IsAfterRefreshTokenCalled = false;

        /// <summary>
        /// Initializes a new instance
        /// </summary>
        internal RestBuilder()
        {
#if NETSTANDARD2_0 || NETSTANDARD2_1 || NETFRAMEWORK
            ServicePointManager.ServerCertificateValidationCallback = (object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
                => true;
#endif
        }

        #region [ Certificate Validation ]

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
#if NETSTANDARD2_0 || NETSTANDARD2_1 || NETFRAMEWORK
            ServicePointManager.ServerCertificateValidationCallback = new RemoteCertificateValidationCallback(callback);
#endif
            return result;
        }

        #endregion

        #region [ Network Credential ]

        /// <summary>
        /// Authentication information used by this func
        /// </summary>
        /// <param name="credential"></param>
        /// <returns></returns>
        public RestBuilder NetworkCredential(Func<NetworkCredential> credential)
        {
            var result = (RestBuilder)this.MemberwiseClone();
            result.Credentials = credential();
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
            return result;
        }
#if NETSTANDARD2_0 || NETSTANDARD2_1
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
            return result;
        }
#endif
        #endregion

        #region [ Authentication ]

        private Func<AuthenticationHeaderValue> onAuthentication { get; set; } = null;

        /// <summary>
        /// The Authorization header for an HTTP request
        /// </summary>
        /// <param name="authentication"></param>
        /// <returns></returns>
        public RestBuilder Authentication(Func<AuthenticationHeaderValue> authentication)
        {
             var result = (RestBuilder)this.MemberwiseClone();
            result.onAuthentication = new Func<AuthenticationHeaderValue>(() => authentication());
            return result;
        }

        /// <summary>
        /// The Authorization header for an HTTP request (This method will be removed)
        /// </summary>
        /// <param name="authentication"></param>
        /// <returns></returns>
        [Obsolete("OnAuthentication: This method will be removed with 2.0.0 version.", false)]
        public RestBuilder OnAuthentication(Func<AuthenticationHeaderValue> authentication) => Authentication(authentication);

        /// <summary>
        /// The Authorization header for an HTTP request
        /// </summary>
        /// <param name="scheme"></param>
        /// <returns></returns>
        public RestBuilder Authentication(string scheme)
        {
             var result = (RestBuilder)this.MemberwiseClone();
            result.onAuthentication = new Func<AuthenticationHeaderValue>(() => new AuthenticationHeaderValue(scheme));
            return result;
        }

        /// <summary>
        /// The Authorization header for an HTTP request (This method will be removed)
        /// </summary>
        /// <param name="scheme"></param>
        /// <returns></returns>
        [Obsolete("OnAuthentication: This method will be removed with 2.0.0 version.", false)]
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
            result.onAuthentication = new Func<AuthenticationHeaderValue>(() => new AuthenticationHeaderValue(scheme, parameter));
            return result;
        }

        /// <summary>
        /// The Authorization header for an HTTP request (This method will be removed)
        /// </summary>
        /// <param name="scheme"></param>
        /// <param name="parameter"></param>
        /// <returns></returns>
        [Obsolete("OnAuthentication: This method will be removed with 2.0.0 version.", false)]
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
            result.Properties.Timeout = timeOut;
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
            result.Properties.Timeout = TimeSpan.FromMilliseconds(milliseconds);
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
             if (bufferSize <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(bufferSize));
            }

            var result = (RestBuilder)this.MemberwiseClone();
            result.Properties.BufferSize = bufferSize;
            return result;
        }

        #endregion

        #region [ Header ]

        Action<HttpRequestHeaders> onDefaultRequestHeaders { get; set; }

        /// <summary>
        ///  Sets the headers which should be sent with this o children requests
        /// </summary>
        /// <param name="defaultRequestHeaders"></param>
        /// <returns></returns>
        public RestBuilder Header(Action<HttpRequestHeaders> defaultRequestHeaders)
        {
             var result = (RestBuilder)this.MemberwiseClone();
            result.onDefaultRequestHeaders = defaultRequestHeaders;
            return result;
        }

        #endregion

        #region [ Compression ]

        /// <summary>
        /// true if gzip compression is enabled, false otherwise
        /// </summary>
        bool enabledGZipCompression { get; set; } = false;


        /// <summary>
        /// Enables gzip compression
        /// </summary>
        /// <returns></returns>
        public RestBuilder EnableGZipCompression(bool deflate = true)
        {
            var result = (RestBuilder)this.MemberwiseClone();
            result.enabledGZipCompression = true;
            return result;
        }

        /// <summary>
        /// Enables gzip compression
        /// </summary>
        /// <returns></returns>
        [Obsolete("Use: EnableGZipCompression(). This method will be removed with 2.0.0 version.", true)]
        public RestBuilder Compression() => EnableGZipCompression();

        #endregion

        #region [ Command ]

        /// <summary>
        /// Adds the command to request
        /// </summary>
        /// <param name="command"></param>
        /// <returns></returns>
        public RestBuilder Command(string command)
        {
             if (command == null) { throw new ArgumentNullException(); }
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
        public RestBuilder Command(uint command)
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
            if (result.Parameters.ContainsKey(key))
            {
                result.Parameters[key] = value.ToString();
            }
            else
            {
                result.Parameters.Add(key, value.ToString());
            }
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
            result.Parameters.Add(parameter.Key, parameter.Value.ToString());
            foreach (RestParameter p in others)
            {
                result.Parameters.Add(p.Key, p.Value.ToString());
            }
            return result;
        }

        /// <summary>
        /// Adds list of rest parameter to querystring
        /// </summary>
        /// <param name="parameters"></param>
        /// <returns></returns>
        public RestBuilder Parameter(Action<List<RestParameter>> parameters)
        {
            var result = (RestBuilder)this.MemberwiseClone();
            List<RestParameter> list = new List<RestParameter>();
            parameters(list);
            foreach (RestParameter p in list)
            {
                result.Parameters.Add(p.Key, p.Value.ToString());
            }
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
        /// Sets payload as generic type
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="payload"></param>
        /// <returns></returns>
        public RestBuilder Payload<T>(T payload)
        {
            var result = (RestBuilder)this.MemberwiseClone();
            result.PayloadContent = payload;
            return result;
        }

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
        /// <param name="enableFormUrlEncoded"></param>
        /// <param name="keyValues"></param>
        /// <returns></returns>
        public RestBuilder FormUrlEncoded(bool enableFormUrlEncoded, Dictionary<string, string> keyValues)
        {
            var result = (RestBuilder)this.MemberwiseClone();
            result.IsEnabledFormUrlEncoded = enableFormUrlEncoded;
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

        /// <summary>
        /// x-www-form-urlencoded key values
        /// </summary>
        /// <param name="enableFormUrlEncoded"></param>
        /// <param name="kesValues"></param>
        /// <returns></returns>
        public RestBuilder FormUrlEncoded(bool enableFormUrlEncoded, Action<Dictionary<string, string>> kesValues)
        {
            var result = (RestBuilder)this.MemberwiseClone();
            result.IsEnabledFormUrlEncoded = enableFormUrlEncoded;
            result.FormUrlEncodedKeyValues = new Dictionary<string, string>();
            kesValues(result.FormUrlEncodedKeyValues);
            return result;
        }

        #endregion

        #region [ OnStart ]

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

        #region [ OnPreviewContentRequestAsString ]

        /// <summary>
        ///  Sets OnPreviewContentRequestAsString, displays the response as string
        /// </summary>
        /// <param name="onPreviewContent"></param>
        /// <returns></returns>
        public RestBuilder OnPreviewContentRequestAsString(Action<PreviewContentAsStringEventArgs> onPreviewContent)
        {
            var result = (RestBuilder)this.MemberwiseClone();
            result.OnPreviewContentRequestAsStringAction = onPreviewContent;
            return result;
        }

        #endregion

        #region [ OnUploadProgress & OnDownloadProgress ]

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

        #region [ OnPreCompleted ]

        /// <summary>
        /// Sets onPreResult, occurs when the result of request il builded and it isn't completed yet
        /// </summary>
        /// <param name="restResult"></param>
        /// <returns></returns>
        [Obsolete("Use: .OnPreCompleted((e)=> { var result = e.Result; }). OnPreResult method will be removed with 2.0.0 version.", true)]
        public RestBuilder OnPreResult(Action<RestResult> restResult)
        {
            var result = (RestBuilder)this.MemberwiseClone();
            result.OnPreResultAction = restResult;
            return result;
        }

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

        #region [ OnPreviewContentResponseAsString ]

        /// <summary>
        ///  Sets OnPreviewContentResponseAsString, displays the response as string
        /// </summary>
        /// <param name="onPreviewContent"></param>
        /// <returns></returns>
        public RestBuilder OnPreviewContentResponseAsString(Action<PreviewContentAsStringEventArgs> onPreviewContent)
        {
            var result = (RestBuilder)this.MemberwiseClone();
            result.OnPreviewContentAsStringAction = onPreviewContent;
            return result;
        }

        [Obsolete("Use: OnPreviewContentResponseAsString(onPreviewContent). OnPreviewContentAsString This method will be removed with 2.0.0 version.", true)]
        public RestBuilder OnPreviewContentAsString(Action<PreviewContentAsStringEventArgs> onPreviewContent)
            => OnPreviewContentResponseAsString(onPreviewContent);

        #endregion

        #region [ OnCompleted ]

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

        #region [ OnException ]

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
            => await SendAsStringAsync(HttpMethod.Get, token);

        /// <summary>
        ///  Send a GET request to the specified Uri as an asynchronous operation
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public async Task<RestResult<T>> GetAsync<T>(CancellationToken token = new CancellationToken()) where T : new()
            => await SendAsync<T>(HttpMethod.Get, token);

        /// <summary>
        ///  Send a GET request to the specified Uri as an asynchronous operation
        /// </summary>
        /// <returns></returns>
        public async Task<RestResult<byte[]>> GetAsByteArrayAsync(CancellationToken token = new CancellationToken())
            => await SendAsByteArrayAsync(HttpMethod.Get, token);

        /// <summary>
        ///  Send a GET request to the specified Uri as an asynchronous operation
        /// </summary>
        /// <returns></returns>
        public async Task<RestResult<Stream>> GetAsStreamAsync(CancellationToken token = new CancellationToken())
            => await SendAsStreamAsync(HttpMethod.Get, token);

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
            => await SendAsStringAsync(HttpMethod.Post, token);

        /// <summary>
        /// Send a POST request to the specified Uri as an asynchronous operation
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public async Task<RestResult<T>> PostAsync<T>(CancellationToken token = new CancellationToken()) where T : new()
            => await SendAsync<T>(HttpMethod.Post, token);

        /// <summary>
        /// Send a POST request to the specified Uri as an asynchronous operation
        /// </summary>
        /// <returns></returns>
        public async Task<RestResult<byte[]>> PostAsByteArrayAsync(CancellationToken token = new CancellationToken())
            => await SendAsByteArrayAsync(HttpMethod.Post, token);

        /// <summary>
        /// Send a POST request to the specified Uri as an asynchronous operation
        /// </summary>
        /// <returns></returns>
        public async Task<RestResult<Stream>> PostAsStreamAsync(CancellationToken token = new CancellationToken())
            => await SendAsStreamAsync(HttpMethod.Post, token);

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
            => await SendAsStringAsync(HttpMethod.Put, token);

        /// <summary>
        /// Send a PUT request to the specified Uri as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public async Task<RestResult<T>> PutAsync<T>(CancellationToken token = new CancellationToken()) where T : new()
            => await SendAsync<T>(HttpMethod.Put, token);

        /// <summary>
        /// Send a PUT request to the specified Uri as an asynchronous operation.
        /// </summary>
        /// <returns></returns>
        public async Task<RestResult<byte[]>> PutAsByteArrayAsync(CancellationToken token = new CancellationToken())
            => await SendAsByteArrayAsync(HttpMethod.Put, token);

        /// <summary>
        /// Send a PUT request to the specified Uri as an asynchronous operation.
        /// </summary>
        /// <returns></returns>
        public async Task<RestResult<Stream>> PutAsStreamAsync(CancellationToken token = new CancellationToken())
            => await SendAsStreamAsync(HttpMethod.Put, token);

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
            => await SendAsStringAsync(HttpMethod.Delete, token);

        /// <summary>
        ///  Send a DELETE request to the specified Uri as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public async Task<RestResult<T>> DeleteAsync<T>(CancellationToken token = new CancellationToken()) where T : new()
            => await SendAsync<T>(HttpMethod.Delete, token);

        /// <summary>
        ///  Send a DELETE request to the specified Uri as an asynchronous operation.
        /// </summary>
        /// <returns></returns>
        public async Task<RestResult<byte[]>> DeleteAsByteArrayAsync(CancellationToken token = new CancellationToken())
            => await SendAsByteArrayAsync(HttpMethod.Delete, token);

        /// <summary>
        ///  Send a DELETE request to the specified Uri as an asynchronous operation.
        /// </summary>
        /// <returns></returns>
        public async Task<RestResult<Stream>> DeleteAsStreamAsync(CancellationToken token = new CancellationToken())
            => await SendAsStreamAsync(HttpMethod.Delete, token);

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
            => await SendAsStringAsync(PATCH, token);

        /// <summary>
        /// Send a PATCH request to the specified Uri as an asynchronous operation
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public async Task<RestResult<T>> PatchAsync<T>(CancellationToken token = new CancellationToken()) where T : new()
            => await SendAsync<T>(PATCH, token);

        /// <summary>
        /// Send a PATCH request to the specified Uri as an asynchronous operation
        /// </summary>
        /// <returns></returns>
        public async Task<RestResult<byte[]>> PatchAsByteArrayAsync(CancellationToken token = new CancellationToken())
            => await SendAsByteArrayAsync(PATCH, token);

        /// <summary>
        /// Send a PATCH request to the specified Uri as an asynchronous operation
        /// </summary>
        /// <returns></returns>
        public async Task<RestResult<Stream>> PatchAsStreamAsync(CancellationToken token = new CancellationToken())
            => await SendAsStreamAsync(PATCH, token);

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
            => await SendAsStringAsync(method, token);

        /// <summary>
        /// Send a CUSTOM CALL request to the specified Uri as an asynchronous operation
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public async Task<RestResult<T>> CustomCallAsync<T>(HttpMethod method, CancellationToken token = new CancellationToken()) where T : new()
            => await SendAsync<T>(method, token);

        /// <summary>
        /// Send a CUSTOM CALL request to the specified Uri as an asynchronous operation
        /// </summary>
        /// <returns></returns>
        public async Task<RestResult<byte[]>> CustomCallAsByteArrayAsync(HttpMethod method, CancellationToken token = new CancellationToken())
            => await SendAsByteArrayAsync(method, token);

        /// <summary>
        /// Send a CUSTOM CALL request to the specified Uri as an asynchronous operation
        /// </summary>
        /// <returns></returns>
        public async Task<RestResult<Stream>> CustomCallAsStreamAsync(HttpMethod method, CancellationToken token = new CancellationToken())
            => await SendAsStreamAsync(method, token);

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
        ///  Send an HTTP request as an asynchronous operation.
        /// </summary>
        /// <param name="method"></param>
        /// <returns></returns>
        private async Task<RestResult<string>> SendAsStringAsync(HttpMethod method, CancellationToken cancellationToken = new CancellationToken())
        {
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            RestResult<string> response = null;

            try
            {
                string url = BuildFinalUrl();

                StartEventArgs startEventArgs = new StartEventArgs()
                {
                    Cancel = false,
                    Payload = PayloadContent,
                    Url = url
                };

                OnStartAction?.Invoke(startEventArgs);

                if (startEventArgs.Cancel)
                {
                    throw new OperationCanceledException();
                }

                using (HttpRequestMessage request = new HttpRequestMessage(method, url))
                {
                    HttpResponseMessage responseMessage = null;

                    using (request.Content = MakeHttpContent())
                    {
                        using (HttpClient client = new HttpClient(new HttpClientCompressionHandler
                        {
                            EnabledGZipCompression = this.enabledGZipCompression,
                            UploadingProgressChanged = (s, e) => OnUploadProgressAction?.Invoke(e),
                            DownloadingProgressChanged = (s, e) => OnDownloadProgressAction?.Invoke(e),
                            BufferSize = Properties.BufferSize,
                            Credentials = Credentials,
                            ClientCertificateOptions = Properties.CertificateOption,
#if !(NET45 || NET451 || NET452)
                            ServerCertificateCustomValidationCallback = CertificateCallback
#endif
                        })
                        {
                            Timeout = Properties.Timeout,
                        })
                        {
                            if (enabledGZipCompression)
                            {
                                client.DefaultRequestHeaders.AcceptEncoding.Add(new StringWithQualityHeaderValue("gzip"));
                            }
                            if (onAuthentication != null)
                            {
                                client.DefaultRequestHeaders.Authorization = onAuthentication();
                            }

                            onDefaultRequestHeaders?.Invoke(client.DefaultRequestHeaders);

                            responseMessage = await client.SendAsync(request, cancellationToken);

                            if (!IsAfterRefreshTokenCalled && responseMessage.StatusCode == HttpStatusCode.Unauthorized && RefreshTokenExecution)
                            {
                                stopwatch.Stop();

                                IsAfterRefreshTokenCalled = true;

                                if (RefreshTokenApi != null && RefreshTokenApi().StatusCode == HttpStatusCode.OK)
                                {
                                    return await SendAsStringAsync(method, cancellationToken);
                                }

                                if (RefreshTokenApiAsync != null && (await RefreshTokenApiAsync()).StatusCode == HttpStatusCode.OK)
                                {
                                    return await SendAsStringAsync(method, cancellationToken);
                                }
                            }

                            response = RestResult<string>.CreateInstanceFrom<string>(responseMessage);

                            string serializedObject = await responseMessage.Content.ReadAsStringAsync();
                            OnPreviewContentAsStringAction?.Invoke(new PreviewContentAsStringEventArgs { ContentAsString = serializedObject });
                            response.Content = serializedObject;
                        }
                    }
                }

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
        /// <returns></returns>
        private async Task<RestResult<Stream>> SendAsStreamAsync(HttpMethod method, CancellationToken cancellationToken = new CancellationToken())
        {
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            RestResult<Stream> response = null;

            try
            {
                string url = BuildFinalUrl();

                StartEventArgs startEventArgs = new StartEventArgs()
                {
                    Cancel = false,
                    Payload = PayloadContent,
                    Url = url
                };

                OnStartAction?.Invoke(startEventArgs);

                if (startEventArgs.Cancel)
                {
                    throw new OperationCanceledException();
                }

                using (HttpRequestMessage request = new HttpRequestMessage(method, url))
                {
                    HttpResponseMessage responseMessage = null;

                    using (request.Content = MakeHttpContent())
                    {
                        using (HttpClient client = new HttpClient(new HttpClientCompressionHandler
                        {
                            EnabledGZipCompression = this.enabledGZipCompression,
                            UploadingProgressChanged = (s, e) => OnUploadProgressAction?.Invoke(e),
                            DownloadingProgressChanged = (s, e) => OnDownloadProgressAction?.Invoke(e),
                            BufferSize = Properties.BufferSize,
                            Credentials = Credentials,
                            ClientCertificateOptions = Properties.CertificateOption,
#if !(NET45 || NET451 || NET452)
                            ServerCertificateCustomValidationCallback = CertificateCallback
#endif
                        })
                        {
                            Timeout = Properties.Timeout,
                        })
                        {
                            if (enabledGZipCompression)
                            {
                                client.DefaultRequestHeaders.AcceptEncoding.Add(new StringWithQualityHeaderValue("gzip"));
                            }

                            if (onAuthentication != null)
                            {
                                client.DefaultRequestHeaders.Authorization = onAuthentication();
                            }

                            onDefaultRequestHeaders?.Invoke(client.DefaultRequestHeaders);

                            responseMessage = await client.SendAsync(request, cancellationToken);

                            if (!IsAfterRefreshTokenCalled && responseMessage.StatusCode == HttpStatusCode.Unauthorized && RefreshTokenExecution)
                            {
                                stopwatch.Stop();

                                IsAfterRefreshTokenCalled = true;

                                if (RefreshTokenApi != null && RefreshTokenApi().StatusCode == HttpStatusCode.OK)
                                {
                                    return await SendAsStreamAsync(method, cancellationToken);
                                }

                                if (RefreshTokenApiAsync != null && (await RefreshTokenApiAsync()).StatusCode == HttpStatusCode.OK)
                                {
                                    return await SendAsStreamAsync(method, cancellationToken);
                                }
                            }

                            response = RestResult<Stream>.CreateInstanceFrom<Stream>(responseMessage);

                            Stream stream = await responseMessage.Content.ReadAsStreamAsync();
                            OnPreviewContentAsStringAction?.Invoke(new PreviewContentAsStringEventArgs { ContentAsString = string.Empty });
                            response.Content = stream;
                        }
                    }
                }

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
        /// <returns></returns>
        private async Task<RestResult<byte[]>> SendAsByteArrayAsync(HttpMethod method, CancellationToken cancellationToken = new CancellationToken())
        {
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            RestResult<byte[]> response = null;

            try
            {
                string url = BuildFinalUrl();

                StartEventArgs startEventArgs = new StartEventArgs()
                {
                    Cancel = false,
                    Payload = PayloadContent,
                    Url = url
                };

                OnStartAction?.Invoke(startEventArgs);

                if (startEventArgs.Cancel)
                {
                     throw new OperationCanceledException();
                }

                using (HttpRequestMessage request = new HttpRequestMessage(method, url))
                {
                    HttpResponseMessage responseMessage = null;

                    using (request.Content = MakeHttpContent())
                    {
                        using (HttpClient client = new HttpClient(new HttpClientCompressionHandler
                        {
                            EnabledGZipCompression = this.enabledGZipCompression,
                            UploadingProgressChanged = (s, e) => OnUploadProgressAction?.Invoke(e),
                            DownloadingProgressChanged = (s, e) => OnDownloadProgressAction?.Invoke(e),
                            BufferSize = Properties.BufferSize,
                            Credentials = Credentials,
                            ClientCertificateOptions = Properties.CertificateOption,
#if !(NET45 || NET451 || NET452)
                            ServerCertificateCustomValidationCallback = CertificateCallback
#endif
                        })
                        {
                            Timeout = Properties.Timeout,
                        })
                        {
                            if (enabledGZipCompression)
                            {
                                client.DefaultRequestHeaders.AcceptEncoding.Add(new StringWithQualityHeaderValue("gzip"));
                            }

                            if (onAuthentication != null)
                            {
                                client.DefaultRequestHeaders.Authorization = onAuthentication();
                            }

                            onDefaultRequestHeaders?.Invoke(client.DefaultRequestHeaders);

                            responseMessage = await client.SendAsync(request, cancellationToken);

                            if (!IsAfterRefreshTokenCalled && responseMessage.StatusCode == HttpStatusCode.Unauthorized && RefreshTokenExecution)
                            {
                                stopwatch.Stop();

                                IsAfterRefreshTokenCalled = true;

                                if (RefreshTokenApi != null && RefreshTokenApi().StatusCode == HttpStatusCode.OK)
                                {
                                    return await SendAsByteArrayAsync(method, cancellationToken);
                                }

                                if (RefreshTokenApiAsync != null && (await RefreshTokenApiAsync()).StatusCode == HttpStatusCode.OK)
                                {
                                    return await SendAsByteArrayAsync(method, cancellationToken);
                                }
                            }

                            response = RestResult<byte[]>.CreateInstanceFrom<byte[]>(responseMessage);
                            response.Content = await responseMessage.Content.ReadAsByteArrayAsync();
                        }
                    }
                }

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
        /// <returns></returns>
        private async Task<RestResult<T>> SendAsync<T>(HttpMethod method, CancellationToken cancellationToken = new CancellationToken())
            where T : new()
        {
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            RestResult<T> response = null;

            try
            {
                string url = BuildFinalUrl();

                StartEventArgs startEventArgs = new StartEventArgs()
                {
                    Cancel = false,
                    Payload = PayloadContent,
                    Url = url
                };

                OnStartAction?.Invoke(startEventArgs);

                if (startEventArgs.Cancel)
                {
                     throw new OperationCanceledException();
                }

                using (HttpRequestMessage request = new HttpRequestMessage(method, url))
                {
                    HttpResponseMessage responseMessage = null;

                    using (request.Content = MakeHttpContent())
                    {
                        using (HttpClient client = new HttpClient(new HttpClientCompressionHandler
                        {
                            EnabledGZipCompression = this.enabledGZipCompression,
                            UploadingProgressChanged = (s, e) => OnUploadProgressAction?.Invoke(e),
                            DownloadingProgressChanged = (s, e) => OnDownloadProgressAction?.Invoke(e),
                            BufferSize = Properties.BufferSize,
                            Credentials = Credentials,
                            ClientCertificateOptions = Properties.CertificateOption,
#if !(NET45 || NET451 || NET452)
                            ServerCertificateCustomValidationCallback = CertificateCallback
#endif
                        })
                        {
                            Timeout = Properties.Timeout,
                        })
                        {
                            if (enabledGZipCompression)
                            {
                                client.DefaultRequestHeaders.AcceptEncoding.Add(new StringWithQualityHeaderValue("gzip"));
                            }

                            if (onAuthentication != null)
                            {
                                client.DefaultRequestHeaders.Authorization = onAuthentication();
                            }

                            onDefaultRequestHeaders?.Invoke(client.DefaultRequestHeaders);

                            responseMessage = await client.SendAsync(request, cancellationToken);

                            if (!IsAfterRefreshTokenCalled && responseMessage.StatusCode == HttpStatusCode.Unauthorized && RefreshTokenExecution)
                            {
                                stopwatch.Stop();

                                IsAfterRefreshTokenCalled = true;

                                if (RefreshTokenApi != null && RefreshTokenApi().StatusCode == HttpStatusCode.OK)
                                {
                                    return await SendAsync<T>(method, cancellationToken);
                                }

                                if (RefreshTokenApiAsync != null && (await RefreshTokenApiAsync()).StatusCode == HttpStatusCode.OK)
                                {
                                    return await SendAsync<T>(method, cancellationToken);
                                }
                            }

                            response = RestResult<T>.CreateInstanceFrom<T>(responseMessage);

                            string serializedObject = await responseMessage.Content.ReadAsStringAsync();
                            OnPreviewContentAsStringAction?.Invoke(new PreviewContentAsStringEventArgs { ContentAsString = serializedObject });
                            response.Content = (T)Serializer.DeserializeObject(serializedObject, typeof(T));
                        }
                    }
                }

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

        #region [ Make Http Content ]

        /// <summary>
        /// Make Http Contenxt
        /// </summary>
        /// <returns></returns>
        private HttpContent MakeHttpContent(CancellationToken cancellationToken = new CancellationToken())
        {
            if (!IsEnabledFormUrlEncoded && PayloadContent != null)
            {
                var serializedObject = Serializer.SerializeObject(PayloadContent, PayloadContent.GetType());
                OnPreviewContentRequestAsStringAction?.Invoke(new PreviewContentAsStringEventArgs
                {
                    ContentAsString = serializedObject,
                    ContentType = PayloadContent.GetType()
                });
                return new StringContent(serializedObject, Encoding.UTF8, Serializer.MediaTypeAsString);
            }
            else if (IsEnabledFormUrlEncoded && FormUrlEncodedKeyValues != null)
            {
                var serializedObject = Serializer.SerializeObject(FormUrlEncodedKeyValues, FormUrlEncodedKeyValues.GetType());
                OnPreviewContentRequestAsStringAction?.Invoke(new PreviewContentAsStringEventArgs
                {
                    ContentAsString = serializedObject,
                    ContentType = FormUrlEncodedKeyValues.GetType()
                });
                return new FormUrlEncodedContent(FormUrlEncodedKeyValues);
            }
            return null;
        }
        #endregion
    }
}