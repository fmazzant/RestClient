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
    using System;
    using System.Collections.Generic;
    using System.Threading;

    /// <summary>
    /// How the client credentials are sent to the token endpoint (RFC 6749, section 2.3.1)
    /// </summary>
    public enum OAuth2ClientAuthentication
    {
        /// <summary>
        /// client_id and client_secret in the form body of the token request (the default)
        /// </summary>
        RequestBody = 0,

        /// <summary>
        /// client_id and client_secret in the <c>Authorization: Basic</c> header of the token request
        /// </summary>
        BasicHeader = 1,
    }

    /// <summary>
    /// The configuration of the OAuth2 client credentials grant (RestBuilder.OAuth2ClientCredentials)
    /// </summary>
    public sealed class OAuth2ClientCredentialsOptions
    {
        /// <summary>
        /// The absolute URI of the token endpoint (required), e.g. https://login.example.com/oauth2/token
        /// </summary>
        public Uri? TokenEndpoint { get; set; }

        /// <summary>
        /// The client identifier (required)
        /// </summary>
        public string? ClientId { get; set; }

        /// <summary>
        /// The client secret. It is never logged.
        /// </summary>
        public string? ClientSecret { get; set; }

        /// <summary>
        /// The requested scopes, sent as the space-separated <c>scope</c> parameter. Null or empty sends no scope.
        /// </summary>
        public IEnumerable<string>? Scopes { get; set; }

        /// <summary>
        /// Additional parameters of the token request, e.g. <c>audience</c> or <c>resource</c>
        /// </summary>
        public IDictionary<string, string> Parameters { get; } = new Dictionary<string, string>(StringComparer.Ordinal);

        /// <summary>
        /// How the client credentials are sent to the token endpoint. The default is RequestBody.
        /// </summary>
        public OAuth2ClientAuthentication ClientAuthentication { get; set; } = OAuth2ClientAuthentication.RequestBody;

        /// <summary>
        /// The token is requested again when it expires within this time (<c>expires_in</c> of the token response).
        /// The default is 30 seconds. A token response without <c>expires_in</c> is used until a 401 Unauthorized.
        /// </summary>
        public TimeSpan RefreshBeforeExpiration { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>
        /// The cache of the token. By default every OAuth2ClientCredentials call creates its own cache, shared by the
        /// builders derived from it. Set a shared instance (e.g. a singleton) when the builder is configured many times
        /// for the same client, as with the dependency injection IRestClientFactory. Use one cache per client configuration.
        /// </summary>
        public OAuth2TokenCache? Cache { get; set; }
    }

    /// <summary>
    /// Caches the OAuth2 token of a client configuration and makes the concurrent requests wait for a single token request.
    /// Thread safe.
    /// </summary>
    public sealed class OAuth2TokenCache
    {
        private OAuth2Token? token;

        /// <summary>
        /// Allows only one token request at a time
        /// </summary>
        internal SemaphoreSlim Gate { get; } = new SemaphoreSlim(1, 1);

        /// <summary>
        /// The cached token (null if none)
        /// </summary>
        internal OAuth2Token? Token
        {
            get => Volatile.Read(ref token);
            set => Volatile.Write(ref token, value);
        }

        /// <summary>
        /// Discards the cached token (also its refresh token): the next request asks the token endpoint for a new one.
        /// </summary>
        public void Clear() => Token = null;

        /// <summary>
        /// Discards the access token used by a request that received 401 Unauthorized, keeping its refresh token.
        /// Nothing changes if another request already replaced it.
        /// </summary>
        internal void Invalidate(OAuth2Token used)
            => Interlocked.CompareExchange(ref token, used.WithoutAccessToken(), used);
    }
}
