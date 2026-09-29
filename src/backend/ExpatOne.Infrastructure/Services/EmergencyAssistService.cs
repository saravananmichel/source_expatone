using ExpatOne.Application.DTOs;
using ExpatOne.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ExpatOne.Infrastructure.Services;

public class EmergencyAssistService : IEmergencyAssistService
{
    private readonly IAIService _aiService;
    private readonly ILogger<EmergencyAssistService> _logger;
    private readonly int _maxMessageLength;

    private static readonly HashSet<string> SupportedLanguageCodes =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "en", "ms", "zh", "ta", "hi", "ar", "ja", "ko"
        };

    private static readonly Dictionary<string, string> LanguageNames =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["en"] = "English",
            ["ms"] = "Malay",
            ["zh"] = "Chinese (Simplified)",
            ["ta"] = "Tamil",
            ["hi"] = "Hindi",
            ["ar"] = "Arabic",
            ["ja"] = "Japanese",
            ["ko"] = "Korean",
        };

    public EmergencyAssistService(
        IAIService aiService,
        IConfiguration configuration,
        ILogger<EmergencyAssistService> logger)
    {
        _aiService = aiService;
        _logger = logger;
        _maxMessageLength = int.TryParse(configuration["Emergency:MaxMessageLength"], out var max) ? max : 2000;
    }

    public async Task<EmergencyAssistResponseDto> AssistAsync(EmergencyAssistRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
            throw new ArgumentException("Message cannot be empty.");

        if (request.Message.Length > _maxMessageLength)
            throw new ArgumentException($"Message exceeds the maximum length of {_maxMessageLength} characters.");

        if (!string.IsNullOrEmpty(request.TargetLanguage) &&
            !SupportedLanguageCodes.Contains(request.TargetLanguage))
            throw new ArgumentException($"Target language '{request.TargetLanguage}' is not supported.");

        _logger.LogInformation("Emergency assist request, length={Length}, targetLang={Lang}",
            request.Message.Length, request.TargetLanguage ?? "none");

        var context = new AIContext
        {
            SystemPrompt = BuildEmergencySystemPrompt(),
        };

        var aiResponse = await _aiService.GenerateResponseAsync(request.Message, context);

        string? translatedMessage = null;
        if (!string.IsNullOrEmpty(request.TargetLanguage))
        {
            var targetName = LanguageNames[request.TargetLanguage];
            translatedMessage = await _aiService.TranslateAsync(request.Message, "auto", targetName);
        }

        return new EmergencyAssistResponseDto
        {
            Response = aiResponse.Content,
            TranslatedMessage = translatedMessage,
            TargetLanguage = request.TargetLanguage,
        };
    }

    private static string BuildEmergencySystemPrompt()
    {
        return """
            You are an emergency communication assistant for foreigners in Malaysia.

            CRITICAL SAFETY RULES:
            - Emergency services take priority over everything else.
            - If the situation may be an emergency, encourage the user to call 999 immediately.
            - Do NOT delay calling emergency services.
            - Do NOT claim an emergency call was made unless the app actually launched the call action.
            - Do NOT diagnose medical conditions.
            - Do NOT prescribe medical treatment.
            - Do NOT invent emergency numbers.
            - Do NOT invent government procedures.
            - Do NOT fabricate location information.
            - Do NOT fabricate symptoms or injuries.
            - Do NOT fabricate responder availability or response times.
            - Do NOT tell the user that emergency services are on their way.
            - Do NOT imply that you are monitoring the emergency.
            - Keep responses short and actionable during emergencies.

            YOUR ROLE:
            - Help the user prepare a concise description of their emergency for responders.
            - Help the user communicate key information: location, nature of emergency, number of people affected.
            - Suggest what information to have ready when calling 999.
            - If asked, help translate emergency phrases.
            - Always remind the user to call 999 if they describe an emergency situation.

            RESPONSE FORMAT:
            - Be concise. Emergency situations require fast, clear communication.
            - Use short sentences.
            - Prioritize actionable information.
            - If the user describes an immediate emergency, start your response with: "Call 999 now."

            VERIFIED EMERGENCY NUMBER:
            - Malaysia Emergency Response Services (MERS): 999
            - This connects to police, fire, ambulance, and other emergency agencies.
            - Do NOT provide any other emergency numbers unless they are explicitly verified.
            """;
    }
}
