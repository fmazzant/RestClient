/// <summary>
/// 
/// The MIT License (MIT)
/// 
/// Copyright (c) 2020 Federico Mazzanti
/// 
/// Permission is hereby granted, free of charge, to any person
/// obtaining a copy of this software and associated documentation
/// files (the "Software"), to deal in the Software without
/// restriction, including without limitation the rights to use,
/// copy, modify, merge, publish, distribute, sublicense, and/or sell
/// copies of the Software, and to permit persons to whom the
/// Software is furnished to do so, subject to the following
/// conditions:
/// 
/// The above copyright notice and this permission notice shall be
/// included in all copies or substantial portions of the Software.
/// 
/// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
/// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES
/// OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
/// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT
/// HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY,
/// WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
/// FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR
/// OTHER DEALINGS IN THE SOFTWARE.
/// 
/// </summary>

namespace Net.Http.Rest
{
    using System;
    using System.Net.Http;

    /// <summary>
    /// Provides a set of static (Shared in Visual Basic) methods for connecting to REST webapi.
    /// </summary>
    public static class HttpClientExtensions
    {
        /// <summary>
        /// Creates a RestClientBuilder instance to build the connection command
        /// </summary>
        /// <returns></returns>
        public static RestClientBuilder Rest(this HttpClient httpClient)
            => new RestClientBuilder
            {
                HttpClient = httpClient
            };

        /// <summary>
        /// Creates a RestClientBuilder instance to build the connection command
        /// </summary>
        /// <param name="restProperties">Rest's properties</param>
        /// <returns></returns>
        public static RestClientBuilder Rest(this HttpClient httpClient, RestProperties restProperties)
            => new RestClientBuilder
            {
                HttpClient = httpClient,
                Properties = restProperties
            };

        /// <summary>
        /// Creates a RestClientBuilder instance to build the connection command
        /// </summary>
        /// <param name="properties">Rest properties action</param>
        /// <returns></returns>
        public static RestClientBuilder Rest(this HttpClient httpClient, Action<RestProperties> properties)
        {
            RestClientBuilder restClient = new RestClientBuilder()
            {
                HttpClient = httpClient
            };
            properties(restClient.Properties);
            return restClient;
        }
    }
}
