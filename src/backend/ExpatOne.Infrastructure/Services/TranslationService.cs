using ExpatOne.Application.DTOs;
using ExpatOne.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ExpatOne.Infrastructure.Services;

public class TranslationService : ITranslationService
{
    private readonly IAIService _aiService;
    private readonly ILogger<TranslationService> _logger;
    private readonly int _maxTextLength;

    private static readonly IReadOnlyList<SupportedLanguageDto> Languages =
    [
        new() { Code = "en", Name = "English" },
        new() { Code = "ms", Name = "Malay" },
        new() { Code = "zh", Name = "Chinese (Simplified)" },
        new() { Code = "ta", Name = "Tamil" },
        new() { Code = "hi", Name = "Hindi" },
        new() { Code = "ar", Name = "Arabic" },
        new() { Code = "ja", Name = "Japanese" },
        new() { Code = "ko", Name = "Korean" },
    ];

    private static readonly HashSet<string> SupportedLanguageCodes =
        new(Languages.Select(l => l.Code), StringComparer.OrdinalIgnoreCase);

    private static readonly Dictionary<string, string> LanguageNames =
        Languages.ToDictionary(l => l.Code, l => l.Name, StringComparer.OrdinalIgnoreCase);

    public TranslationService(
        IAIService aiService,
        IConfiguration configuration,
        ILogger<TranslationService> logger)
    {
        _aiService = aiService;
        _logger = logger;
        _maxTextLength = int.TryParse(configuration["Translation:MaxTextLength"], out var max) ? max : 5000;
    }

    public IReadOnlyList<SupportedLanguageDto> GetSupportedLanguages() => Languages;

    public async Task<TranslationResultDto> TranslateAsync(TranslateRequestDto request)
    {
        ValidateRequest(request);

        var sourceCode = request.SourceLanguage.ToLowerInvariant();
        var targetCode = request.TargetLanguage.ToLowerInvariant();

        if (sourceCode != "auto" && sourceCode == targetCode)
        {
            return new TranslationResultDto
            {
                TranslatedText = request.Text,
                SourceLanguage = sourceCode,
                TargetLanguage = targetCode,
            };
        }

        var sourceName = sourceCode == "auto" ? "auto" : LanguageNames[sourceCode];
        var targetName = LanguageNames[targetCode];

        _logger.LogInformation("Translation request: {Source} → {Target}, length={Length}",
            sourceName, targetName, request.Text.Length);

        var translatedText = await _aiService.TranslateAsync(request.Text, sourceName, targetName);

        return new TranslationResultDto
        {
            TranslatedText = translatedText,
            SourceLanguage = sourceCode,
            TargetLanguage = targetCode,
        };
    }

    private void ValidateRequest(TranslateRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.Text))
            throw new ArgumentException("Text to translate cannot be empty.");

        if (request.Text.Length > _maxTextLength)
            throw new ArgumentException($"Text exceeds the maximum length of {_maxTextLength} characters.");

        if (string.IsNullOrWhiteSpace(request.TargetLanguage))
            throw new ArgumentException("Target language is required.");

        var targetCode = request.TargetLanguage.ToLowerInvariant();
        if (!SupportedLanguageCodes.Contains(targetCode))
            throw new ArgumentException($"Target language '{request.TargetLanguage}' is not supported.");

        var sourceCode = request.SourceLanguage.ToLowerInvariant();
        if (sourceCode != "auto" && !SupportedLanguageCodes.Contains(sourceCode))
            throw new ArgumentException($"Source language '{request.SourceLanguage}' is not supported.");
    }
}
