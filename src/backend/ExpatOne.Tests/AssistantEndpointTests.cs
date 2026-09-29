using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ExpatOne.Application.Common;
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
public class AssistantEndpointTests
{
    private readonly WebApplicationFactory<Program> _factory;

    public AssistantEndpointTests(AppFactory factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                var descriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(DbContextOptions<ExpatOneDbContext>));
                if (descriptor != null)
                    services.Remove(descriptor);

                var dbName = $"AssistantTests_{Guid.NewGuid()}";
                services.AddDbContext<ExpatOneDbContext>(options =>
                    options.UseInMemoryDatabase(dbName));

                services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = "Test";
                    options.DefaultChallengeScheme = "Test";
                }).AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", null);

                var mockAi = new Mock<IAIService>();
                mockAi.Setup(a => a.GenerateResponseAsync(It.IsAny<string>(), It.IsAny<AIContext>()))
                    .ReturnsAsync(new AIResponse
                    {
                        Content = "Based on the Employment Pass Salary Policy, the minimum salary for Category II is RM10,000 - RM19,999."
                    });
                mockAi.Setup(a => a.GenerateEmbeddingAsync(It.IsAny<string>()))
                    .ReturnsAsync(new float[768]);
                services.AddSingleton(mockAi.Object);

                var mockSearch = new Mock<IKnowledgeSearchService>();
                mockSearch.Setup(s => s.SearchAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()))
                    .ReturnsAsync(new List<KnowledgeSearchResult>
                    {
                        new()
                        {
                            KnowledgeId = Guid.NewGuid(),
                            Title = "Employment Pass Salary Policy Effective 1 June 2026",
                            Content = "Category II: Revised minimum salary RM10,000 - RM19,999.",
                            SourceUrl = "https://esd.imi.gov.my/portal/latest-news/announcement/announcement-266-ep-salary-policy-2026/",
                            SourceName = "ESD Immigration",
                            Department = "Immigration Department of Malaysia",
                            Category = "employment-pass",
                            CountryCode = "MY",
                            RelevanceScore = 0.78,
                        }
                    });
                services.AddSingleton(mockSearch.Object);

                services.AddScoped<IAssistantService, AssistantService>();
            });

            builder.ConfigureAppConfiguration((context, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Assistant:RelevanceThreshold"] = "0.7",
                    ["Firebase:ProjectId"] = "",
                    ["Firebase:CredentialPath"] = "",
                });
            });
        });
    }

    private HttpClient CreateAuthClient(string uid, string email = "test@example.com", string name = "Test")
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Test", $"uid={uid}&email={email}&name={name}");
        return client;
    }

    [Fact]
    public async Task CreateConversation_RequiresAuth()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/assistant/conversations", new { });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateConversation_Authenticated_Succeeds()
    {
        var client = CreateAuthClient("assistant-user-1");
        var response = await client.PostAsJsonAsync("/api/assistant/conversations", new { countryCode = "MY" });
        response.EnsureSuccessStatusCode();

        var dto = await response.Content.ReadFromJsonAsync<ConversationDto>();
        Assert.NotNull(dto);
        Assert.NotEqual(Guid.Empty, dto!.Id);
        Assert.Equal("government-assistant", dto.Module);
    }

    [Fact]
    public async Task GetConversations_ReturnsUserConversations()
    {
        var client = CreateAuthClient("assistant-user-2");

        await client.PostAsJsonAsync("/api/assistant/conversations", new { });
        await client.PostAsJsonAsync("/api/assistant/conversations", new { });

        var response = await client.GetAsync("/api/assistant/conversations");
        response.EnsureSuccessStatusCode();

        var conversations = await response.Content.ReadFromJsonAsync<List<ConversationSummaryDto>>();
        Assert.NotNull(conversations);
        Assert.True(conversations!.Count >= 2);
    }

    [Fact]
    public async Task GetConversation_NotFound_Returns404()
    {
        var client = CreateAuthClient("assistant-user-3");
        var response = await client.GetAsync($"/api/assistant/conversations/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetConversation_WrongUser_Returns401()
    {
        var ownerClient = CreateAuthClient("owner-user-1");
        var createResponse = await ownerClient.PostAsJsonAsync("/api/assistant/conversations", new { });
        var conversation = await createResponse.Content.ReadFromJsonAsync<ConversationDto>();

        var otherClient = CreateAuthClient("other-user-1");
        var response = await otherClient.GetAsync($"/api/assistant/conversations/{conversation!.Id}");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task SendMessage_EmptyMessage_Returns400()
    {
        var client = CreateAuthClient("assistant-user-4");
        var createResponse = await client.PostAsJsonAsync("/api/assistant/conversations", new { });
        var conversation = await createResponse.Content.ReadFromJsonAsync<ConversationDto>();

        var response = await client.PostAsJsonAsync(
            $"/api/assistant/conversations/{conversation!.Id}/messages",
            new { message = "" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task SendMessage_ReturnsAssistantResponseWithSources()
    {
        var client = CreateAuthClient("assistant-user-5");
        var createResponse = await client.PostAsJsonAsync("/api/assistant/conversations", new { });
        var conversation = await createResponse.Content.ReadFromJsonAsync<ConversationDto>();

        var response = await client.PostAsJsonAsync(
            $"/api/assistant/conversations/{conversation!.Id}/messages",
            new { message = "What is the minimum salary for EP Category II?" });
        response.EnsureSuccessStatusCode();

        var msg = await response.Content.ReadFromJsonAsync<AssistantMessageDto>();
        Assert.NotNull(msg);
        Assert.Equal("assistant", msg!.Role);
        Assert.Contains("RM10,000", msg.Content);
        Assert.NotNull(msg.Sources);
        Assert.NotEmpty(msg.Sources!);
        Assert.Equal("Employment Pass Salary Policy Effective 1 June 2026", msg.Sources![0].Title);
        Assert.Equal("Immigration Department of Malaysia", msg.Sources![0].Department);
    }

    [Fact]
    public async Task SendMessage_NoRelevantKnowledge_ReturnsSafeResponse()
    {
        var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                var emptySearch = new Mock<IKnowledgeSearchService>();
                emptySearch.Setup(s => s.SearchAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()))
                    .ReturnsAsync(new List<KnowledgeSearchResult>());
                services.AddSingleton(emptySearch.Object);
            });
        });

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Test", "uid=no-context-user&email=nc@test.com&name=NC");

        var createResponse = await client.PostAsJsonAsync("/api/assistant/conversations", new { });
        var conversation = await createResponse.Content.ReadFromJsonAsync<ConversationDto>();

        var response = await client.PostAsJsonAsync(
            $"/api/assistant/conversations/{conversation!.Id}/messages",
            new { message = "What is the motorcycle driving test procedure?" });
        response.EnsureSuccessStatusCode();

        var msg = await response.Content.ReadFromJsonAsync<AssistantMessageDto>();
        Assert.NotNull(msg);
        // MODE 2: GENERAL_UNVERIFIED — Gemini is called with the unverified prompt
        Assert.Equal("general_unverified", msg!.ResponseMode);
        Assert.Null(msg.Sources);
    }

    [Fact]
    public async Task SendMessage_SetsConversationTitle()
    {
        var client = CreateAuthClient("assistant-user-6");
        var createResponse = await client.PostAsJsonAsync("/api/assistant/conversations", new { });
        var conversation = await createResponse.Content.ReadFromJsonAsync<ConversationDto>();
        Assert.Null(conversation!.Title);

        await client.PostAsJsonAsync(
            $"/api/assistant/conversations/{conversation.Id}/messages",
            new { message = "What is the minimum salary for EP Category II?" });

        var getResponse = await client.GetAsync($"/api/assistant/conversations/{conversation.Id}");
        var updated = await getResponse.Content.ReadFromJsonAsync<ConversationDto>();
        Assert.NotNull(updated!.Title);
        Assert.Contains("minimum salary", updated.Title!);
    }

    [Fact]
    public async Task SendMessage_WrongUser_Returns401()
    {
        var ownerClient = CreateAuthClient("owner-user-2");
        var createResponse = await ownerClient.PostAsJsonAsync("/api/assistant/conversations", new { });
        var conversation = await createResponse.Content.ReadFromJsonAsync<ConversationDto>();

        var otherClient = CreateAuthClient("other-user-2");
        var response = await otherClient.PostAsJsonAsync(
            $"/api/assistant/conversations/{conversation!.Id}/messages",
            new { message = "Test" });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task DeleteConversation_Succeeds()
    {
        var client = CreateAuthClient("assistant-user-7");
        var createResponse = await client.PostAsJsonAsync("/api/assistant/conversations", new { });
        var conversation = await createResponse.Content.ReadFromJsonAsync<ConversationDto>();

        var deleteResponse = await client.DeleteAsync($"/api/assistant/conversations/{conversation!.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var getResponse = await client.GetAsync($"/api/assistant/conversations/{conversation.Id}");
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    [Fact]
    public async Task DeleteConversation_WrongUser_Returns401()
    {
        var ownerClient = CreateAuthClient("owner-user-3");
        var createResponse = await ownerClient.PostAsJsonAsync("/api/assistant/conversations", new { });
        var conversation = await createResponse.Content.ReadFromJsonAsync<ConversationDto>();

        var otherClient = CreateAuthClient("other-user-3");
        var response = await otherClient.DeleteAsync($"/api/assistant/conversations/{conversation!.Id}");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetConversation_WithMessages_ReturnsAll()
    {
        var client = CreateAuthClient("assistant-user-8");
        var createResponse = await client.PostAsJsonAsync("/api/assistant/conversations", new { });
        var conversation = await createResponse.Content.ReadFromJsonAsync<ConversationDto>();

        await client.PostAsJsonAsync(
            $"/api/assistant/conversations/{conversation!.Id}/messages",
            new { message = "First question" });

        var getResponse = await client.GetAsync($"/api/assistant/conversations/{conversation.Id}");
        var result = await getResponse.Content.ReadFromJsonAsync<ConversationDto>();

        Assert.NotNull(result);
        Assert.True(result!.Messages.Count >= 2);

        var userMsg = result.Messages.FirstOrDefault(m => m.Role == "user");
        Assert.NotNull(userMsg);
        Assert.Equal("First question", userMsg!.Content);

        var assistantMsg = result.Messages.FirstOrDefault(m => m.Role == "assistant");
        Assert.NotNull(assistantMsg);
    }

    [Fact]
    public async Task SendMessage_AIProviderUnavailable_Returns503()
    {
        var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                var mockAi = new Mock<IAIService>();
                mockAi.Setup(a => a.GenerateResponseAsync(It.IsAny<string>(), It.IsAny<AIContext>()))
                    .ThrowsAsync(new AIProviderUnavailableException(
                        "The Government Assistant is temporarily unavailable. Please try again in a moment."));
                mockAi.Setup(a => a.GenerateEmbeddingAsync(It.IsAny<string>()))
                    .ReturnsAsync(new float[768]);
                services.AddSingleton(mockAi.Object);
            });
        });

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Test", "uid=unavail-user&email=unavail@test.com&name=Unavail");

        var createResponse = await client.PostAsJsonAsync("/api/assistant/conversations", new { });
        var conversation = await createResponse.Content.ReadFromJsonAsync<ConversationDto>();

        var response = await client.PostAsJsonAsync(
            $"/api/assistant/conversations/{conversation!.Id}/messages",
            new { message = "What is the EP salary?" });

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        Assert.NotNull(body);
        Assert.Contains("temporarily unavailable", body!["message"]);
    }
}
