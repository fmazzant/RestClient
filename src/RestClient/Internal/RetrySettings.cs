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

namespace RestClient.Internal
{
    using System;
    using System.Net.Http;
    using System.Threading;

    /// <summary>
    /// Retry policy configured by RestBuilder.Retry
    /// </summary>
    internal sealed class RetrySettings
    {
        /// <summary>
        /// Maximum delay between two attempts (also when Retry-After requests more)
        /// </summary>
        internal static readonly TimeSpan MaxDelay = TimeSpan.FromSeconds(30);

        private static readonly ThreadLocal<Random> random = new ThreadLocal<Random>(() => new Random(Guid.NewGuid().GetHashCode()));

        public RetrySettings(int maxRetries, TimeSpan delay, bool retryNonIdempotentMethods)
        {
            MaxRetries = maxRetries;
            Delay = delay;
            RetryNonIdempotentMethods = retryNonIdempotentMethods;
        }

        public int MaxRetries { get; }

        public TimeSpan Delay { get; }

        public bool RetryNonIdempotentMethods { get; }

        /// <summary>
        /// Exponential backoff with jitter, or the Retry-After value
        /// </summary>
        public TimeSpan GetDelay(int retries, TimeSpan? retryAfter)
        {
            TimeSpan delay;
            if (retryAfter.HasValue)
            {
                delay = retryAfter.Value;
            }
            else
            {
                double baseMilliseconds = Delay.TotalMilliseconds * Math.Pow(2, retries);
                double jitter = Delay.TotalMilliseconds * random.Value!.NextDouble() * 0.5;
                delay = TimeSpan.FromMilliseconds(baseMilliseconds + jitter);
            }
            return delay > MaxDelay ? MaxDelay : delay;
        }

        public static bool IsIdempotent(HttpMethod method)
        {
            switch (method.Method.ToUpperInvariant())
            {
                case "GET":
                case "HEAD":
                case "OPTIONS":
                case "PUT":
                case "DELETE":
                case "TRACE":
                    return true;
                default:
                    return false;
            }
        }
    }
}
