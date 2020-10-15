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

namespace RestClient.Serialization.Xml
{
    using System;
    using System.IO;
    using System.Xml;
    using Serializer = System.Xml.Serialization;

    /// <summary>
    /// Implements a RestClient.Serialization.ISerializerContent for custom XML object Serialization
    /// </summary>
    public sealed class XML : ISerializerContent
    {
        /// <summary>
        /// Initializes a new instance of the RestClient.Serialization.Xml.XML class for the specified JSON by using the default Serialization.
        /// </summary>
        public XML() : base() { }

        /// <summary>
        /// Gets XML content-type 
        /// </summary>
        public string MediaTypeAsString => "application/xml";

        /// <summary>
        /// Deserializes the XML string to the specified type.
        /// </summary>
        /// <param name="value">The XML string to deserialize.</param>
        /// <param name="typeOf">The System.Type of object being deserialized.</param>
        /// <returns>The deserialized object from the XML string.</returns>
        public object DeserializeObject(string value, Type typeOf, object options = null)
        {
            XmlReaderSettings xmlOptions = options as XmlReaderSettings ?? new XmlReaderSettings();
            if (string.IsNullOrEmpty(value))
            {
                return null;
            }
            Serializer.XmlSerializer xml = new Serializer.XmlSerializer(typeOf);
            StringReader reader = new StringReader(value);
            return xml.Deserialize(reader);
        }

        /// <summary>
        /// Serializes the specified object to a XML string
        /// </summary>
        /// <param name="value">The object to serialize.</param>
        /// <param name="typeOf">The type of the value being serialized.</param>
        /// <returns>A XML string representation of the object.</returns>
        public string SerializeObject(object value, Type typeOf, object options = null)
        {
            XmlWriterSettings xmlOptions = options as XmlWriterSettings ?? new XmlWriterSettings();
            if (value == null)
            {
                return string.Empty;
            }

            Serializer.XmlSerializer xml = new Serializer.XmlSerializer(typeOf);
            StringWriter writer = new StringWriter();
            xml.Serialize(writer, value);
            return writer.ToString();
        }
    }
}
