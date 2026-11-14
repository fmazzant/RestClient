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
    using System;
    using System.Net.Http.Headers;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// The Authorization header of the requests
    /// </summary>
    public sealed partial class RestBuilder
    {
        /// <summary>
        /// The Authorization header for an HTTP request. The func is invoked for every request,
        /// so after a refresh token the new value is used.
        /// It replaces any authentication set before (also an asynchronous one or OAuth2ClientCredentials); null removes the authentication.
        /// </summary>
        /// <param name="authentication"></param>
        /// <returns></returns>
        public RestBuilder Authentication(Func<AuthenticationHeaderValue>? authentication)
            => With(r =>
            {
                r.onAuthentication = authentication;
                r.onAuthenticationAsync = null;
                r.oauth2TokenProvider = null;
            });

        /// <summary>
        /// The Authorization header for an HTTP request, provided asynchronously (e.g. a token from MSAL,
        /// a cache or a secret store). The func is invoked for every attempt: the first request, every retry
        /// and the request sent again after a refresh token. A null header sends the request without authorization.
        /// An exception thrown by the func is reported by the result, like the other errors of the request.
        /// It replaces any authentication set before (also OAuth2ClientCredentials); null removes the authentication.
        /// </summary>
        /// <param name="authenticationProvider">
        /// The func that provides the header; it receives the cancellation token of the attempt
        /// (canceled by the request token and by the Timeout).
        /// </param>
        /// <returns></returns>
        public RestBuilder Authentication(Func<CancellationToken, Task<AuthenticationHeaderValue>>? authenticationProvider)
            => With(r =>
            {
                r.onAuthentication = null;
                r.onAuthenticationAsync = authenticationProvider;
                r.oauth2TokenProvider = null;
            });

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

        /// <summary>
        /// The bearer token of the Authorization header (<c>Authorization: Bearer &lt;token&gt;</c>), provided asynchronously
        /// (e.g. a token from MSAL, a cache or a secret store). The func is invoked for every attempt: the first request,
        /// every retry and the request sent again after a refresh token. A null or empty token sends the request without
        /// authorization. An exception thrown by the func is reported by the result, like the other errors of the request.
        /// It replaces any authentication set before (also OAuth2ClientCredentials); null removes the authentication.
        /// </summary>
        /// <param name="tokenProvider">
        /// The func that provides the token; it receives the cancellation token of the attempt
        /// (canceled by the request token and by the Timeout).
        /// </param>
        /// <returns></returns>
        public RestBuilder BearerToken(Func<CancellationToken, Task<string>>? tokenProvider)
        {
            if (tokenProvider == null)
            {
                return Authentication((Func<CancellationToken, Task<AuthenticationHeaderValue>>?)null);
            }

            return Authentication(async cancellationToken =>
            {
                string token = await (tokenProvider(cancellationToken)
                    ?? throw new InvalidOperationException("The bearer token provider returned a null task.")).ConfigureAwait(false);
                return string.IsNullOrEmpty(token) ? null! : new AuthenticationHeaderValue("Bearer", token);
            });
        }
    }
}
