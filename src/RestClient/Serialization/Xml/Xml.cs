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

namespace Mafesoft.RestClient.Serialization.Xml
{
    using System;
    using System.Collections.Concurrent;
    using System.Diagnostics.CodeAnalysis;
    using System.IO;
    using System.Text;
    using System.Xml;
    using Serializer = System.Xml.Serialization;

    /// <summary>
    /// Implements a Mafesoft.RestClient.Serialization.ISerializerContent for XML object serialization.
    /// XmlSerializer uses reflection and it is not compatible with trimming and Native AOT.
    /// </summary>
    public sealed class XML : ISerializerContent
    {
        internal const string ReflectionMessage = "XmlSerializer uses reflection and runtime code generation: it is not compatible with trimming and Native AOT.";

        /// <summary>
        /// XmlSerializer instances are expensive to create: they are cached per type.
        /// </summary>
        private static readonly ConcurrentDictionary<Type, Serializer.XmlSerializer> serializers
            = new ConcurrentDictionary<Type, Serializer.XmlSerializer>();

        /// <summary>
        /// Initializes a new instance of the Mafesoft.RestClient.Serialization.Xml.XML class.
        /// </summary>
#if NET
        [RequiresUnreferencedCode(ReflectionMessage)]
        [RequiresDynamicCode(ReflectionMessage)]
#endif
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
        /// <param name="options">The System.Xml.XmlReaderSettings</param>
        /// <returns>The deserialized object from the XML string.</returns>
        public object? DeserializeObject(string value, Type typeOf, object? options = null)
        {
            if (string.IsNullOrEmpty(value))
            {
                return null;
            }

            XmlReaderSettings xmlOptions = options as XmlReaderSettings ?? new XmlReaderSettings();
            using (var reader = XmlReader.Create(new StringReader(value), xmlOptions))
            {
                return Deserialize(typeOf, reader);
            }
        }

        /// <summary>
        /// Serializes the specified object to a XML string
        /// </summary>
        /// <param name="value">The object to serialize.</param>
        /// <param name="typeOf">The type of the value being serialized.</param>
        /// <param name="options">The System.Xml.XmlWriterSettings</param>
        /// <returns>A XML string representation of the object.</returns>
        public string SerializeObject(object? value, Type typeOf, object? options = null)
        {
            if (value == null)
            {
                return string.Empty;
            }

            XmlWriterSettings xmlOptions = options as XmlWriterSettings ?? new XmlWriterSettings();
            var builder = new StringBuilder();
            using (var writer = XmlWriter.Create(builder, xmlOptions))
            {
                Serialize(typeOf ?? value.GetType(), writer, value);
            }
            return builder.ToString();
        }

#if NET
        [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "The constructor of XML is annotated: an instance exists only if the caller accepted the warning.")]
        [UnconditionalSuppressMessage("AOT", "IL3050", Justification = "The constructor of XML is annotated: an instance exists only if the caller accepted the warning.")]
#endif
        private static object? Deserialize(Type type, XmlReader reader)
            => serializers.GetOrAdd(type, t => new Serializer.XmlSerializer(t)).Deserialize(reader);

#if NET
        [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "The constructor of XML is annotated: an instance exists only if the caller accepted the warning.")]
        [UnconditionalSuppressMessage("AOT", "IL3050", Justification = "The constructor of XML is annotated: an instance exists only if the caller accepted the warning.")]
#endif
        private static void Serialize(Type type, XmlWriter writer, object value)
            => serializers.GetOrAdd(type, t => new Serializer.XmlSerializer(t)).Serialize(writer, value);
    }
}
