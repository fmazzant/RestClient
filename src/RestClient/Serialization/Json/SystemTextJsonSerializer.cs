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

namespace Mafesoft.RestClient.Serialization.Json
{
    using System;
    using System.IO;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Text.Json.Serialization.Metadata;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// JSON serializer based on System.Text.Json. The type metadata is always resolved with JsonSerializerOptions.GetTypeInfo:
    /// create it with a JsonSerializerContext (source generator) to be Native AOT and trimming safe.
    /// It is the default serializer of RestBuilder.
    /// </summary>
    public sealed class SystemTextJsonSerializer : IAsyncRestSerializer
    {
        private static readonly object resolverLock = new object();

        /// <summary>
        /// Default options used when no options are provided: web defaults (camelCase names, case-insensitive properties, numbers allowed as strings).
        /// They use reflection: in Native AOT applications use a JsonSerializerContext.
        /// </summary>
        public static JsonSerializerOptions DefaultOptions { get; } = new JsonSerializerOptions(JsonSerializerDefaults.Web);

        /// <summary>
        /// Initializes a new instance with the given options.
        /// </summary>
        /// <param name="options">The System.Text.Json options. When null DefaultOptions (web defaults) are used.</param>
        public SystemTextJsonSerializer(JsonSerializerOptions? options = null)
        {
            Options = options ?? DefaultOptions;
        }

        /// <summary>
        /// Initializes a new instance with a source generated context:
        /// no reflection is used, so it is compatible with trimming and Native AOT.
        /// </summary>
        /// <param name="context">The JsonSerializerContext that contains all the types sent and received</param>
        public SystemTextJsonSerializer(JsonSerializerContext context)
        {
            if (context == null) { throw new ArgumentNullException(nameof(context)); }
            Options = context.Options;
        }

        /// <summary>
        /// Gets the System.Text.Json options used by the serializer
        /// </summary>
        public JsonSerializerOptions Options { get; }

        /// <summary>
        /// Gets the JSON media type
        /// </summary>
        public string MediaType => "application/json";

        /// <summary>
        /// Serializes the value to a JSON string (an empty string when value is null).
        /// </summary>
        /// <param name="value">The value to serialize.</param>
        /// <param name="type">The type of the value being serialized.</param>
        /// <returns>A JSON string representation of the value.</returns>
        public string Serialize(object? value, Type type)
        {
            if (value == null)
            {
                return string.Empty;
            }
            return JsonSerializer.Serialize(value, GetTypeInfo(type ?? value.GetType(), Options));
        }

        /// <summary>
        /// Deserializes the JSON string to the specified type (null when the string is empty).
        /// </summary>
        /// <param name="value">The JSON string to deserialize.</param>
        /// <param name="type">The type of the object being deserialized.</param>
        /// <returns>The deserialized object.</returns>
        public object? Deserialize(string value, Type type)
        {
            if (string.IsNullOrEmpty(value))
            {
                return null;
            }
            return JsonSerializer.Deserialize(value, GetTypeInfo(type, Options));
        }

        /// <summary>
        /// Deserializes the JSON stream to the specified type.
        /// </summary>
        /// <param name="stream">The JSON stream to deserialize.</param>
        /// <param name="type">The type of the object being deserialized.</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The deserialized object.</returns>
        public async Task<object?> DeserializeAsync(Stream stream, Type type, CancellationToken cancellationToken = default)
        {
            if (stream == null) { throw new ArgumentNullException(nameof(stream)); }
            return await JsonSerializer.DeserializeAsync(stream, GetTypeInfo(type, Options), cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets the metadata of T from the options of the serializer
        /// </summary>
        internal JsonTypeInfo<T> GetTypeInfo<T>()
            => (JsonTypeInfo<T>)GetTypeInfo(typeof(T), Options);

        /// <summary>
        /// Gets the metadata of the type from the options. Options without a resolver get the reflection
        /// resolver, only when reflection is enabled (it is not in Native AOT/trimmed apps).
        /// </summary>
        internal static JsonTypeInfo GetTypeInfo(Type type, JsonSerializerOptions options)
        {
            if (type == null) { throw new ArgumentNullException(nameof(type)); }

            if (!options.IsReadOnly && options.TypeInfoResolver == null && JsonSerializer.IsReflectionEnabledByDefault)
            {
                lock (resolverLock)
                {
                    if (!options.IsReadOnly && options.TypeInfoResolver == null)
                    {
#pragma warning disable IL2026, IL3050 // guarded by JsonSerializer.IsReflectionEnabledByDefault, false in Native AOT/trimmed apps
                        options.TypeInfoResolver = new DefaultJsonTypeInfoResolver();
#pragma warning restore IL2026, IL3050
                    }
                }
            }

            return options.GetTypeInfo(type);
        }
    }
}
