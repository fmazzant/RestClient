// The obsolete serializer API (ISerializerContent, CustomSerializer) must keep working until it is removed
#pragma warning disable CS0618

namespace Mafesoft.RestClient.Tests
{
    using Mafesoft.RestClient.Serialization;
    using System.Net;
    using System.Text;

    /// <summary>
    /// XML serialization and custom serializers (also run on net48, where the netstandard2.0 build is used)
    /// </summary>
    public class SerializationTests
    {
        private static RestBuilder Root(FakeHttpMessageHandler handler)
            => Rest.Build(handler.CreateClient()).Url("https://api.example.com/v1/");

        [Fact]
        public async Task Xml_round_trip_with_default_settings()
        {
            var handler = FakeHttpMessageHandler.Returns(HttpStatusCode.OK,
                "<?xml version=\"1.0\" encoding=\"utf-8\"?><User><Id>7</Id><Name>Anna</Name></User>", "application/xml");

            RestResult<User> result = await Root(handler).Xml().Payload(new User { Id = 1, Name = "Mario" }).PutAsync<User>();

            Assert.Null(result.Exception);
            Assert.Equal(7, result.Content.Id);
            Assert.Equal("Anna", result.Content.Name);
            Assert.Equal("application/xml", handler.Requests[0].Content.Headers.ContentType.MediaType);
            Assert.StartsWith("<?xml", handler.RequestBodies[0]);
            Assert.Contains("<Id>1</Id>", handler.RequestBodies[0]);
            Assert.Contains("<Name>Mario</Name>", handler.RequestBodies[0]);
        }

        [Fact]
        public void Xml_synchronous_get()
        {
            var handler = FakeHttpMessageHandler.Returns(HttpStatusCode.OK, "<User><Id>3</Id><Name>Luigi</Name></User>", "application/xml");

            RestResult<User> result = Root(handler).Xml().Command("users").Command(3).Get<User>();

            Assert.Equal(3, result.Content.Id);
            Assert.Equal("Luigi", result.Content.Name);
        }

        [Fact]
        public async Task Xml_invalid_body_keeps_the_http_response()
        {
            var handler = FakeHttpMessageHandler.Returns(HttpStatusCode.OK, "<User><Id>not-a-number</Id></User>", "application/xml");
            Exception raised = null;

            RestResult<User> result = await Root(handler).Xml().OnException(e => raised = e).GetAsync<User>();

            Assert.Equal(HttpStatusCode.OK, result.StatusCode);
            Assert.NotNull(result.Exception);
            Assert.Same(result.Exception, raised);
            Assert.Null(result.Content);
        }

        [Fact]
        public async Task Xml_empty_body_is_a_null_content()
        {
            var handler = FakeHttpMessageHandler.Returns(HttpStatusCode.OK, string.Empty, "application/xml");

            RestResult<User> result = await Root(handler).Xml().GetAsync<User>();

            Assert.Null(result.Exception);
            Assert.Null(result.Content);
        }

        [Fact]
        public async Task Xml_reader_settings_are_used_to_deserialize()
        {
            // DTD processing is prohibited by default: a document with a DTD is rejected unless the settings allow it
            string body = "<?xml version=\"1.0\"?><!DOCTYPE User [<!ELEMENT User ANY>]><User><Id>5</Id><Name>Anna</Name></User>";
            var handler = FakeHttpMessageHandler.Returns(HttpStatusCode.OK, body, "application/xml");

            RestResult<User> rejected = await Root(handler).Xml().GetAsync<User>();
            RestResult<User> accepted = await Root(handler)
                .Xml(new System.Xml.XmlReaderSettings { DtdProcessing = System.Xml.DtdProcessing.Ignore })
                .GetAsync<User>();

            Assert.NotNull(rejected.Exception);
            Assert.Null(accepted.Exception);
            Assert.Equal(5, accepted.Content.Id);
        }

        [Fact]
        public async Task Custom_serializer_is_used_for_the_payload_and_the_response()
        {
            var handler = FakeHttpMessageHandler.Returns(HttpStatusCode.OK, "id=9;name=Peach", "text/x-key-value");
            var serializer = new KeyValueSerializer();
            var options = new object();

            RestResult<User> result = await Root(handler)
                .CustomSerializer(serializer, options)
                .Payload(new User { Id = 1, Name = "Mario" })
                .PostAsync<User>();

            Assert.Null(result.Exception);
            Assert.Equal(9, result.Content.Id);
            Assert.Equal("Peach", result.Content.Name);
            Assert.Equal("id=1;name=Mario", handler.RequestBodies[0]);
            Assert.Equal("text/x-key-value", handler.Requests[0].Content.Headers.ContentType.MediaType);
            Assert.Equal(new[] { options, options }, serializer.ReceivedOptions);
        }

        [Fact]
        public async Task Custom_serializer_errors_are_reported_on_the_result()
        {
            var handler = FakeHttpMessageHandler.Returns(HttpStatusCode.OK, "garbage", "text/x-key-value");

            RestResult<User> result = await Root(handler).CustomSerializer(new KeyValueSerializer()).GetAsync<User>();

            Assert.Equal(HttpStatusCode.OK, result.StatusCode);
            Assert.IsType<FormatException>(result.Exception);
        }

        [Fact]
        public async Task Custom_async_serializer_reads_the_response_stream()
        {
            var handler = FakeHttpMessageHandler.Returns(HttpStatusCode.OK, "id=4;name=Toad", "text/x-key-value");
            var serializer = new AsyncKeyValueSerializer();

            RestResult<User> result = await Root(handler).CustomSerializer(serializer).GetAsync<User>();

            Assert.Equal(4, result.Content.Id);
            Assert.Equal("Toad", result.Content.Name);
            Assert.Equal(1, serializer.StreamReads);
        }

        [Fact]
        public void Custom_serializer_cannot_be_null()
        {
            Assert.Throws<ArgumentNullException>(() => Rest.Build().CustomSerializer(null));
        }

        [Fact]
        public async Task Custom_serializer_with_the_obsolete_JSON_keeps_its_options_and_streaming()
        {
            var handler = FakeHttpMessageHandler.Returns(HttpStatusCode.OK, "[{\"Id\":1,\"Name\":\"a\"}]");
            var options = new System.Text.Json.JsonSerializerOptions();

            RestBuilder api = Root(handler).CustomSerializer(new Mafesoft.RestClient.Serialization.Json.JSON(), options);
            var names = new List<string>();
            await foreach (User user in api.GetAsyncEnumerable<User>())
            {
                names.Add(user.Name);
            }
            RestResult<List<User>> list = await api.GetAsync<List<User>>();

            Assert.Equal(new[] { "a" }, names);
            Assert.Equal("a", list.Content[0].Name);
        }

        [Fact]
        public async Task Custom_serializer_with_the_obsolete_JSON_and_a_context()
        {
            var handler = FakeHttpMessageHandler.Returns(HttpStatusCode.OK, "{\"id\":2,\"name\":\"b\"}");

            RestResult<User> result = await Root(handler)
                .CustomSerializer(new Mafesoft.RestClient.Serialization.Json.JSON(), TestJsonContext.Default)
                .GetAsync<User>();

            Assert.Equal(2, result.Content.Id);
        }

        [Fact]
        public void Obsolete_JSON_default_options_are_the_SystemTextJsonSerializer_ones()
        {
            Assert.Same(Mafesoft.RestClient.Serialization.Json.SystemTextJsonSerializer.DefaultOptions,
                Mafesoft.RestClient.Serialization.Json.JSON.DefaultOptions);
        }

        [Fact]
        public async Task Custom_serializer_is_used_by_ErrorAs()
        {
            var handler = FakeHttpMessageHandler.Returns(HttpStatusCode.BadRequest, "id=5;name=Bad", "text/x-key-value");
            var serializer = new KeyValueSerializer();
            var options = new object();

            RestResult<string> result = await Root(handler).CustomSerializer(serializer, options).GetAsync();

            Assert.Equal(5, result.ErrorAs<User>().Id);
            Assert.Same(options, serializer.ReceivedOptions.Single());
        }

        /// <summary>
        /// Serializes a User as "id=1;name=Mario"
        /// </summary>
        private class KeyValueSerializer : ISerializerContent
        {
            public List<object> ReceivedOptions { get; } = new List<object>();

            public string MediaTypeAsString => "text/x-key-value";

            public object DeserializeObject(string value, Type typeOf, object options = null)
            {
                ReceivedOptions.Add(options);
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

            public string SerializeObject(object value, Type typeOf, object options = null)
            {
                ReceivedOptions.Add(options);
                var user = (User)value;
                return $"id={user.Id};name={user.Name}";
            }
        }

        private sealed class AsyncKeyValueSerializer : KeyValueSerializer, IAsyncSerializerContent
        {
            public int StreamReads { get; private set; }

            public async Task<object> DeserializeObjectAsync(Stream stream, Type typeOf, object options = null, CancellationToken cancellationToken = default)
            {
                StreamReads++;
                using var reader = new StreamReader(stream, Encoding.UTF8);
                return DeserializeObject(await reader.ReadToEndAsync(), typeOf, options);
            }
        }
    }
}
