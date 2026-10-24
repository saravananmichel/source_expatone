using System.Net;
using ExpatOne.Api.Services.Destinations;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ExpatOne.Tests;

public class DestinationWeatherTests
{
    private sealed class Handler(string body, HttpStatusCode status = HttpStatusCode.OK) : HttpMessageHandler
    {
        public int Calls;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Calls++;
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body) });
        }
    }
    private static DestinationWeatherService Service(Handler handler, IConfiguration? config = null)
    {
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient("DestinationWeather")).Returns(new HttpClient(handler));
        return new(factory.Object, new MemoryCache(new MemoryCacheOptions()),
            config ?? new ConfigurationBuilder().Build(), NullLogger<DestinationWeatherService>.Instance);
    }
    private static string Current(double temperature) =>
        "{\"current\":{\"temperature_2m\":" + temperature.ToString(System.Globalization.CultureInfo.InvariantCulture) +
        ",\"weather_code\":3,\"is_day\":1,\"time\":" + DateTimeOffset.UtcNow.ToUnixTimeSeconds() + "}}";
    private static readonly DestinationPlace Place = new("Singapore", 1.35, 103.82, "Marina Bay Sands");

    [Fact]
    public async Task ConcurrentRequestsShareOneProviderCall()
    {
        var handler = new Handler(Current(28.5));
        var service = Service(handler);
        var results = await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => service.GetAsync("SG", Place, default)));
        Assert.Equal(1, handler.Calls);
        Assert.All(results, r => { Assert.True(r.Available); Assert.Equal(28.5, r.TemperatureC); });
    }
    [Fact]
    public async Task FailedProviderReturnsNoInventedTemperatureAndCachesFailure()
    {
        var handler = new Handler("{}", HttpStatusCode.TooManyRequests);
        var service = Service(handler);
        var result = await service.GetAsync("SG", Place, default);
        await service.GetAsync("SG", Place, default);
        Assert.False(result.Available);
        Assert.Null(result.TemperatureC);
        Assert.Equal(1, handler.Calls);
    }
    [Fact]
    public async Task MalformedPayloadDoesNotBreakHomepage()
    {
        var result = await Service(new Handler("{\"current\":{}}")).GetAsync("SG", Place, default);
        Assert.False(result.Available);
    }
    [Fact]
    public async Task DifferentCountriesHaveSeparateWeather()
    {
        var handler = new Handler(Current(30));
        var service = Service(handler);
        var sg = await service.GetAsync("SG", Place, default);
        var my = await service.GetAsync("MY", new("Kuala Lumpur", 3.139, 101.6869, "Petronas"), default);
        Assert.Equal(2, handler.Calls);
        Assert.Equal("SG", sg.CountryCode); Assert.Equal("MY", my.CountryCode);
    }
    [Fact]
    public void CatalogCoversAllCurrentCountriesAndDoesNotInventUnknownPlaces()
    {
        var catalog = new DestinationCatalog(new ConfigurationBuilder().Build());
        foreach (var code in new[] { "AE", "BH", "CN", "ID", "IN", "KW", "MY", "OM", "QA", "SA", "SG" })
            Assert.NotNull(catalog.Find(code));
        Assert.Null(catalog.Find("ZZ"));
    }
}
