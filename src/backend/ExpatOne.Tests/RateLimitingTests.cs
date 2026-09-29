using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ExpatOne.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ExpatOne.Tests;

/// <summary>
/// Verifies that per-user AI rate limiting works correctly:
/// - Normal requests succeed.
/// - After exceeding the limit, the same user receives 429.
/// - A different user is not affected by the first user's limit.
/// - Emergency CALL 999 has no backend endpoint (native) — only /assist is limited.
/// - Health endpoint is never rate-limited.
///
/// Test-friendly config overrides the limit to 3 requests / 60 s so tests run quickly.
/// </summary>
[Collection("Integration")]
public class RateLimitingTests : IClassFixture<RateLimitAppFactory>
{
    private readonly RateLimitAppFactory _factory;

    public RateLimitingTests(RateLimitAppFactory factory)
    {
        _factory = factory;
    }

    private HttpClient AuthClient(string uid) =>
        _factory.CreateAuthClient($"uid={uid}&email={uid}@test.com&name=User{uid}");

    // ── 1. Normal request succeeds ────────────────────────────────────────────

    [Fact]
    public async Task Translation_WithinLimit_Returns200Or503()
    {
        var client = AuthClient("rl-user-a");
        var response = await client.PostAsJsonAsync("/api/translation/translate",
            new { text = "hello", sourceLanguage = "auto", targetLanguage = "ms" });

        // 503 = Gemini not configured in test env; 200 = success — both mean the
        // rate limiter allowed the request through.
        Assert.True(
            response.StatusCode == HttpStatusCode.OK ||
            response.StatusCode == HttpStatusCode.ServiceUnavailable,
            $"Expected 200/503 but got {(int)response.StatusCode}");
    }

    // ── 2. Exceeding limit → 429 ─────────────────────────────────────────────

    [Fact]
    public async Task Translation_ExceedsLimit_Returns429()
    {
        var client = AuthClient("rl-limit-user");

        // Test config sets limit to 3 per 60 s. Send 4 requests; the 4th must be 429.
        HttpStatusCode? lastStatus = null;
        for (var i = 0; i < 4; i++)
        {
            var response = await client.PostAsJsonAsync("/api/translation/translate",
                new { text = "hello", sourceLanguage = "auto", targetLanguage = "ms" });
            lastStatus = response.StatusCode;
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, lastStatus);
    }

    // ── 3. Different users have independent limits ─────────────────────────────

    [Fact]
    public async Task Translation_UserA_LimitDoesNotAffectUserB()
    {
        var clientA = AuthClient("rl-user-a2");
        var clientB = AuthClient("rl-user-b2");

        // Exhaust user A (4 requests)
        for (var i = 0; i < 4; i++)
            await clientA.PostAsJsonAsync("/api/translation/translate",
                new { text = "hello", sourceLanguage = "auto", targetLanguage = "ms" });

        // User B should still get through (200 or 503, not 429)
        var bResponse = await clientB.PostAsJsonAsync("/api/translation/translate",
            new { text = "hello", sourceLanguage = "auto", targetLanguage = "ms" });

        Assert.NotEqual(HttpStatusCode.TooManyRequests, bResponse.StatusCode);
    }

    // ── 4. Health endpoint is never rate-limited ──────────────────────────────

    [Fact]
    public async Task Health_IsNeverRateLimited()
    {
        var client = _factory.CreateClient(); // no auth needed
        for (var i = 0; i < 10; i++)
        {
            var response = await client.GetAsync("/api/health");
            Assert.NotEqual(HttpStatusCode.TooManyRequests, response.StatusCode);
        }
    }

    // ── 5. Emergency AI endpoint is rate-limited ──────────────────────────────

    [Fact]
    public async Task EmergencyAssist_ExceedsLimit_Returns429()
    {
        var client = AuthClient("rl-emergency-user");

        HttpStatusCode? lastStatus = null;
        for (var i = 0; i < 4; i++)
        {
            var response = await client.PostAsJsonAsync("/api/emergency/assist",
                new { message = "I need help", targetLanguage = "en" });
            lastStatus = response.StatusCode;
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, lastStatus);
    }

    // ── 6. Assistant messages endpoint is rate-limited ────────────────────────

    [Fact]
    public async Task AssistantMessages_ExceedsLimit_Returns429()
    {
        var client = AuthClient("rl-assistant-user");
        var fakeConversationId = Guid.NewGuid();

        HttpStatusCode? lastStatus = null;
        for (var i = 0; i < 4; i++)
        {
            var response = await client.PostAsJsonAsync(
                $"/api/assistant/conversations/{fakeConversationId}/messages",
                new { message = "hello" });
            lastStatus = response.StatusCode;
        }

        // 404 (conversation not found) or 503 (Gemini unconfigured) are fine up to limit.
        // At limit we expect 429.
        Assert.Equal(HttpStatusCode.TooManyRequests, lastStatus);
    }

    // ── 7. Document analyze endpoint is rate-limited ──────────────────────────

    [Fact]
    public async Task DocumentAnalyze_ExceedsLimit_Returns429()
    {
        var client = AuthClient("rl-docanalyze-user");
        var fakeDocId = Guid.NewGuid();

        HttpStatusCode? lastStatus = null;
        for (var i = 0; i < 4; i++)
        {
            var response = await client.PostAsJsonAsync(
                $"/api/documents/{fakeDocId}/analyze", new { });
            lastStatus = response.StatusCode;
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, lastStatus);
    }

    // ── 8. 429 response includes Retry-After header ───────────────────────────

    [Fact]
    public async Task RateLimited_Response_IncludesRetryAfterHeader()
    {
        var client = AuthClient("rl-header-user");

        HttpResponseMessage? last = null;
        for (var i = 0; i < 4; i++)
            last = await client.PostAsJsonAsync("/api/translation/translate",
                new { text = "hello", sourceLanguage = "auto", targetLanguage = "ms" });

        Assert.Equal(HttpStatusCode.TooManyRequests, last!.StatusCode);
        Assert.True(last.Headers.Contains("Retry-After"),
            "Expected Retry-After header on 429 response");
    }
}

// ── Test factory with tight rate limit ────────────────────────────────────────

public class RateLimitAppFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        builder.UseSetting("RateLimiting:AiEndpoints:PermitLimit", "3");
        builder.UseSetting("RateLimiting:AiEndpoints:WindowSeconds", "60");
        builder.UseSetting("RateLimiting:EmergencyAi:PermitLimit", "3");
        builder.UseSetting("RateLimiting:EmergencyAi:WindowSeconds", "60");

        builder.ConfigureServices(services =>
        {
            // Replace PostgreSQL with in-memory for tests
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<ExpatOneDbContext>));
            if (descriptor != null) services.Remove(descriptor);
            services.AddDbContext<ExpatOneDbContext>(o =>
                o.UseInMemoryDatabase("RateLimitTestDb"));

            // Replace Firebase auth with test handler
            var authDescriptors = services
                .Where(d => d.ServiceType.Name.Contains("Authentication") ||
                            d.ImplementationType?.Name.Contains("Firebase") == true)
                .ToList();

            services.AddAuthentication("Test")
                .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions,
                    TestAuthHandler>("Test", null);
        });
    }

    public HttpClient CreateAuthClient(string authHeader)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Test", authHeader);
        return client;
    }
}
