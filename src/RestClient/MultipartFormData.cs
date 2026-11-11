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

namespace Mafesoft.RestClient
{
    using Mafesoft.RestClient.Http;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net.Http;
    using System.Net.Http.Headers;

    /// <summary>
    /// Describes a multipart/form-data content (fields and files).
    /// The content is created for every attempt, so the request can be retried
    /// (streams must be seekable or provided by a factory to be sent more than once).
    /// </summary>
    public sealed class MultipartFormData
    {
        private readonly List<Part> parts = new List<Part>();

        internal MultipartFormData() { }

        /// <summary>
        /// Adds a text field
        /// </summary>
        /// <param name="name">Field name</param>
        /// <param name="value">Field value</param>
        /// <returns>The current instance</returns>
        public MultipartFormData Add(string name, string value)
        {
            if (name == null) { throw new ArgumentNullException(nameof(name)); }
            if (value == null) { throw new ArgumentNullException(nameof(value)); }
            parts.Add(new Part(name, null, () => new StringContent(value)));
            return this;
        }

        /// <summary>
        /// Adds a file from a byte array
        /// </summary>
        /// <param name="name">Field name</param>
        /// <param name="content">File content</param>
        /// <param name="fileName">File name</param>
        /// <param name="contentType">Content type, application/octet-stream when null</param>
        /// <returns>The current instance</returns>
        public MultipartFormData AddFile(string name, byte[] content, string fileName, string? contentType = null)
        {
            if (content == null) { throw new ArgumentNullException(nameof(content)); }
            return AddFile(name, fileName, contentType, () => new ByteArrayContent(content));
        }

        /// <summary>
        /// Adds a file from a stream. The stream is not disposed by RestClient.
        /// A seekable stream is rewound to its current position before every attempt (retry, refresh token);
        /// a non seekable stream can be sent only once.
        /// </summary>
        /// <param name="name">Field name</param>
        /// <param name="content">File content</param>
        /// <param name="fileName">File name</param>
        /// <param name="contentType">Content type, application/octet-stream when null</param>
        /// <returns>The current instance</returns>
        public MultipartFormData AddFile(string name, Stream content, string fileName, string? contentType = null)
        {
            if (content == null) { throw new ArgumentNullException(nameof(content)); }
            long start = content.CanSeek ? content.Position : 0;
            return AddFile(name, fileName, contentType, () =>
            {
                if (content.CanSeek)
                {
                    content.Position = start;
                }
                return new StreamContent(new LeaveOpenStream(content));
            });
        }

        /// <summary>
        /// Adds a file from a stream factory, invoked for every attempt. The stream is disposed by RestClient.
        /// </summary>
        /// <param name="name">Field name</param>
        /// <param name="contentFactory">Creates the file content</param>
        /// <param name="fileName">File name</param>
        /// <param name="contentType">Content type, application/octet-stream when null</param>
        /// <returns>The current instance</returns>
        public MultipartFormData AddFile(string name, Func<Stream> contentFactory, string fileName, string? contentType = null)
        {
            if (contentFactory == null) { throw new ArgumentNullException(nameof(contentFactory)); }
            return AddFile(name, fileName, contentType, () => new StreamContent(contentFactory()));
        }

        /// <summary>
        /// Adds a file from the file system. The file is opened when the request is sent.
        /// </summary>
        /// <param name="name">Field name</param>
        /// <param name="filePath">Path of the file</param>
        /// <param name="contentType">Content type, application/octet-stream when null</param>
        /// <returns>The current instance</returns>
        public MultipartFormData AddFile(string name, string filePath, string? contentType = null)
        {
            if (filePath == null) { throw new ArgumentNullException(nameof(filePath)); }
            return AddFile(name, () => File.OpenRead(filePath), Path.GetFileName(filePath), contentType);
        }

        private MultipartFormData AddFile(string name, string fileName, string? contentType, Func<HttpContent> factory)
        {
            if (name == null) { throw new ArgumentNullException(nameof(name)); }
            if (fileName == null) { throw new ArgumentNullException(nameof(fileName)); }

            parts.Add(new Part(name, fileName, () =>
            {
                HttpContent content = factory();
                content.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType ?? "application/octet-stream");
                return content;
            }));
            return this;
        }

        /// <summary>
        /// Description used by OnPreviewContentRequestAsString
        /// </summary>
        internal string Describe()
            => string.Join("&", parts.Select(p => p.FileName == null ? p.Name : $"{p.Name}=@{p.FileName}"));

        /// <summary>
        /// Creates the HTTP content
        /// </summary>
        internal HttpContent CreateContent()
        {
            MultipartFormDataContent content = new MultipartFormDataContent();
            try
            {
                foreach (Part part in parts)
                {
                    if (part.FileName == null)
                    {
                        content.Add(part.Factory(), part.Name);
                    }
                    else
                    {
                        content.Add(part.Factory(), part.Name, part.FileName);
                    }
                }
                return content;
            }
            catch
            {
                content.Dispose();
                throw;
            }
        }

        private sealed class Part
        {
            public Part(string name, string? fileName, Func<HttpContent> factory)
            {
                Name = name;
                FileName = fileName;
                Factory = factory;
            }

            public string Name { get; }
            public string? FileName { get; }
            public Func<HttpContent> Factory { get; }
        }
    }
}
