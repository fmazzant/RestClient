namespace Mafesoft.RestClient.Tests
{
    using Microsoft.Extensions.DependencyInjection;
    using System.Net;
    using System.Net.Http;

    public class DependencyInjectionTests
    {
        public sealed class UsersClient
        {
            private readonly RestBuilder root;

            public UsersClient(RestBuilder root)
            {
                this.root = root;
            }

            public Task<RestResult<User>> GetAsync(int id) => root.Command("users").Command(id).GetAsync<User>();
        }

        [Fact]
        public async Task Named_client_uses_http_client_factory_and_configuration()
        {
            var handler = FakeHttpMessageHandler.Returns(HttpStatusCode.OK, "{\"id\":1}");
            var services = new ServiceCollection();
            services
                .AddRestClient("api", builder => builder.Url("https://api.example.com").Header("X-Api", "1"))
                .ConfigurePrimaryHttpMessageHandler(() => handler);

            using ServiceProvider provider = services.BuildServiceProvider();
            RestBuilder api = provider.GetRequiredService<IRestClientFactory>().CreateClient("api");

            RestResult<User> result = await api.Command("users").GetAsync<User>();

            Assert.Equal(1, result.Content.Id);
            Assert.Equal("https://api.example.com/users", handler.Requests[0].RequestUri.AbsoluteUri);
            Assert.Equal("1", handler.Requests[0].Headers.GetValues("X-Api").Single());
        }

        [Fact]
        public async Task Typed_client_receives_the_builder()
        {
            var handler = FakeHttpMessageHandler.Returns(HttpStatusCode.OK, "{\"id\":5,\"name\":\"Anna\"}");
            var services = new ServiceCollection();
            services
                .AddRestClient<UsersClient>(builder => builder.Url("https://api.example.com"))
                .ConfigurePrimaryHttpMessageHandler(() => handler);

            using ServiceProvider provider = services.BuildServiceProvider();
            RestResult<User> result = await provider.GetRequiredService<UsersClient>().GetAsync(5);

            Assert.Equal("Anna", result.Content.Name);
            Assert.Equal("https://api.example.com/users/5", handler.Requests[0].RequestUri.AbsoluteUri);
        }

        public sealed class TokenProvider
        {
            public int Calls;

            public async Task<string> GetTokenAsync(CancellationToken cancellationToken)
            {
                await Task.Yield();
                return "token-" + Interlocked.Increment(ref Calls);
            }
        }

        [Fact]
        public async Task Bearer_token_provider_resolved_from_the_service_provider()
        {
            var handler = FakeHttpMessageHandler.Returns(HttpStatusCode.OK);
            var services = new ServiceCollection();
            services.AddSingleton<TokenProvider>();
            services
                .AddRestClient("secure", (provider, builder) =>
                {
                    TokenProvider tokens = provider.GetRequiredService<TokenProvider>();
                    return builder.Url("https://api.example.com").BearerToken(tokens.GetTokenAsync);
                })
                .ConfigurePrimaryHttpMessageHandler(() => handler);

            using ServiceProvider provider = services.BuildServiceProvider();
            RestBuilder api = provider.GetRequiredService<IRestClientFactory>().CreateClient("secure");
            await api.GetAsync();
            await api.GetAsync();

            Assert.Equal(2, provider.GetRequiredService<TokenProvider>().Calls);
            Assert.Equal("Bearer token-1", handler.Requests[0].Headers.Authorization.ToString());
            Assert.Equal("Bearer token-2", handler.Requests[1].Headers.Authorization.ToString());
        }

#if NET
        [Fact]
        public async Task Works_with_the_standard_resilience_handler()
        {
            int calls = 0;
            var handler = new FakeHttpMessageHandler((r, i) =>
                new HttpResponseMessage(++calls < 3 ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.OK) { Content = new StringContent("ok") });

            var services = new ServiceCollection();
            services
                .AddRestClient("resilient", builder => builder.Url("https://api.example.com"))
                .ConfigurePrimaryHttpMessageHandler(() => handler)
                .AddStandardResilienceHandler(options =>
                {
                    options.Retry.Delay = TimeSpan.Zero;
                    options.Retry.MaxRetryAttempts = 3;
                });

            using ServiceProvider provider = services.BuildServiceProvider();
            RestResult<string> result = await provider.GetRequiredService<IRestClientFactory>().CreateClient("resilient").GetAsync();

            Assert.Equal("ok", result.Content);
            Assert.Equal(3, handler.Requests.Count);
        }
#endif
    }
}
