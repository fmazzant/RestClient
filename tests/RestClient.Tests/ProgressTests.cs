namespace Mafesoft.RestClient.Tests
{
    using System.Net;
    using System.Net.Http;
    using System.Text;

    /// <summary>
    /// Upload and download progress callbacks and BufferSize (also run on net48, where the netstandard2.0 build is used)
    /// </summary>
    public class ProgressTests
    {
        private static RestBuilder Root(FakeHttpMessageHandler handler)
            => Rest.Build(handler.CreateClient()).Url("https://api.example.com/v1/");

        private static FakeHttpMessageHandler ReturnsBytes(byte[] body)
            => new FakeHttpMessageHandler((r, i) => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(body) });

        [Fact]
        public async Task Download_progress_is_raised_once_per_buffer()
        {
            byte[] body = new byte[25_000];
            var download = new List<ProgressEventArgs>();

            RestResult<byte[]> result = await Root(ReturnsBytes(body)).BufferSize(10_000).OnDownloadProgress(download.Add).GetAsync<byte[]>();

            Assert.Equal(body.Length, result.Content.Length);
            Assert.Equal(new long[] { 10_000, 20_000, 25_000 }, download.Select(p => p.CurrentBytes));
            Assert.All(download, p => Assert.Equal(25_000, p.TotalBytes));
            Assert.Equal(new[] { 40, 80, 100 }, download.Select(p => p.ProgressPercentage));
            Assert.Equal(1f, download.Last().ProgressFloat);
        }

        [Fact]
        public async Task Upload_progress_is_raised_once_per_buffer()
        {
            var handler = FakeHttpMessageHandler.Returns(HttpStatusCode.OK);
            var upload = new List<ProgressEventArgs>();

            await Root(handler)
                .BufferSize(1_000)
                .Payload(new { text = new string('x', 4_500) })
                .OnUploadProgress(upload.Add)
                .PostAsync();

            int length = Encoding.UTF8.GetByteCount(handler.RequestBodies[0]);
            Assert.Equal((length + 999) / 1_000, upload.Count);
            Assert.All(upload.Take(upload.Count - 1), p => Assert.Equal(0, p.CurrentBytes % 1_000));
            Assert.Equal(length, upload.Last().CurrentBytes);
            Assert.All(upload, p => Assert.Equal(length, p.TotalBytes));
            Assert.Equal(100, upload.Last().ProgressPercentage);
        }

        [Fact]
        public async Task Upload_progress_of_a_form_url_encoded_content()
        {
            var handler = FakeHttpMessageHandler.Returns(HttpStatusCode.OK);
            var upload = new List<ProgressEventArgs>();

            await Root(handler)
                .FormUrlEncoded(kv => kv.Add("value", new string('a', 300)))
                .OnUploadProgress(upload.Add)
                .BufferSize(100)
                .PostAsync();

            Assert.Equal(4, upload.Count); // "value=" + 300 characters = 306 bytes
            Assert.Equal(306, upload.Last().CurrentBytes);
            Assert.Equal(306, upload.Last().TotalBytes);
        }

        [Fact]
        public async Task Download_progress_without_content_length()
        {
            byte[] body = Encoding.UTF8.GetBytes(new string('z', 2_500));
            var handler = new FakeHttpMessageHandler((r, i) => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StreamContent(new NonSeekableStream(body))
            });
            var download = new List<ProgressEventArgs>();

            RestResult<string> result = await Root(handler).BufferSize(1_000).OnDownloadProgress(download.Add).GetAsync();

            Assert.Equal(2_500, result.Content.Length);
            Assert.Equal(2_500, download.Last().CurrentBytes);
            Assert.All(download, p => Assert.Equal(0, p.TotalBytes));
            Assert.All(download, p => Assert.Equal(0, p.ProgressPercentage));
        }

        [Fact]
        public async Task Download_progress_while_deserializing()
        {
            string json = "{\"id\":1,\"name\":\"" + new string('n', 5_000) + "\"}";
            var handler = FakeHttpMessageHandler.Returns(HttpStatusCode.OK, json);
            var download = new List<ProgressEventArgs>();

            RestResult<User> result = await Root(handler).BufferSize(1_024).OnDownloadProgress(download.Add).GetAsync<User>();

            Assert.Equal(1, result.Content.Id);
            Assert.NotEmpty(download);
            Assert.Equal(Encoding.UTF8.GetByteCount(json), download.Last().CurrentBytes);
            Assert.Equal(100, download.Last().ProgressPercentage);
        }

        [Fact]
        public async Task No_progress_without_callbacks()
        {
            var handler = FakeHttpMessageHandler.Returns(HttpStatusCode.OK, "ok", "text/plain");

            RestResult<string> result = await Root(handler).BufferSize(1).Payload(new { a = 1 }).PostAsync();

            Assert.Equal("ok", result.Content);
            Assert.Equal("{\"a\":1}", handler.RequestBodies[0]);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void BufferSize_must_be_positive(int bufferSize)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => Rest.Build().BufferSize(bufferSize));
        }

        [Fact]
        public void BufferSize_has_a_default_and_belongs_to_the_builder()
        {
            RestBuilder root = Rest.Build();
            RestBuilder small = root.BufferSize(512);

            Assert.Equal(RestProperties.DefaultBufferSize, root.Properties.BufferSize);
            Assert.Equal(512, small.Properties.BufferSize);
        }

        /// <summary>
        /// A stream without length: the response has no Content-Length
        /// </summary>
        private sealed class NonSeekableStream : Stream
        {
            private readonly MemoryStream inner;

            public NonSeekableStream(byte[] content) => inner = new MemoryStream(content);

            public override int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, count);
            public override bool CanRead => true;
            public override bool CanSeek => false;
            public override bool CanWrite => false;
            public override long Length => throw new NotSupportedException();
            public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
            public override void Flush() { }
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        }
    }
}
