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

namespace Mafesoft.RestClient.Internal
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http.Headers;
    using System.Runtime.ExceptionServices;
    using System.Text;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// An access token received from the token endpoint
    /// </summary>
    internal sealed class OAuth2Token
    {
        public OAuth2Token(string? accessToken, string tokenType, string? refreshToken, DateTimeOffset? expiresAt)
        {
            AccessToken = accessToken;
            TokenType = tokenType;
            RefreshToken = refreshToken;
            ExpiresAt = expiresAt;
        }

        /// <summary>
        /// The access token; null after a 401 Unauthorized (only the refresh token is kept)
        /// </summary>
        public string? AccessToken { get; }

        public string TokenType { get; }

        public string? RefreshToken { get; }

        public DateTimeOffset? ExpiresAt { get; }

        /// <summary>
        /// true when the access token can be used: present and not expiring within refreshBeforeExpiration
        /// </summary>
        public bool IsUsable(TimeSpan refreshBeforeExpiration)
            => AccessToken != null && (ExpiresAt == null || DateTimeOffset.UtcNow + refreshBeforeExpiration < ExpiresAt.Value);

        public OAuth2Token WithoutAccessToken() => new OAuth2Token(null, TokenType, RefreshToken, ExpiresAt);

        public AuthenticationHeaderValue ToHeader()
            => new AuthenticationHeaderValue(
                string.Equals(TokenType, "bearer", StringComparison.OrdinalIgnoreCase) ? "Bearer" : TokenType,
                AccessToken);
    }

    /// <summary>
    /// The token response (RFC 6749, sections 5.1 and 5.2), read with a source generated context (Native AOT safe)
    /// </summary>
    internal sealed class OAuth2TokenResponse
    {
        [JsonPropertyName("access_token")]
        public string? AccessToken { get; set; }

        [JsonPropertyName("token_type")]
        public string? TokenType { get; set; }

        [JsonPropertyName("expires_in")]
        [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
        public long? ExpiresIn { get; set; }

        [JsonPropertyName("refresh_token")]
        public string? RefreshToken { get; set; }

        [JsonPropertyName("error")]
        public string? Error { get; set; }

        [JsonPropertyName("error_description")]
        public string? ErrorDescription { get; set; }
    }

    [JsonSerializable(typeof(OAuth2TokenResponse))]
    internal sealed partial class OAuth2JsonContext : JsonSerializerContext
    {
    }

    /// <summary>
    /// Requests, caches and renews the token of the OAuth2 client credentials grant (and of the refresh token grant).
    /// The configuration is a snapshot of OAuth2ClientCredentialsOptions; the state is in the OAuth2TokenCache.
    /// </summary>
    internal sealed class OAuth2TokenProvider
    {
        private readonly Uri tokenEndpoint;
        private readonly string clientId;
        private readonly string? clientSecret;
        private readonly string? scope;
        private readonly KeyValuePair<string, string>[] parameters;
        private readonly OAuth2ClientAuthentication clientAuthentication;
        private readonly TimeSpan refreshBeforeExpiration;
        private readonly OAuth2TokenCache cache;

        public OAuth2TokenProvider(OAuth2ClientCredentialsOptions options)
        {
            if (options.TokenEndpoint == null || !options.TokenEndpoint.IsAbsoluteUri)
            {
                throw new ArgumentException("OAuth2ClientCredentialsOptions.TokenEndpoint must be an absolute URI.", nameof(options));
            }
            if (string.IsNullOrEmpty(options.ClientId))
            {
                throw new ArgumentException("OAuth2ClientCredentialsOptions.ClientId is required.", nameof(options));
            }
            if (options.RefreshBeforeExpiration < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(options), "OAuth2ClientCredentialsOptions.RefreshBeforeExpiration cannot be negative.");
            }

            tokenEndpoint = options.TokenEndpoint;
            clientId = options.ClientId!;
            clientSecret = options.ClientSecret;
            string[] scopes = options.Scopes?.Where(s => !string.IsNullOrWhiteSpace(s)).ToArray() ?? Array.Empty<string>();
            scope = scopes.Length > 0 ? string.Join(" ", scopes) : null;
            parameters = options.Parameters.ToArray();
            clientAuthentication = options.ClientAuthentication;
            refreshBeforeExpiration = options.RefreshBeforeExpiration;
            cache = options.Cache ?? new OAuth2TokenCache();
        }

        public OAuth2TokenCache Cache => cache;

        /// <summary>
        /// Gets the cached token or requests a new one, once for all the concurrent requests.
        /// </summary>
        /// <param name="builder">The builder of the request: the token request uses its handlers, timeout, retry and logging</param>
        /// <param name="cancellationToken">Cancellation token of the attempt</param>
        public async Task<OAuth2Token> GetTokenAsync(RestBuilder builder, CancellationToken cancellationToken)
        {
            OAuth2Token? token = cache.Token;
            if (token != null && token.IsUsable(refreshBeforeExpiration))
            {
                return token;
            }

            await cache.Gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                // another request may have renewed it while waiting
                token = cache.Token;
                if (token != null && token.IsUsable(refreshBeforeExpiration))
                {
                    return token;
                }

                OAuth2Token? renewed = null;
                if (token?.RefreshToken != null)
                {
                    renewed = await RequestTokenAsync(builder, RefreshTokenForm(token.RefreshToken), fallback: true, cancellationToken).ConfigureAwait(false);
                }
                renewed ??= await RequestTokenAsync(builder, ClientCredentialsForm(), fallback: false, cancellationToken).ConfigureAwait(false);

                cache.Token = renewed;
                return renewed!;
            }
            finally
            {
                cache.Gate.Release();
            }
        }

        private Dictionary<string, string> ClientCredentialsForm()
        {
            var form = new Dictionary<string, string>(StringComparer.Ordinal) { ["grant_type"] = "client_credentials" };
            if (scope != null)
            {
                form["scope"] = scope;
            }
            return form;
        }

        private Dictionary<string, string> RefreshTokenForm(string refreshToken)
        {
            var form = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["grant_type"] = "refresh_token",
                ["refresh_token"] = refreshToken,
            };
            if (scope != null)
            {
                form["scope"] = scope;
            }
            return form;
        }

        /// <summary>
        /// Sends the token request. With fallback a token error response (e.g. an expired refresh token) returns null.
        /// </summary>
        private async Task<OAuth2Token?> RequestTokenAsync(RestBuilder builder, Dictionary<string, string> form, bool fallback, CancellationToken cancellationToken)
        {
            foreach (KeyValuePair<string, string> parameter in parameters)
            {
                if (!form.ContainsKey(parameter.Key))
                {
                    form[parameter.Key] = parameter.Value;
                }
            }

            AuthenticationHeaderValue? basic = null;
            if (clientAuthentication == OAuth2ClientAuthentication.BasicHeader)
            {
                string credentials = FormEncode(clientId) + ":" + FormEncode(clientSecret ?? string.Empty);
                basic = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes(credentials)));
            }
            else
            {
                form["client_id"] = clientId;
                if (clientSecret != null)
                {
                    form["client_secret"] = clientSecret;
                }
            }

            string endpoint = tokenEndpoint.GetLeftPart(UriPartial.Path);
            RestResult<string> result = await builder.CreateOAuth2TokenRequest(tokenEndpoint, form, basic)
                .PostAsync(cancellationToken)
                .ConfigureAwait(false);

            if (result.Response == null)
            {
                Exception error = result.Exception ?? new InvalidOperationException("No response.");
                if (error is OperationCanceledException)
                {
                    ExceptionDispatchInfo.Capture(error).Throw();
                }
                throw new InvalidOperationException($"The OAuth2 token request to {endpoint} failed: {error.Message}", error);
            }

            OAuth2TokenResponse? response = Parse(result.Content);

            if (!result.IsSuccessStatusCode)
            {
                if (fallback)
                {
                    return null;
                }

                string reason = response?.Error == null
                    ? string.Empty
                    : $": {response.Error}{(string.IsNullOrEmpty(response.ErrorDescription) ? string.Empty : " - " + response.ErrorDescription)}";
                throw new InvalidOperationException($"The OAuth2 token request to {endpoint} failed with HTTP {(int)result.StatusCode}{reason}.");
            }

            if (response == null || string.IsNullOrEmpty(response.AccessToken))
            {
                throw new InvalidOperationException($"The OAuth2 token response of {endpoint} has no access_token.");
            }

            DateTimeOffset? expiresAt = response.ExpiresIn is long seconds && seconds > 0
                ? DateTimeOffset.UtcNow.AddSeconds(seconds)
                : (DateTimeOffset?)null;

            return new OAuth2Token(
                response.AccessToken,
                string.IsNullOrEmpty(response.TokenType) ? "Bearer" : response.TokenType!,
                response.RefreshToken,
                expiresAt);
        }

        private static OAuth2TokenResponse? Parse(string? json)
        {
            if (json == null || json.Trim().Length == 0)
            {
                return null;
            }
            try
            {
                return JsonSerializer.Deserialize(json, OAuth2JsonContext.Default.OAuth2TokenResponse);
            }
            catch (JsonException)
            {
                return null;
            }
        }

        /// <summary>
        /// application/x-www-form-urlencoded encoding of the client credentials (RFC 6749, section 2.3.1)
        /// </summary>
        private static string FormEncode(string value) => Uri.EscapeDataString(value).Replace("%20", "+");
    }
}
