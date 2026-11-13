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
    using System.Collections;
    using System.Collections.Generic;

    /// <summary>
    /// The route parameters of the url template
    /// </summary>
    public sealed partial class RestBuilder
    {
        /// <summary>
        /// Sets the value of a {name} placeholder of the url, for example
        /// Url("https://api.example.com/users/{id}/orders/{orderId}").Route("id", 42).Route("orderId", "A/1").
        /// The placeholders can be in the url and in the Command segments; the same placeholder used more than once
        /// gets the same value everywhere. The value is formatted with the invariant culture (as the query string parameters)
        /// and url-encoded as a single segment ('/', '?', '#', spaces and non-ASCII characters are percent-encoded).
        /// Use {{ and }} for literal braces. A value without a placeholder is ignored, so a base builder can set values
        /// used only by some requests. When a placeholder has no value the request is not sent: the result reports an
        /// InvalidOperationException. Placeholders are not supported in the scheme, host and port of the url.
        /// The url is treated as a template only when the builder has at least one route value: without Route
        /// (or after removing the last value) Url and Command are used untouched, braces included.
        /// </summary>
        /// <param name="name">The placeholder name, without braces (case-sensitive)</param>
        /// <param name="value">The value, null to remove a value set before</param>
        /// <returns></returns>
        public RestBuilder Route(string name, object? value)
        {
            if (name == null) { throw new ArgumentNullException(nameof(name)); }
            if (name.Length == 0 || name.IndexOf('{') >= 0 || name.IndexOf('}') >= 0)
            {
                throw new ArgumentException("The route name cannot be empty or contain braces.", nameof(name));
            }
            if (value is IEnumerable && !(value is string))
            {
                throw new ArgumentException("The route value cannot be a collection.", nameof(value));
            }

            string? encoded = value == null ? null : Uri.EscapeDataString(FormatValue(value));
            return With(r =>
            {
                // copy on write: the dictionary is shared with the builder this one derives from
                var values = new Dictionary<string, string>(routeValues, StringComparer.Ordinal);
                if (encoded == null)
                {
                    values.Remove(name);
                }
                else
                {
                    values[name] = encoded;
                }
                r.routeValues = values;
            });
        }
    }
}
