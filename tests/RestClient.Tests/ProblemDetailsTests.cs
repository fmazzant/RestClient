namespace Mafesoft.RestClient.Tests
{
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Text;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    public class ApiError
    {
        public string Code { get; set; }
        public string Message { get; set; }
    }

    [JsonSerializable(typeof(ApiError))]
    [JsonSerializable(typeof(User))]
    [JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
    public partial class ErrorJsonContext : JsonSerializerContext
    {
    }

    public class ProblemDetailsTests
    {
        private const string ProblemJson =
            "{\"type\":\"https://example.com/probs/out-of-credit\",\"title\":\"You do not have enough credit.\"," +
            "\"status\":403,\"detail\":\"Your current balance is 30, but that costs 50.\",\"instance\":\"/account/12345/msgs/abc\"," +
            "\"balance\":30,\"accounts\":[\"/account/12345\",\"/account/67890\"],\"traceId\":\"00-abc-01\"}";

        private const string ValidationJson =
            "{\"type\":\"https://tools.ietf.org/html/rfc9110#section-15.5.1\",\"title\":\"One or more validation errors occurred.\"," +
            "\"status\":400,\"errors\":{\"Name\":[\"The Name field is required.\"],\"Age\":[\"Too young.\",\"Not a number.\"]}}";

        private static RestBuilder Root(FakeHttpMessageHandler handler)
            => Rest.Build(handler.CreateClient()).Url("https://api.example.com");

        [Fact]
        public async Task Problem_is_read_from_an_application_problem_json_error()
        {
            var handler = FakeHttpMessageHandler.Returns(HttpStatusCode.Forbidden, ProblemJson, "application/problem+json");

            RestResult<User> result = await Root(handler).GetAsync<User>();

            RestProblemDetails problem = result.Problem;
            Assert.NotNull(problem);
            Assert.Equal("https://example.com/probs/out-of-credit", problem.Type);
            Assert.Equal("You do not have enough credit.", problem.Title);
            Assert.Equal(403, problem.Status);
            Assert.Equal("Your current balance is 30, but that costs 50.", problem.Detail);
            Assert.Equal("/account/12345/msgs/abc", problem.Instance);
            Assert.Empty(problem.Errors);
            Assert.Equal(30, problem.Extensions["balance"].GetInt32());
            Assert.Equal(2, problem.Extensions["accounts"].GetArrayLength());
            Assert.Equal("00-abc-01", problem.Extensions["traceId"].GetString());
            Assert.Same(problem, result.Problem);
        }

        [Fact]
        public async Task Validation_errors_are_read()
        {
            var handler = FakeHttpMessageHandler.Returns(HttpStatusCode.BadRequest, ValidationJson, "application/problem+json");

            RestResult<string> result = await Root(handler).PostAsync();

            RestProblemDetails problem = result.Problem;
            Assert.Equal(400, problem.Status);
            Assert.Equal(new[] { "The Name field is required." }, problem.Errors["Name"]);
            Assert.Equal(new[] { "Too young.", "Not a number." }, problem.Errors["Age"]);
            Assert.False(problem.Extensions.ContainsKey("errors"));
            // the string content is unchanged
            Assert.Equal(ValidationJson, result.Content);
        }

        [Fact]
        public async Task Errors_that_are_not_validation_errors_are_an_extension()
        {
            var handler = FakeHttpMessageHandler.Returns(HttpStatusCode.BadRequest,
                "{\"title\":\"bad\",\"errors\":[{\"code\":\"E1\"}]}", "application/problem+json");

            RestResult<string> result = await Root(handler).GetAsync();

            Assert.Empty(result.Problem.Errors);
            Assert.Equal(JsonValueKind.Array, result.Problem.Extensions["errors"].ValueKind);
        }

        [Fact]
        public async Task Members_of_the_wrong_type_are_ignored()
        {
            var handler = new FakeHttpMessageHandler((r, i) =>
            {
                var content = new ByteArrayContent(Encoding.UTF8.GetBytes("{\"type\":1,\"title\":\"bad\",\"status\":\"400\",\"detail\":null}"));
                content.Headers.ContentType = MediaTypeHeaderValue.Parse("Application/Problem+JSON; charset=utf-8");
                return new HttpResponseMessage(HttpStatusCode.BadRequest) { Content = content };
            });

            RestResult<string> result = await Root(handler).GetAsync();

            Assert.Null(result.Problem.Type);
            Assert.Equal("bad", result.Problem.Title);
            Assert.Null(result.Problem.Status);
            Assert.Null(result.Problem.Detail);
        }

        [Fact]
        public async Task Problem_is_null_for_another_content_type()
        {
            var handler = FakeHttpMessageHandler.Returns(HttpStatusCode.BadRequest, ProblemJson, "application/json");

            RestResult<User> result = await Root(handler).GetAsync<User>();

            Assert.Null(result.Problem);
        }

        [Theory]
        [InlineData("{\"title\":")]
        [InlineData("[1,2]")]
        [InlineData("")]
        [InlineData("not json")]
        public async Task Problem_is_null_when_the_body_is_not_a_json_object(string body)
        {
            var handler = FakeHttpMessageHandler.Returns(HttpStatusCode.InternalServerError, body, "application/problem+json");

            RestResult<string> result = await Root(handler).GetAsync();

            Assert.Null(result.Problem);
            Assert.Equal(body, result.ErrorAs<string>());
        }

        [Fact]
        public async Task Problem_and_error_are_null_for_a_success_response()
        {
            var handler = FakeHttpMessageHandler.Returns(HttpStatusCode.OK, ProblemJson, "application/problem+json");

            RestResult<string> result = await Root(handler).GetAsync();

            Assert.Null(result.Problem);
            Assert.Null(result.ErrorAs<ApiError>());
            Assert.Null(result.ErrorAs<string>());
            Assert.Equal(ProblemJson, result.Content);
        }

        [Fact]
        public async Task Problem_and_error_are_null_without_a_response()
        {
            var handler = new FakeHttpMessageHandler((r, i) => Task.FromException<HttpResponseMessage>(new HttpRequestException("network down")));

            RestResult<User> result = await Root(handler).GetAsync<User>();

            Assert.Null(result.Response);
            Assert.Null(result.Problem);
            Assert.Null(result.ErrorAs<ApiError>());
        }

        [Fact]
        public async Task Error_body_is_deserialized_as_a_custom_type_with_the_configured_context()
        {
            var handler = FakeHttpMessageHandler.Returns(HttpStatusCode.Conflict, "{\"code\":\"E42\",\"message\":\"Already exists\"}");

            RestResult<User> result = await Root(handler).Json(ErrorJsonContext.Default).Payload(new User { Id = 1 }).PostAsync<User>();

            Assert.False(result.IsSuccessStatusCode);
            Assert.Null(result.Problem);
            ApiError error = result.ErrorAs<ApiError>();
            Assert.Equal("E42", error.Code);
            Assert.Equal("Already exists", error.Message);
        }

        [Fact]
        public async Task Error_body_as_string_bytes_and_stream()
        {
            const string body = "{\"code\":\"E1\"}";
            var handler = FakeHttpMessageHandler.Returns(HttpStatusCode.BadRequest, body);

            RestResult<User> result = await Root(handler).GetAsync<User>();

            Assert.Equal(body, result.ErrorAs<string>());
            Assert.Equal(body, Encoding.UTF8.GetString(result.ErrorAs<byte[]>()));
            using var reader = new StreamReader(result.ErrorAs<Stream>());
            Assert.Equal(body, reader.ReadToEnd());
        }

        [Fact]
        public async Task Error_that_cannot_be_deserialized_is_the_default_value()
        {
            var handler = FakeHttpMessageHandler.Returns(HttpStatusCode.BadRequest, "<html>oops</html>", "text/html");

            RestResult<string> result = await Root(handler).GetAsync();

            Assert.Null(result.ErrorAs<ApiError>());
            Assert.Equal("<html>oops</html>", result.Content);
        }

        [Fact]
        public async Task Stream_error_body_is_read_once_and_content_keeps_the_same_bytes()
        {
            var source = new CountingStream(Encoding.UTF8.GetBytes(ProblemJson));
            var handler = new FakeHttpMessageHandler((r, i) =>
            {
                var content = new StreamContent(source);
                content.Headers.ContentType = new MediaTypeHeaderValue("application/problem+json");
                return new HttpResponseMessage(HttpStatusCode.Forbidden) { Content = content };
            });

            using RestResult<Stream> result = await Root(handler).GetAsync<Stream>();

            // not read until Problem or ErrorAs are used
            Assert.Equal(0, source.BytesRead);

            Assert.Equal(403, result.Problem.Status);
            Assert.Equal("You do not have enough credit.", result.Problem.Title);
            Assert.NotNull(result.ErrorAs<ApiError>());
            Assert.Equal(ProblemJson, result.ErrorAs<string>());
            Assert.Equal(ProblemJson.Length, source.BytesRead);

            using var reader = new StreamReader(result.Content);
            Assert.Equal(ProblemJson, reader.ReadToEnd());
            Assert.Equal(ProblemJson.Length, source.BytesRead);
        }

        [Fact]
        public async Task Stream_error_body_is_not_read_for_another_content_type_problem()
        {
            var source = new CountingStream(Encoding.UTF8.GetBytes("plain error"));
            var handler = new FakeHttpMessageHandler((r, i) => new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StreamContent(source) });

            using RestResult<Stream> result = await Root(handler).GetAsync<Stream>();
            Stream content = result.Content;

            Assert.Null(result.Problem);
            Assert.Equal(0, source.BytesRead);
            Assert.Same(content, result.Content);
        }

        [Fact]
        public async Task Stream_success_content_is_not_touched()
        {
            var source = new CountingStream(Encoding.UTF8.GetBytes(ProblemJson));
            var handler = new FakeHttpMessageHandler((r, i) =>
            {
                var content = new StreamContent(source);
                content.Headers.ContentType = new MediaTypeHeaderValue("application/problem+json");
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
            });

            using RestResult<Stream> result = await Root(handler).GetAsync<Stream>();
            Stream content = result.Content;

            Assert.Null(result.Problem);
            Assert.Null(result.ErrorAs<string>());
            Assert.Equal(0, source.BytesRead);
            Assert.Same(content, result.Content);
        }

        [Fact]
        public async Task Problem_of_a_typed_result_keeps_the_existing_content_and_exception()
        {
            var handler = FakeHttpMessageHandler.Returns(HttpStatusCode.BadRequest, "{\"title\":\"bad\",\"status\":400}", "application/problem+json");
            string preview = null;

            RestResult<User> result = await Root(handler).OnPreviewContentResponseAsString(e => preview = e.ContentAsString).GetAsync<User>();

            Assert.Equal("{\"title\":\"bad\",\"status\":400}", preview);
            Assert.Null(result.Exception);
            Assert.NotNull(result.Content);
            Assert.Equal("bad", result.Problem.Title);
        }

        /// <summary>
        /// A non seekable stream that counts the bytes read
        /// </summary>
        private sealed class CountingStream : Stream
        {
            private readonly MemoryStream inner;

            public CountingStream(byte[] data) => inner = new MemoryStream(data);

            public long BytesRead { get; private set; }

            public override bool CanRead => true;
            public override bool CanSeek => false;
            public override bool CanWrite => false;
            public override long Length => throw new NotSupportedException();
            public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

            public override int Read(byte[] buffer, int offset, int count)
            {
                int read = inner.Read(buffer, offset, count);
                BytesRead += read;
                return read;
            }

            public override void Flush() { }
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        }
    }
}
