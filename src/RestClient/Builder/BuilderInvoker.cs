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

namespace RestClient.Builder
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
    public class BuilderInvoker
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
        /// Payload content's type
        /// </summary>
        Type PayloadContentType { get; set; }

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
        /// isAfterRefreshTokenCalled
        /// </summary>
        bool IsAfterRefreshTokenCalled = false;

        /// <summary>
        /// Initializes a new instance
        /// </summary>
        public BuilderInvoker()
        {

        }

        #region [ Fetch ]

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
                    }
                    else
                    {
                        //responseMessage = await HttpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                    }
                }
                else
                {
                    request.Content = new FormUrlEncodedContent(FormUrlEncodedKeyValues);
                    //responseMessage = await HttpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                }

                await HttpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

                using (HttpContentStream streamContent = new HttpContentStream(request.Content, Properties.BufferSize))
                {
                    streamContent.ProgressChanged += (s, e) => OnUploadProgressAction?.Invoke(e);
                    responseMessage = await streamContent.WriteStringAsStreamAsync(HttpClient, request, cancellationToken);
                }

                responseMessage = await HttpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

                if (!IsAfterRefreshTokenCalled && RefreshTokenExecution && responseMessage.StatusCode == HttpStatusCode.Unauthorized)
                {
                    IsAfterRefreshTokenCalled = true;

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

                if (!IsAfterRefreshTokenCalled && RefreshTokenExecution && responseMessage.StatusCode == HttpStatusCode.Unauthorized)
                {
                    IsAfterRefreshTokenCalled = true;

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

                if (!IsAfterRefreshTokenCalled && RefreshTokenExecution && responseMessage.StatusCode == HttpStatusCode.Unauthorized)
                {
                    IsAfterRefreshTokenCalled = true;

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

                if (!IsAfterRefreshTokenCalled && RefreshTokenExecution && responseMessage.StatusCode == HttpStatusCode.Unauthorized)
                {
                    IsAfterRefreshTokenCalled = true;

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

        #region [ Create New HttpClient Instance and set into HttpClient ]

        /// <summary>
        /// Create new HttpClient instance and set into result.HttpClient
        /// </summary>
        /// <param name="result"></param>
        private void CreateNewHttpClientInstance(BuilderInvoker result)
        {
            result.HttpClient = new HttpClient(new HttpClientHandler()
            {
                Credentials = result.Credentials,
                ClientCertificateOptions = Properties.CertificateOption,
                ServerCertificateCustomValidationCallback = result.CertificateCallback
            })
            {
                Timeout = Properties.Timeout
            };
        }

        #endregion
    }
}