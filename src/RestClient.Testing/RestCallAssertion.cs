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

namespace Mafesoft.RestClient.Testing
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http;
    using System.Text;

    /// <summary>
    /// Checks the calls received by the fake server (RestTest.ShouldHaveCalled). Every method narrows the matching
    /// calls and throws RestCallAssertionException when none is left; Times checks how many there are.
    /// </summary>
    public sealed class RestCallAssertion
    {
        private readonly IReadOnlyList<RestTestCall> log;
        private readonly string[] conditions;

        internal RestCallAssertion(IReadOnlyList<RestTestCall> log, string[] conditions)
            : this(log, log, conditions)
        {
        }

        private RestCallAssertion(IReadOnlyList<RestTestCall> log, IReadOnlyList<RestTestCall> matching, string[] conditions)
        {
            this.log = log;
            Calls = matching;
            this.conditions = conditions;
        }

        /// <summary>
        /// The calls that satisfy all the conditions so far
        /// </summary>
        public IReadOnlyList<RestTestCall> Calls { get; }

        /// <summary>
        /// Keeps the calls with the HTTP method
        /// </summary>
        /// <param name="method">The HTTP method</param>
        public RestCallAssertion WithVerb(HttpMethod method)
        {
            if (method == null) { throw new ArgumentNullException(nameof(method)); }
            return Where(call => call.Method == method, $"method {method}");
        }

        /// <summary>
        /// Keeps the calls with the request header (also a content header, e.g. Content-Type) and, when valuePattern
        /// is not null, a value that matches it (<c>*</c> matches any text, case-insensitive)
        /// </summary>
        /// <param name="name">The header name</param>
        /// <param name="valuePattern">The value pattern, null for any value</param>
        public RestCallAssertion WithHeader(string name, string? valuePattern = null)
        {
            if (name == null) { throw new ArgumentNullException(nameof(name)); }
            return Where(
                call => GetHeaderValues(call, name).Any(value => valuePattern == null || UrlPattern.IsMatch(valuePattern, value)),
                valuePattern == null ? $"header {name}" : $"header {name}: {valuePattern}");
        }

        /// <summary>
        /// Keeps the calls without the request header
        /// </summary>
        /// <param name="name">The header name</param>
        public RestCallAssertion WithoutHeader(string name)
        {
            if (name == null) { throw new ArgumentNullException(nameof(name)); }
            return Where(call => !GetHeaderValues(call, name).Any(), $"no header {name}");
        }

        /// <summary>
        /// Keeps the calls with the query string parameter and, when value is not null, that value (url-decoded)
        /// </summary>
        /// <param name="name">The parameter name</param>
        /// <param name="value">The value, null for any value</param>
        public RestCallAssertion WithQueryParam(string name, string? value = null)
        {
            if (name == null) { throw new ArgumentNullException(nameof(name)); }
            return Where(
                call => GetQuery(call.Url).Any(p => p.Key == name && (value == null || p.Value == value)),
                value == null ? $"query parameter {name}" : $"query parameter {name}={value}");
        }

        /// <summary>
        /// Keeps the calls whose body matches the pattern (<c>*</c> matches any text, case-insensitive)
        /// </summary>
        /// <param name="bodyPattern">The body pattern</param>
        public RestCallAssertion WithRequestBody(string bodyPattern)
        {
            if (bodyPattern == null) { throw new ArgumentNullException(nameof(bodyPattern)); }
            return Where(call => call.RequestBody != null && UrlPattern.IsMatch(bodyPattern, call.RequestBody), $"body {bodyPattern}");
        }

        /// <summary>
        /// Keeps the calls whose content has the media type (e.g. application/json)
        /// </summary>
        /// <param name="mediaType">The media type</param>
        public RestCallAssertion WithContentType(string mediaType)
        {
            if (mediaType == null) { throw new ArgumentNullException(nameof(mediaType)); }
            return Where(call => string.Equals(call.ContentType, mediaType, StringComparison.OrdinalIgnoreCase), $"content type {mediaType}");
        }

        /// <summary>
        /// Keeps the calls with <c>Authorization: Bearer</c> and, when token is not null, that token
        /// </summary>
        /// <param name="token">The token, null for any token</param>
        public RestCallAssertion WithOAuthBearerToken(string? token = null)
            => Where(
                call => string.Equals(call.Request.Headers.Authorization?.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase)
                    && (token == null || call.Request.Headers.Authorization?.Parameter == token),
                token == null ? "a bearer token" : "the expected bearer token");

        /// <summary>
        /// Keeps the calls that satisfy the predicate
        /// </summary>
        /// <param name="predicate">The condition</param>
        /// <param name="description">The description of the condition, shown when the assertion fails</param>
        public RestCallAssertion With(Func<RestTestCall, bool> predicate, string? description = null)
        {
            if (predicate == null) { throw new ArgumentNullException(nameof(predicate)); }
            return Where(predicate, description ?? "custom condition");
        }

        /// <summary>
        /// Asserts the number of calls that satisfy all the conditions
        /// </summary>
        /// <param name="expected">The expected number of calls</param>
        /// <returns>This assertion</returns>
        public RestCallAssertion Times(int expected)
        {
            if (expected < 0) { throw new ArgumentOutOfRangeException(nameof(expected)); }
            if (Calls.Count != expected)
            {
                throw new RestCallAssertionException(Describe(
                    $"Expected {expected} call(s) with {string.Join(", ", conditions)}, but there were {Calls.Count}.", log));
            }
            return this;
        }

        internal RestCallAssertion Where(Func<RestTestCall, bool> predicate, string condition)
        {
            string[] all = conditions.Concat(new[] { condition }).ToArray();
            RestTestCall[] matching = Calls.Where(predicate).ToArray();
            if (matching.Length == 0)
            {
                throw new RestCallAssertionException(Describe($"Expected a call with {string.Join(", ", all)}, but there were none.", log));
            }
            return new RestCallAssertion(log, matching, all);
        }

        internal static string Describe(string message, IReadOnlyList<RestTestCall> log)
        {
            var text = new StringBuilder(message);
            text.AppendLine();
            if (log.Count == 0)
            {
                text.Append("No calls were made.");
            }
            else
            {
                text.Append("Calls made:");
                foreach (RestTestCall call in log)
                {
                    text.AppendLine().Append("  ").Append(call);
                }
            }
            return text.ToString();
        }

        private static IEnumerable<string> GetHeaderValues(RestTestCall call, string name)
        {
            if (call.Request.Headers.TryGetValues(name, out IEnumerable<string>? values))
            {
                return values;
            }
            if (string.Equals(name, "Content-Type", StringComparison.OrdinalIgnoreCase) && call.ContentType != null)
            {
                return new[] { call.ContentType };
            }
            return Array.Empty<string>();
        }

        private static IEnumerable<KeyValuePair<string, string>> GetQuery(Uri url)
        {
            string query = url.Query.TrimStart('?');
            if (query.Length == 0)
            {
                yield break;
            }
            foreach (string pair in query.Split('&'))
            {
                int separator = pair.IndexOf('=');
                string key = separator < 0 ? pair : pair.Substring(0, separator);
                string value = separator < 0 ? string.Empty : pair.Substring(separator + 1);
                yield return new KeyValuePair<string, string>(Decode(key), Decode(value));
            }
        }

        private static string Decode(string value) => Uri.UnescapeDataString(value.Replace('+', ' '));
    }
}
