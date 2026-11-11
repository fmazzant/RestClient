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
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Runs the refresh token func once for all the concurrent requests that received 401 Unauthorized.
    /// The instance is shared by all the builders derived from the one where RefreshTokenInvoke was called.
    /// </summary>
    internal sealed class RefreshTokenCoordinator
    {
        /// <summary>
        /// true while the refresh func is running in the current async flow:
        /// the requests sent by the refresh func itself never trigger another refresh (no deadlock, no recursion).
        /// </summary>
        private static readonly AsyncLocal<bool> refreshing = new AsyncLocal<bool>();

        private readonly SemaphoreSlim gate = new SemaphoreSlim(1, 1);

        private int generation;

        public RefreshTokenCoordinator(Func<RestResult>? refreshToken, Func<Task<RestResult>>? refreshTokenAsync)
        {
            RefreshTokenFunc = refreshToken;
            RefreshTokenFuncAsync = refreshTokenAsync;
        }

        public Func<RestResult>? RefreshTokenFunc { get; }

        public Func<Task<RestResult>>? RefreshTokenFuncAsync { get; }

        /// <summary>
        /// true when the current code is running inside a refresh func
        /// </summary>
        public static bool IsRefreshing => refreshing.Value;

        /// <summary>
        /// Incremented after every successful refresh
        /// </summary>
        public int Generation => Volatile.Read(ref generation);

        /// <summary>
        /// Refreshes the token. If another request already refreshed it after <paramref name="observedGeneration"/>
        /// (the generation read before sending the request) the func is not invoked again.
        /// </summary>
        /// <returns>true if the request can be sent again</returns>
        public async Task<bool> RefreshAsync(int observedGeneration, CancellationToken cancellationToken)
        {
            await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (Generation != observedGeneration)
                {
                    return true;
                }

                refreshing.Value = true;
                try
                {
                    bool refreshed = (RefreshTokenFunc != null && IsSuccess(RefreshTokenFunc()))
                        || (RefreshTokenFuncAsync != null && IsSuccess(await RefreshTokenFuncAsync().ConfigureAwait(false)));

                    if (refreshed)
                    {
                        Interlocked.Increment(ref generation);
                    }

                    return refreshed;
                }
                finally
                {
                    refreshing.Value = false;
                }
            }
            finally
            {
                gate.Release();
            }
        }

        private static bool IsSuccess(RestResult? result)
            => result != null && result.IsSuccessStatusCode;
    }
}
