namespace Mafesoft.RestClient.Tests
{
    using Mafesoft.RestClient.Serialization;
    using Mafesoft.RestClient.Serialization.Json;
    using Mafesoft.RestClient.Serialization.Xml;
    using System.Net;
    using System.Text;
    using System.Text.Json;

    /// <summary>
    /// IRestSerializer, IAsyncRestSerializer and WithSerializer (also run on net48, where the netstandard2.0 build is used)
    /// </summary>
    public class RestSerializerTests
    {
        private static RestBuilder Root(FakeHttpMessageHandler handler)
            => Rest.Build(handler.CreateClient()).Url("https://api.example.com/v1/");

        [Fact]
        public async Task WithSerializer_is_used_for_the_payload_and_the_response()
        {
            var handler = FakeHttpMessageHandler.Returns(HttpStatusCode.OK, "id=9;name=Peach", "text/x-key-value");
            var serializer = new KeyValueSerializer();

            RestResult<User> result = await Root(handler)
                .WithSerializer(serializer)
                .Payload(new User { Id = 1, Name = "Mario" })
                .PostAsync<User>();

            Assert.Null(result.Exception);
            Assert.Equal(9, result.Content.Id);
            Assert.Equal("Peach", result.Content.Name);
            Assert.Equal("id=1;name=Mario", handler.RequestBodies[0]);
            Assert.Equal("text/x-key-value", handler.Requests[0].Content.Headers.ContentType.MediaType);
            Assert.Equal(new[] { "serialize", "deserialize" }, serializer.Calls);
        }

        [Fact]
        public async Task WithSerializer_errors_are_reported_on_the_result()
        {
            var handler = FakeHttpMessageHandler.Returns(HttpStatusCode.OK, "garbage", "text/x-key-value");

            RestResult<User> result = await Root(handler).WithSerializer(new KeyValueSerializer()).GetAsync<User>();

            Assert.Equal(HttpStatusCode.OK, result.StatusCode);
            Assert.IsType<FormatException>(result.Exception);
        }

        [Fact]
        public async Task WithSerializer_async_serializer_reads_the_response_stream()
        {
            var handler = FakeHttpMessageHandler.Returns(HttpStatusCode.OK, "id=4;name=Toad", "text/x-key-value");
            var serializer = new AsyncKeyValueSerializer();

            RestResult<User> result = await Root(handler).WithSerializer(serializer).GetAsync<User>();

            Assert.Equal(4, result.Content.Id);
            Assert.Equal("Toad", result.Content.Name);
            Assert.Equal(1, serializer.StreamReads);
        }

        [Fact]
        public async Task WithSerializer_async_serializer_is_not_used_with_a_response_preview()
        {
            var handler = FakeHttpMessageHandler.Returns(HttpStatusCode.OK, "id=4;name=Toad", "text/x-key-value");
            var serializer = new AsyncKeyValueSerializer();
            string preview = null;

            RestResult<User> result = await Root(handler)
                .WithSerializer(serializer)
                .OnPreviewContentResponseAsString(e => preview = e.ContentAsString)
                .GetAsync<User>();

            Assert.Equal(4, result.Content.Id);
            Assert.Equal("id=4;name=Toad", preview);
            Assert.Equal(0, serializer.StreamReads);
        }

        [Fact]
        public async Task WithSerializer_is_used_by_ErrorAs()
        {
            var handler = FakeHttpMessageHandler.Returns(HttpStatusCode.BadRequest, "id=5;name=Bad", "text/x-key-value");

            RestResult<User> result = await Root(handler).WithSerializer(new KeyValueSerializer()).GetAsync<User>();

            Assert.Equal(5, result.ErrorAs<User>().Id);
            Assert.Equal("Bad", result.ErrorAs<User>().Name);
        }

        [Fact]
        public void WithSerializer_cannot_be_null()
        {
            Assert.Throws<ArgumentNullException>(() => Rest.Build().WithSerializer(null));
        }

        [Fact]
        public async Task WithSerializer_does_not_change_the_original_builder()
        {
            var handler = FakeHttpMessageHandler.Returns(HttpStatusCode.OK, "{\"id\":1,\"name\":\"a\"}");
            RestBuilder json = Root(handler);
            RestBuilder custom = json.WithSerializer(new KeyValueSerializer());

            RestResult<User> fromJson = await json.GetAsync<User>();
            RestResult<User> fromCustom = await custom.GetAsync<User>();

            Assert.Equal("a", fromJson.Content.Name);
            Assert.IsType<FormatException>(fromCustom.Exception);
        }

        [Fact]
        public async Task SystemTextJsonSerializer_owns_its_options()
        {
            var handler = FakeHttpMessageHandler.Returns(HttpStatusCode.OK, "{\"Id\":3,\"Name\":\"Pascal\"}");
            var options = new JsonSerializerOptions { PropertyNamingPolicy = null };
            var serializer = new SystemTextJsonSerializer(options);

            RestResult<User> result = await Root(handler)
                .WithSerializer(serializer)
                .Payload(new User { Id = 1, Name = "Mario" })
                .PostAsync<User>();

            Assert.Same(options, serializer.Options);
            Assert.Equal("Pascal", result.Content.Name);
            Assert.Equal("{\"Id\":1,\"Name\":\"Mario\"}", handler.RequestBodies[0]);
            Assert.Equal("application/json", handler.Requests[0].Content.Headers.ContentType.MediaType);
        }

        [Fact]
        public async Task SystemTextJsonSerializer_with_a_source_generated_context()
        {
            var handler = FakeHttpMessageHandler.Returns(HttpStatusCode.OK, "{\"id\":7,\"name\":\"ctx\"}");
            var serializer = new SystemTextJsonSerializer(TestJsonContext.Default);

            RestResult<User> result = await Root(handler)
                .WithSerializer(serializer)
                .Payload(new User { Id = 1, Name = "Mario" })
                .PostAsync<User>();

            Assert.Same(TestJsonContext.Default.Options, serializer.Options);
            Assert.Equal(7, result.Content.Id);
            Assert.Equal("{\"id\":1,\"name\":\"Mario\"}", handler.RequestBodies[0]);
        }

        [Fact]
        public void SystemTextJsonSerializer_defaults()
        {
            var serializer = new SystemTextJsonSerializer();

            Assert.Same(SystemTextJsonSerializer.DefaultOptions, serializer.Options);
            Assert.Equal("application/json", serializer.MediaType);
            Assert.Equal(string.Empty, serializer.Serialize(null, typeof(User)));
            Assert.Null(serializer.Deserialize(string.Empty, typeof(User)));
            Assert.Equal("{\"id\":1,\"name\":\"a\"}", serializer.Serialize(new User { Id = 1, Name = "a" }, typeof(User)));
            Assert.Throws<ArgumentNullException>(() => new SystemTextJsonSerializer((System.Text.Json.Serialization.JsonSerializerContext)null));
        }

        [Fact]
        public async Task SystemTextJsonSerializer_deserializes_a_stream()
        {
            var serializer = new SystemTextJsonSerializer();
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes("{\"id\":4,\"name\":\"s\"}"));

            var user = (User)await serializer.DeserializeAsync(stream, typeof(User));

            Assert.Equal(4, user.Id);
        }

        [Fact]
        public async Task SystemTextJsonSerializer_set_with_WithSerializer_streams_json_arrays()
        {
            var handler = FakeHttpMessageHandler.Returns(HttpStatusCode.OK, "[{\"id\":1,\"name\":\"a\"},{\"id\":2,\"name\":\"b\"}]");
            var names = new List<string>();

            await foreach (User user in Root(handler).WithSerializer(new SystemTextJsonSerializer(TestJsonContext.Default)).GetAsyncEnumerable<User>())
            {
                names.Add(user.Name);
            }

            Assert.Equal(new[] { "a", "b" }, names);
        }

        [Fact]
        public void Custom_serializer_does_not_support_json_streaming()
        {
            var handler = FakeHttpMessageHandler.Returns(HttpStatusCode.OK);

            NotSupportedException exception = Assert.Throws<NotSupportedException>(
                () => Root(handler).WithSerializer(new KeyValueSerializer()).GetAsyncEnumerable<User>());

            Assert.Contains(nameof(SystemTextJsonSerializer), exception.Message);
        }

        [Fact]
        public async Task XmlRestSerializer_uses_its_reader_and_writer_settings()
        {
            string body = "<?xml version=\"1.0\"?><!DOCTYPE User [<!ELEMENT User ANY>]><User><Id>5</Id><Name>Anna</Name></User>";
            var handler = FakeHttpMessageHandler.Returns(HttpStatusCode.OK, body, "application/xml");
            var serializer = new XmlRestSerializer(
                new System.Xml.XmlReaderSettings { DtdProcessing = System.Xml.DtdProcessing.Ignore },
                new System.Xml.XmlWriterSettings { OmitXmlDeclaration = true });

            RestResult<User> result = await Root(handler)
                .WithSerializer(serializer)
                .Payload(new User { Id = 1, Name = "Mario" })
                .PostAsync<User>();

            Assert.Null(result.Exception);
            Assert.Equal(5, result.Content.Id);
            Assert.StartsWith("<User", handler.RequestBodies[0]);
            Assert.Equal("application/xml", handler.Requests[0].Content.Headers.ContentType.MediaType);
        }

        [Fact]
        public async Task XmlRestSerializer_default_settings_reject_a_dtd()
        {
            string body = "<?xml version=\"1.0\"?><!DOCTYPE User [<!ELEMENT User ANY>]><User><Id>5</Id></User>";
            var handler = FakeHttpMessageHandler.Returns(HttpStatusCode.OK, body, "application/xml");

            RestResult<User> result = await Root(handler).WithSerializer(new XmlRestSerializer()).GetAsync<User>();

            Assert.NotNull(result.Exception);
            Assert.Equal(string.Empty, new XmlRestSerializer().Serialize(null, typeof(User)));
            Assert.Null(new XmlRestSerializer().Deserialize(string.Empty, typeof(User)));
        }

        /// <summary>
        /// Serializes a User as "id=1;name=Mario"
        /// </summary>
        private class KeyValueSerializer : IRestSerializer
        {
            public List<string> Calls { get; } = new List<string>();

            public string MediaType => "text/x-key-value";

            public object Deserialize(string value, Type type)
            {
                Calls.Add("deserialize");
                Dictionary<string, string> values = value.Split(';')
                    .Select(pair => pair.Split('='))
                    .Where(pair => pair.Length == 2)
                    .ToDictionary(pair => pair[0], pair => pair[1]);
                if (!values.ContainsKey("id"))
                {
                    throw new FormatException("missing id");
                }
                return new User { Id = int.Parse(values["id"]), Name = values.TryGetValue("name", out string name) ? name : null };
            }

            public string Serialize(object value, Type type)
            {
                Calls.Add("serialize");
                var user = (User)value;
                return $"id={user.Id};name={user.Name}";
            }
        }

        private sealed class AsyncKeyValueSerializer : KeyValueSerializer, IAsyncRestSerializer
        {
            public int StreamReads { get; private set; }

            public async Task<object> DeserializeAsync(Stream stream, Type type, CancellationToken cancellationToken = default)
            {
                StreamReads++;
                using var reader = new StreamReader(stream, Encoding.UTF8);
                return Deserialize(await reader.ReadToEndAsync(), type);
            }
        }
    }
}
