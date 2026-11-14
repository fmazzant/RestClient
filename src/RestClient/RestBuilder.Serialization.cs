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
        /// Sets the serializer of the payload and of the responses. The serializer owns its options, e.g.
        /// <c>WithSerializer(new SystemTextJsonSerializer(MyJsonContext.Default))</c> or
        /// <c>WithSerializer(new XmlRestSerializer(readerSettings, writerSettings))</c>.
        /// Implement IAsyncRestSerializer to deserialize directly from the response stream.
        /// The default serializer is SystemTextJsonSerializer with the web defaults.
        /// </summary>
        /// <param name="serializer">The serializer</param>
        /// <returns></returns>
        public RestBuilder WithSerializer(IRestSerializer serializer)
        {
            if (serializer == null) { throw new ArgumentNullException(nameof(serializer)); }
            return With(r => r.serializer = serializer);
        }

        /// <summary>
        /// Sets xml as serialization: a shortcut for <c>WithSerializer(new XmlRestSerializer(readerOptions, writerOptions))</c>.
        /// XmlSerializer is not compatible with trimming and Native AOT.
        /// </summary>
        /// <param name="readerOptions">Settings used to read (deserialize) the response</param>
        /// <param name="writerOptions">Settings used to write (serialize) the payload</param>
        /// <returns></returns>
#if NET
        [RequiresUnreferencedCode(XmlRestSerializer.ReflectionMessage)]
        [RequiresDynamicCode(XmlRestSerializer.ReflectionMessage)]
#endif
        public RestBuilder Xml(System.Xml.XmlReaderSettings? readerOptions = null, System.Xml.XmlWriterSettings? writerOptions = null)
            => WithSerializer(new XmlRestSerializer(readerOptions, writerOptions));

        /// <summary>
        /// Sets json as serialization (System.Text.Json): a shortcut for <c>WithSerializer(new SystemTextJsonSerializer(options))</c>.
        /// When options is null SystemTextJsonSerializer.DefaultOptions (web defaults) are used.
        /// </summary>
        /// <returns></returns>
        public RestBuilder Json(JsonSerializerOptions? options = null)
            => WithSerializer(new SystemTextJsonSerializer(options));

        /// <summary>
        /// Sets json as serialization using a System.Text.Json source generated context:
        /// no reflection is used, so it is compatible with trimming and Native AOT.
        /// A shortcut for <c>WithSerializer(new SystemTextJsonSerializer(context))</c>.
        /// </summary>
        /// <param name="context">The JsonSerializerContext that contains all the types sent and received</param>
        /// <returns></returns>
        public RestBuilder Json(JsonSerializerContext context)
        {
            if (context == null) { throw new ArgumentNullException(nameof(context)); }
            return WithSerializer(new SystemTextJsonSerializer(context));
        }

        /// <summary>
        /// Sets custom serialization. Implement IAsyncSerializerContent to deserialize directly from the response stream.
        /// Obsolete: implement IRestSerializer (the serializer owns its options) and use WithSerializer.
        /// </summary>
        /// <param name="serializer"></param>
        /// <param name="options">Options passed to the serializer</param>
        /// <returns></returns>
        [Obsolete("Implement IRestSerializer (the serializer owns its options) and use WithSerializer. CustomSerializer will be removed in a future major version.")]
        public RestBuilder CustomSerializer(ISerializerContent serializer, object? options = null)
        {
            if (serializer == null) { throw new ArgumentNullException(nameof(serializer)); }

            // the built-in JSON becomes SystemTextJsonSerializer, so streaming and server-sent events keep working
            IRestSerializer restSerializer = serializer is JSON
                ? new SystemTextJsonSerializer(JSON.ToJsonSerializerOptions(options))
                : SerializerContentAdapter.Create(serializer, options);
            return WithSerializer(restSerializer);
        }
    }
}
