using ExpatOne.Application.Common;
using ExpatOne.Application.DTOs;
using ExpatOne.Application.Interfaces;
using ExpatOne.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;

namespace ExpatOne.Tests;

public class TranslationServiceTests
{
    private readonly Mock<IAIService> _mockAi = new();
    private readonly Mock<ILogger<TranslationService>> _mockLogger = new();

    private TranslationService CreateService(int maxTextLength = 5000)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Translation:MaxTextLength"] = maxTextLength.ToString(),
            })
            .Build();
        return new TranslationService(_mockAi.Object, config, _mockLogger.Object);
    }

    [Fact]
    public async Task Translate_ValidRequest_ReturnsTranslation()
    {
        _mockAi.Setup(a => a.TranslateAsync("Hello", "English", "Malay"))
            .ReturnsAsync("Helo");

        var service = CreateService();
        var result = await service.TranslateAsync(new TranslateRequestDto
        {
            Text = "Hello",
            SourceLanguage = "en",
            TargetLanguage = "ms",
        });

        Assert.Equal("Helo", result.TranslatedText);
        Assert.Equal("en", result.SourceLanguage);
        Assert.Equal("ms", result.TargetLanguage);
        _mockAi.Verify(a => a.TranslateAsync("Hello", "English", "Malay"), Times.Once);
    }

    [Fact]
    public async Task Translate_MalayToEnglish_ReturnsTranslation()
    {
        _mockAi.Setup(a => a.TranslateAsync("Selamat pagi", "Malay", "English"))
            .ReturnsAsync("Good morning");

        var service = CreateService();
        var result = await service.TranslateAsync(new TranslateRequestDto
        {
            Text = "Selamat pagi",
            SourceLanguage = "ms",
            TargetLanguage = "en",
        });

        Assert.Equal("Good morning", result.TranslatedText);
        Assert.Equal("ms", result.SourceLanguage);
        Assert.Equal("en", result.TargetLanguage);
    }

    [Fact]
    public async Task Translate_SameLanguage_DoesNotCallGemini()
    {
        var service = CreateService();
        var result = await service.TranslateAsync(new TranslateRequestDto
        {
            Text = "Hello world",
            SourceLanguage = "en",
            TargetLanguage = "en",
        });

        Assert.Equal("Hello world", result.TranslatedText);
        Assert.Equal("en", result.SourceLanguage);
        Assert.Equal("en", result.TargetLanguage);
        _mockAi.Verify(a => a.TranslateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never,
            "Gemini must not be called when source and target languages are the same");
    }

    [Fact]
    public async Task Translate_SameLanguageCaseInsensitive_DoesNotCallGemini()
    {
        var service = CreateService();
        var result = await service.TranslateAsync(new TranslateRequestDto
        {
            Text = "Test",
            SourceLanguage = "EN",
            TargetLanguage = "en",
        });

        Assert.Equal("Test", result.TranslatedText);
        _mockAi.Verify(a => a.TranslateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Translate_AutoDetect_CallsGeminiWithAuto()
    {
        _mockAi.Setup(a => a.TranslateAsync("Bonjour", "auto", "English"))
            .ReturnsAsync("Hello");

        var service = CreateService();
        var result = await service.TranslateAsync(new TranslateRequestDto
        {
            Text = "Bonjour",
            SourceLanguage = "auto",
            TargetLanguage = "en",
        });

        Assert.Equal("Hello", result.TranslatedText);
        Assert.Equal("auto", result.SourceLanguage);
        Assert.Equal("en", result.TargetLanguage);
        _mockAi.Verify(a => a.TranslateAsync("Bonjour", "auto", "English"), Times.Once);
    }

    [Fact]
    public async Task Translate_EmptyText_ThrowsArgumentException()
    {
        var service = CreateService();
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.TranslateAsync(new TranslateRequestDto
            {
                Text = "",
                SourceLanguage = "en",
                TargetLanguage = "ms",
            }));

        _mockAi.Verify(a => a.TranslateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Translate_WhitespaceText_ThrowsArgumentException()
    {
        var service = CreateService();
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.TranslateAsync(new TranslateRequestDto
            {
                Text = "   \n\t  ",
                SourceLanguage = "en",
                TargetLanguage = "ms",
            }));

        _mockAi.Verify(a => a.TranslateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Translate_UnsupportedTargetLanguage_ThrowsArgumentException()
    {
        var service = CreateService();
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.TranslateAsync(new TranslateRequestDto
            {
                Text = "Hello",
                SourceLanguage = "en",
                TargetLanguage = "xx",
            }));

        Assert.Contains("not supported", ex.Message);
        _mockAi.Verify(a => a.TranslateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Translate_UnsupportedSourceLanguage_ThrowsArgumentException()
    {
        var service = CreateService();
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.TranslateAsync(new TranslateRequestDto
            {
                Text = "Hello",
                SourceLanguage = "xx",
                TargetLanguage = "ms",
            }));

        Assert.Contains("not supported", ex.Message);
    }

    [Fact]
    public async Task Translate_TextExceedsMaxLength_ThrowsArgumentException()
    {
        var service = CreateService(maxTextLength: 100);
        var longText = new string('a', 101);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.TranslateAsync(new TranslateRequestDto
            {
                Text = longText,
                SourceLanguage = "en",
                TargetLanguage = "ms",
            }));

        Assert.Contains("maximum length", ex.Message);
        _mockAi.Verify(a => a.TranslateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Translate_TextAtMaxLength_Succeeds()
    {
        var exactText = new string('a', 100);
        _mockAi.Setup(a => a.TranslateAsync(exactText, "English", "Malay"))
            .ReturnsAsync("translated");

        var service = CreateService(maxTextLength: 100);
        var result = await service.TranslateAsync(new TranslateRequestDto
        {
            Text = exactText,
            SourceLanguage = "en",
            TargetLanguage = "ms",
        });

        Assert.Equal("translated", result.TranslatedText);
    }

    [Fact]
    public async Task Translate_MissingTargetLanguage_ThrowsArgumentException()
    {
        var service = CreateService();
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.TranslateAsync(new TranslateRequestDto
            {
                Text = "Hello",
                SourceLanguage = "en",
                TargetLanguage = "",
            }));
    }

    [Fact]
    public async Task Translate_GeminiFailure_PropagatesException()
    {
        _mockAi.Setup(a => a.TranslateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ThrowsAsync(new AIProviderUnavailableException("Translation is temporarily unavailable."));

        var service = CreateService();
        await Assert.ThrowsAsync<AIProviderUnavailableException>(() =>
            service.TranslateAsync(new TranslateRequestDto
            {
                Text = "Hello",
                SourceLanguage = "en",
                TargetLanguage = "ms",
            }));
    }

    [Fact]
    public async Task Translate_DoesNotInvokeKnowledgeSearch()
    {
        _mockAi.Setup(a => a.TranslateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync("translated");

        var mockSearch = new Mock<IKnowledgeSearchService>();
        var mockAssistant = new Mock<IAssistantService>();

        var service = CreateService();
        await service.TranslateAsync(new TranslateRequestDto
        {
            Text = "Employment Pass Category II requires RM10,000",
            SourceLanguage = "en",
            TargetLanguage = "ms",
        });

        mockSearch.Verify(s => s.SearchAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()), Times.Never,
            "Translation must NEVER call KnowledgeSearchService");
        mockAssistant.Verify(a => a.SendMessageAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>()), Times.Never,
            "Translation must NEVER call AssistantService");
    }

    [Fact]
    public void GetSupportedLanguages_ReturnsExpectedLanguages()
    {
        var service = CreateService();
        var languages = service.GetSupportedLanguages();

        Assert.True(languages.Count >= 8);
        Assert.Contains(languages, l => l.Code == "en" && l.Name == "English");
        Assert.Contains(languages, l => l.Code == "ms" && l.Name == "Malay");
        Assert.Contains(languages, l => l.Code == "zh" && l.Name == "Chinese (Simplified)");
        Assert.Contains(languages, l => l.Code == "ta" && l.Name == "Tamil");
        Assert.Contains(languages, l => l.Code == "hi" && l.Name == "Hindi");
        Assert.Contains(languages, l => l.Code == "ar" && l.Name == "Arabic");
        Assert.Contains(languages, l => l.Code == "ja" && l.Name == "Japanese");
        Assert.Contains(languages, l => l.Code == "ko" && l.Name == "Korean");
    }

    [Fact]
    public async Task Translate_AllSupportedTargetLanguages_AcceptsEach()
    {
        _mockAi.Setup(a => a.TranslateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync("translated");

        var service = CreateService();
        var languages = service.GetSupportedLanguages();

        foreach (var lang in languages)
        {
            var result = await service.TranslateAsync(new TranslateRequestDto
            {
                Text = "Test",
                SourceLanguage = "auto",
                TargetLanguage = lang.Code,
            });

            Assert.Equal("translated", result.TranslatedText);
        }
    }

    [Fact]
    public async Task Translate_NumbersInText_PassedToGeminiUnchanged()
    {
        var textWithNumbers = "Employment Pass Category II requires RM10,000 to RM19,999.";
        _mockAi.Setup(a => a.TranslateAsync(textWithNumbers, "English", "Malay"))
            .ReturnsAsync("Pas Penggajian Kategori II memerlukan RM10,000 hingga RM19,999.");

        var service = CreateService();
        var result = await service.TranslateAsync(new TranslateRequestDto
        {
            Text = textWithNumbers,
            SourceLanguage = "en",
            TargetLanguage = "ms",
        });

        _mockAi.Verify(a => a.TranslateAsync(textWithNumbers, "English", "Malay"), Times.Once,
            "The exact text with numbers must be passed to Gemini unchanged");
    }
}
