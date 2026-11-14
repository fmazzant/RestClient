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
    /// Serializes the payload of the requests and deserializes the responses.
    /// The serializer owns its configuration (set it in the constructor): set it on a builder with RestBuilder.WithSerializer.
    /// </summary>
    public interface IRestSerializer
    {
        /// <summary>
        /// Gets the media type of the serialized payload (the Content-Type of the request), e.g. "application/json"
        /// </summary>
        string MediaType { get; }

        /// <summary>
        /// Serializes the value to a string.
        /// </summary>
        /// <param name="value">The value to serialize.</param>
        /// <param name="type">The type of the value being serialized.</param>
        /// <returns>The string representation of the value.</returns>
        string Serialize(object? value, Type type);

        /// <summary>
        /// Deserializes the string to the specified type.
        /// </summary>
        /// <param name="value">The string to deserialize. It can be empty.</param>
        /// <param name="type">The type of the object being deserialized.</param>
        /// <returns>The deserialized object.</returns>
        object? Deserialize(string value, Type type);
    }

    /// <summary>
    /// A serializer that can deserialize directly from the response stream, without buffering it as a string.
    /// RestClient uses it for successful responses when no OnPreviewContentResponseAsString handler is set.
    /// </summary>
    public interface IAsyncRestSerializer : IRestSerializer
    {
        /// <summary>
        /// Deserializes the stream to the specified type.
        /// </summary>
        /// <param name="stream">The stream to deserialize. It can be empty.</param>
        /// <param name="type">The type of the object being deserialized.</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The deserialized object.</returns>
        Task<object?> DeserializeAsync(Stream stream, Type type, CancellationToken cancellationToken = default);
    }
}
