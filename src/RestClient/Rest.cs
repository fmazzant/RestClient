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

namespace RestClient
{
    using System;

    /// <summary>
    /// Provides a set of static (Shared in Visual Basic) methods for connecting to REST webapi.
    /// </summary>
    public static class Rest : object
    {
        /// <summary>
        /// Creates a RestBuilder instance to build the connection command
        /// </summary>
        /// <returns></returns>
        public static RestBuilder Build()
            => new RestBuilder();

        /// <summary>
        /// Creates a RestBuilder instance to build the connection command
        /// </summary>
        /// <param name="restProperties">Rest's properties</param>
        /// <returns></returns>
        public static RestBuilder Build(RestProperties restProperties)
            => new RestBuilder { Properties = restProperties };

        /// <summary>
        /// Creates a RestBuilder instance to build the connection command
        /// </summary>
        /// <param name="properties">Rest properties action</param>
        /// <returns></returns>
        public static RestBuilder Build(Action<RestProperties> properties)
        {
            RestBuilder restClient = new RestBuilder() { };
            properties(restClient.Properties);
            return restClient;
        }
    }
}
