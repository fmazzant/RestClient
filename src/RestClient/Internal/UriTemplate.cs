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

namespace Mafesoft.RestClient.Internal
{
    using System;
    using System.Collections.Generic;
    using System.Text;

    /// <summary>
    /// Expands the {name} placeholders of the url with the route values set by RestBuilder.Route
    /// (used only when the builder has at least one route value)
    /// </summary>
    internal static class UriTemplate
    {
        /// <summary>
        /// Replaces every {name} placeholder with its value (already url-encoded).
        /// {{ and }} are written as literal braces. A placeholder without a value, an empty placeholder
        /// or a { without the closing } throws InvalidOperationException. Values without a placeholder are ignored.
        /// </summary>
        internal static string Expand(string template, IReadOnlyDictionary<string, string> values)
        {
            if (template.IndexOf('{') < 0 && template.IndexOf('}') < 0)
            {
                return template;
            }

            StringBuilder builder = new StringBuilder(template.Length + 32);
            int index = 0;
            while (index < template.Length)
            {
                char c = template[index];
                if (c == '}')
                {
                    builder.Append('}');
                    index += index + 1 < template.Length && template[index + 1] == '}' ? 2 : 1;
                    continue;
                }

                if (c != '{')
                {
                    builder.Append(c);
                    index++;
                    continue;
                }

                if (index + 1 < template.Length && template[index + 1] == '{')
                {
                    builder.Append('{');
                    index += 2;
                    continue;
                }

                int end = template.IndexOf('}', index + 1);
                int nested = template.IndexOf('{', index + 1);
                if (end < 0 || (nested >= 0 && nested < end))
                {
                    throw new InvalidOperationException(
                        $"The url template is not valid: the '{{' at position {index} has no closing '}}' (use '{{{{' and '}}}}' for literal braces).");
                }

                string name = template.Substring(index + 1, end - index - 1);
                if (name.Length == 0)
                {
                    throw new InvalidOperationException("The url template contains an empty placeholder '{}'.");
                }

                if (!values.TryGetValue(name, out string? value))
                {
                    throw new InvalidOperationException(
                        $"The url template contains the placeholder '{{{name}}}' but no value was set: use Route(\"{name}\", value).");
                }

                builder.Append(value);
                index = end + 1;
            }

            return builder.ToString();
        }
    }
}
