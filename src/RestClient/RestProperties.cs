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

namespace RestClient
{
    using System;
    using System.Net.Http;

    /// <summary>
    /// Provides a class for rest connection properties
    /// </summary>
    public class RestProperties
    {
        /// <summary>
        /// Default buffer size 20k
        /// </summary>
        private const int DefaultBufferSize = 5 * 4096 * 4; //80kb

        /// <summary>
        /// End Point
        /// </summary>
        public Uri EndPoint { get; set; }

        /// <summary>
        /// Specifies how client certificated are provided. Default is "Manual"
        /// </summary>
        public ClientCertificateOption CertificateOption { get; set; } = ClientCertificateOption.Manual;

        /// <summary>
        /// Specifies timeout. Default 100.000 milliseconds.
        /// </summary>
        public TimeSpan Timeout { get; set; } = TimeSpan.FromMilliseconds(100000);

        /// <summary>
        /// Specifies Buffer Size. Default 20kb.
        /// </summary>
        public int BufferSize { get; set; } = DefaultBufferSize;
    }
}
