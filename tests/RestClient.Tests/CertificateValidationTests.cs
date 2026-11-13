namespace Mafesoft.RestClient.Tests
{
    using System.Net;
    using System.Net.Http;
    using System.Net.Security;
    using System.Net.Sockets;
    using System.Security.Authentication;
    using System.Security.Cryptography;
    using System.Security.Cryptography.X509Certificates;
    using System.Text;

    /// <summary>
    /// The certificate validation callback is applied only to the requests of its builder, without global side effects.
    /// A minimal HTTPS server (SslStream) with a self-signed certificate, so the tests also run on net48
    /// (where the netstandard2.0 build uses HttpClientHandler).
    /// </summary>
    public sealed class CertificateValidationTests : IDisposable
    {
        private readonly TlsServer server = new TlsServer();

        public void Dispose() => server.Dispose();

        private RestBuilder Root() => Rest.Build().Url(server.Url).Timeout(TimeSpan.FromSeconds(30));

        [Fact]
        public async Task Callback_receives_the_server_certificate_and_the_errors()
        {
            string receivedHash = null; // the certificate is disposed after the validation: keep its hash
            SslPolicyErrors errors = SslPolicyErrors.None;

            RestResult<string> result = await Root()
                .CertificateValidation((sender, certificate, chain, policyErrors) =>
                {
                    receivedHash = certificate?.GetCertHashString();
                    errors = policyErrors;
                    return true;
                })
                .GetAsync();

            Assert.Null(result.Exception);
            Assert.Equal(HttpStatusCode.OK, result.StatusCode);
            Assert.Equal("secure", result.Content);
            Assert.NotNull(receivedHash);
            Assert.Equal(server.Certificate.GetCertHashString(), receivedHash);
            // self-signed: the chain is not trusted
            Assert.True(errors.HasFlag(SslPolicyErrors.RemoteCertificateChainErrors));
        }

        [Fact]
        public async Task Callback_returning_false_rejects_the_certificate()
        {
            int calls = 0;

            RestResult<string> result = await Root()
                .CertificateValidation((sender, certificate, chain, errors) => { calls++; return false; })
                .GetAsync();

            Assert.True(calls > 0);
            Assert.Null(result.Response);
            Assert.IsType<HttpRequestException>(result.Exception);
        }

        [Fact]
        public async Task Callback_is_applied_only_to_its_builder()
        {
            RestBuilder root = Root();
            RestBuilder trusting = root.CertificateValidation((sender, certificate, chain, errors) => true);

            RestResult<string> accepted = await trusting.GetAsync();
            RestResult<string> rejected = await root.GetAsync();
            RestResult<string> reset = await trusting.CertificateValidation(null).GetAsync();

            Assert.Equal("secure", accepted.Content);
            Assert.IsType<HttpRequestException>(rejected.Exception);
            Assert.IsType<HttpRequestException>(reset.Exception);

            // no global side effects: a plain HttpClient still rejects the certificate
            using var plain = new HttpClient();
            await Assert.ThrowsAsync<HttpRequestException>(() => plain.GetStringAsync(server.Url));
#pragma warning disable SYSLIB0014 // ServicePointManager is obsolete on .NET
            Assert.Null(ServicePointManager.ServerCertificateValidationCallback);
#pragma warning restore SYSLIB0014
        }

        [Fact]
        public void Synchronous_request_uses_the_callback()
        {
            RestResult<string> result = Root().CertificateValidation((sender, certificate, chain, errors) => true).Get();

            Assert.Equal("secure", result.Content);
        }

        /// <summary>
        /// Answers "secure" to every request, over TLS with a self-signed certificate (one request per connection)
        /// </summary>
        private sealed class TlsServer : IDisposable
        {
            private readonly TcpListener listener = new TcpListener(IPAddress.Loopback, 0);

            public TlsServer()
            {
                Certificate = CreateSelfSignedCertificate();
                listener.Start();
                Url = $"https://127.0.0.1:{((IPEndPoint)listener.LocalEndpoint).Port}/";
                _ = AcceptAsync();
            }

            public X509Certificate2 Certificate { get; }

            public string Url { get; }

            private async Task AcceptAsync()
            {
                while (true)
                {
                    TcpClient client;
                    try
                    {
                        client = await listener.AcceptTcpClientAsync();
                    }
                    catch (Exception)
                    {
                        return; // stopped
                    }
                    _ = ServeAsync(client);
                }
            }

            private async Task ServeAsync(TcpClient client)
            {
                try
                {
                    using (client)
                    using (var ssl = new SslStream(client.GetStream(), leaveInnerStreamOpen: false))
                    {
                        await ssl.AuthenticateAsServerAsync(Certificate, clientCertificateRequired: false, SslProtocols.Tls12, checkCertificateRevocation: false);

                        // reads the request headers (the tests send no body)
                        var request = new StringBuilder();
                        byte[] buffer = new byte[4096];
                        while (!request.ToString().Contains("\r\n\r\n"))
                        {
                            int read = await ssl.ReadAsync(buffer, 0, buffer.Length);
                            if (read == 0) { return; }
                            request.Append(Encoding.ASCII.GetString(buffer, 0, read));
                        }

                        byte[] response = Encoding.ASCII.GetBytes(
                            "HTTP/1.1 200 OK\r\nContent-Type: text/plain\r\nContent-Length: 6\r\nConnection: close\r\n\r\nsecure");
                        await ssl.WriteAsync(response, 0, response.Length);
                        await ssl.FlushAsync();
                    }
                }
                catch (Exception)
                {
                    // the client rejected the certificate
                }
            }

            public void Dispose()
            {
                listener.Stop();
                Certificate.Dispose();
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
}
