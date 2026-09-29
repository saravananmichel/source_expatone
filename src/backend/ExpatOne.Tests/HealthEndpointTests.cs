using System.Net.Http.Json;
using ExpatOne.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ExpatOne.Tests;

[Collection("Integration")]
public class HealthEndpointTests
{
    private readonly WebApplicationFactory<Program> _factory;

    public HealthEndpointTests(AppFactory factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                // Replace PostgreSQL with in-memory for testing
                var descriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(DbContextOptions<ExpatOneDbContext>));
                if (descriptor != null)
                    services.Remove(descriptor);

                services.AddDbContext<ExpatOneDbContext>(options =>
                    options.UseInMemoryDatabase("TestDb"));
            });
        });
    }

    [Fact]
    public async Task HealthEndpoint_ReturnsOk()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/health");

        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"status\"", content);
    }

    [Fact]
    public async Task HealthEndpoint_DoesNotExposeVersion()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/health");
        var content = await response.Content.ReadAsStringAsync();

        Assert.DoesNotContain("version", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task HealthEndpoint_DoesNotExposeDatabaseState()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/health");
        var content = await response.Content.ReadAsStringAsync();

        Assert.DoesNotContain("database", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("connected", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("unavailable", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task HealthEndpoint_ReturnsStatusField()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/health");
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var status = body.GetProperty("status").GetString();
        Assert.True(status == "ok" || status == "degraded",
            $"Expected 'ok' or 'degraded', got '{status}'");
    }
}
