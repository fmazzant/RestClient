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
    using System.Globalization;
    using System.Linq;

    /// <summary>
    /// The query string parameters of the url
    /// </summary>
    public sealed partial class RestBuilder
    {
        /// <summary>
        /// Sets a query string parameter, replacing the previous values of the key.
        /// Key and value are url-encoded and the value is formatted with the invariant culture.
        /// When the value is a collection (not a string) the key is repeated for each item: ?id=1&amp;id=2
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public RestBuilder Parameter(string key, object? value)
            => Parameter(new RestParameter(key, value));

        /// <summary>
        /// Sets a list of query string parameters, replacing the previous values of the keys.
        /// Parameters with the same key in the same call are all added: ?id=1&amp;id=2
        /// </summary>
        /// <param name="parameter"></param>
        /// <param name="others"></param>
        /// <returns></returns>
        public RestBuilder Parameter(RestParameter parameter, params RestParameter[] others)
            => SetParameters(new[] { parameter }.Concat(others ?? Array.Empty<RestParameter>()).ToList());

        /// <summary>
        /// Sets a list of query string parameters, replacing the previous values of the keys.
        /// Parameters with the same key in the same call are all added: ?id=1&amp;id=2
        /// </summary>
        /// <param name="parameters"></param>
        /// <returns></returns>
        public RestBuilder Parameter(Action<List<RestParameter>> parameters)
        {
            if (parameters == null) { throw new ArgumentNullException(nameof(parameters)); }
            List<RestParameter> list = new List<RestParameter>();
            parameters(list);
            return SetParameters(list);
        }

        private RestBuilder SetParameters(List<RestParameter> list)
        {
            if (list.Any(p => !p.HasKey)) { throw new ArgumentException("The parameter key cannot be null."); }

            var expanded = list.SelectMany(p => ExpandValues(p.Value).Select(v => new KeyValuePair<string, string>(p.Key!, v))).ToList();
            var keys = new HashSet<string>(list.Select(p => p.Key!), StringComparer.Ordinal);

            return With(r =>
            {
                r.parameters.RemoveAll(p => keys.Contains(p.Key));
                r.parameters.AddRange(expanded);
            });
        }

        private static IEnumerable<string> ExpandValues(object? value)
        {
            if (value is IEnumerable enumerable && !(value is string))
            {
                foreach (object? item in enumerable)
                {
                    yield return FormatValue(item);
                }
            }
            else
            {
                yield return FormatValue(value);
            }
        }

        /// <summary>
        /// Formats a value using the invariant culture (dates as ISO 8601, booleans lowercase)
        /// </summary>
        private static string FormatValue(object? value)
        {
            switch (value)
            {
                case null: return string.Empty;
                case string s: return s;
                case bool b: return b ? "true" : "false";
                case DateTime dt: return dt.ToString("O", CultureInfo.InvariantCulture);
                case DateTimeOffset dto: return dto.ToString("O", CultureInfo.InvariantCulture);
                case IFormattable f: return f.ToString(null, CultureInfo.InvariantCulture);
                default: return value.ToString() ?? string.Empty;
            }
        }
    }
}
