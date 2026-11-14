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
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Net.Security;
    using System.Security.Cryptography.X509Certificates;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Provides a set of methods for building the requests.
    /// The builder is immutable: every method returns a new independent instance,
    /// so a configured builder can be safely reused (also concurrently) as a base for many requests.
    /// </summary>
    public sealed partial class RestBuilder
    {
        /// <summary>
        /// The connection properties of the builder (read-only: use Url, Timeout, BufferSize and CertificateOption to change them)
        /// </summary>
        public RestProperties Properties => properties.Clone();

        private RestProperties properties;
        private List<string> commands = new List<string>();
        private List<KeyValuePair<string, string>> parameters = new List<KeyValuePair<string, string>>();
        private Dictionary<string, string> routeValues = new Dictionary<string, string>(StringComparer.Ordinal);
        private IRestSerializer serializer = new SystemTextJsonSerializer();
        private HttpClient? externalHttpClient;
        private Func<object, X509Certificate?, X509Chain?, SslPolicyErrors, bool>? certificateCallback;
        private ICredentials? credentials;
        private bool useProxy = true;
        private IWebProxy? proxy;
        private CookieContainer? cookies;
        private Func<AuthenticationHeaderValue>? onAuthentication;
        private Func<CancellationToken, Task<AuthenticationHeaderValue>>? onAuthenticationAsync;
        private Action<HttpRequestHeaders>? onRequestHeaders;
        private bool enabledCompression;
        private RefreshTokenCoordinator? refreshTokenCoordinator;
        private RetrySettings? retrySettings;
        private ILogger? logger;
        private string? clientName;
        private object? payloadContent;
        private bool isEnabledFormUrlEncoded;
        private Dictionary<string, string>? formUrlEncodedKeyValues;
        private MultipartFormData? multipartFormData;
        private Version? httpVersion;
#if NET
        private HttpVersionPolicy? httpVersionPolicy;
#endif

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
    }
}
