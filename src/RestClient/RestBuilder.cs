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
    using System.IO;
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Net.Security;
    using System.Security.Cryptography.X509Certificates;
    using System.Text;
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

        /// <summary>
        /// The callback to validate a server certificate
        /// </summary>
        /// <param name="callback"></param>
        /// <returns></returns>
        public RestBuilder CertificateValidation(Func<object, X509Certificate, X509Chain, SslPolicyErrors, bool> callback)
        {
            var result = (RestBuilder)this.MemberwiseClone();
            ServicePointManager.ServerCertificateValidationCallback = new RemoteCertificateValidationCallback(callback);
            return result;
        }

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
            result.HttpClient = new HttpClient(new HttpClientHandler() { Credentials = result.Credentials })
            {
                Timeout = TimeOut
            };
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
            result.HttpClient = new HttpClient(new HttpClientHandler() { Credentials = result.Credentials })
            {
                Timeout = TimeOut
            };
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
            result.HttpClient = new HttpClient(new HttpClientHandler() { Credentials = result.Credentials })
            {
                Timeout = TimeOut
            };
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
            result.HttpClient = new HttpClient(new HttpClientHandler() { Credentials = result.Credentials })
            {
                Timeout = TimeOut
            };
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
            result.HttpClient = new HttpClient(new HttpClientHandler() { Credentials = result.Credentials })
            {
                Timeout = TimeOut
            };
            return result;
        }

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
        
        [Obsolete("OnAuthentication: this method will be removed soon", false)]
        public RestBuilder OnAuthentication(string scheme, string parameter) => Authentication(scheme, parameter);

        /// <summary>
        /// Timeout
        /// </summary>
        TimeSpan TimeOut = TimeSpan.Zero;

        /// <summary>
        /// Sets the timespan to wait before the request times out
        /// </summary>
        /// <param name="timeOut"></param>
        /// <returns></returns>
        public RestBuilder Timeout(TimeSpan timeOut)
        {
            var result = (RestBuilder)this.MemberwiseClone();
            result.HttpClient.Timeout
                = result.TimeOut
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
                = result.TimeOut
                = TimeSpan.FromMilliseconds(milliseconds);
            return result;
        }

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

        /// <summary>
        /// On Completed Action
        /// </summary>
        Action<EventArgs> OnCompletedAction = null;

        /// <summary>
        /// Sets onCompleted action, occurs when the call is completed.
        /// </summary>
        /// <param name="onCompleted"></param>
        /// <returns></returns>
        public RestBuilder OnCompleted(Action<EventArgs> onCompleted)
        {
            var result = (RestBuilder)this.MemberwiseClone();
            result.OnCompletedAction = onCompleted;
            return result;
        }

        /// <summary>
        /// On Pre Result Action
        /// </summary>
        Action<RestResult> OnPreResultAction = null;

        /// <summary>
        /// Sets onPreResult, occurs when the result of request il builded.
        /// </summary>
        /// <param name="restResult"></param>
        /// <returns></returns>
        public RestBuilder OnPreResult(Action<RestResult> restResult)
        {
            var result = (RestBuilder)this.MemberwiseClone();
            result.OnPreResultAction = restResult;
            return result;
        }

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

        #region [ Get ]
        /// <summary>
        ///  Send a GET request to the specified Uri as an asynchronous operation
        /// </summary>
        /// <returns></returns>
        public async Task<RestResult<string>> GetAsync() => await SendAsStringAsync(HttpMethod.Get, PayloadContent, PayloadContentType);

        /// <summary>
        ///  Send a GET request to the specified Uri as an asynchronous operation
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public async Task<RestResult<T>> GetAsync<T>() where T : new() => await SendAsync<T>(HttpMethod.Get, PayloadContent, PayloadContentType);

        /// <summary>
        ///  Send a GET request to the specified Uri as an asynchronous operation
        /// </summary>
        /// <returns></returns>
        public async Task<RestResult<byte[]>> GetAsByteArrayAsync() => await SendAsByteArrayAsync(HttpMethod.Get, PayloadContent, PayloadContentType);

        /// <summary>
        ///  Send a GET request to the specified Uri as an asynchronous operation
        /// </summary>
        /// <returns></returns>
        public async Task<RestResult<Stream>> GetAsStreamAsync() => await SendAsStreamAsync(HttpMethod.Get, PayloadContent, PayloadContentType);

        /// <summary>
        /// Send a GET request to the specified Uri as an synchronous operation
        /// </summary>
        /// <returns></returns>
        public RestResult<string> Get() => GetAsync().Result;

        /// <summary>
        /// Send a GET request to the specified Uri as an synchronous operation
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        public RestResult<string> Get(string url) => Url(url).Get();

        /// <summary>
        /// Send a GET request to the specified Uri as an synchronous operation
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        public RestResult<string> Get(Uri url) => Url(url).Get();

        /// <summary>
        /// Send a GET request to the specified Uri as an synchronous operation
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public RestResult<T> Get<T>() where T : new() => this.GetAsync<T>().Result;

        /// <summary>
        /// Send a GET request to the specified Uri as an synchronous operation
        /// </summary>
        /// <returns></returns>
        public RestResult<byte[]> GetAsByteArray() => GetAsByteArrayAsync().Result;

        /// <summary>
        /// Send a GET request to the specified Uri as an synchronous operation
        /// </summary>
        /// <returns></returns>
        public RestResult<Stream> GetAsStream() => GetAsStreamAsync().Result;
        #endregion

        #region [ Post ]

        /// <summary>
        /// Send a POST request to the specified Uri as an asynchronous operation
        /// </summary>
        /// <returns></returns>
        public async Task<RestResult<string>> PostAsync() => await SendAsStringAsync(HttpMethod.Post, PayloadContent, PayloadContentType);

        /// <summary>
        /// Send a POST request to the specified Uri as an asynchronous operation
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public async Task<RestResult<T>> PostAsync<T>() where T : new() => await SendAsync<T>(HttpMethod.Post, PayloadContent, PayloadContentType);

        /// <summary>
        /// Send a POST request to the specified Uri as an asynchronous operation
        /// </summary>
        /// <returns></returns>
        public async Task<RestResult<byte[]>> PostAsByteArrayAsync() => await SendAsByteArrayAsync(HttpMethod.Post, PayloadContent, PayloadContentType);

        /// <summary>
        /// Send a POST request to the specified Uri as an asynchronous operation
        /// </summary>
        /// <returns></returns>
        public async Task<RestResult<Stream>> PostAsStreamAsync() => await SendAsStreamAsync(HttpMethod.Post, PayloadContent, PayloadContentType);

        /// <summary>
        /// Send a POST request to the specified Uri as a synchronous operation
        /// </summary>
        /// <returns></returns>
        public RestResult<string> Post() => PostAsync().Result;

        /// <summary>
        /// Send a POST request to the specified Uri as a synchronous operation
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public RestResult<T> Post<T>() where T : new() => PostAsync<T>().Result;

        /// <summary>
        /// Send a POST request to the specified Uri as a synchronous operation
        /// </summary>
        /// <returns></returns>
        public RestResult<byte[]> PostAsByteArray() => PostAsByteArrayAsync().Result;

        /// <summary>
        /// Send a POST request to the specified Uri as a synchronous operation
        /// </summary>
        /// <returns></returns>
        public RestResult<Stream> PostAsStream() => PostAsStreamAsync().Result;
        #endregion

        #region [ Put ]

        /// <summary>
        /// Send a PUT request to the specified Uri as an asynchronous operation.
        /// </summary>
        /// <returns></returns>
        public async Task<RestResult<string>> PutAsync() => await SendAsStringAsync(HttpMethod.Put, PayloadContent, PayloadContentType);

        /// <summary>
        /// Send a PUT request to the specified Uri as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public async Task<RestResult<T>> PutAsync<T>() where T : new() => await SendAsync<T>(HttpMethod.Put, PayloadContent, PayloadContentType);

        /// <summary>
        /// Send a PUT request to the specified Uri as an asynchronous operation.
        /// </summary>
        /// <returns></returns>
        public async Task<RestResult<byte[]>> PutAsByteArrayAsync() => await SendAsByteArrayAsync(HttpMethod.Put, PayloadContent, PayloadContentType);

        /// <summary>
        /// Send a PUT request to the specified Uri as an asynchronous operation.
        /// </summary>
        /// <returns></returns>
        public async Task<RestResult<Stream>> PutAsStreamAsync() => await SendAsStreamAsync(HttpMethod.Put, PayloadContent, PayloadContentType);

        /// <summary>
        /// Send a PUT request to the specified Uri as a synchronous operation.
        /// </summary>
        /// <returns></returns>
        public RestResult<string> Put() => PutAsync().Result;

        /// <summary>
        /// Send a PUT request to the specified Uri as a synchronous operation.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public RestResult<T> Put<T>() where T : new() => PutAsync<T>().Result;

        /// <summary>
        /// Send a PUT request to the specified Uri as a synchronous operation.
        /// </summary>
        /// <returns></returns>
        public RestResult<byte[]> PutAsByteArray() => PutAsByteArrayAsync().Result;

        /// <summary>
        /// Send a PUT request to the specified Uri as a synchronous operation.
        /// </summary>
        /// <returns></returns>
        public RestResult<Stream> PutAsStream() => PutAsStreamAsync().Result;
        #endregion

        #region [ Delete ]

        /// <summary>
        ///  Send a DELETE request to the specified Uri as an asynchronous operation.
        /// </summary>
        /// <returns></returns>
        public async Task<RestResult<string>> DeleteAsync() => await SendAsStringAsync(HttpMethod.Delete, PayloadContent, PayloadContentType);

        /// <summary>
        ///  Send a DELETE request to the specified Uri as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public async Task<RestResult<T>> DeleteAsync<T>() where T : new() => await SendAsync<T>(HttpMethod.Delete, PayloadContent, PayloadContentType);

        /// <summary>
        ///  Send a DELETE request to the specified Uri as an asynchronous operation.
        /// </summary>
        /// <returns></returns>
        public async Task<RestResult<byte[]>> DeleteAsByteArrayAsync() => await SendAsByteArrayAsync(HttpMethod.Delete, PayloadContent, PayloadContentType);

        /// <summary>
        ///  Send a DELETE request to the specified Uri as an asynchronous operation.
        /// </summary>
        /// <returns></returns>
        public async Task<RestResult<Stream>> DeleteAsStreamAsync() => await SendAsStreamAsync(HttpMethod.Delete, PayloadContent, PayloadContentType);

        /// <summary>
        /// Send a DELETE request to the specified Uri as a synchronous operation.
        /// </summary>
        /// <returns></returns>
        public RestResult<string> Delete() => DeleteAsync().Result;

        /// <summary>
        /// Send a DELETE request to the specified Uri as a synchronous operation.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public RestResult<T> Delete<T>() where T : new() => DeleteAsync<T>().Result;

        /// <summary>
        /// Send a DELETE request to the specified Uri as a synchronous operation.
        /// </summary>
        /// <returns></returns>
        public RestResult<byte[]> DeleteAsByteArray() => DeleteAsByteArrayAsync().Result;

        /// <summary>
        /// Send a DELETE request to the specified Uri as a synchronous operation.
        /// </summary>
        /// <returns></returns>
        public RestResult<Stream> DeleteAsStream() => DeleteAsStreamAsync().Result;
        #endregion

        #region [ Send ]

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
        private async Task<RestResult<string>> SendAsStringAsync(HttpMethod method, object payloadContent = null, Type payloadContentType = null)
        {
            RestResult<string> response = null;
            try
            {
                string url = Properties.EndPoint.OriginalString;

                Commands.ForEach(c => url = url + c);

                if (Parameters.Count > 0)
                {
                    url = $"{url}?";
                    foreach (string key in Parameters.Keys)
                        url += $"{key}={Parameters[key]}&";
                    url = url.Substring(0, url.Length - 1);
                }

                if (Logger) Console.WriteLine(url);

                StartEventArgs startEventArgs = new StartEventArgs()
                {
                    Cancel = false,
                    Payload = payloadContent,
                    Url = url
                };
                OnStartAction?.Invoke(startEventArgs);
                if (startEventArgs.Cancel) return null;

                HttpRequestMessage request = new HttpRequestMessage(method, url);
                System.Threading.CancellationToken cancellationToken = new System.Threading.CancellationToken();
                HttpResponseMessage responseMessage = null;// await HttpClient.SendAsync(request);

                if (!IsEnabledFormUrlEncoded)
                {
                    if (payloadContent != null)
                    {
                        string json = Serializer.SerializeObject(payloadContent, payloadContentType);
                        HttpContent hc = new StringContent(json, Encoding.UTF8, Serializer.MediaTypeAsString);
                        request.Content = hc;

                        using (HttpContentStream streamContent = new HttpContentStream(request.Content))
                        {
                            streamContent.ProgressChanged += (s, e) => OnUploadProgressAction?.Invoke(e);
                            responseMessage = await streamContent.WriteStringAsStreamAsync(HttpClient, request, cancellationToken);
                        }
                    }
                    else
                    {
                        responseMessage = HttpClient.SendAsync(request).Result;
                    }
                }
                else
                {
                    request.Content = new FormUrlEncodedContent(FormUrlEncodedKeyValues);
                    responseMessage = HttpClient.SendAsync(request).Result;
                }

                if (!isAfterRefreshTokenCalled && RefreshTokenExecution && responseMessage.StatusCode == HttpStatusCode.Unauthorized)
                {
                    isAfterRefreshTokenCalled = true;

                    if (RefreshTokenApi != null && RefreshTokenApi().StatusCode == HttpStatusCode.OK)
                        return await SendAsStringAsync(method, payloadContent, payloadContentType);

                    if (RefreshTokenApiAsync != null && (await RefreshTokenApiAsync()).StatusCode == HttpStatusCode.OK)
                        return await SendAsStringAsync(method, payloadContent, payloadContentType);
                }

                using (HttpContentStream streamContent = new HttpContentStream(responseMessage.Content))
                {
                    streamContent.ProgressChanged += (s, e) => OnDownloadProgressAction?.Invoke(e);
                    response = RestResult<string>.CreateInstanceFrom<string>(responseMessage);
                    var result = await streamContent.ReadStringAsStreamAsync(cancellationToken);
                    response.StringContent = result;
                    response.Content = result;
                }

                OnPreResultAction?.Invoke(response);
                OnCompletedAction?.Invoke(new EventArgs());
            }
            catch (Exception ex)
            {
                OnExceptionAction?.Invoke(ex);
                response = RestResult<string>.CreateFromException(ex);
            }
            return response;
        }

        /// <summary>
        ///  Send an HTTP request as an asynchronous operation.
        /// </summary>
        /// <param name="method"></param>
        /// <param name="payloadContent"></param>
        /// <param name="payloadContentType"></param>
        /// <returns></returns>
        private async Task<RestResult<Stream>> SendAsStreamAsync(HttpMethod method, object payloadContent = null, Type payloadContentType = null)
        {
            RestResult<Stream> response = null;
            try
            {
                string url = Properties.EndPoint.OriginalString;

                Commands.ForEach(c => url = url + c);

                if (Parameters.Count > 0)
                {
                    url = $"{url}?";
                    foreach (string key in Parameters.Keys)
                        url += $"{key}={Parameters[key]}&";
                    url = url.Substring(0, url.Length - 1);
                }

                if (Logger) Console.WriteLine(url);

                StartEventArgs startEventArgs = new StartEventArgs();
                OnStartAction?.Invoke(startEventArgs);
                if (startEventArgs.Cancel) return null;

                HttpRequestMessage request = new HttpRequestMessage(method, url);
                HttpResponseMessage responseMessage = null;// await HttpClient.SendAsync(request);
                System.Threading.CancellationToken cancellationToken = new System.Threading.CancellationToken();

                if (!IsEnabledFormUrlEncoded)
                {
                    if (payloadContent != null)
                    {
                        string json = Serializer.SerializeObject(payloadContent, payloadContentType);
                        HttpContent hc = new StringContent(json, Encoding.UTF8, Serializer.MediaTypeAsString);
                        request.Content = hc;

                        using (HttpContentStream streamContent = new HttpContentStream(request.Content))
                        {
                            streamContent.ProgressChanged += (s, e) => OnUploadProgressAction?.Invoke(e);
                            responseMessage = await streamContent.WriteStringAsStreamAsync(HttpClient, request, cancellationToken);
                        }
                    }
                    else
                    {
                        responseMessage = HttpClient.SendAsync(request).Result;
                    }
                }
                else
                {
                    request.Content = new FormUrlEncodedContent(FormUrlEncodedKeyValues);
                    responseMessage = HttpClient.SendAsync(request).Result;
                }

                if (!isAfterRefreshTokenCalled && RefreshTokenExecution && responseMessage.StatusCode == HttpStatusCode.Unauthorized)
                {
                    isAfterRefreshTokenCalled = true;

                    if (RefreshTokenApi != null && RefreshTokenApi().StatusCode == HttpStatusCode.OK)
                        return await SendAsStreamAsync(method, payloadContent, payloadContentType);

                    if (RefreshTokenApiAsync != null && (await RefreshTokenApiAsync()).StatusCode == HttpStatusCode.OK)
                        return await SendAsStreamAsync(method, payloadContent, payloadContentType);
                }

                using (HttpContentStream streamContent = new HttpContentStream(responseMessage.Content))
                {
                    streamContent.ProgressChanged += (s, e) => OnDownloadProgressAction?.Invoke(e);
                    response = RestResult<Stream>.CreateInstanceFrom<Stream>(responseMessage);
                    response.StringContent = null;
                    response.Content = await responseMessage.Content.ReadAsStreamAsync();
                }

                OnPreResultAction?.Invoke(response);
                OnCompletedAction?.Invoke(new EventArgs());
            }
            catch (Exception ex)
            {
                OnExceptionAction?.Invoke(ex);
                response = RestResult<Stream>.CreateFromException(ex);
            }
            return response;
        }

        /// <summary>
        ///  Send an HTTP request as an asynchronous operation.
        /// </summary>
        /// <param name="method"></param>
        /// <param name="payloadContent"></param>
        /// <param name="payloadContentType"></param>
        /// <returns></returns>
        private async Task<RestResult<byte[]>> SendAsByteArrayAsync(HttpMethod method, object payloadContent = null, Type payloadContentType = null)
        {
            RestResult<byte[]> response = null;
            try
            {
                string url = Properties.EndPoint.OriginalString;

                Commands.ForEach(c => url = url + c);

                if (Parameters.Count > 0)
                {
                    url = $"{url}?";
                    foreach (string key in Parameters.Keys)
                        url += $"{key}={Parameters[key]}&";
                    url = url.Substring(0, url.Length - 1);
                }

                if (Logger) Console.WriteLine(url);

                StartEventArgs startEventArgs = new StartEventArgs();
                OnStartAction?.Invoke(startEventArgs);
                if (startEventArgs.Cancel) return null;

                HttpRequestMessage request = new HttpRequestMessage(method, url);
                System.Threading.CancellationToken cancellationToken = new System.Threading.CancellationToken();
                HttpResponseMessage responseMessage = null;// await HttpClient.SendAsync(request);

                if (!IsEnabledFormUrlEncoded)
                {
                    if (payloadContent != null)
                    {
                        string json = Serializer.SerializeObject(payloadContent, payloadContentType);
                        HttpContent hc = new StringContent(json, Encoding.UTF8, Serializer.MediaTypeAsString);
                        request.Content = hc;

                        using (HttpContentStream streamContent = new HttpContentStream(request.Content))
                        {
                            streamContent.ProgressChanged += (s, e) => OnUploadProgressAction?.Invoke(e);
                            responseMessage = await streamContent.WriteStringAsStreamAsync(HttpClient, request, cancellationToken);
                        }
                    }
                    else
                    {
                        responseMessage = HttpClient.SendAsync(request).Result;
                    }
                }
                else
                {
                    request.Content = new FormUrlEncodedContent(FormUrlEncodedKeyValues);
                    responseMessage = HttpClient.SendAsync(request).Result;
                }

                if (!isAfterRefreshTokenCalled && RefreshTokenExecution && responseMessage.StatusCode == HttpStatusCode.Unauthorized)
                {
                    isAfterRefreshTokenCalled = true;

                    if (RefreshTokenApi != null && RefreshTokenApi().StatusCode == HttpStatusCode.OK)
                        return await SendAsByteArrayAsync(method, payloadContent, payloadContentType);

                    if (RefreshTokenApiAsync != null && (await RefreshTokenApiAsync()).StatusCode == HttpStatusCode.OK)
                        return await SendAsByteArrayAsync(method, payloadContent, payloadContentType);
                }

                using (HttpContentStream streamContent = new HttpContentStream(responseMessage.Content))
                {
                    streamContent.ProgressChanged += (s, e) => OnDownloadProgressAction?.Invoke(e);
                    response = RestResult<byte[]>.CreateInstanceFrom<byte[]>(responseMessage);
                    response.Content = await streamContent.ReadBytesAsStreamAsync();
                    response.StringContent = BitConverter.ToString(response.Content);
                }

                OnPreResultAction?.Invoke(response);
                OnCompletedAction?.Invoke(new EventArgs());
            }
            catch (Exception ex)
            {
                OnExceptionAction?.Invoke(ex);
                response = RestResult<byte[]>.CreateFromException(ex);
            }
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
        private async Task<RestResult<T>> SendAsync<T>(HttpMethod method, object payloadContent = null, Type payloadContentType = null)
            where T : new()
        {
            RestResult<T> response = null;
            try
            {
                string url = Properties.EndPoint.OriginalString;

                Commands.ForEach(c => url = url + c);

                if (Parameters.Count > 0)
                {
                    url = $"{url}?";
                    foreach (string key in Parameters.Keys)
                        url += $"{key}={Parameters[key]}&";
                    url = url.Substring(0, url.Length - 1);
                }

                if (Logger) Console.WriteLine(url);

                StartEventArgs startEventArgs = new StartEventArgs();
                OnStartAction?.Invoke(startEventArgs);
                if (startEventArgs.Cancel) return null;

                HttpRequestMessage request = new HttpRequestMessage(method, url);
                HttpResponseMessage responseMessage = null;// await HttpClient.SendAsync(request);
                System.Threading.CancellationToken cancellationToken = new System.Threading.CancellationToken();

                if (!IsEnabledFormUrlEncoded)
                {
                    if (payloadContent != null)
                    {
                        string json = Serializer.SerializeObject(payloadContent, payloadContentType);
                        HttpContent hc = new StringContent(json, Encoding.UTF8, Serializer.MediaTypeAsString);
                        request.Content = hc;

                        using (HttpContentStream streamContent = new HttpContentStream(request.Content))
                        {
                            streamContent.ProgressChanged += (s, e) => OnUploadProgressAction?.Invoke(e);
                            responseMessage = await streamContent.WriteStringAsStreamAsync(HttpClient, request, cancellationToken);
                        }
                    }
                    else
                    {
                        responseMessage = HttpClient.SendAsync(request).Result;
                    }
                }
                else
                {
                    request.Content = new FormUrlEncodedContent(FormUrlEncodedKeyValues);
                    responseMessage = HttpClient.SendAsync(request).Result;
                }

                if (!isAfterRefreshTokenCalled && RefreshTokenExecution && responseMessage.StatusCode == HttpStatusCode.Unauthorized)
                {
                    isAfterRefreshTokenCalled = true;

                    if (RefreshTokenApi != null && RefreshTokenApi().StatusCode == HttpStatusCode.OK)
                        return await SendAsync<T>(method, payloadContent, payloadContentType);

                    if (RefreshTokenApiAsync != null && (await RefreshTokenApiAsync()).StatusCode == HttpStatusCode.OK)
                        return await SendAsync<T>(method, payloadContent, payloadContentType);
                }

                using (HttpContentStream streamContent = new HttpContentStream(responseMessage.Content))
                {
                    streamContent.ProgressChanged += (s, e) => OnDownloadProgressAction?.Invoke(e);
                    response = Generic.RestResult<T>.CreateInstanceFrom<T>(responseMessage);
                    response.StringContent = await streamContent.ReadStringAsStreamAsync(cancellationToken);
                    response.Content = (T)Serializer.DeserializeObject(response.StringContent, typeof(T));
                }

                OnPreResultAction?.Invoke(response);
                OnCompletedAction?.Invoke(new EventArgs());
            }
            catch (Exception ex)
            {
                OnExceptionAction?.Invoke(ex);
                response = RestResult<T>.CreateFromException(ex);
            }
            return response;
        }
        #endregion
    }
}
