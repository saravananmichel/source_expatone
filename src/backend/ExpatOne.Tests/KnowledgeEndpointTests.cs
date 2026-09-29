using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ExpatOne.Application.DTOs;
using ExpatOne.Application.Interfaces;
using ExpatOne.Infrastructure.Persistence;
using ExpatOne.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace ExpatOne.Tests;

[Collection("Integration")]
public class KnowledgeEndpointTests
{
    private const string AdminKey = "test-admin-key-12345";
    private readonly WebApplicationFactory<Program> _factory;

    public KnowledgeEndpointTests(AppFactory factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                var descriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(DbContextOptions<ExpatOneDbContext>));
                if (descriptor != null)
                    services.Remove(descriptor);

                var dbName = $"KnowledgeTests_{Guid.NewGuid()}";
                services.AddDbContext<ExpatOneDbContext>(options =>
                    options.UseInMemoryDatabase(dbName));

                services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = "Test";
                    options.DefaultChallengeScheme = "Test";
                }).AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", null);

                var mockAi = new Mock<IAIService>();
                mockAi.Setup(a => a.GenerateEmbeddingAsync(It.IsAny<string>()))
                    .ReturnsAsync(new float[768]);
                services.AddSingleton(mockAi.Object);

                services.AddScoped<IKnowledgeIngestionService, KnowledgeIngestionService>();

                var mockSearch = new Mock<IKnowledgeSearchService>();
                mockSearch.Setup(s => s.SearchAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()))
                    .ReturnsAsync(new List<KnowledgeSearchResult>());
                services.AddSingleton(mockSearch.Object);
            });

            builder.ConfigureAppConfiguration((context, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Knowledge:AdminKey"] = AdminKey,
                });
            });
        });
    }

    [Fact]
    public async Task Search_RequiresAuth()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/knowledge/search?q=test");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Search_WithoutQuery_Returns400()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Test", "uid=search-user&email=s@test.com&name=Searcher");

        var response = await client.GetAsync("/api/knowledge/search");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Search_Authenticated_Returns200()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Test", "uid=search-user-2&email=s2@test.com&name=Searcher");

        var response = await client.GetAsync("/api/knowledge/search?q=employment+pass");
        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task AdminEndpoint_WithoutAdminKey_Returns403()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Test", "uid=user-1&email=u@test.com&name=User");

        var response = await client.GetAsync("/api/knowledge/sources");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AdminEndpoint_WrongAdminKey_Returns403()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Test", "uid=user-2&email=u2@test.com&name=User");
        client.DefaultRequestHeaders.Add("X-Admin-Key", "wrong-key");

        var response = await client.GetAsync("/api/knowledge/sources");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AdminEndpoint_CorrectAdminKey_Succeeds()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Test", "uid=admin-1&email=admin@test.com&name=Admin");
        client.DefaultRequestHeaders.Add("X-Admin-Key", AdminKey);

        var response = await client.GetAsync("/api/knowledge/sources");
        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task RegisterSource_Succeeds()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Test", "uid=admin-2&email=admin2@test.com&name=Admin");
        client.DefaultRequestHeaders.Add("X-Admin-Key", AdminKey);

        var response = await client.PostAsJsonAsync("/api/knowledge/sources", new
        {
            Name = "Immigration Dept",
            Url = "https://www.imi.gov.my",
            Department = "Immigration",
        });
        response.EnsureSuccessStatusCode();

        var dto = await response.Content.ReadFromJsonAsync<GovernmentSourceDto>();
        Assert.NotNull(dto);
        Assert.Equal("Immigration Dept", dto!.Name);
    }

    [Fact]
    public async Task IngestSource_EmptyContent_Returns400()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Test", "uid=admin-3&email=admin3@test.com&name=Admin");
        client.DefaultRequestHeaders.Add("X-Admin-Key", AdminKey);

        var sourceResponse = await client.PostAsJsonAsync("/api/knowledge/sources", new
        {
            Name = "Test Source",
        });
        var source = await sourceResponse.Content.ReadFromJsonAsync<GovernmentSourceDto>();

        var response = await client.PostAsJsonAsync($"/api/knowledge/sources/{source!.Id}/ingest", new
        {
            Title = "Test",
            Content = "",
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task DeactivateSource_NonExistent_Returns404()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Test", "uid=admin-4&email=admin4@test.com&name=Admin");
        client.DefaultRequestHeaders.Add("X-Admin-Key", AdminKey);

        var response = await client.DeleteAsync($"/api/knowledge/sources/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GenerateEmbeddings_Succeeds()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Test", "uid=admin-5&email=admin5@test.com&name=Admin");
        client.DefaultRequestHeaders.Add("X-Admin-Key", AdminKey);

        var response = await client.PostAsync("/api/knowledge/embeddings/generate", null);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<EmbeddingGenerationResultDto>();
        Assert.NotNull(result);
    }
}
