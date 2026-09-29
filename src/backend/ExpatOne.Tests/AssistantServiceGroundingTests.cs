using ExpatOne.Application.DTOs;
using ExpatOne.Application.Interfaces;
using ExpatOne.Domain.Entities;
using ExpatOne.Infrastructure.Persistence;
using ExpatOne.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;

namespace ExpatOne.Tests;

public class AssistantServiceGroundingTests : IDisposable
{
    private readonly ExpatOneDbContext _dbContext;
    private readonly Mock<IAIService> _mockAi;
    private readonly Mock<IKnowledgeSearchService> _mockSearch;
    private readonly Mock<ILogger<AssistantService>> _mockLogger;

    private static readonly Guid TestUserId = Guid.NewGuid();

    public AssistantServiceGroundingTests()
    {
        var options = new DbContextOptionsBuilder<ExpatOneDbContext>()
            .UseInMemoryDatabase($"GroundingTests_{Guid.NewGuid()}")
            .Options;
        _dbContext = new ExpatOneDbContext(options);

        _mockAi = new Mock<IAIService>();
        _mockAi.Setup(a => a.GenerateResponseAsync(It.IsAny<string>(), It.IsAny<AIContext>()))
            .ReturnsAsync(new AIResponse { Content = "Gemini response" });

        _mockSearch = new Mock<IKnowledgeSearchService>();
        _mockLogger = new Mock<ILogger<AssistantService>>();
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    private AssistantService CreateService(double threshold = 0.7)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Assistant:RelevanceThreshold"] = threshold.ToString(),
            })
            .Build();

        return new AssistantService(_dbContext, _mockAi.Object, _mockSearch.Object, config, _mockLogger.Object);
    }

    private async Task<Guid> CreateConversation(AssistantService service)
    {
        var user = new User
        {
            Id = TestUserId,
            ExternalId = "test-ext-id",
            ExternalProvider = "firebase",
            Email = "test@example.com",
        };
        if (!await _dbContext.Users.AnyAsync(u => u.Id == TestUserId))
        {
            _dbContext.Users.Add(user);
            await _dbContext.SaveChangesAsync();
        }

        var conversation = await service.CreateConversationAsync(TestUserId);
        return conversation.Id;
    }

    [Fact]
    public async Task SendMessage_ResultsBelowThreshold_ReturnsGeneralUnverifiedMode()
    {
        _mockSearch.Setup(s => s.SearchAsync(It.IsAny<string>(), "MY", 5))
            .ReturnsAsync(new List<KnowledgeSearchResult>
            {
                new()
                {
                    KnowledgeId = Guid.NewGuid(),
                    Title = "Unrelated Topic",
                    Content = "Some content",
                    CountryCode = "MY",
                    RelevanceScore = 0.65,
                }
            });

        var service = CreateService();
        var conversationId = await CreateConversation(service);

        var result = await service.SendMessageAsync(conversationId, TestUserId, "Test query");

        // MODE 2: Gemini IS called with the unverified prompt when results are below threshold
        _mockAi.Verify(
            a => a.GenerateResponseAsync(It.IsAny<string>(), It.IsAny<AIContext>()),
            Times.Once,
            "Gemini SHOULD be called in GENERAL_UNVERIFIED mode when results are below the threshold");
        Assert.Equal("general_unverified", result.ResponseMode);
        Assert.Equal("Gemini response", result.Content);
        Assert.Null(result.Sources);
    }

    [Fact]
    public async Task SendMessage_ResultsAboveThreshold_CallsGemini()
    {
        _mockSearch.Setup(s => s.SearchAsync(It.IsAny<string>(), "MY", 5))
            .ReturnsAsync(new List<KnowledgeSearchResult>
            {
                new()
                {
                    KnowledgeId = Guid.NewGuid(),
                    Title = "Employment Pass Policy",
                    Content = "Category II minimum salary RM10,000",
                    SourceUrl = "https://example.com",
                    Department = "Immigration",
                    Category = "employment-pass",
                    CountryCode = "MY",
                    RelevanceScore = 0.78,
                }
            });

        var service = CreateService();
        var conversationId = await CreateConversation(service);

        var result = await service.SendMessageAsync(conversationId, TestUserId, "What is EP salary?");

        _mockAi.Verify(
            a => a.GenerateResponseAsync(It.IsAny<string>(), It.IsAny<AIContext>()),
            Times.Once,
            "Gemini SHOULD be called when results are above the threshold");
        Assert.Equal("Gemini response", result.Content);
    }

    [Fact]
    public async Task SendMessage_ZeroResults_ReturnsGeneralUnverifiedMode()
    {
        _mockSearch.Setup(s => s.SearchAsync(It.IsAny<string>(), "MY", 5))
            .ReturnsAsync(new List<KnowledgeSearchResult>());

        var service = CreateService();
        var conversationId = await CreateConversation(service);

        var result = await service.SendMessageAsync(conversationId, TestUserId, "Completely unknown topic");

        // MODE 2: Gemini IS called even with zero search results
        _mockAi.Verify(
            a => a.GenerateResponseAsync(It.IsAny<string>(), It.IsAny<AIContext>()),
            Times.Once,
            "Gemini SHOULD be called in GENERAL_UNVERIFIED mode when there are zero search results");
        Assert.Equal("general_unverified", result.ResponseMode);
        Assert.Equal("Gemini response", result.Content);
        Assert.Null(result.Sources);
    }

    [Fact]
    public async Task SendMessage_MixedRelevance_OnlyPassesRelevantToGemini()
    {
        _mockSearch.Setup(s => s.SearchAsync(It.IsAny<string>(), "MY", 5))
            .ReturnsAsync(new List<KnowledgeSearchResult>
            {
                new()
                {
                    KnowledgeId = Guid.NewGuid(),
                    Title = "Relevant Result",
                    Content = "Good content",
                    SourceUrl = "https://example.com/relevant",
                    Department = "Immigration",
                    CountryCode = "MY",
                    RelevanceScore = 0.85,
                },
                new()
                {
                    KnowledgeId = Guid.NewGuid(),
                    Title = "Irrelevant Result",
                    Content = "Off-topic content",
                    SourceUrl = "https://example.com/irrelevant",
                    Department = "Transport",
                    CountryCode = "MY",
                    RelevanceScore = 0.25,
                }
            });

        var service = CreateService(threshold: 0.7);
        var conversationId = await CreateConversation(service);

        var result = await service.SendMessageAsync(conversationId, TestUserId, "EP salary question");

        _mockAi.Verify(
            a => a.GenerateResponseAsync(It.IsAny<string>(), It.IsAny<AIContext>()),
            Times.Once);
        Assert.NotNull(result.Sources);
        Assert.Single(result.Sources!);
        Assert.Equal("Relevant Result", result.Sources![0].Title);
    }

    [Fact]
    public async Task SendMessage_AllResultsBelowHighThreshold_ReturnsGeneralUnverifiedMode()
    {
        _mockSearch.Setup(s => s.SearchAsync(It.IsAny<string>(), "MY", 5))
            .ReturnsAsync(new List<KnowledgeSearchResult>
            {
                new()
                {
                    KnowledgeId = Guid.NewGuid(),
                    Title = "Motorcycle Driving Test",
                    Content = "Malaysian driving test procedure for motorcycles",
                    CountryCode = "MY",
                    RelevanceScore = 0.613,
                },
                new()
                {
                    KnowledgeId = Guid.NewGuid(),
                    Title = "General Transport Info",
                    Content = "Transport information",
                    CountryCode = "MY",
                    RelevanceScore = 0.55,
                }
            });

        var service = CreateService(threshold: 0.7);
        var conversationId = await CreateConversation(service);

        var result = await service.SendMessageAsync(conversationId, TestUserId,
            "What is the Malaysian driving test procedure for motorcycles?");

        // MODE 2: results exist but all below 0.7 → GENERAL_UNVERIFIED, Gemini still called
        _mockAi.Verify(
            a => a.GenerateResponseAsync(It.IsAny<string>(), It.IsAny<AIContext>()),
            Times.Once,
            "Gemini SHOULD be called in GENERAL_UNVERIFIED mode when all results (0.613, 0.55) are below threshold 0.7");
        Assert.Equal("general_unverified", result.ResponseMode);
    }

    [Fact]
    public async Task SendMessage_ThresholdIsConfigurable()
    {
        var searchResult = new KnowledgeSearchResult
        {
            KnowledgeId = Guid.NewGuid(),
            Title = "Test Result",
            Content = "Test content",
            SourceUrl = "https://example.com",
            Department = "Test",
            CountryCode = "MY",
            RelevanceScore = 0.65,
        };

        _mockSearch.Setup(s => s.SearchAsync(It.IsAny<string>(), "MY", 5))
            .ReturnsAsync(new List<KnowledgeSearchResult> { searchResult });

        var lowThresholdService = CreateService(threshold: 0.5);
        var conversationId1 = await CreateConversation(lowThresholdService);
        await lowThresholdService.SendMessageAsync(conversationId1, TestUserId, "Query A");

        _mockAi.Verify(
            a => a.GenerateResponseAsync(It.IsAny<string>(), It.IsAny<AIContext>()),
            Times.Once,
            "Score 0.65 should pass threshold 0.5");

        _mockAi.Reset();
        _mockAi.Setup(a => a.GenerateResponseAsync(It.IsAny<string>(), It.IsAny<AIContext>()))
            .ReturnsAsync(new AIResponse { Content = "Gemini response" });

        var highThresholdService = CreateService(threshold: 0.7);
        var conversationId2 = await CreateConversation(highThresholdService);
        await highThresholdService.SendMessageAsync(conversationId2, TestUserId, "Query B");

        // MODE 2: score 0.65 < threshold 0.7 → GENERAL_UNVERIFIED, Gemini still called once
        _mockAi.Verify(
            a => a.GenerateResponseAsync(It.IsAny<string>(), It.IsAny<AIContext>()),
            Times.Once,
            "Score 0.65 should NOT pass threshold 0.7 — triggers GENERAL_UNVERIFIED, Gemini called once");
    }

    [Fact]
    public async Task SendMessage_E2E_RelevantEPQuery_GeminiCalled()
    {
        _mockSearch.Setup(s => s.SearchAsync(It.IsAny<string>(), "MY", 5))
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
                    RelevanceScore = 0.780491606490603,
                }
            });

        var service = CreateService();
        var conversationId = await CreateConversation(service);

        var result = await service.SendMessageAsync(conversationId, TestUserId,
            "What is the minimum salary for Employment Pass Category II?");

        _mockAi.Verify(
            a => a.GenerateResponseAsync(It.IsAny<string>(), It.IsAny<AIContext>()),
            Times.Once,
            "EP query with score 0.780 should pass default threshold 0.7 and call Gemini");
        Assert.Equal("official_grounded", result.ResponseMode);
        Assert.NotNull(result.Sources);
        Assert.Single(result.Sources!);
        Assert.Equal("Employment Pass Salary Policy Effective 1 June 2026", result.Sources![0].Title);
    }

    [Fact]
    public async Task SendMessage_E2E_IrrelevantMotorcycleQuery_ReturnsGeneralUnverifiedMode()
    {
        _mockSearch.Setup(s => s.SearchAsync(It.IsAny<string>(), "MY", 5))
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
                    RelevanceScore = 0.6131554389477418,
                }
            });

        var service = CreateService();
        var conversationId = await CreateConversation(service);

        var result = await service.SendMessageAsync(conversationId, TestUserId,
            "What is the Malaysian driving test procedure for motorcycles?");

        // MODE 2: score 0.613 < threshold 0.7 → GENERAL_UNVERIFIED, Gemini called once
        _mockAi.Verify(
            a => a.GenerateResponseAsync(It.IsAny<string>(), It.IsAny<AIContext>()),
            Times.Once,
            "Motorcycle query with score 0.613 should NOT pass threshold 0.7 — triggers GENERAL_UNVERIFIED");
        Assert.Equal("general_unverified", result.ResponseMode);
        Assert.Null(result.Sources);
    }

    [Fact]
    public async Task SendMessage_ExactThresholdBoundary_ResultAtThresholdPassesGate()
    {
        _mockSearch.Setup(s => s.SearchAsync(It.IsAny<string>(), "MY", 5))
            .ReturnsAsync(new List<KnowledgeSearchResult>
            {
                new()
                {
                    KnowledgeId = Guid.NewGuid(),
                    Title = "Boundary Result",
                    Content = "Content at exact threshold",
                    SourceUrl = "https://example.com",
                    Department = "Test",
                    CountryCode = "MY",
                    RelevanceScore = 0.7,
                }
            });

        var service = CreateService(threshold: 0.7);
        var conversationId = await CreateConversation(service);

        await service.SendMessageAsync(conversationId, TestUserId, "Boundary test");

        _mockAi.Verify(
            a => a.GenerateResponseAsync(It.IsAny<string>(), It.IsAny<AIContext>()),
            Times.Once,
            "Score exactly at threshold (>=) should pass the gate");
    }
}
