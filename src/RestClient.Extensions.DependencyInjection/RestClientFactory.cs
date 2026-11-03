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

namespace RestClient.Extensions.DependencyInjection
{
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;
    using System;
    using System.Collections.Generic;
    using System.Net.Http;

    /// <summary>
    /// Configuration callbacks of a named rest client
    /// </summary>
    internal sealed class RestClientFactoryOptions
    {
        public List<Func<IServiceProvider, RestBuilder, RestBuilder>> Configurations { get; } = new List<Func<IServiceProvider, RestBuilder, RestBuilder>>();
    }

    /// <summary>
    /// Default IRestClientFactory: IHttpClientFactory + logging + configuration callbacks
    /// </summary>
    internal sealed class RestClientFactory : IRestClientFactory
    {
        internal const string LoggerCategoryPrefix = "Mafe.RestClient.";

        private readonly IServiceProvider services;
        private readonly IHttpClientFactory httpClientFactory;
        private readonly IOptionsMonitor<RestClientFactoryOptions> options;
        private readonly ILoggerFactory loggerFactory;

        public RestClientFactory(
            IServiceProvider services,
            IHttpClientFactory httpClientFactory,
            IOptionsMonitor<RestClientFactoryOptions> options,
            ILoggerFactory loggerFactory)
        {
            this.services = services;
            this.httpClientFactory = httpClientFactory;
            this.options = options;
            this.loggerFactory = loggerFactory;
        }

        public RestBuilder CreateClient(string name)
        {
            if (name == null) { throw new ArgumentNullException(nameof(name)); }

            RestBuilder builder = Rest
                .Build(httpClientFactory.CreateClient(name))
                .Logger(loggerFactory.CreateLogger(LoggerCategoryPrefix + name));

            foreach (var configure in options.Get(name).Configurations)
            {
                builder = configure(services, builder)
                    ?? throw new InvalidOperationException($"The configuration of the rest client '{name}' returned null.");
            }

            return builder;
        }
    }
}
