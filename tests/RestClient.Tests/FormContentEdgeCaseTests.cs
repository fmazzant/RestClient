namespace Mafesoft.RestClient.Tests
{
    using System.Net;
    using System.Net.Http;
    using System.Text;

    /// <summary>
    /// Multipart and form-url-encoded edge cases: empty, unicode, large files
    /// (also run on net48, where the netstandard2.0 build is used)
    /// </summary>
    public class FormContentEdgeCaseTests
    {
        private static RestBuilder Root(FakeHttpMessageHandler handler)
            => Rest.Build(handler.CreateClient()).Url("https://api.example.com/v1/");

        [Fact]
        public async Task Empty_form_url_encoded_content()
        {
            var handler = FakeHttpMessageHandler.Returns(HttpStatusCode.OK);
            string preview = null;

            await Root(handler)
                .OnPreviewContentRequestAsString(e => preview = e.ContentAsString)
                .FormUrlEncoded(new Dictionary<string, string>())
                .PostAsync();

            Assert.Equal("application/x-www-form-urlencoded", handler.Requests[0].Content.Headers.ContentType.MediaType);
            Assert.Equal(string.Empty, handler.RequestBodies[0]);
            Assert.Equal(string.Empty, preview);
        }

        [Fact]
        public async Task Form_url_encoded_without_values_sends_no_content()
        {
            var handler = FakeHttpMessageHandler.Returns(HttpStatusCode.OK);

            await Root(handler).EnableFormUrlEncoded().PostAsync();

            Assert.Null(handler.Requests[0].Content);
        }

        [Fact]
        public async Task Form_url_encoded_unicode_and_null_values()
        {
            var handler = FakeHttpMessageHandler.Returns(HttpStatusCode.OK);
            string preview = null;

            await Root(handler)
                .OnPreviewContentRequestAsString(e => preview = e.ContentAsString)
                .FormUrlEncoded(kv => { kv.Add("città", "perché \U0001F600"); kv.Add("empty", null); })
                .PostAsync();

            // UTF-8, percent-encoded; the space is "+" in the body
            Assert.Equal("citt%C3%A0=perch%C3%A9+%F0%9F%98%80&empty=", handler.RequestBodies[0]);
            Assert.Equal("citt%C3%A0=perch%C3%A9%20%F0%9F%98%80&empty=", preview);
        }

        [Fact]
        public async Task Empty_multipart_content()
        {
            var handler = FakeHttpMessageHandler.Returns(HttpStatusCode.OK);
            string preview = null;

            RestResult<string> result = await Root(handler)
                .OnPreviewContentRequestAsString(e => preview = e.ContentAsString)
                .Multipart(m => { })
                .PostAsync();

            Assert.Equal(HttpStatusCode.OK, result.StatusCode);
            var contentType = handler.Requests[0].Content.Headers.ContentType;
            Assert.Equal("multipart/form-data", contentType.MediaType);
            string boundary = contentType.Parameters.Single(p => p.Name == "boundary").Value.Trim('"');
            Assert.Contains($"--{boundary}--", handler.RequestBodies[0]);
            Assert.DoesNotContain("Content-Disposition", handler.RequestBodies[0]);
            Assert.Equal(string.Empty, preview);
        }

        [Fact]
        public async Task Multipart_unicode_values_and_file_names()
        {
            var handler = FakeHttpMessageHandler.Returns(HttpStatusCode.OK);
            string preview = null;

            await Root(handler)
                .OnPreviewContentRequestAsString(e => preview = e.ContentAsString)
                .Multipart(m => m
                    .Add("titolo", "perché \U0001F600")
                    .AddFile("documento", Encoding.UTF8.GetBytes("così"), "città.txt", "text/plain"))
                .PostAsync();

            string body = handler.RequestBodies[0];
            Assert.Contains("perché \U0001F600", body);
            Assert.Contains("così", body);
            Assert.Contains("filename*=utf-8''citt%C3%A0.txt", body);
            Assert.Equal("titolo&documento=@città.txt", preview);
        }

        [Fact]
        public async Task Multipart_empty_file()
        {
            var handler = FakeHttpMessageHandler.Returns(HttpStatusCode.OK);

            await Root(handler)
                .Multipart(m => m.AddFile("empty", new byte[0], "empty.bin"))
                .PostAsync();

            string body = handler.RequestBodies[0];
            Assert.Contains("filename=empty.bin", body);
            Assert.Contains("Content-Type: application/octet-stream", body);
        }

        [Fact]
        public async Task Multipart_large_file_from_disk_with_upload_progress()
        {
            const int size = 4 * 1024 * 1024;
            string path = Path.Combine(Path.GetTempPath(), $"restclient-{Guid.NewGuid():N}.bin");
            File.WriteAllBytes(path, Enumerable.Repeat((byte)'a', size).ToArray());
            try
            {
                var handler = FakeHttpMessageHandler.Returns(HttpStatusCode.OK);
                var upload = new List<ProgressEventArgs>();

                RestResult<string> result = await Root(handler)
                    .BufferSize(64 * 1024)
                    .OnUploadProgress(upload.Add)
                    .Multipart(m => m.Add("name", "large").AddFile("file", path, "application/octet-stream"))
                    .PostAsync();

                Assert.Equal(HttpStatusCode.OK, result.StatusCode);
                string body = handler.RequestBodies[0];
                Assert.Contains($"filename={Path.GetFileName(path)}", body);
                Assert.Contains(new string('a', size), body);
                Assert.True(upload.Count > size / (64 * 1024));
                Assert.Equal(Encoding.UTF8.GetByteCount(body), upload.Last().CurrentBytes);
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Fact]
        public void Multipart_rejects_null_arguments()
        {
            Assert.Throws<ArgumentNullException>(() => Rest.Build().Multipart(null));
            Assert.Throws<ArgumentNullException>(() => Rest.Build().Multipart(m => m.Add("name", null)));
            Assert.Throws<ArgumentNullException>(() => Rest.Build().Multipart(m => m.Add(null, "value")));
            Assert.Throws<ArgumentNullException>(() => Rest.Build().Multipart(m => m.AddFile("file", new byte[0], null)));
            Assert.Throws<ArgumentNullException>(() => Rest.Build().Multipart(m => m.AddFile("file", (byte[])null, "a.bin")));
        }
    }
}
