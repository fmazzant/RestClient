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
    /// XML serializer based on System.Xml.Serialization.XmlSerializer.
    /// XmlSerializer uses reflection and it is not compatible with trimming and Native AOT: the constructor is annotated.
    /// </summary>
    public sealed class XmlRestSerializer : IRestSerializer
    {
        internal const string ReflectionMessage = "XmlSerializer uses reflection and runtime code generation: it is not compatible with trimming and Native AOT.";

        private const string Justification = "The constructors of XmlRestSerializer and XML are annotated: an instance exists only if the caller accepted the warning.";

        /// <summary>
        /// XmlSerializer instances are expensive to create: they are cached per type.
        /// </summary>
        private static readonly ConcurrentDictionary<Type, Serializer.XmlSerializer> serializers
            = new ConcurrentDictionary<Type, Serializer.XmlSerializer>();

        private readonly XmlReaderSettings? readerSettings;
        private readonly XmlWriterSettings? writerSettings;

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="readerSettings">Settings used to read (deserialize) the response. When null the XmlReaderSettings defaults are used.</param>
        /// <param name="writerSettings">Settings used to write (serialize) the payload. When null the XmlWriterSettings defaults are used.</param>
#if NET
        [RequiresUnreferencedCode(ReflectionMessage)]
        [RequiresDynamicCode(ReflectionMessage)]
#endif
        public XmlRestSerializer(XmlReaderSettings? readerSettings = null, XmlWriterSettings? writerSettings = null)
        {
            this.readerSettings = readerSettings;
            this.writerSettings = writerSettings;
        }

        /// <summary>
        /// Gets the XML media type
        /// </summary>
        public string MediaType => "application/xml";

        /// <summary>
        /// Serializes the value to a XML string (an empty string when value is null).
        /// </summary>
        /// <param name="value">The value to serialize.</param>
        /// <param name="type">The type of the value being serialized.</param>
        /// <returns>A XML string representation of the value.</returns>
        public string Serialize(object? value, Type type) => Serialize(value, type, writerSettings);

        /// <summary>
        /// Deserializes the XML string to the specified type (null when the string is empty).
        /// </summary>
        /// <param name="value">The XML string to deserialize.</param>
        /// <param name="type">The type of the object being deserialized.</param>
        /// <returns>The deserialized object.</returns>
        public object? Deserialize(string value, Type type) => Deserialize(value, type, readerSettings);

        /// <summary>
        /// Serializes the value with the given settings (shared with the obsolete XML class)
        /// </summary>
#if NET
        [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = Justification)]
        [UnconditionalSuppressMessage("AOT", "IL3050", Justification = Justification)]
#endif
        internal static string Serialize(object? value, Type type, XmlWriterSettings? settings)
        {
            if (value == null)
            {
                return string.Empty;
            }

            var builder = new StringBuilder();
            using (var writer = XmlWriter.Create(builder, settings ?? new XmlWriterSettings()))
            {
                GetSerializer(type ?? value.GetType()).Serialize(writer, value);
            }
            return builder.ToString();
        }

        /// <summary>
        /// Deserializes the value with the given settings (shared with the obsolete XML class)
        /// </summary>
#if NET
        [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = Justification)]
        [UnconditionalSuppressMessage("AOT", "IL3050", Justification = Justification)]
#endif
        internal static object? Deserialize(string value, Type type, XmlReaderSettings? settings)
        {
            if (string.IsNullOrEmpty(value))
            {
                return null;
            }

            using (var reader = XmlReader.Create(new StringReader(value), settings ?? new XmlReaderSettings()))
            {
                return GetSerializer(type).Deserialize(reader);
            }
        }

#if NET
        [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = Justification)]
        [UnconditionalSuppressMessage("AOT", "IL3050", Justification = Justification)]
#endif
        private static Serializer.XmlSerializer GetSerializer(Type type)
            => serializers.GetOrAdd(type, t => new Serializer.XmlSerializer(t));
    }
}
