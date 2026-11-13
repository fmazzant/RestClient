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
    using Mafesoft.RestClient.Serialization;
    using Mafesoft.RestClient.Serialization.Json;
    using Mafesoft.RestClient.Serialization.Xml;
    using System;
    using System.Diagnostics.CodeAnalysis;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    /// <summary>
    /// The serialization of the payload and the deserialization of the response
    /// </summary>
    public sealed partial class RestBuilder
    {
        /// <summary>
        /// Sets xml as serialization. XmlSerializer is not compatible with trimming and Native AOT.
        /// </summary>
        /// <param name="readerOptions">Settings used to read (deserialize) the response</param>
        /// <param name="writerOptions">Settings used to write (serialize) the payload</param>
        /// <returns></returns>
#if NET
        [RequiresUnreferencedCode(XML.ReflectionMessage)]
        [RequiresDynamicCode(XML.ReflectionMessage)]
#endif
        public RestBuilder Xml(System.Xml.XmlReaderSettings? readerOptions = null, System.Xml.XmlWriterSettings? writerOptions = null)
        {
            XML xml = new XML();
            return With(r =>
            {
                r.serializer = xml;
                r.serializeOptions = writerOptions;
                r.deserializeOptions = readerOptions;
            });
        }

        /// <summary>
        /// Sets json as serialization (System.Text.Json). When options is null JSON.DefaultOptions (web defaults) are used.
        /// </summary>
        /// <returns></returns>
        public RestBuilder Json(JsonSerializerOptions? options = null)
            => With(r =>
            {
                r.serializer = new JSON();
                r.serializeOptions = options;
                r.deserializeOptions = options;
            });

        /// <summary>
        /// Sets json as serialization using a System.Text.Json source generated context:
        /// no reflection is used, so it is compatible with trimming and Native AOT.
        /// </summary>
        /// <param name="context">The JsonSerializerContext that contains all the types sent and received</param>
        /// <returns></returns>
        public RestBuilder Json(JsonSerializerContext context)
        {
            if (context == null) { throw new ArgumentNullException(nameof(context)); }
            return Json(context.Options);
        }

        /// <summary>
        /// Sets custom serialization. Implement IAsyncSerializerContent to deserialize directly from the response stream.
        /// </summary>
        /// <param name="serializer"></param>
        /// <param name="options">Options passed to the serializer</param>
        /// <returns></returns>
        public RestBuilder CustomSerializer(ISerializerContent serializer, object? options = null)
        {
            if (serializer == null) { throw new ArgumentNullException(nameof(serializer)); }
            return With(r =>
            {
                r.serializer = serializer;
                r.serializeOptions = options;
                r.deserializeOptions = options;
            });
        }
    }
}
