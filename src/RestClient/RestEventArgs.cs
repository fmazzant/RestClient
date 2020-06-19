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

    /// <summary>
    /// Represents the class that contain start event data, and provides a value to use when the communication is starting.
    /// </summary>
    public class StartEventArgs : EventArgs
    {
        /// <summary>
        /// if true the request is aborted.
        /// </summary>
        public bool Cancel { get; set; } = false;

        /// <summary>
        /// The payload of request
        /// </summary>
        public object Payload { get; set; }
    }

    /// <summary>    
    /// Provides data for generic network communication events related to progress changed
    /// </summary>
    public class ProgressEventArgs : EventArgs
    {
        /// <summary>
        /// Gets a value indicating the bytes received.
        /// </summary>
        public long CurrentBytes { get; internal set; }

        /// <summary>
        /// Gets a value indicating the total bytes to receive.
        /// </summary>
        public long TotalBytes { get; internal set; }

        /// <summary>
        /// Get a value indication the progress float number of comminication. The value can be from 0 to 1
        /// </summary>
        public float ProgressFloat
        {
            get
            {
                if (TotalBytes == 0) return 0;
                return (float)((float)CurrentBytes / (float)TotalBytes);
            }
        }

        /// <summary>
        /// Get a value indication the progress percentage number of comminication. The value can be from 0 to 100
        /// </summary>
        public int ProgressPercentage
        {
            get
            {
                return (int)(ProgressFloat * 100);
            }
        }
    }
}
