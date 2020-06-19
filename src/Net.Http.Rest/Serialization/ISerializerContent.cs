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

namespace Mafe.Net.Rest.Serialization
{
    using System;

    /// <summary>
    /// Provides custom formatting for generic serialization and deserialization.
    /// </summary>
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
        /// <returns>The deserialized object from the custom string.</returns>
        object DeserializeObject(string value, Type typeOf);

        /// <summary>
        /// Serializes the specified object to a custom string
        /// </summary>
        /// <param name="value">The object to serialize.</param>
        /// <param name="typeOf">The type of the value being serialized.</param>
        /// <returns>A custom string representation of the object.</returns>
        string SerializeObject(object value, Type typeOf);
    }
}
