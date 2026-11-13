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
    using System.Collections.Generic;
    using System.Net.Http.Headers;
    using System.Text.Json;

    /// <summary>
    /// A Problem Details object (RFC 9457, <c>application/problem+json</c>), the error format of ASP.NET Core and of
    /// many other APIs. It is read from the body of an error response by <see cref="RestResult.Problem"/>.
    /// It has no dependency on ASP.NET Core and it is read without reflection (Native AOT and trimming safe).
    /// </summary>
    public sealed class RestProblemDetails
    {
        /// <summary>
        /// The media type of a Problem Details object in JSON: <c>application/problem+json</c>.
        /// </summary>
        public const string MediaType = "application/problem+json";

        /// <summary>
        /// Gets or sets the URI reference that identifies the problem type ("type" member).
        /// It is null when the member is missing: the RFC says that it is then "about:blank".
        /// </summary>
        public string? Type { get; set; }

        /// <summary>
        /// Gets or sets a short, human-readable summary of the problem type ("title" member).
        /// </summary>
        public string? Title { get; set; }

        /// <summary>
        /// Gets or sets the HTTP status code set by the server ("status" member).
        /// </summary>
        public int? Status { get; set; }

        /// <summary>
        /// Gets or sets a human-readable explanation specific to this occurrence of the problem ("detail" member).
        /// </summary>
        public string? Detail { get; set; }

        /// <summary>
        /// Gets or sets the URI reference that identifies this occurrence of the problem ("instance" member).
        /// </summary>
        public string? Instance { get; set; }

        /// <summary>
        /// Gets the validation errors ("errors" member, as written by ASP.NET Core <c>ValidationProblem</c>):
        /// the name of the field and its error messages. It is empty when the problem has no validation errors.
        /// </summary>
        public IDictionary<string, string[]> Errors { get; } = new Dictionary<string, string[]>(StringComparer.Ordinal);

        /// <summary>
        /// Gets the extension members: every member other than type, title, status, detail, instance and errors
        /// (for example "traceId"), as JSON elements that can be read without reflection.
        /// </summary>
        public IDictionary<string, JsonElement> Extensions { get; } = new Dictionary<string, JsonElement>(StringComparer.Ordinal);

        /// <summary>
        /// Gets true when the media type is <c>application/problem+json</c>.
        /// </summary>
        internal static bool IsProblemMediaType(MediaTypeHeaderValue? contentType)
            => contentType?.MediaType != null && string.Equals(contentType.MediaType, MediaType, StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Reads a Problem Details object from its UTF-8 JSON representation.
        /// Returns null when the body is empty, is not valid JSON or is not a JSON object.
        /// Members of the wrong type are ignored, as the RFC requires.
        /// </summary>
        internal static RestProblemDetails? Parse(byte[] utf8Json)
        {
            ReadOnlyMemory<byte> json = utf8Json;
            if (json.Length >= 3 && utf8Json[0] == 0xEF && utf8Json[1] == 0xBB && utf8Json[2] == 0xBF)
            {
                json = json.Slice(3);
            }
            if (json.IsEmpty)
            {
                return null;
            }

            try
            {
                using (JsonDocument document = JsonDocument.Parse(json))
                {
                    JsonElement root = document.RootElement;
                    if (root.ValueKind != JsonValueKind.Object)
                    {
                        return null;
                    }

                    RestProblemDetails problem = new RestProblemDetails();
                    foreach (JsonProperty member in root.EnumerateObject())
                    {
                        JsonElement value = member.Value;
                        switch (member.Name)
                        {
                            case "type":
                                problem.Type = value.ValueKind == JsonValueKind.String ? value.GetString() : problem.Type;
                                break;
                            case "title":
                                problem.Title = value.ValueKind == JsonValueKind.String ? value.GetString() : problem.Title;
                                break;
                            case "detail":
                                problem.Detail = value.ValueKind == JsonValueKind.String ? value.GetString() : problem.Detail;
                                break;
                            case "instance":
                                problem.Instance = value.ValueKind == JsonValueKind.String ? value.GetString() : problem.Instance;
                                break;
                            case "status":
                                if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out int status))
                                {
                                    problem.Status = status;
                                }
                                break;
                            case "errors":
                                if (!TryReadErrors(value, problem.Errors))
                                {
                                    problem.Extensions[member.Name] = value.Clone();
                                }
                                break;
                            default:
                                problem.Extensions[member.Name] = value.Clone();
                                break;
                        }
                    }
                    return problem;
                }
            }
            catch (Exception ex) when (ex is JsonException || ex is InvalidOperationException || ex is ArgumentException)
            {
                // not valid JSON (or not valid UTF-8): not a problem details object
                return null;
            }
        }

        /// <summary>
        /// Reads the validation errors: an object whose members are a string or an array of strings.
        /// </summary>
        private static bool TryReadErrors(JsonElement value, IDictionary<string, string[]> errors)
        {
            if (value.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            Dictionary<string, string[]> read = new Dictionary<string, string[]>(StringComparer.Ordinal);
            foreach (JsonProperty field in value.EnumerateObject())
            {
                if (field.Value.ValueKind == JsonValueKind.String)
                {
                    read[field.Name] = new[] { field.Value.GetString()! };
                    continue;
                }

                if (field.Value.ValueKind != JsonValueKind.Array)
                {
                    return false;
                }

                List<string> messages = new List<string>();
                foreach (JsonElement message in field.Value.EnumerateArray())
                {
                    if (message.ValueKind != JsonValueKind.String)
                    {
                        return false;
                    }
                    messages.Add(message.GetString()!);
                }
                read[field.Name] = messages.ToArray();
            }

            foreach (KeyValuePair<string, string[]> error in read)
            {
                errors[error.Key] = error.Value;
            }
            return true;
        }
    }
}
