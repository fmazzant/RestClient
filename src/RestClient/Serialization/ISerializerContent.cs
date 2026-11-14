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

    /// <summary>
    /// Provides custom formatting for generic serialization and deserialization.
    /// Obsolete: implement IRestSerializer and set it with RestBuilder.WithSerializer.
    /// </summary>
    [Obsolete("Implement IRestSerializer (or IAsyncRestSerializer) and set it with RestBuilder.WithSerializer: the serializer owns its options. ISerializerContent will be removed in a future major version.")]
    public interface ISerializerContent
    {
        /// <summary>
        /// Gets content-type as string
        /// </summary>
        string MediaTypeAsString { get; }

        /// <summary>
        /// Deserializes the custom string to the specified type.
        /// </summary>
        /// <param name="value">The custom string to deserialize.</param>
        /// <param name="typeOf">The System.Type of object being deserialized.</param>
        /// <param name="options">The serializer options</param>
        /// <returns>The deserialized object from the custom string.</returns>
        object? DeserializeObject(string value, Type typeOf, object? options = null);

        /// <summary>
        /// Serializes the specified object to a custom string
        /// </summary>
        /// <param name="value">The object to serialize.</param>
        /// <param name="typeOf">The type of the value being serialized.</param>
        /// <param name="options">The serializer options</param>
        /// <returns>A custom string representation of the object.</returns>
        string SerializeObject(object? value, Type typeOf, object? options = null);
    }

    /// <summary>
    /// A serializer that can deserialize directly from the response stream, without buffering it as a string.
    /// RestClient uses it for successful responses when no OnPreviewContentResponseAsString handler is set.
    /// Obsolete: implement IAsyncRestSerializer and set it with RestBuilder.WithSerializer.
    /// </summary>
    [Obsolete("Implement IAsyncRestSerializer and set it with RestBuilder.WithSerializer: the serializer owns its options. IAsyncSerializerContent will be removed in a future major version.")]
    public interface IAsyncSerializerContent : ISerializerContent
    {
        /// <summary>
        /// Deserializes the stream to the specified type.
        /// </summary>
        /// <param name="stream">The stream to deserialize. It can be empty.</param>
        /// <param name="typeOf">The System.Type of object being deserialized.</param>
        /// <param name="options">The serializer options</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The deserialized object.</returns>
        Task<object?> DeserializeObjectAsync(Stream stream, Type typeOf, object? options = null, CancellationToken cancellationToken = default);
    }
}
