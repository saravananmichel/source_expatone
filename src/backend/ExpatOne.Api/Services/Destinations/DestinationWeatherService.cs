using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;

namespace ExpatOne.Api.Services.Destinations;

public record DestinationWeather(string CountryCode, string City, bool Available,
    double? TemperatureC, int? WeatherCode, bool? IsDay, DateTimeOffset? ObservedAt,
    DateTimeOffset? FetchedAt, string Source = "Open-Meteo");

// Registered as a singleton: cache and per-country gates are shared by requests.
public sealed class DestinationWeatherService(
    IHttpClientFactory clients, IMemoryCache cache, IConfiguration configuration,
    ILogger<DestinationWeatherService> logger)
{
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _gates = new();

    public async Task<DestinationWeather> GetAsync(string code, DestinationPlace place, CancellationToken ct)
    {
        var key = $"destination-weather:{code}:{place.Latitude}:{place.Longitude}";
        if (cache.TryGetValue(key, out DestinationWeather? hit) && hit is not null) return hit;
        var gate = _gates.GetOrAdd(code, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct);
        try
        {
            if (cache.TryGetValue(key, out hit) && hit is not null) return hit;
            var fallback = new DestinationWeather(code, place.City, false, null, null, null, null, null);
            if (!configuration.GetValue("Weather:Enabled", true)) return fallback;
            try
            {
                var apiKey = configuration["Weather:ApiKey"];
                // Fixed provider hosts avoid arbitrary URL fetching. Keys never leave the server.
                var host = string.IsNullOrWhiteSpace(apiKey) ? "api.open-meteo.com" : "customer-api.open-meteo.com";
                var query = FormattableString.Invariant(
                    $"latitude={place.Latitude}&longitude={place.Longitude}&current=temperature_2m,weather_code,is_day&temperature_unit=celsius&timeformat=unixtime&timezone=GMT");
                if (!string.IsNullOrWhiteSpace(apiKey)) query += "&apikey=" + Uri.EscapeDataString(apiKey);
                using var response = await clients.CreateClient("DestinationWeather")
                    .GetAsync($"https://{host}/v1/forecast?{query}", ct);
                response.EnsureSuccessStatusCode();
                using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
                var current = json.RootElement.GetProperty("current");
                var temperature = current.GetProperty("temperature_2m").GetDouble();
                var weatherCode = current.GetProperty("weather_code").GetInt32();
                var isDay = current.GetProperty("is_day").GetInt32();
                var observed = DateTimeOffset.FromUnixTimeSeconds(current.GetProperty("time").GetInt64());
                if (!double.IsFinite(temperature) || temperature is < -100 or > 70 || isDay is < 0 or > 1 ||
                    observed < DateTimeOffset.UtcNow.AddHours(-2) || observed > DateTimeOffset.UtcNow.AddHours(1))
                    throw new JsonException("Invalid or outdated weather data");
                var result = new DestinationWeather(code, place.City, true, temperature, weatherCode,
                    isDay == 1, observed, DateTimeOffset.UtcNow);
                cache.Set(key, result, TimeSpan.FromMinutes(15));
                return result;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or JsonException
                or KeyNotFoundException or InvalidOperationException or FormatException or ArgumentOutOfRangeException)
            {
                // Do not log exception/URL; customer API keys are query parameters.
                logger.LogWarning("Weather unavailable for {CountryCode}; reason {FailureType}", code, ex.GetType().Name);
                cache.Set(key, fallback, TimeSpan.FromSeconds(60));
                return fallback;
            }
        }
        finally { gate.Release(); }
    }
}
