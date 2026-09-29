using ExpatOne.Application.DTOs;
using ExpatOne.Application.Common;
using ExpatOne.Application.Interfaces;
using ExpatOne.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;

namespace ExpatOne.Tests;

public class EmergencyAssistServiceTests
{
    private readonly Mock<IAIService> _mockAi = new();
    private readonly Mock<ILogger<EmergencyAssistService>> _mockLogger = new();

    private EmergencyAssistService CreateService(int maxMessageLength = 2000)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Emergency:MaxMessageLength"] = maxMessageLength.ToString(),
            })
            .Build();
        return new EmergencyAssistService(_mockAi.Object, config, _mockLogger.Object);
    }

    [Fact]
    public async Task Assist_ValidRequest_ReturnsResponse()
    {
        _mockAi.Setup(a => a.GenerateResponseAsync(
                "I need an ambulance, someone is injured",
                It.IsAny<AIContext>()))
            .ReturnsAsync(new AIResponse
            {
                Content = "Call 999 now. Tell the operator: someone is injured and you need an ambulance."
            });

        var service = CreateService();
        var result = await service.AssistAsync(new EmergencyAssistRequestDto
        {
            Message = "I need an ambulance, someone is injured",
        });

        Assert.Contains("Call 999", result.Response);
        Assert.Null(result.TranslatedMessage);
        Assert.Null(result.TargetLanguage);
    }

    [Fact]
    public async Task Assist_WithTargetLanguage_ReturnsTranslation()
    {
        _mockAi.Setup(a => a.GenerateResponseAsync(It.IsAny<string>(), It.IsAny<AIContext>()))
            .ReturnsAsync(new AIResponse
            {
                Content = "Call 999 now."
            });

        _mockAi.Setup(a => a.TranslateAsync(
                "I need help, there is a fire",
                "auto",
                "Malay"))
            .ReturnsAsync("Saya perlukan bantuan, ada kebakaran");

        var service = CreateService();
        var result = await service.AssistAsync(new EmergencyAssistRequestDto
        {
            Message = "I need help, there is a fire",
            TargetLanguage = "ms",
        });

        Assert.NotNull(result.Response);
        Assert.Equal("Saya perlukan bantuan, ada kebakaran", result.TranslatedMessage);
        Assert.Equal("ms", result.TargetLanguage);
    }

    [Fact]
    public async Task Assist_EmptyMessage_ThrowsArgumentException()
    {
        var service = CreateService();
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.AssistAsync(new EmergencyAssistRequestDto
            {
                Message = "",
            }));

        _mockAi.Verify(a => a.GenerateResponseAsync(It.IsAny<string>(), It.IsAny<AIContext>()), Times.Never);
    }

    [Fact]
    public async Task Assist_WhitespaceMessage_ThrowsArgumentException()
    {
        var service = CreateService();
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.AssistAsync(new EmergencyAssistRequestDto
            {
                Message = "   \t\n  ",
            }));
    }

    [Fact]
    public async Task Assist_MessageExceedsMaxLength_ThrowsArgumentException()
    {
        var service = CreateService(maxMessageLength: 100);
        var longMessage = new string('a', 101);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.AssistAsync(new EmergencyAssistRequestDto
            {
                Message = longMessage,
            }));

        Assert.Contains("maximum length", ex.Message);
    }

    [Fact]
    public async Task Assist_UnsupportedTargetLanguage_ThrowsArgumentException()
    {
        var service = CreateService();
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.AssistAsync(new EmergencyAssistRequestDto
            {
                Message = "Help",
                TargetLanguage = "xx",
            }));

        Assert.Contains("not supported", ex.Message);
    }

    [Fact]
    public async Task Assist_GeminiFailure_PropagatesException()
    {
        _mockAi.Setup(a => a.GenerateResponseAsync(It.IsAny<string>(), It.IsAny<AIContext>()))
            .ThrowsAsync(new AIProviderUnavailableException("Service unavailable."));

        var service = CreateService();
        await Assert.ThrowsAsync<AIProviderUnavailableException>(() =>
            service.AssistAsync(new EmergencyAssistRequestDto
            {
                Message = "Help me",
            }));
    }

    [Fact]
    public async Task Assist_SystemPromptIncludesEmergencyRules()
    {
        AIContext? capturedContext = null;
        _mockAi.Setup(a => a.GenerateResponseAsync(It.IsAny<string>(), It.IsAny<AIContext>()))
            .Callback<string, AIContext?>((_, ctx) => capturedContext = ctx)
            .ReturnsAsync(new AIResponse { Content = "Response" });

        var service = CreateService();
        await service.AssistAsync(new EmergencyAssistRequestDto
        {
            Message = "I need help",
        });

        Assert.NotNull(capturedContext?.SystemPrompt);
        Assert.Contains("999", capturedContext!.SystemPrompt!);
        Assert.Contains("Do NOT diagnose", capturedContext.SystemPrompt);
        Assert.Contains("Do NOT prescribe", capturedContext.SystemPrompt);
        Assert.Contains("Do NOT invent emergency numbers", capturedContext.SystemPrompt);
        Assert.Contains("Do NOT fabricate", capturedContext.SystemPrompt);
        Assert.Contains("Call 999 now", capturedContext.SystemPrompt);
    }

    [Fact]
    public async Task Assist_DoesNotInvokeKnowledgeSearch()
    {
        _mockAi.Setup(a => a.GenerateResponseAsync(It.IsAny<string>(), It.IsAny<AIContext>()))
            .ReturnsAsync(new AIResponse { Content = "Response" });

        var mockSearch = new Mock<IKnowledgeSearchService>();

        var service = CreateService();
        await service.AssistAsync(new EmergencyAssistRequestDto
        {
            Message = "There is a fire at my building",
        });

        mockSearch.Verify(s => s.SearchAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()), Times.Never,
            "Emergency assist must NOT call KnowledgeSearchService");
    }

    [Fact]
    public async Task Assist_NoTargetLanguage_SkipsTranslation()
    {
        _mockAi.Setup(a => a.GenerateResponseAsync(It.IsAny<string>(), It.IsAny<AIContext>()))
            .ReturnsAsync(new AIResponse { Content = "Call 999." });

        var service = CreateService();
        var result = await service.AssistAsync(new EmergencyAssistRequestDto
        {
            Message = "Help",
        });

        _mockAi.Verify(a => a.TranslateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never,
            "When no targetLanguage is specified, translation must not be called");
        Assert.Null(result.TranslatedMessage);
    }

    [Fact]
    public async Task Assist_MessageAtMaxLength_Succeeds()
    {
        var exactMessage = new string('a', 100);
        _mockAi.Setup(a => a.GenerateResponseAsync(It.IsAny<string>(), It.IsAny<AIContext>()))
            .ReturnsAsync(new AIResponse { Content = "Response" });

        var service = CreateService(maxMessageLength: 100);
        var result = await service.AssistAsync(new EmergencyAssistRequestDto
        {
            Message = exactMessage,
        });

        Assert.Equal("Response", result.Response);
    }
}
