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
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// The refresh token and the retry policy
    /// </summary>
    public sealed partial class RestBuilder
    {
        /// <summary>
        /// Enables the refresh token: when the response is 401 Unauthorized the func is invoked and,
        /// if it returns a success status code, the request is sent again (only once).
        /// When many concurrent requests of this builder (and of the builders derived from it) receive 401
        /// the func is invoked only once. The requests sent from inside the func never trigger another refresh.
        /// </summary>
        /// <param name="refreshToken">
        /// The func that refreshes the token; it receives the cancellation token of the request.
        /// null disables the refresh token for this builder and the builders derived from it.
        /// </param>
        /// <returns></returns>
        public RestBuilder RefreshToken(Func<CancellationToken, Task<RestResult>>? refreshToken)
            => With(r => r.refreshTokenCoordinator = refreshToken == null ? null : new RefreshTokenCoordinator(refreshToken));

        /// <summary>
        /// Retries the request when it fails with a transient error: network error, timeout,
        /// 408 Request Timeout, 429 Too Many Requests, 500, 502, 503, 504.
        /// The delay is exponential with jitter (delay, 2*delay, 4*delay, ...) or the one requested by the Retry-After header.
        /// </summary>
        /// <param name="maxRetries">Maximum number of retries (0 disables the retry)</param>
        /// <param name="delay">Base delay, 200 milliseconds when null</param>
        /// <param name="retryNonIdempotentMethods">
        /// false (default) retries only idempotent methods (GET, HEAD, OPTIONS, PUT, DELETE, TRACE): a POST/PATCH could be executed twice.
        /// </param>
        /// <returns></returns>
        public RestBuilder Retry(int maxRetries, TimeSpan? delay = null, bool retryNonIdempotentMethods = false)
        {
            if (maxRetries < 0) { throw new ArgumentOutOfRangeException(nameof(maxRetries)); }
            if (delay < TimeSpan.Zero) { throw new ArgumentOutOfRangeException(nameof(delay)); }
            return With(r => r.retrySettings = maxRetries == 0
                ? null
                : new RetrySettings(maxRetries, delay ?? TimeSpan.FromMilliseconds(200), retryNonIdempotentMethods));
        }
    }
}
