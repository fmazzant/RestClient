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

namespace RestClient.Serialization.Json
{
    using System;
    using System.IO;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Text.Json.Serialization.Metadata;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Implements a RestClient.Serialization.ISerializerContent for JSON object serialization based on System.Text.Json.
    /// The type metadata is always resolved with JsonSerializerOptions.GetTypeInfo: pass the options of a
    /// JsonSerializerContext (source generator) to be Native AOT and trimming safe.
    /// </summary>
    public sealed class JSON : IAsyncSerializerContent
    {
        /// <summary>
        /// Default options used when no options are provided: web defaults (camelCase names, case-insensitive properties, numbers allowed as strings).
        /// They use reflection: in Native AOT applications pass the options of a JsonSerializerContext.
        /// </summary>
        public static JsonSerializerOptions DefaultOptions { get; } = new JsonSerializerOptions(JsonSerializerDefaults.Web);

        private static readonly object resolverLock = new object();

        /// <summary>
        /// Initializes a new instance of the RestClient.Serialization.Json.JSON class.
        /// </summary>
        public JSON() : base() { }

        /// <summary>
        /// Gets JSON content-type
        /// </summary>
        public string MediaTypeAsString => "application/json";

        /// <summary>
        /// Deserializes the JSON string to the specified type.
        /// </summary>
        /// <param name="value">The JSON string to deserialize.</param>
        /// <param name="typeOf">The System.Type of object being deserialized.</param>
        /// <param name="options">The JsonSerializerOptions or the JsonSerializerContext. When null DefaultOptions are used.</param>
        /// <returns>The deserialized object from the JSON string.</returns>
        public object? DeserializeObject(string value, Type typeOf, object? options = null)
        {
            if (string.IsNullOrEmpty(value))
            {
                return null;
            }
            return JsonSerializer.Deserialize(value, GetTypeInfo(typeOf, options));
        }

        /// <summary>
        /// Deserializes the JSON stream to the specified type.
        /// </summary>
        /// <param name="stream">The JSON stream to deserialize.</param>
        /// <param name="typeOf">The System.Type of object being deserialized.</param>
        /// <param name="options">The JsonSerializerOptions or the JsonSerializerContext. When null DefaultOptions are used.</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The deserialized object.</returns>
        public async Task<object?> DeserializeObjectAsync(Stream stream, Type typeOf, object? options = null, CancellationToken cancellationToken = default)
        {
            if (stream == null) { throw new ArgumentNullException(nameof(stream)); }
            return await JsonSerializer.DeserializeAsync(stream, GetTypeInfo(typeOf, options), cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Serializes the specified object to a JSON string
        /// </summary>
        /// <param name="value">The object to serialize.</param>
        /// <param name="typeOf">The type of the value being serialized.</param>
        /// <param name="options">The JsonSerializerOptions or the JsonSerializerContext. When null DefaultOptions are used.</param>
        /// <returns>A JSON string representation of the object.</returns>
        public string SerializeObject(object? value, Type typeOf, object? options = null)
        {
            if (value == null)
            {
                return string.Empty;
            }
            return JsonSerializer.Serialize(value, GetTypeInfo(typeOf ?? value.GetType(), options));
        }

        private static JsonTypeInfo GetTypeInfo(Type type, object? options)
        {
            if (type == null) { throw new ArgumentNullException(nameof(type)); }

            JsonSerializerOptions jsonOptions = options switch
            {
                JsonSerializerContext context => context.Options,
                JsonSerializerOptions o => o,
                _ => DefaultOptions
            };

            if (!jsonOptions.IsReadOnly && jsonOptions.TypeInfoResolver == null && JsonSerializer.IsReflectionEnabledByDefault)
            {
                lock (resolverLock)
                {
                    if (!jsonOptions.IsReadOnly && jsonOptions.TypeInfoResolver == null)
                    {
#pragma warning disable IL2026, IL3050 // guarded by JsonSerializer.IsReflectionEnabledByDefault, false in Native AOT/trimmed apps
                        jsonOptions.TypeInfoResolver = new DefaultJsonTypeInfoResolver();
#pragma warning restore IL2026, IL3050
                    }
                }
            }

            return jsonOptions.GetTypeInfo(type);
        }
    }
}
