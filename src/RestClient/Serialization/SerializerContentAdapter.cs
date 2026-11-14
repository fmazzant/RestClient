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

namespace Mafesoft.RestClient.Serialization
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;

#pragma warning disable CS0618 // the adapter exists to keep the obsolete ISerializerContent working

    /// <summary>
    /// Adapts an obsolete ISerializerContent (and its untyped options) to IRestSerializer.
    /// The same options are passed to SerializeObject and DeserializeObject, as in the previous versions.
    /// </summary>
    internal class SerializerContentAdapter : IRestSerializer
    {
        protected SerializerContentAdapter(ISerializerContent serializer, object? options)
        {
            Serializer = serializer;
            Options = options;
        }

        /// <summary>
        /// The adapted serializer
        /// </summary>
        internal ISerializerContent Serializer { get; }

        /// <summary>
        /// The options passed to every call
        /// </summary>
        internal object? Options { get; }

        public string MediaType => Serializer.MediaTypeAsString;

        public string Serialize(object? value, Type type) => Serializer.SerializeObject(value, type, Options);

        public object? Deserialize(string value, Type type) => Serializer.DeserializeObject(value, type, Options);

        /// <summary>
        /// Creates the adapter: an IAsyncSerializerContent becomes an IAsyncRestSerializer
        /// </summary>
        internal static IRestSerializer Create(ISerializerContent serializer, object? options)
        {
            if (serializer == null) { throw new ArgumentNullException(nameof(serializer)); }
            return serializer is IAsyncSerializerContent asyncSerializer
                ? new AsyncSerializerContentAdapter(asyncSerializer, options)
                : new SerializerContentAdapter(serializer, options);
        }

        private sealed class AsyncSerializerContentAdapter : SerializerContentAdapter, IAsyncRestSerializer
        {
            private readonly IAsyncSerializerContent asyncSerializer;

            internal AsyncSerializerContentAdapter(IAsyncSerializerContent serializer, object? options)
                : base(serializer, options)
            {
                asyncSerializer = serializer;
            }

            public Task<object?> DeserializeAsync(Stream stream, Type type, CancellationToken cancellationToken = default)
                => asyncSerializer.DeserializeObjectAsync(stream, type, Options, cancellationToken);
        }
    }

#pragma warning restore CS0618
}
