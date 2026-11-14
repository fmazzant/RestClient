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
    using Mafesoft.RestClient.Internal;
    using Mafesoft.RestClient.Serialization.Json;
    using System;
    using System.Collections.Generic;
    using System.Net.Http.Headers;

    /// <summary>
    /// OAuth2 client credentials grant
    /// </summary>
    public sealed partial class RestBuilder
    {
        /// <summary>
        /// Authenticates the requests with the OAuth2 client credentials grant (RFC 6749, section 4.4).
        /// The token is requested from the token endpoint before the first request, cached until shortly before it
        /// expires (<c>expires_in</c>) and requested again only once for all the concurrent requests. When the token
        /// response contains a <c>refresh_token</c>, the refresh token grant is used to renew it (falling back to the
        /// client credentials grant if the refresh is rejected). On 401 Unauthorized the cached token is discarded and
        /// the request is sent again once with a new token.
        /// The token request uses the handlers, the HttpClient, the timeout, the retry policy and the logger of the
        /// builder of the request; the client secret and the tokens are never logged. A failed token request is reported
        /// by the result, like the other errors of the request.
        /// It replaces any authentication set before; Authentication(...) and BearerToken(...) replace it.
        /// </summary>
        /// <param name="configure">Sets the token endpoint, the client id and secret, the scopes</param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException">configure is null</exception>
        /// <exception cref="ArgumentException">The token endpoint is not an absolute URI or the client id is missing</exception>
        public RestBuilder OAuth2ClientCredentials(Action<OAuth2ClientCredentialsOptions> configure)
        {
            if (configure == null) { throw new ArgumentNullException(nameof(configure)); }

            var options = new OAuth2ClientCredentialsOptions();
            configure(options);
            var provider = new OAuth2TokenProvider(options);

            return With(r =>
            {
                r.onAuthentication = null;
                r.onAuthenticationAsync = null;
                r.oauth2TokenProvider = provider;
            });
        }

        /// <summary>
        /// The token request: the connection settings, the retry policy and the logger of this builder,
        /// without its authentication, URL, query string, headers, payload, cookies and events
        /// (the events would receive the client secret and the token).
        /// </summary>
        internal RestBuilder CreateOAuth2TokenRequest(Uri tokenEndpoint, Dictionary<string, string> form, AuthenticationHeaderValue? clientAuthorization)
            => With(r =>
            {
                r.properties.EndPoint = tokenEndpoint;
                r.commands.Clear();
                r.parameters.Clear();
                r.routeValues = new Dictionary<string, string>(StringComparer.Ordinal);
                r.serializer = new SystemTextJsonSerializer();
                r.onAuthentication = clientAuthorization == null ? null : new Func<AuthenticationHeaderValue>(() => clientAuthorization);
                r.onAuthenticationAsync = null;
                r.oauth2TokenProvider = null;
                r.refreshTokenCoordinator = null;
                r.onRequestHeaders = headers => headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                r.cookies = null;
                r.payloadContent = null;
                r.isEnabledFormUrlEncoded = true;
                r.formUrlEncodedKeyValues = form;
                r.multipartFormData = null;
                r.onStartAction = null;
                r.onPreviewContentRequestAsStringAction = null;
                r.onUploadProgressAction = null;
                r.onDownloadProgressAction = null;
                r.onPreCompletedAction = null;
                r.onPreviewContentResponseAsStringAction = null;
                r.onCompletedAction = null;
                r.onExceptionAction = null;
            });
    }
}
