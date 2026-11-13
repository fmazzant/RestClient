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
    using System.Collections.Generic;

    /// <summary>
    /// The content of the requests: payload, form url encoded and multipart
    /// </summary>
    public sealed partial class RestBuilder
    {
        /// <summary>
        /// Sets the payload, serialized with the configured serializer (JSON by default)
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="payload"></param>
        /// <returns></returns>
        public RestBuilder Payload<T>(T payload)
            => With(r =>
            {
                r.payloadContent = payload;
                r.multipartFormData = null;
            });

        /// <summary>
        /// Enable form Url Encoding
        /// </summary>
        /// <param name="enableFormUrlEncoded"></param>
        /// <returns></returns>
        public RestBuilder EnableFormUrlEncoded(bool enableFormUrlEncoded = true)
            => With(r => r.isEnabledFormUrlEncoded = enableFormUrlEncoded);

        /// <summary>
        /// Sets x-www-form-urlencoded key values and enables form url encoding
        /// </summary>
        /// <param name="keyValues"></param>
        /// <returns></returns>
        public RestBuilder FormUrlEncoded(Dictionary<string, string> keyValues)
            => FormUrlEncoded(true, keyValues);

        /// <summary>
        /// Sets x-www-form-urlencoded key values
        /// </summary>
        /// <param name="enableFormUrlEncoded"></param>
        /// <param name="keyValues"></param>
        /// <returns></returns>
        public RestBuilder FormUrlEncoded(bool enableFormUrlEncoded, Dictionary<string, string>? keyValues)
        {
            var copy = keyValues == null ? null : new Dictionary<string, string>(keyValues);
            return With(r =>
            {
                r.isEnabledFormUrlEncoded = enableFormUrlEncoded;
                r.formUrlEncodedKeyValues = copy;
                r.multipartFormData = null;
            });
        }

        /// <summary>
        /// Sets x-www-form-urlencoded key values and enables form url encoding
        /// </summary>
        /// <param name="keyValues"></param>
        /// <returns></returns>
        public RestBuilder FormUrlEncoded(Action<Dictionary<string, string>> keyValues)
            => FormUrlEncoded(true, keyValues);

        /// <summary>
        /// Sets x-www-form-urlencoded key values
        /// </summary>
        /// <param name="enableFormUrlEncoded"></param>
        /// <param name="keyValues"></param>
        /// <returns></returns>
        public RestBuilder FormUrlEncoded(bool enableFormUrlEncoded, Action<Dictionary<string, string>> keyValues)
        {
            if (keyValues == null) { throw new ArgumentNullException(nameof(keyValues)); }
            var values = new Dictionary<string, string>();
            keyValues(values);
            return FormUrlEncoded(enableFormUrlEncoded, values);
        }

        /// <summary>
        /// Sends a multipart/form-data content (fields and files).
        /// </summary>
        /// <param name="configure">Adds the fields and the files</param>
        /// <returns></returns>
        public RestBuilder Multipart(Action<MultipartFormData> configure)
        {
            if (configure == null) { throw new ArgumentNullException(nameof(configure)); }
            MultipartFormData multipart = new MultipartFormData();
            configure(multipart);
            return With(r =>
            {
                r.multipartFormData = multipart;
                r.isEnabledFormUrlEncoded = false;
                r.payloadContent = null;
            });
        }
    }
}
