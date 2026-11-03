#if NET
namespace RestClient.Tests
{
    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.AspNetCore.Http;
    using Microsoft.Extensions.Logging;
    using System.IO.Compression;
    using System.Net;
    using System.Net.Security;
    using System.Security.Cryptography;
    using System.Security.Cryptography.X509Certificates;
    using System.Text;

    /// <summary>
    /// Tests the HttpClient managed by RestClient (pooled handlers, compression, credentials, TLS) against a real Kestrel server.
    /// </summary>
    public sealed class KestrelIntegrationTests : IAsyncLifetime
    {
        private WebApplication app;
        private string httpUrl;
        private string httpsUrl;

        public async Task InitializeAsync()
        {
            X509Certificate2 certificate = CreateSelfSignedCertificate();

            WebApplicationBuilder builder = WebApplication.CreateBuilder();
            builder.Logging.ClearProviders();
            builder.WebHost.ConfigureKestrel(kestrel =>
            {
                kestrel.Listen(IPAddress.Loopback, 0);
                kestrel.Listen(IPAddress.Loopback, 0, listen => listen.UseHttps(certificate));
            });

            app = builder.Build();

            app.MapGet("/connection", (HttpContext context) => context.Connection.Id);

            app.MapGet("/gzip", async (HttpContext context) =>
            {
                if (!context.Request.Headers.AcceptEncoding.ToString().Contains("gzip"))
                {
                    await context.Response.WriteAsync("not-compressed");
                    return;
                }
                context.Response.Headers.ContentEncoding = "gzip";
                context.Response.ContentType = "application/json";
                await using var gzip = new GZipStream(context.Response.Body, CompressionLevel.Fastest, leaveOpen: true);
                await gzip.WriteAsync(Encoding.UTF8.GetBytes("{\"id\":42,\"name\":\"compressed\"}"));
            });

            app.MapGet("/basic", (HttpContext context) =>
            {
                string expected = "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes("mario:secret"));
                if (context.Request.Headers.Authorization != expected)
                {
                    context.Response.Headers.WWWAuthenticate = "Basic realm=\"test\"";
                    return Results.StatusCode(401);
                }
                return Results.Text("welcome");
            });

            app.MapGet("/large", async (HttpContext context) =>
            {
                byte[] chunk = new byte[64 * 1024];
                for (int i = 0; i < 32; i++)
                {
                    await context.Response.Body.WriteAsync(chunk);
                }
            });

            await app.StartAsync();

            var addresses = app.Urls.ToList();
            httpUrl = addresses.Single(a => a.StartsWith("http://"));
            httpsUrl = addresses.Single(a => a.StartsWith("https://"));
        }

        public async Task DisposeAsync()
        {
            await app.DisposeAsync();
        }

        [Fact]
        public async Task Connections_are_reused_across_requests_and_builders()
        {
            RestResult<string> first = await Rest.Build().Url(httpUrl).Command("connection").GetAsync();
            RestResult<string> second = await Rest.Build().Url(httpUrl).Command("connection").GetAsync();

            Assert.Equal(HttpStatusCode.OK, first.StatusCode);
            Assert.Equal(first.Content, second.Content);
        }

        [Fact]
        public async Task Gzip_responses_are_decompressed()
        {
            RestResult<User> compressed = await Rest.Build().Url(httpUrl).Command("gzip").EnableGZipCompression().GetAsync<User>();
            RestResult<string> plain = await Rest.Build().Url(httpUrl).Command("gzip").GetAsync();

            Assert.Null(compressed.Exception);
            Assert.Equal(42, compressed.Content.Id);
            Assert.Equal("compressed", compressed.Content.Name);
            Assert.Equal("not-compressed", plain.Content);
        }

        [Fact]
        public async Task Network_credentials_answer_the_challenge()
        {
            RestResult<string> withCredentials = await Rest.Build().Url(httpUrl).Command("basic").NetworkCredential("mario", "secret").GetAsync();
            RestResult<string> without = await Rest.Build().Url(httpUrl).Command("basic").GetAsync();

            Assert.Equal("welcome", withCredentials.Content);
            Assert.Equal(HttpStatusCode.Unauthorized, without.StatusCode);
        }

        private static bool TrustEverything(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors errors) => true;

        [Fact]
        public async Task Untrusted_certificates_are_rejected_unless_the_callback_accepts_them()
        {
            RestResult<string> rejected = await Rest.Build().Url(httpsUrl).Command("connection").GetAsync();
            RestResult<string> accepted = await Rest.Build().Url(httpsUrl).Command("connection").CertificateValidation(TrustEverything).GetAsync();

            Assert.Null(rejected.Response);
            Assert.IsType<HttpRequestException>(rejected.Exception);
            Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        }

        [Fact]
        public async Task Large_stream_download_with_progress()
        {
            long lastProgress = 0;
            using RestResult<Stream> result = await Rest.Build().Url(httpUrl).Command("large")
                .OnDownloadProgress(p => lastProgress = p.CurrentBytes)
                .GetAsync<Stream>();

            long total = 0;
            byte[] buffer = new byte[8192];
            int read;
            while ((read = await result.Content.ReadAsync(buffer)) > 0)
            {
                total += read;
            }

            Assert.Equal(32 * 64 * 1024, total);
            Assert.Equal(total, lastProgress);
        }

        private static X509Certificate2 CreateSelfSignedCertificate()
        {
            using RSA rsa = RSA.Create(2048);
            var request = new CertificateRequest("CN=localhost", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            var san = new SubjectAlternativeNameBuilder();
            san.AddIpAddress(IPAddress.Loopback);
            san.AddDnsName("localhost");
            request.CertificateExtensions.Add(san.Build());
            request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(new OidCollection { new Oid("1.3.6.1.5.5.7.3.1") }, false));

            using X509Certificate2 certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
            byte[] pfx = certificate.Export(X509ContentType.Pfx);
#if NET9_0_OR_GREATER
            return X509CertificateLoader.LoadPkcs12(pfx, null);
#else
            return new X509Certificate2(pfx);
#endif
        }
    }
}
#endif
