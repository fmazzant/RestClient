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

namespace Microsoft.Extensions.DependencyInjection
{
    using Microsoft.Extensions.DependencyInjection.Extensions;
    using Mafesoft.RestClient;
    using Mafesoft.RestClient.Extensions.DependencyInjection;
    using System;
#if NET
    using System.Diagnostics.CodeAnalysis;
#endif

    /// <summary>
    /// Registers Mafesoft.RestClient in the service collection.
    /// </summary>
    public static class RestClientServiceCollectionExtensions
    {
        /// <summary>
        /// Registers a named rest client. Resolve it with IRestClientFactory.CreateClient(name).
        /// The requests are sent with the HttpClient created by IHttpClientFactory for the same name:
        /// use the returned IHttpClientBuilder to add delegating handlers or resilience
        /// (for example AddStandardResilienceHandler() of Microsoft.Extensions.Http.Resilience).
        /// </summary>
        /// <param name="services">The service collection</param>
        /// <param name="name">The name of the client</param>
        /// <param name="configure">Configures the builder (Url, headers, authentication, serialization, ...)</param>
        /// <returns>The IHttpClientBuilder of the named HttpClient</returns>
        public static IHttpClientBuilder AddRestClient(this IServiceCollection services, string name, Func<RestBuilder, RestBuilder> configure)
        {
            if (configure == null) { throw new ArgumentNullException(nameof(configure)); }
            return services.AddRestClient(name, (_, builder) => configure(builder));
        }

        /// <summary>
        /// Registers a named rest client. Resolve it with IRestClientFactory.CreateClient(name).
        /// The requests are sent with the HttpClient created by IHttpClientFactory for the same name:
        /// use the returned IHttpClientBuilder to add delegating handlers or resilience
        /// (for example AddStandardResilienceHandler() of Microsoft.Extensions.Http.Resilience).
        /// </summary>
        /// <param name="services">The service collection</param>
        /// <param name="name">The name of the client</param>
        /// <param name="configure">
        /// Configures the builder (Url, headers, authentication, serialization, ...).
        /// The IServiceProvider is the root provider: do not resolve scoped services from it.
        /// </param>
        /// <returns>The IHttpClientBuilder of the named HttpClient</returns>
        public static IHttpClientBuilder AddRestClient(this IServiceCollection services, string name, Func<IServiceProvider, RestBuilder, RestBuilder> configure)
        {
            if (services == null) { throw new ArgumentNullException(nameof(services)); }
            if (name == null) { throw new ArgumentNullException(nameof(name)); }
            if (configure == null) { throw new ArgumentNullException(nameof(configure)); }

            IHttpClientBuilder httpClientBuilder = services.AddHttpClient(name);
            services.TryAddSingleton<IRestClientFactory, RestClientFactory>();
            services.Configure<RestClientFactoryOptions>(name, options => options.Configurations.Add(configure));
            return httpClientBuilder;
        }

        /// <summary>
        /// Registers a typed rest client: TClient is transient and receives a configured RestBuilder in its constructor.
        /// The name of the client is the full name of TClient.
        /// </summary>
        /// <typeparam name="TClient">The class that uses the RestBuilder</typeparam>
        /// <param name="services">The service collection</param>
        /// <param name="configure">Configures the builder (Url, headers, authentication, serialization, ...)</param>
        /// <returns>The IHttpClientBuilder of the named HttpClient</returns>
        public static IHttpClientBuilder AddRestClient<
#if NET
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
#endif
            TClient>(this IServiceCollection services, Func<RestBuilder, RestBuilder> configure)
            where TClient : class
        {
            if (configure == null) { throw new ArgumentNullException(nameof(configure)); }
            return services.AddRestClient<TClient>((_, builder) => configure(builder));
        }

        /// <summary>
        /// Registers a typed rest client: TClient is transient and receives a configured RestBuilder in its constructor.
        /// The name of the client is the full name of TClient.
        /// </summary>
        /// <typeparam name="TClient">The class that uses the RestBuilder</typeparam>
        /// <param name="services">The service collection</param>
        /// <param name="configure">
        /// Configures the builder (Url, headers, authentication, serialization, ...).
        /// The IServiceProvider is the root provider: do not resolve scoped services from it.
        /// </param>
        /// <returns>The IHttpClientBuilder of the named HttpClient</returns>
        public static IHttpClientBuilder AddRestClient<
#if NET
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
#endif
            TClient>(this IServiceCollection services, Func<IServiceProvider, RestBuilder, RestBuilder> configure)
            where TClient : class
        {
            string name = typeof(TClient).FullName ?? typeof(TClient).Name;
            IHttpClientBuilder httpClientBuilder = services.AddRestClient(name, configure);
            services.TryAddTransient(provider => ActivatorUtilities.CreateInstance<TClient>(
                provider,
                provider.GetRequiredService<IRestClientFactory>().CreateClient(name)));
            return httpClientBuilder;
        }
    }
}
