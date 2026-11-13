namespace Mafesoft.RestClient.Tests
{
    using Microsoft.Extensions.DependencyInjection;
    using System.Collections.Concurrent;
    using System.Diagnostics.Metrics;
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Headers;

    public class MetricsTests
    {
        private const string Duration = "restclient.request.duration";
        private const string Retries = "restclient.retries";
        private const string RefreshToken = "restclient.refresh_token";
        private const string ClientNameTag = "restclient.client.name";

        /// <summary>
        /// Collects the measurements of the RestClient meter. The tests run in parallel: every test filters
        /// the measurements by its own client name (or HTTP method).
        /// </summary>
        private sealed class MetricsCollector : IDisposable
        {
            private readonly MeterListener listener = new MeterListener();

            public MetricsCollector()
            {
                listener.InstrumentPublished = (instrument, l) =>
                {
                    if (instrument.Meter.Name == RestClientTelemetry.MeterName)
                    {
                        l.EnableMeasurementEvents(instrument);
                    }
                };
                listener.SetMeasurementEventCallback<double>((instrument, value, tags, state) => Add(instrument, value, tags));
                listener.SetMeasurementEventCallback<long>((instrument, value, tags, state) => Add(instrument, value, tags));
                listener.Start();
            }

            public ConcurrentQueue<Measurement> Measurements { get; } = new();

            public Measurement[] For(string instrument, string tag, object value)
                => Measurements.Where(m => m.Instrument == instrument && m.Tags.TryGetValue(tag, out object v) && Equals(v, value)).ToArray();

            private void Add(Instrument instrument, double value, ReadOnlySpan<KeyValuePair<string, object>> tags)
            {
                var dictionary = new Dictionary<string, object>();
                foreach (var tag in tags)
                {
                    dictionary[tag.Key] = tag.Value;
                }
                Measurements.Enqueue(new Measurement(instrument.Name, instrument.Unit, value, dictionary));
            }

            public void Dispose() => listener.Dispose();
        }

        private sealed class Measurement
        {
            public Measurement(string instrument, string unit, double value, Dictionary<string, object> tags)
            {
                Instrument = instrument;
                Unit = unit;
                Value = value;
                Tags = tags;
            }

            public string Instrument { get; }

            public string Unit { get; }

            public double Value { get; }

            public Dictionary<string, object> Tags { get; }
        }

        private static string UniqueName() => "metrics-" + Guid.NewGuid().ToString("N");

        private static (ServiceProvider Provider, RestBuilder Api) CreateClient(string name, FakeHttpMessageHandler handler, Func<RestBuilder, RestBuilder> configure = null)
        {
            var services = new ServiceCollection();
            services
                .AddRestClient(name, builder => (configure ?? (b => b))(builder.Url("https://api.example.com")))
                .ConfigurePrimaryHttpMessageHandler(() => handler);
            ServiceProvider provider = services.BuildServiceProvider();
            return (provider, provider.GetRequiredService<IRestClientFactory>().CreateClient(name));
        }

        [Fact]
        public async Task Request_duration_is_recorded_with_method_status_code_and_client_name()
        {
            using var metrics = new MetricsCollector();
            string name = UniqueName();
            var (provider, api) = CreateClient(name, FakeHttpMessageHandler.Returns(HttpStatusCode.OK));
            using (provider)
            {
                await api.Command("users").GetAsync();
            }

            Measurement measurement = Assert.Single(metrics.For(Duration, ClientNameTag, name));
            Assert.Equal("s", measurement.Unit);
            Assert.True(measurement.Value >= 0 && measurement.Value < 60, $"duration {measurement.Value}");
            Assert.Equal("GET", measurement.Tags["http.request.method"]);
            Assert.Equal(200, measurement.Tags["http.response.status_code"]);
            Assert.False(measurement.Tags.ContainsKey("error.type"));
        }

        [Fact]
        public async Task Request_duration_has_no_client_name_without_the_factory()
        {
            using var metrics = new MetricsCollector();
            var method = new HttpMethod("METRICS-" + Guid.NewGuid().ToString("N"));
            var handler = FakeHttpMessageHandler.Returns(HttpStatusCode.Accepted);

            await Rest.Build(handler.CreateClient()).Url("https://api.example.com").SendAsync<string>(method);

            Measurement measurement = Assert.Single(metrics.For(Duration, "http.request.method", method.Method));
            Assert.Equal(202, measurement.Tags["http.response.status_code"]);
            Assert.False(measurement.Tags.ContainsKey(ClientNameTag));
        }

        [Fact]
        public async Task Client_name_set_without_the_factory_tags_the_metrics_and_null_removes_it()
        {
            using var metrics = new MetricsCollector();
            string name = UniqueName();
            var handler = FakeHttpMessageHandler.Returns(HttpStatusCode.ServiceUnavailable);
            RestBuilder api = Rest.Build(handler.CreateClient()).Url("https://api.example.com").ClientName(name);

            await api.Retry(1, TimeSpan.Zero).GetAsync();

            Measurement duration = Assert.Single(metrics.For(Duration, ClientNameTag, name));
            Assert.Equal(503, duration.Tags["http.response.status_code"]);
            Assert.Equal(1, Assert.Single(metrics.For(Retries, ClientNameTag, name)).Value);

            var method = new HttpMethod("METRICS-" + Guid.NewGuid().ToString("N"));
            await api.ClientName(null).SendAsync<string>(method);

            Measurement removed = Assert.Single(metrics.For(Duration, "http.request.method", method.Method));
            Assert.False(removed.Tags.ContainsKey(ClientNameTag));
        }

        [Fact]
        public async Task Retries_are_counted_and_the_duration_is_recorded_once()
        {
            using var metrics = new MetricsCollector();
            string name = UniqueName();
            var statuses = new ConcurrentQueue<HttpStatusCode>(new[] { HttpStatusCode.ServiceUnavailable, HttpStatusCode.BadGateway, HttpStatusCode.OK });
            var handler = new FakeHttpMessageHandler((r, i) =>
            {
                statuses.TryDequeue(out HttpStatusCode status);
                return new HttpResponseMessage(status);
            });
            var (provider, api) = CreateClient(name, handler, b => b.Retry(3, TimeSpan.Zero));
            using (provider)
            {
                RestResult<string> result = await api.GetAsync();
                Assert.Equal(2, result.RetryCount);
            }

            Measurement[] retries = metrics.For(Retries, ClientNameTag, name);
            Assert.Equal(2, retries.Sum(m => m.Value));
            Assert.All(retries, m => Assert.Equal("GET", m.Tags["http.request.method"]));
            Assert.All(retries, m => Assert.Equal("{retry}", m.Unit));
            Measurement duration = Assert.Single(metrics.For(Duration, ClientNameTag, name));
            Assert.Equal(200, duration.Tags["http.response.status_code"]);
        }

        [Fact]
        public async Task Refresh_token_is_counted_once_for_concurrent_requests()
        {
            using var metrics = new MetricsCollector();
            string name = UniqueName();
            string token = "expired";
            var handler = new FakeHttpMessageHandler(async (r, i) =>
            {
                await Task.Delay(20);
                return new HttpResponseMessage(r.Headers.Authorization.Parameter == "valid" ? HttpStatusCode.OK : HttpStatusCode.Unauthorized);
            });
            var refreshHandler = FakeHttpMessageHandler.Returns(HttpStatusCode.OK);
            var (provider, api) = CreateClient(name, handler, b => b
                .Authentication(() => new AuthenticationHeaderValue("Bearer", token))
                .RefreshToken(async ct =>
                {
                    await Task.Delay(50, ct);
                    token = "valid";
                    return await Rest.Build(refreshHandler.CreateClient()).Url("https://auth.example.com").PostAsync(ct);
                }));
            using (provider)
            {
                RestResult<string>[] results = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => api.GetAsync()));
                Assert.All(results, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
            }

            Measurement refresh = Assert.Single(metrics.For(RefreshToken, ClientNameTag, name));
            Assert.Equal(1, refresh.Value);
            Assert.Equal("GET", refresh.Tags["http.request.method"]);
            Assert.Equal(5, metrics.For(Duration, ClientNameTag, name).Length);
        }

        [Fact]
        public async Task Error_type_is_the_status_code_of_an_unsuccessful_response()
        {
            using var metrics = new MetricsCollector();
            string name = UniqueName();
            var (provider, api) = CreateClient(name, FakeHttpMessageHandler.Returns(HttpStatusCode.NotFound));
            using (provider)
            {
                await api.PostAsync();
            }

            Measurement measurement = Assert.Single(metrics.For(Duration, ClientNameTag, name));
            Assert.Equal("POST", measurement.Tags["http.request.method"]);
            Assert.Equal(404, measurement.Tags["http.response.status_code"]);
            Assert.Equal("404", measurement.Tags["error.type"]);
        }

        [Fact]
        public async Task Error_type_is_the_exception_type_when_there_is_no_response()
        {
            using var metrics = new MetricsCollector();
            string name = UniqueName();
            var handler = new FakeHttpMessageHandler((r, i) => Task.FromException<HttpResponseMessage>(new HttpRequestException("down")));
            var (provider, api) = CreateClient(name, handler);
            using (provider)
            {
                RestResult<string> result = await api.GetAsync();
                Assert.IsType<HttpRequestException>(result.Exception);
            }

            Measurement measurement = Assert.Single(metrics.For(Duration, ClientNameTag, name));
            Assert.Equal(typeof(HttpRequestException).FullName, measurement.Tags["error.type"]);
            Assert.False(measurement.Tags.ContainsKey("http.response.status_code"));
        }

        [Fact]
        public async Task Instruments_not_enabled_record_nothing()
        {
            var measurements = new ConcurrentQueue<string>();
            using var listener = new MeterListener();
            listener.InstrumentPublished = (instrument, l) => { };   // nothing enabled
            listener.SetMeasurementEventCallback<double>((instrument, value, tags, state) => measurements.Enqueue(instrument.Name));
            listener.Start();
            string name = UniqueName();
            var (provider, api) = CreateClient(name, FakeHttpMessageHandler.Returns(HttpStatusCode.ServiceUnavailable), b => b.Retry(1, TimeSpan.Zero));
            using (provider)
            {
                RestResult<string> result = await api.GetAsync();
                Assert.Equal(HttpStatusCode.ServiceUnavailable, result.StatusCode);
            }

            Assert.Empty(measurements);
        }
    }
}
