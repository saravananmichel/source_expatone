using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using ExpatOne.Application.DTOs;
using ExpatOne.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ExpatOne.Tests;

[Collection("Integration")]
public class AuthEndpointTests
{
    private readonly WebApplicationFactory<Program> _factory;

    public AuthEndpointTests(AppFactory factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                var descriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(DbContextOptions<ExpatOneDbContext>));
                if (descriptor != null)
                    services.Remove(descriptor);

                var dbName = $"AuthTests_{Guid.NewGuid()}";
                services.AddDbContext<ExpatOneDbContext>(options =>
                    options.UseInMemoryDatabase(dbName));

                services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = "Test";
                    options.DefaultChallengeScheme = "Test";
                }).AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", null);
            });
        });
    }

    [Fact]
    public async Task GetMe_WithoutAuth_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/users/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PutMe_WithoutAuth_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.PutAsJsonAsync("/api/users/me", new { DisplayName = "Test" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetMe_WithAuth_ReturnsUser()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Test", "uid=user-a&email=a@test.com&name=User+A");

        var response = await client.GetAsync("/api/users/me");
        response.EnsureSuccessStatusCode();

        var user = await response.Content.ReadFromJsonAsync<UserDto>();
        Assert.NotNull(user);
        Assert.Equal("a@test.com", user.Email);
        Assert.Equal("User A", user.DisplayName);
        Assert.Equal("firebase", user.ExternalProvider);
    }

    [Fact]
    public async Task GetMe_IsIdempotent()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Test", "uid=user-idem&email=idem@test.com&name=Idem");

        var r1 = await client.GetAsync("/api/users/me");
        var u1 = await r1.Content.ReadFromJsonAsync<UserDto>();

        var r2 = await client.GetAsync("/api/users/me");
        var u2 = await r2.Content.ReadFromJsonAsync<UserDto>();

        Assert.Equal(u1!.Id, u2!.Id);
    }

    [Fact]
    public async Task PutMe_UpdatesProfile()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Test", "uid=user-put&email=put@test.com&name=Original");

        await client.GetAsync("/api/users/me");

        var response = await client.PutAsJsonAsync("/api/users/me", new
        {
            DisplayName = "Updated",
            PhoneNumber = "+60123456789"
        });
        response.EnsureSuccessStatusCode();

        var updated = await response.Content.ReadFromJsonAsync<UserDto>();
        Assert.Equal("Updated", updated!.DisplayName);
        Assert.Equal("+60123456789", updated.PhoneNumber);
    }

    [Fact]
    public async Task UserA_CannotAccessUserB_Data()
    {
        var client = _factory.CreateClient();

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Test", "uid=user-a-iso&email=a@test.com&name=A");
        var rA = await client.GetAsync("/api/users/me");
        var userA = await rA.Content.ReadFromJsonAsync<UserDto>();

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Test", "uid=user-b-iso&email=b@test.com&name=B");
        var rB = await client.GetAsync("/api/users/me");
        var userB = await rB.Content.ReadFromJsonAsync<UserDto>();

        Assert.NotEqual(userA!.Id, userB!.Id);
        Assert.Equal("a@test.com", userA.Email);
        Assert.Equal("b@test.com", userB.Email);
    }

    [Fact]
    public async Task HealthEndpoint_RemainsPublic()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/health");

        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task OldGetById_NoLongerAccessible()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync($"/api/users/{Guid.NewGuid()}");

        Assert.True(
            response.StatusCode == HttpStatusCode.Unauthorized ||
            response.StatusCode == HttpStatusCode.NotFound,
            $"Expected 401 or 404 but got {response.StatusCode}");
    }

    [Fact]
    public async Task OldPostUser_NoLongerAccessible()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/users", new
        {
            ExternalId = "x",
            Email = "x@test.com"
        });

        Assert.True(
            response.StatusCode == HttpStatusCode.Unauthorized ||
            response.StatusCode == HttpStatusCode.NotFound ||
            response.StatusCode == HttpStatusCode.MethodNotAllowed,
            $"Expected 401/404/405 but got {response.StatusCode}");
    }
}

public class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.ContainsKey("Authorization"))
            return Task.FromResult(AuthenticateResult.NoResult());

        var authHeader = Request.Headers.Authorization.ToString();
        if (!authHeader.StartsWith("Test "))
            return Task.FromResult(AuthenticateResult.NoResult());

        var paramString = authHeader["Test ".Length..];
        var parameters = System.Web.HttpUtility.ParseQueryString(paramString);

        var uid = parameters["uid"];
        if (string.IsNullOrEmpty(uid))
            return Task.FromResult(AuthenticateResult.Fail("Missing uid"));

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, uid),
            new("firebase_uid", uid),
        };

        if (parameters["email"] is { } email)
            claims.Add(new Claim(ClaimTypes.Email, email));
        if (parameters["name"] is { } name)
            claims.Add(new Claim(ClaimTypes.Name, name));

        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, "Test");

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
