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
    using System.Collections.Concurrent;
    using System.Net;
    using System.Net.Http;
    using System.Net.Security;
    using System.Security.Cryptography.X509Certificates;

    /// <summary>
    /// Keeps the HTTP handlers alive and shares them between requests with the same configuration,
    /// so that TCP connections are reused (no socket exhaustion, no TLS handshake for every request).
    /// </summary>
    internal static class HttpHandlerPool
    {
        /// <summary>
        /// Maximum number of pooled handlers. When the limit is reached (for example because a new certificate
        /// validation closure is created for every request) the handler is created per request, as in 1.x.
        /// </summary>
        internal const int MaxPooledHandlers = 32;

        /// <summary>
        /// Lifetime of a pooled connection: after it the connection is recreated, so that DNS changes are honored.
        /// </summary>
        internal static readonly TimeSpan PooledConnectionLifetime = TimeSpan.FromMinutes(2);

        private static readonly ConcurrentDictionary<HandlerKey, HttpMessageHandler> handlers
            = new ConcurrentDictionary<HandlerKey, HttpMessageHandler>();

        /// <summary>
        /// Number of pooled handlers
        /// </summary>
        internal static int Count => handlers.Count;

        /// <summary>
        /// Gets an HttpClient for the configuration. The returned client must be disposed by the caller
        /// (disposing it never disposes a pooled handler).
        /// </summary>
        internal static HttpClient CreateClient(HandlerKey key)
        {
            if (handlers.TryGetValue(key, out HttpMessageHandler? pooled))
            {
                return new HttpClient(pooled, disposeHandler: false);
            }

            if (handlers.Count < MaxPooledHandlers)
            {
                HttpMessageHandler created = CreateHandler(key);
                HttpMessageHandler handler = handlers.GetOrAdd(key, created);
                if (!ReferenceEquals(handler, created))
                {
                    created.Dispose();
                }
                return new HttpClient(handler, disposeHandler: false);
            }

            return new HttpClient(CreateHandler(key), disposeHandler: true);
        }

        private static HttpMessageHandler CreateHandler(HandlerKey key)
        {
#if NET
            if (key.CertificateOption == ClientCertificateOption.Manual)
            {
                SocketsHttpHandler socketsHandler = new SocketsHttpHandler
                {
                    PooledConnectionLifetime = PooledConnectionLifetime,
                    UseCookies = false,
                    AutomaticDecompression = key.Compression
                        ? DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli
                        : DecompressionMethods.None,
                    UseProxy = key.UseProxy,
                };

                if (key.Proxy != null)
                {
                    socketsHandler.Proxy = key.Proxy;
                }

                if (key.Credentials != null)
                {
                    socketsHandler.Credentials = key.Credentials;
                }

                if (key.CertificateCallback != null)
                {
                    var callback = key.CertificateCallback;
                    socketsHandler.SslOptions.RemoteCertificateValidationCallback = (sender, certificate, chain, errors)
                        => callback(sender, certificate, chain, errors);
                }

                return socketsHandler;
            }
#endif
            HttpClientHandler handler = new HttpClientHandler
            {
                ClientCertificateOptions = key.CertificateOption,
                UseCookies = false,
                UseProxy = key.UseProxy,
            };

            if (key.Proxy != null)
            {
                handler.Proxy = key.Proxy;
            }

            if (key.Credentials != null)
            {
                handler.Credentials = key.Credentials;
            }

            if (key.CertificateCallback != null)
            {
                var callback = key.CertificateCallback;
                handler.ServerCertificateCustomValidationCallback = (message, certificate, chain, errors)
                    => callback(message, certificate, chain, errors);
            }

            if (key.Compression && handler.SupportsAutomaticDecompression)
            {
#if NET
                handler.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli;
#else
                handler.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
#endif
            }

            return handler;
        }

        /// <summary>
        /// Identifies a handler configuration
        /// </summary>
        internal readonly struct HandlerKey : IEquatable<HandlerKey>
        {
            public HandlerKey(
                bool compression,
                ClientCertificateOption certificateOption,
                ICredentials? credentials,
                Func<object, X509Certificate?, X509Chain?, SslPolicyErrors, bool>? certificateCallback,
                bool useProxy,
                IWebProxy? proxy)
            {
                Compression = compression;
                CertificateOption = certificateOption;
                Credentials = credentials;
                CertificateCallback = certificateCallback;
                UseProxy = useProxy;
                Proxy = proxy;
            }

            public bool Compression { get; }

            public ClientCertificateOption CertificateOption { get; }

            public ICredentials? Credentials { get; }

            public Func<object, X509Certificate?, X509Chain?, SslPolicyErrors, bool>? CertificateCallback { get; }

            /// <summary>
            /// False: no proxy. True with a null Proxy: the system proxy (the default).
            /// </summary>
            public bool UseProxy { get; }

            /// <summary>
            /// The proxy, compared by reference: builders with the same instance share the handler.
            /// </summary>
            public IWebProxy? Proxy { get; }

            public bool Equals(HandlerKey other)
                => Compression == other.Compression
                && CertificateOption == other.CertificateOption
                && UseProxy == other.UseProxy
                && ReferenceEquals(Proxy, other.Proxy)
                && Equals(CertificateCallback, other.CertificateCallback)
                && CredentialsEquals(Credentials, other.Credentials);

            public override bool Equals(object? obj) => obj is HandlerKey other && Equals(other);

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = Compression ? 1 : 0;
                    hash = (hash * 397) ^ (int)CertificateOption;
                    hash = (hash * 397) ^ (CertificateCallback?.GetHashCode() ?? 0);
                    hash = (hash * 397) ^ CredentialsHashCode(Credentials);
                    hash = (hash * 397) ^ (UseProxy ? 1 : 0);
                    hash = (hash * 397) ^ (Proxy == null ? 0 : System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(Proxy));
                    return hash;
                }
            }

            /// <summary>
            /// NetworkCredential has reference equality: compare the values, so that
            /// NetworkCredential("user", "pwd") created for every request still shares the handler.
            /// </summary>
            private static bool CredentialsEquals(ICredentials? a, ICredentials? b)
            {
                if (ReferenceEquals(a, b)) { return true; }
                if (a is NetworkCredential na && b is NetworkCredential nb)
                {
                    return na.UserName == nb.UserName && na.Password == nb.Password && na.Domain == nb.Domain;
                }
                return false;
            }

            private static int CredentialsHashCode(ICredentials? credentials)
            {
                if (credentials is NetworkCredential nc)
                {
                    return (nc.UserName?.GetHashCode() ?? 0) ^ (nc.Domain?.GetHashCode() ?? 0);
                }
                return credentials?.GetHashCode() ?? 0;
            }
        }
    }
}
