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

    /// <summary>
    /// The headers of the requests
    /// </summary>
    public sealed partial class RestBuilder
    {
        /// <summary>
        /// Sets the headers which should be sent with this or children requests.
        /// Calling Header more than once combines the actions.
        /// </summary>
        /// <param name="requestHeaders"></param>
        /// <returns></returns>
        public RestBuilder Header(Action<HttpRequestHeaders> requestHeaders)
            => With(r => r.onRequestHeaders = (Action<HttpRequestHeaders>?)Delegate.Combine(onRequestHeaders, requestHeaders));

        /// <summary>
        /// Adds a header which should be sent with this or children requests.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public RestBuilder Header(string name, string? value)
            => Header(h => h.TryAddWithoutValidation(name, value));
    }
}
