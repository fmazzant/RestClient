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

namespace RestClient.Serialization.Json
{
    using System;

    /// <summary>
    /// Implements a RestClient.Serialization.ISerializerContent for custom JSON object Serialization
    /// </summary>
    public sealed class JSON : ISerializerContent
    {
        /// <summary>
        /// Initializes a new instance of the RestClient.Serialization.Json.JSON class for the specified JSON by using the default Serialization.
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
        /// <returns>The deserialized object from the JSON string.</returns>
        public object DeserializeObject(string value, Type typeOf)
        {
            if (string.IsNullOrEmpty(value))
            {
                return null;
            }
#if NETSTANDARD1_3
            return Newtonsoft.Json.JsonConvert.DeserializeObject(value, typeOf);
#endif
#if NETSTANDARD2_0 || NETSTANDARD2_1
            return System.Text.Json.JsonSerializer.Deserialize(value, typeOf);
#endif
        }

        /// <summary>
        /// Serializes the specified object to a JSON string
        /// </summary>
        /// <param name="value">The object to serialize.</param>
        /// <param name="typeOf">The type of the value being serialized.</param>
        /// <returns>A JSON string representation of the object.</returns>
        public string SerializeObject(object value, Type typeOf)
        {
            if (value == null)
            {
                return string.Empty;
            }

#if NETSTANDARD1_3
            return Newtonsoft.Json.JsonConvert.SerializeObject(value);
#endif
#if NETSTANDARD2_0 || NETSTANDARD2_1
            return System.Text.Json.JsonSerializer.Serialize(value);
#endif
        }
    }
}
