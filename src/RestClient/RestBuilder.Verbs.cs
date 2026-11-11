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
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// HTTP verbs. Every method is a shortcut of SendAsync/Send:
    /// the non generic methods return the body as string, the generic ones return T
    /// (string, byte[], Stream or a type deserialized with the configured serializer).
    /// </summary>
    public sealed partial class RestBuilder
    {
        private static readonly HttpMethod PATCH = new HttpMethod("PATCH");

        #region [ GET ]

        /// <summary>Sends a GET request and returns the body as string.</summary>
        public Task<RestResult<string>> GetAsync(CancellationToken cancellationToken = default) => SendAsync<string>(HttpMethod.Get, cancellationToken);

        /// <summary>Sends a GET request and returns the body as T.</summary>
        public Task<RestResult<T>> GetAsync<T>(CancellationToken cancellationToken = default) => SendAsync<T>(HttpMethod.Get, cancellationToken);

        /// <summary>Sends a GET request synchronously and returns the body as string.</summary>
        public RestResult<string> Get(CancellationToken cancellationToken = default) => Send<string>(HttpMethod.Get, cancellationToken);

        /// <summary>Sends a GET request synchronously and returns the body as T.</summary>
        public RestResult<T> Get<T>(CancellationToken cancellationToken = default) => Send<T>(HttpMethod.Get, cancellationToken);

        #endregion

        #region [ POST ]

        /// <summary>Sends a POST request and returns the body as string.</summary>
        public Task<RestResult<string>> PostAsync(CancellationToken cancellationToken = default) => SendAsync<string>(HttpMethod.Post, cancellationToken);

        /// <summary>Sends a POST request and returns the body as T.</summary>
        public Task<RestResult<T>> PostAsync<T>(CancellationToken cancellationToken = default) => SendAsync<T>(HttpMethod.Post, cancellationToken);

        /// <summary>Sends a POST request synchronously and returns the body as string.</summary>
        public RestResult<string> Post(CancellationToken cancellationToken = default) => Send<string>(HttpMethod.Post, cancellationToken);

        /// <summary>Sends a POST request synchronously and returns the body as T.</summary>
        public RestResult<T> Post<T>(CancellationToken cancellationToken = default) => Send<T>(HttpMethod.Post, cancellationToken);

        #endregion

        #region [ PUT ]

        /// <summary>Sends a PUT request and returns the body as string.</summary>
        public Task<RestResult<string>> PutAsync(CancellationToken cancellationToken = default) => SendAsync<string>(HttpMethod.Put, cancellationToken);

        /// <summary>Sends a PUT request and returns the body as T.</summary>
        public Task<RestResult<T>> PutAsync<T>(CancellationToken cancellationToken = default) => SendAsync<T>(HttpMethod.Put, cancellationToken);

        /// <summary>Sends a PUT request synchronously and returns the body as string.</summary>
        public RestResult<string> Put(CancellationToken cancellationToken = default) => Send<string>(HttpMethod.Put, cancellationToken);

        /// <summary>Sends a PUT request synchronously and returns the body as T.</summary>
        public RestResult<T> Put<T>(CancellationToken cancellationToken = default) => Send<T>(HttpMethod.Put, cancellationToken);

        #endregion

        #region [ PATCH ]

        /// <summary>Sends a PATCH request and returns the body as string.</summary>
        public Task<RestResult<string>> PatchAsync(CancellationToken cancellationToken = default) => SendAsync<string>(PATCH, cancellationToken);

        /// <summary>Sends a PATCH request and returns the body as T.</summary>
        public Task<RestResult<T>> PatchAsync<T>(CancellationToken cancellationToken = default) => SendAsync<T>(PATCH, cancellationToken);

        /// <summary>Sends a PATCH request synchronously and returns the body as string.</summary>
        public RestResult<string> Patch(CancellationToken cancellationToken = default) => Send<string>(PATCH, cancellationToken);

        /// <summary>Sends a PATCH request synchronously and returns the body as T.</summary>
        public RestResult<T> Patch<T>(CancellationToken cancellationToken = default) => Send<T>(PATCH, cancellationToken);

        #endregion

        #region [ DELETE ]

        /// <summary>Sends a DELETE request and returns the body as string.</summary>
        public Task<RestResult<string>> DeleteAsync(CancellationToken cancellationToken = default) => SendAsync<string>(HttpMethod.Delete, cancellationToken);

        /// <summary>Sends a DELETE request and returns the body as T.</summary>
        public Task<RestResult<T>> DeleteAsync<T>(CancellationToken cancellationToken = default) => SendAsync<T>(HttpMethod.Delete, cancellationToken);

        /// <summary>Sends a DELETE request synchronously and returns the body as string.</summary>
        public RestResult<string> Delete(CancellationToken cancellationToken = default) => Send<string>(HttpMethod.Delete, cancellationToken);

        /// <summary>Sends a DELETE request synchronously and returns the body as T.</summary>
        public RestResult<T> Delete<T>(CancellationToken cancellationToken = default) => Send<T>(HttpMethod.Delete, cancellationToken);

        #endregion

        #region [ HEAD & OPTIONS ]

        /// <summary>Sends a HEAD request (use result.Headers and result.Response.Content.Headers).</summary>
        public Task<RestResult<string>> HeadAsync(CancellationToken cancellationToken = default) => SendAsync<string>(HttpMethod.Head, cancellationToken);

        /// <summary>Sends a HEAD request synchronously (use result.Headers and result.Response.Content.Headers).</summary>
        public RestResult<string> Head(CancellationToken cancellationToken = default) => Send<string>(HttpMethod.Head, cancellationToken);

        /// <summary>Sends an OPTIONS request and returns the body as string.</summary>
        public Task<RestResult<string>> OptionsAsync(CancellationToken cancellationToken = default) => SendAsync<string>(HttpMethod.Options, cancellationToken);

        /// <summary>Sends an OPTIONS request synchronously and returns the body as string.</summary>
        public RestResult<string> Options(CancellationToken cancellationToken = default) => Send<string>(HttpMethod.Options, cancellationToken);

        #endregion
    }
}
