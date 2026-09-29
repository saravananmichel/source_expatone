using ExpatOne.Application.Common;
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

public class AssistantServiceModeTests : IDisposable
{
    private readonly ExpatOneDbContext _dbContext;
    private readonly Mock<IAIService> _mockAi;
    private readonly Mock<IKnowledgeSearchService> _mockSearch;
    private readonly Mock<ILogger<AssistantService>> _mockLogger;

    private static readonly Guid TestUserId = Guid.NewGuid();

    public AssistantServiceModeTests()
    {
        var options = new DbContextOptionsBuilder<ExpatOneDbContext>()
            .UseInMemoryDatabase($"ModeTests_{Guid.NewGuid()}")
            .Options;
        _dbContext = new ExpatOneDbContext(options);

        _mockAi = new Mock<IAIService>();
        _mockAi.Setup(a => a.GenerateResponseAsync(It.IsAny<string>(), It.IsAny<AIContext>()))
            .ReturnsAsync(new AIResponse { Content = "Gemini response" });

        _mockSearch = new Mock<IKnowledgeSearchService>();
        _mockLogger = new Mock<ILogger<AssistantService>>();
    }

    public void Dispose() => _dbContext.Dispose();

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
            ExternalId = "mode-test-ext-id",
            ExternalProvider = "firebase",
            Email = "mode@example.com",
        };
        if (!await _dbContext.Users.AnyAsync(u => u.Id == TestUserId))
        {
            _dbContext.Users.Add(user);
            await _dbContext.SaveChangesAsync();
        }
        var conversation = await service.CreateConversationAsync(TestUserId);
        return conversation.Id;
    }

    // ---- CASUAL detection unit tests ----

    [Theory]
    [InlineData("hello")]
    [InlineData("Hello")]
    [InlineData("HELLO")]
    [InlineData("hi")]
    [InlineData("hey")]
    [InlineData("thanks")]
    [InlineData("thank you")]
    [InlineData("good morning")]
    [InlineData("good afternoon")]
    [InlineData("good evening")]
    [InlineData("bye")]
    [InlineData("help")]
    [InlineData("ok")]
    [InlineData("okay")]
    public void IsCasualMessage_ExactMatches_ReturnsTrue(string message)
    {
        Assert.True(AssistantService.IsCasualMessage(message));
    }

    [Theory]
    [InlineData("hello there")]
    [InlineData("hello!")]
    [InlineData("hey, can you help?")]
    [InlineData("thanks for that")]
    [InlineData("what can you do")]
    [InlineData("what can you help with")]
    [InlineData("who are you")]
    [InlineData("what are you")]
    [InlineData("how can you help")]
    public void IsCasualMessage_RegexMatches_ReturnsTrue(string message)
    {
        Assert.True(AssistantService.IsCasualMessage(message));
    }

    [Theory]
    [InlineData("What is the minimum salary for Employment Pass Category II?")]
    [InlineData("What documents do I need for a Dependent Pass?")]
    [InlineData("How do I apply for a work permit?")]
    [InlineData("What is the motorcycle driving test procedure?")]
    [InlineData("help me understand my Employment Pass requirements")]
    public void IsCasualMessage_SubstantiveQuestions_ReturnsFalse(string message)
    {
        Assert.False(AssistantService.IsCasualMessage(message));
    }

    // ---- CASUAL mode: sends to Gemini without KB search ----

    [Fact]
    public async Task SendMessage_Casual_Hello_SkipsKBSearch_ReturnsCasualMode()
    {
        var service = CreateService();
        var conversationId = await CreateConversation(service);

        var result = await service.SendMessageAsync(conversationId, TestUserId, "hello");

        _mockSearch.Verify(
            s => s.SearchAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()),
            Times.Never, "KB search must not be called for casual messages");
        _mockAi.Verify(
            a => a.GenerateResponseAsync(It.IsAny<string>(), It.IsAny<AIContext>()),
            Times.Once);
        Assert.Equal("casual", result.ResponseMode);
        Assert.Null(result.Sources);
    }

    [Fact]
    public async Task SendMessage_Casual_ThankYou_ReturnsCasualMode()
    {
        var service = CreateService();
        var conversationId = await CreateConversation(service);

        var result = await service.SendMessageAsync(conversationId, TestUserId, "thank you");

        _mockSearch.Verify(
            s => s.SearchAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()),
            Times.Never);
        Assert.Equal("casual", result.ResponseMode);
    }

    [Fact]
    public async Task SendMessage_Casual_WhatCanYouDo_ReturnsCasualMode()
    {
        var service = CreateService();
        var conversationId = await CreateConversation(service);

        var result = await service.SendMessageAsync(conversationId, TestUserId, "what can you do");

        _mockSearch.Verify(
            s => s.SearchAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()),
            Times.Never);
        Assert.Equal("casual", result.ResponseMode);
    }

    [Fact]
    public async Task SendMessage_Casual_GoodMorning_ReturnsCasualMode()
    {
        var service = CreateService();
        var conversationId = await CreateConversation(service);

        var result = await service.SendMessageAsync(conversationId, TestUserId, "good morning");

        _mockSearch.Verify(
            s => s.SearchAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()),
            Times.Never);
        Assert.Equal("casual", result.ResponseMode);
    }

    [Fact]
    public async Task SendMessage_Casual_HelloWithPunctuation_ReturnsCasualMode()
    {
        var service = CreateService();
        var conversationId = await CreateConversation(service);

        var result = await service.SendMessageAsync(conversationId, TestUserId, "hello!");

        _mockSearch.Verify(
            s => s.SearchAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()),
            Times.Never);
        Assert.Equal("casual", result.ResponseMode);
    }

    // ---- CASUAL mode uses CasualSystemPrompt, not STRICT RULES ----

    [Fact]
    public async Task SendMessage_Casual_UsesLightweightPrompt_NotGroundedPrompt()
    {
        AIContext? capturedContext = null;
        _mockAi.Setup(a => a.GenerateResponseAsync(It.IsAny<string>(), It.IsAny<AIContext>()))
            .Callback<string, AIContext?>((_, ctx) => capturedContext = ctx)
            .ReturnsAsync(new AIResponse { Content = "Hi!" });

        var service = CreateService();
        var conversationId = await CreateConversation(service);

        await service.SendMessageAsync(conversationId, TestUserId, "hello");

        Assert.NotNull(capturedContext);
        Assert.DoesNotContain("STRICT RULES", capturedContext!.SystemPrompt ?? "");
        Assert.True(capturedContext.RelevantKnowledge is null or { Count: 0 });
    }

    // ---- OFFICIAL_GROUNDED mode ----

    [Fact]
    public async Task SendMessage_OfficialGrounded_AboveThreshold_ReturnsOfficialMode()
    {
        _mockSearch.Setup(s => s.SearchAsync(It.IsAny<string>(), "MY", 5))
            .ReturnsAsync(new List<KnowledgeSearchResult>
            {
                new()
                {
                    KnowledgeId = Guid.NewGuid(),
                    Title = "Employment Pass Overview",
                    Content = "Category II minimum salary RM10,000",
                    SourceUrl = "https://esd.imi.gov.my/",
                    Department = "Expatriate Services Division, Immigration Department of Malaysia",
                    Category = "immigration",
                    CountryCode = "MY",
                    RelevanceScore = 0.78,
                }
            });

        var service = CreateService();
        var conversationId = await CreateConversation(service);

        var result = await service.SendMessageAsync(conversationId, TestUserId,
            "What is the minimum salary for Employment Pass Category II?");

        Assert.Equal("official_grounded", result.ResponseMode);
        Assert.NotNull(result.Sources);
        Assert.Single(result.Sources!);
        _mockAi.Verify(a => a.GenerateResponseAsync(It.IsAny<string>(), It.IsAny<AIContext>()), Times.Once);
    }

    [Fact]
    public async Task SendMessage_OfficialGrounded_UsesGroundedPromptWithKnowledge()
    {
        AIContext? capturedContext = null;
        _mockAi.Setup(a => a.GenerateResponseAsync(It.IsAny<string>(), It.IsAny<AIContext>()))
            .Callback<string, AIContext?>((_, ctx) => capturedContext = ctx)
            .ReturnsAsync(new AIResponse { Content = "Grounded answer" });

        _mockSearch.Setup(s => s.SearchAsync(It.IsAny<string>(), "MY", 5))
            .ReturnsAsync(new List<KnowledgeSearchResult>
            {
                new()
                {
                    KnowledgeId = Guid.NewGuid(),
                    Title = "EP Policy",
                    Content = "Some official content",
                    CountryCode = "MY",
                    RelevanceScore = 0.80,
                }
            });

        var service = CreateService();
        var conversationId = await CreateConversation(service);

        await service.SendMessageAsync(conversationId, TestUserId, "EP salary question");

        Assert.NotNull(capturedContext);
        Assert.Contains("STRICT RULES", capturedContext!.SystemPrompt ?? "");
        Assert.NotEmpty(capturedContext.RelevantKnowledge!);
    }

    // ---- GENERAL_UNVERIFIED mode ----

    [Fact]
    public async Task SendMessage_GeneralUnverified_EmptyResults_CallsGeminiOnce()
    {
        _mockSearch.Setup(s => s.SearchAsync(It.IsAny<string>(), "MY", 5))
            .ReturnsAsync(new List<KnowledgeSearchResult>());

        var service = CreateService();
        var conversationId = await CreateConversation(service);

        var result = await service.SendMessageAsync(conversationId, TestUserId,
            "What is the motorcycle driving test procedure?");

        _mockAi.Verify(a => a.GenerateResponseAsync(It.IsAny<string>(), It.IsAny<AIContext>()), Times.Once);
        Assert.Equal("general_unverified", result.ResponseMode);
        Assert.Null(result.Sources);
    }

    [Fact]
    public async Task SendMessage_GeneralUnverified_UsesUnverifiedPrompt_NotGroundedPrompt()
    {
        AIContext? capturedContext = null;
        _mockAi.Setup(a => a.GenerateResponseAsync(It.IsAny<string>(), It.IsAny<AIContext>()))
            .Callback<string, AIContext?>((_, ctx) => capturedContext = ctx)
            .ReturnsAsync(new AIResponse { Content = "General answer" });

        _mockSearch.Setup(s => s.SearchAsync(It.IsAny<string>(), "MY", 5))
            .ReturnsAsync(new List<KnowledgeSearchResult>());

        var service = CreateService();
        var conversationId = await CreateConversation(service);

        await service.SendMessageAsync(conversationId, TestUserId, "Some general question");

        Assert.NotNull(capturedContext);
        Assert.DoesNotContain("STRICT RULES", capturedContext!.SystemPrompt ?? "");
        Assert.True(capturedContext.RelevantKnowledge is null or { Count: 0 });
    }

    [Fact]
    public async Task SendMessage_GeneralUnverified_NoSecondPostOccurs_SingleGeminiCall()
    {
        _mockSearch.Setup(s => s.SearchAsync(It.IsAny<string>(), "MY", 5))
            .ReturnsAsync(new List<KnowledgeSearchResult>());

        var service = CreateService();
        var conversationId = await CreateConversation(service);

        await service.SendMessageAsync(conversationId, TestUserId, "Unrelated question");

        // Exactly one Gemini call — no retry or second POST
        _mockAi.Verify(
            a => a.GenerateResponseAsync(It.IsAny<string>(), It.IsAny<AIContext>()),
            Times.Once);
    }

    // ---- Error propagation ----

    [Fact]
    public async Task SendMessage_Casual_GeminiUnavailable_Propagates()
    {
        _mockAi.Setup(a => a.GenerateResponseAsync(It.IsAny<string>(), It.IsAny<AIContext>()))
            .ThrowsAsync(new AIProviderUnavailableException("Unavailable"));

        var service = CreateService();
        var conversationId = await CreateConversation(service);

        await Assert.ThrowsAsync<AIProviderUnavailableException>(
            () => service.SendMessageAsync(conversationId, TestUserId, "hello"));
    }

    [Fact]
    public async Task SendMessage_GeneralUnverified_GeminiUnavailable_Propagates()
    {
        _mockSearch.Setup(s => s.SearchAsync(It.IsAny<string>(), "MY", 5))
            .ReturnsAsync(new List<KnowledgeSearchResult>());

        _mockAi.Setup(a => a.GenerateResponseAsync(It.IsAny<string>(), It.IsAny<AIContext>()))
            .ThrowsAsync(new AIProviderUnavailableException("Unavailable"));

        var service = CreateService();
        var conversationId = await CreateConversation(service);

        await Assert.ThrowsAsync<AIProviderUnavailableException>(
            () => service.SendMessageAsync(conversationId, TestUserId, "Some question"));
    }

    // ---- Persistence round-trip ----

    [Fact]
    public async Task SendMessage_ResponseMode_PersistedAndRestoredFromHistory()
    {
        _mockSearch.Setup(s => s.SearchAsync(It.IsAny<string>(), "MY", 5))
            .ReturnsAsync(new List<KnowledgeSearchResult>());

        var service = CreateService();
        var conversationId = await CreateConversation(service);

        await service.SendMessageAsync(conversationId, TestUserId, "Some question with no KB context");

        var conversation = await service.GetConversationAsync(conversationId, TestUserId);
        var assistantMsg = conversation!.Messages.FirstOrDefault(m => m.Role == "assistant");
        Assert.NotNull(assistantMsg);
        Assert.Equal("general_unverified", assistantMsg!.ResponseMode);
    }

    // ---- Existing auth/length constraints remain ----

    [Fact]
    public async Task SendMessage_ExceedsMaxLength_Throws()
    {
        var service = CreateService();
        var conversationId = await CreateConversation(service);

        var longMessage = new string('x', 8001);
        await Assert.ThrowsAsync<ArgumentException>(
            () => service.SendMessageAsync(conversationId, TestUserId, longMessage));
    }

    [Fact]
    public async Task SendMessage_WrongUser_ThrowsUnauthorized()
    {
        var service = CreateService();
        var conversationId = await CreateConversation(service);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.SendMessageAsync(conversationId, Guid.NewGuid(), "hello"));
    }
}
