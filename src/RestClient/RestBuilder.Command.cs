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
    using System;
    using System.Globalization;

    /// <summary>
    /// The commands (path segments) of the url
    /// </summary>
    public sealed partial class RestBuilder
    {
        /// <summary>
        /// Adds the command (url segment) to request
        /// </summary>
        /// <param name="command"></param>
        /// <returns></returns>
        public RestBuilder Command(string command)
        {
            if (command == null) { throw new ArgumentNullException(nameof(command)); }
            string prefix = !command.StartsWith("/", StringComparison.Ordinal) ? "/" : string.Empty;
            return With(r => r.commands.Add($"{prefix}{command}"));
        }

        /// <summary>
        /// Adds the command (url segment) to request
        /// </summary>
        /// <param name="command"></param>
        /// <returns></returns>
        public RestBuilder Command(int command)
            => Command(command.ToString(CultureInfo.InvariantCulture));

        /// <summary>
        /// Adds the command (url segment) to request
        /// </summary>
        /// <param name="command"></param>
        /// <returns></returns>
        public RestBuilder Command(long command)
            => Command(command.ToString(CultureInfo.InvariantCulture));

        /// <summary>
        /// Adds the command (url segment) to request
        /// </summary>
        /// <param name="command"></param>
        /// <returns></returns>
        public RestBuilder Command(Guid command)
            => Command(command.ToString());
    }
}
